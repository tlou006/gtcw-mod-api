using System;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-041 — Embedded Reporters never reaches the commander who holds it,
    /// and when it reaches anyone it always applies the level I factor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two defects in one line of <c>BattleUnits.CalculateCommanderFame</c>:
    /// </para>
    /// <code>
    /// if (unit.unittyp &gt;= 15 &amp;&amp; CheckForGroupPerk(unit, 14) != null)
    ///     num5 *= GamePrefs.fameadjustmentembeddedreporters[unit.GetPerkLevel(14)];
    /// </code>
    /// <para>
    /// <c>CheckForGroupPerk</c> opens its walk with <c>GrabNextParentUnit</c>, so
    /// it never tests the unit it was asked about — the holder's own commander
    /// is excluded unless the holder's parent holds the perk too. And the factor
    /// is indexed by the <em>subordinate's</em> level, which is 0 whenever the
    /// subordinate does not hold the perk, so every Corps under a holder gets
    /// ×1.25 whatever level the holder has reached.
    /// </para>
    /// <para>
    /// Both are repaired without touching the arithmetic. A postfix on
    /// <c>CheckForGroupPerk</c> returns the unit itself when the walk found
    /// nobody and the unit holds the perk; a postfix on
    /// <c>Regiment.GetPerkLevel</c>, live only inside
    /// <c>CalculateCommanderFame</c>, answers perk 14 for that unit with the
    /// holder's level. C# evaluates the <c>&amp;&amp;</c> before the indexer, so
    /// the holder is always known by the time the level is asked for.
    /// </para>
    /// <para>
    /// <b>On patching <c>GetPerkLevel</c>.</b> It is a small method with about
    /// 330 call sites, so the postfix opens with a single static <c>bool</c>
    /// test and does nothing else outside the fame scope. The scope is one call
    /// at the end of a battle.
    /// </para>
    /// <para>
    /// The factor scales defame as well as fame — <c>num5</c> can be negative —
    /// and that is left alone. The manual says "fame development", and a
    /// commander whose reporters amplify a disaster is a defensible reading of
    /// the same sentence.
    /// </para>
    /// </remarks>
    internal static class EmbeddedReporters
    {
        private const string Id = "GTCW-041";

        private const string Fame = "BattleUnits:CalculateCommanderFame";
        private const string GroupPerk = "BattleUnits:CheckForGroupPerk";
        private const string PerkLevel = "Regiment:GetPerkLevel";

        /// <summary>Army perk 14.</summary>
        private const int EmbeddedReportersPerk = 14;

        /// <summary>True only inside <c>CalculateCommanderFame</c>.</summary>
        private static bool _inFame;

        /// <summary>The unit the last perk-14 holder search was made for.</summary>
        private static Regiment? _askedAbout;

        /// <summary>The holder that search found, if any.</summary>
        private static Regiment? _holder;

        public static FixGroup Group() => new FixGroup(
            Fame + " + " + GroupPerk + " + " + PerkLevel,
            new[]
            {
                new Fix(
                    Id,
                    "Embedded Reporters multiplies the fame of the commander who holds it, at the level it " +
                    "has actually reached. Unfixed the holder gets nothing and its subordinates all get " +
                    "the level I factor.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.ValueOnly,
                    "code-findings/34-perks.md §4.15",
                    Probe,
                    family: Fix.Perks),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("BattleUnits", "CalculateCommanderFame"),
            () => FixProbe.RequireMethod("BattleUnits", "CheckForGroupPerk"),
            () => FixProbe.RequireMethod("Regiment", "GetPerkLevel"),
            () => FixProbe.RequireField("GamePrefs", "fameadjustmentembeddedreporters"));

        private static void Apply(Harmony harmony)
        {
            // The scope closes in a finalizer, not a postfix: the fame pass
            // walks every unit of both alliances and reaches into the commander
            // pool, and a scope left open by an exception would redirect an
            // unrelated perk-level read for the rest of the session.
            Patch(harmony, Fame, prefix: nameof(BeforeFame), finalizer: nameof(AfterFame));
            Patch(harmony, GroupPerk, prefix: nameof(BeforeGroupPerk), postfix: nameof(AfterGroupPerk));
            Patch(harmony, PerkLevel, postfix: nameof(AfterPerkLevel));
        }

        private static void Patch(
            Harmony harmony,
            string target,
            string? prefix = null,
            string? postfix = null,
            string? finalizer = null)
        {
            MethodInfo original = AccessTools.Method(target)
                ?? throw new InvalidOperationException($"'{target}' not found");

            harmony.Patch(
                original,
                prefix: Method(prefix),
                postfix: Method(postfix),
                finalizer: Method(finalizer));
        }

        private static HarmonyMethod? Method(string? name) =>
            name == null ? null : new HarmonyMethod(AccessTools.Method(typeof(EmbeddedReporters), name));

        // --- the scope -----------------------------------------------------

        private static void BeforeFame()
        {
            _inFame = true;
            _askedAbout = null;
            _holder = null;
        }

        private static void AfterFame()
        {
            _inFame = false;
            _askedAbout = null;
            _holder = null;
        }

        // --- the self-skip --------------------------------------------------

        /// <summary>
        /// Captures the unit the question was about, because the method
        /// reassigns its own <c>reg</c> parameter as it walks.
        /// </summary>
        private static void BeforeGroupPerk(Regiment reg, out Regiment? __state)
        {
            __state = reg;
        }

        private static void AfterGroupPerk(int perk, Regiment? __state, ref Regiment? __result)
        {
            if (!FixState.IsOn(Id) || !_inFame || perk != EmbeddedReportersPerk || __state == null)
            {
                return;
            }

            if (__result == null && __state.HasPerk(perk))
            {
                __result = __state;
            }

            // Recorded whether or not the walk found the unit itself: the level
            // redirect below needs to know which unit was asked about and who
            // was found for it.
            _askedAbout = __state;
            _holder = __result;
        }

        // --- the level ------------------------------------------------------

        private static void AfterPerkLevel(Regiment __instance, int perkid, ref int __result)
        {
            if (!_inFame)
            {
                return;
            }

            if (perkid != EmbeddedReportersPerk || !ReferenceEquals(__instance, _askedAbout))
            {
                return;
            }

            Regiment? holder = _holder;

            // Consumed: one holder search answers exactly one level question,
            // and clearing before the recursive call below keeps this postfix
            // from redirecting the holder's own answer.
            _askedAbout = null;
            _holder = null;

            if (holder != null && !ReferenceEquals(holder, __instance))
            {
                __result = holder.GetPerkLevel(perkid);
            }
        }
    }
}
