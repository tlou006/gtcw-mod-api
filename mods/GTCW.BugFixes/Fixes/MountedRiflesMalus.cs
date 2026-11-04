using System;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-028 — Mounted rifles is applied inside out, which makes it weaker
    /// at every level and never worth levelling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both mounted penalties — shot accuracy ×0.6 and reload time ×1.25 —
    /// become <c>(1 − base) × A[level] + base</c>, where <c>A</c> is
    /// <c>perk_mountedriflesmalusfactor</c>. That treats <c>A</c> as the share
    /// of the penalty <em>recovered</em>. The config label calls it the
    /// <i>malus</i> factor and the shipped values decline — 0.75 / 0.5 / 0.2 —
    /// so it is a remaining-malus array and the code reads it backwards:
    /// </para>
    /// <list type="table">
    /// <item><description>mounted accuracy, as shipped: 0.90 / 0.80 / 0.68 — worse each level</description></item>
    /// <item><description>mounted accuracy, fixed: 0.70 / 0.80 / 0.92</description></item>
    /// <item><description>mounted reload, as shipped: 1.06 / 1.13 / 1.20</description></item>
    /// <item><description>mounted reload, fixed: 1.19 / 1.13 / 1.05</description></item>
    /// </list>
    /// <para>
    /// The fix lends the complement <c>1 − A[i]</c> for the duration of each
    /// call, so level I removes a quarter of the malus and level III four
    /// fifths — the declining array read the way its own label describes. The
    /// complement is computed from the <em>live</em> array and cached by
    /// reference identity, so a data mod that re-authors those three numbers is
    /// respected rather than overwritten.
    /// </para>
    /// <para>
    /// Only the reload half changes a fight. Mounted fire is multiplied by 0.6
    /// before the perk, which keeps the shot accuracy under the aim-scatter
    /// floor at every level either way
    /// (<c>34-perks.md</c> §2.4). The accuracy site is fixed anyway: leaving a
    /// known-inverted read in place because it currently cannot be seen is how
    /// it becomes visible later, after a weapon or a pref changes.
    /// </para>
    /// <para>
    /// <b>Two patched methods, one fix.</b> Unlike the grouped fixes elsewhere
    /// in this mod the two patches do not interact — each lends the array to its
    /// own call — but they share an id and a toggle because they are one
    /// defect, and a player who turned off half of it would get a perk that
    /// helps reload and hurts accuracy.
    /// </para>
    /// </remarks>
    internal static class MountedRiflesMalus
    {
        private const string Id = "GTCW-028";
        private const string Accuracy = "Regiment:FireBullet";
        private const string Reload = "Regiment:UpdateFiringAndReloading";

        private const int MountedRiflesPerk = 10;

        /// <summary>The shipped array the complement was built from.</summary>
        private static float[]? _shipped;

        /// <summary>The complement, rebuilt only when the shipped array changes.</summary>
        private static float[]? _complement;

        public static FixGroup Group() => new FixGroup(
            Accuracy + " + " + Reload,
            new[]
            {
                new Fix(
                    Id,
                    "Mounted rifles reduces the penalty for firing from the saddle by more at each level " +
                    "instead of less. Unfixed, level III is the weakest of the three.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "code-findings/34-perks.md §3.11",
                    Probe,
                    family: Fix.Perks),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Regiment", "FireBullet"),
            () => FixProbe.RequireMethod("Regiment", "UpdateFiringAndReloading"),
            () => FixProbe.RequireField("Regiment", "mounted"),
            () => FixProbe.RequireField("GamePrefs", "perk_mountedriflesmalusfactor"));

        private static void Apply(Harmony harmony)
        {
            Patch(harmony, Accuracy);
            Patch(harmony, Reload);
        }

        private static void Patch(Harmony harmony, string target)
        {
            MethodInfo original = AccessTools.Method(target)
                ?? throw new InvalidOperationException($"'{target}' not found");

            harmony.Patch(
                original,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(MountedRiflesMalus), nameof(Before))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(MountedRiflesMalus), nameof(After))));
        }

        /// <summary>
        /// Lends the complement, but only for a call that is going to read the
        /// array at all.
        /// </summary>
        /// <remarks>
        /// <c>FireBullet</c> is one of the hottest methods in the battle scene,
        /// so the test order matters: the toggle, then two field reads, then a
        /// perk lookup, and only then any work. A unit that is not mounted or
        /// does not hold the perk pays three comparisons.
        /// </remarks>
        private static void Before(Regiment __instance, out bool __state)
        {
            bool lent = false;

            if (FixState.IsOn(Id) &&
                __instance.mounted > 0 &&
                __instance.HasPerk(MountedRiflesPerk))
            {
                FixState.Guard(Id, () => { lent = Lend(); });
            }

            __state = lent;
        }

        private static void After(bool __state)
        {
            if (!__state)
            {
                return;
            }

            // A finalizer: both methods can throw downstream — projectile
            // initialisation, sprite work — and the array is a global.
            FixState.Guard(Id, () =>
            {
                if (_shipped != null)
                {
                    GamePrefs.perk_mountedriflesmalusfactor = _shipped;
                }
            });
        }

        private static bool Lend()
        {
            float[] shipped = GamePrefs.perk_mountedriflesmalusfactor;
            if (shipped == null || shipped.Length == 0)
            {
                return false;
            }

            // Already lent, so this is a nested call — one of the two patched
            // methods reached inside the other. Leave the loan to the outer
            // frame, or the complement would be complemented again and the
            // outer finalizer would restore the wrong array.
            if (ReferenceEquals(shipped, _complement))
            {
                return false;
            }

            if (!ReferenceEquals(_shipped, shipped) || _complement == null || _complement.Length != shipped.Length)
            {
                var complement = new float[shipped.Length];
                for (int i = 0; i < shipped.Length; i++)
                {
                    complement[i] = 1f - shipped[i];
                }

                _shipped = shipped;
                _complement = complement;
            }

            GamePrefs.perk_mountedriflesmalusfactor = _complement;
            return true;
        }
    }
}
