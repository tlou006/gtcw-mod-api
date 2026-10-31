using System;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-003 — the Confiscation Act's enemy-soil supply bonus is applied as a
    /// penalty, so foraging on enemy soil is 13 % slower instead of 15 % faster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Regiment.UpdateIIPSupplyLink</c> multiplies
    /// <c>supplyenemysoilfactor</c> (1.15) into <c>num15</c>, which is the
    /// <em>time</em> to resupply — the neighbouring lines settle that:
    /// <c>num15 /= Mathf.Max(0.01f, closestiipforsupply.condition)</c>, so a
    /// worse node means a longer wait. Appendix A, the config comment and the
    /// act's own description all promise the opposite sign.
    /// </para>
    /// <para>
    /// <b>The fix lends the field its reciprocal</b> for the duration of one
    /// call, so the game's own multiply computes the division that was meant.
    /// No IL is rewritten. The precondition — verified in the decompile — is
    /// that the method reads the field exactly twice, the <c>!= 1f</c> guard and
    /// the multiply, and the reciprocal is correct for both.
    /// </para>
    /// </remarks>
    internal static class ConfiscationActSupply
    {
        private const string Id = "GTCW-003";
        private const string Target = "Regiment:UpdateIIPSupplyLink";

        /// <summary>
        /// Nesting depth, so only the outermost call spoofs. If the method ever
        /// reaches itself, the inner call inherits the already-corrected value
        /// instead of inverting it back.
        /// </summary>
        private static int _depth;

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    Id,
                    "The Confiscation Act speeds up foraging on enemy soil, as Appendix A says it does, " +
                    "instead of slowing it down.",
                    FixClass.ManualDeviation,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "mechanics-findings/manual-vs-code.md §18.2",
                    Probe),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Regiment", "UpdateIIPSupplyLink"),
            () => FixProbe.RequireField("Regiment", "alliance"),
            () => FixProbe.RequireField("GameVars+Alliance", "supplyenemysoilfactor"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ConfiscationActSupply), nameof(Before))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(ConfiscationActSupply), nameof(Finally))));
        }

        private static void Before(Regiment __instance, out float __state)
        {
            __state = 0f;

            if (!FixState.IsOn(Id))
            {
                return;
            }

            // Depth is only tracked while the fix is on, so the finalizer's
            // decrement always pairs with an increment here.
            if (++_depth > 1)
            {
                return;
            }

            // A local rather than __state directly: a Harmony out-parameter
            // cannot be captured by the guard's lambda.
            float saved = 0f;

            FixState.Guard(Id, () =>
            {
                int side = __instance.alliance;
                if (GameVars.alliance == null || side < 0 || side >= GameVars.alliance.Length)
                {
                    return;
                }

                GameVars.Alliance a = GameVars.alliance[side];

                // 1f means the act has not been taken and the game skips the
                // multiply entirely; a non-positive value would make the
                // reciprocal meaningless.
                if (a.supplyenemysoilfactor <= 0f || a.supplyenemysoilfactor == 1f)
                {
                    return;
                }

                saved = a.supplyenemysoilfactor;
                a.supplyenemysoilfactor = 1f / saved;
            });

            __state = saved;
        }

        /// <summary>
        /// Restores the field. A finalizer, not a postfix: this method is three
        /// hundred lines long, and an exception part-way through must not leave
        /// an alliance with a permanently inverted supply factor.
        /// </summary>
        private static void Finally(Regiment __instance, float __state)
        {
            if (!FixState.IsOn(Id))
            {
                return;
            }

            if (_depth > 0)
            {
                _depth--;
            }

            if (__state == 0f)
            {
                return;
            }

            FixState.Guard(Id, () =>
            {
                int side = __instance.alliance;
                if (GameVars.alliance != null && side >= 0 && side < GameVars.alliance.Length)
                {
                    GameVars.alliance[side].supplyenemysoilfactor = __state;
                }
            });
        }
    }
}
