using System;
using System.Reflection;
using HarmonyLib;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-014 — cavalry raids are booked into the trade-warfare ledger with
    /// the wrong sign, so raiding an enemy trade node makes the victim's
    /// displayed raid losses go <em>down</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Category 2's convention is that a cost is stored negative and negated for
    /// display. <c>CBuilding</c> honours it explicitly
    /// (<c>... * (0f - (1f - Condition)) * ...</c>);
    /// <c>Regiment.UpdateRaids</c> adds
    /// <c>resourcesCost * elapsed * efficiency</c>, all three strictly positive.
    /// Because slot 0 is recomputed as the sum of the rest, it corrupts the
    /// total too, and therefore the commerce-war slider, which is a ratio of the
    /// two sides' totals.
    /// </para>
    /// <para>
    /// <b>Fixed at the chokepoint rather than at the defective caller.</b> The
    /// amount is a local inside a loop, so reaching it means a transpiler on a
    /// sixty-line method; normalising the sign in <c>AddTradeWarfare</c> is two
    /// comparisons and no IL. The precondition, verified in the decompile: the
    /// correctly-signed caller never passes a positive for this category, since
    /// its term is negative for any condition below 1 and condition is bounded
    /// above by 1. The prefix is therefore a no-op for every caller but the
    /// broken one.
    /// </para>
    /// <para>
    /// Only category 2 is touched. Categories 1 and 3 have no defective writer,
    /// and a blanket sign rule would silently redefine what a positive means for
    /// them.
    /// </para>
    /// </remarks>
    internal static class RaidLedgerSign
    {
        private const string Id = "GTCW-014";
        private const string Target = "GameVars+Alliance:AddTradeWarfare";

        /// <summary>"Losses through raids" — the category with two writers.</summary>
        private const int Raids = 2;

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    Id,
                    "Cavalry raids are charged to the enemy's trade-warfare losses instead of credited, " +
                    "so the commerce-war panel and its slider report the war the right way round.",
                    FixClass.Code,
                    defaultEnabled: true,
                    SaveSafety.ValueOnly,
                    "code-findings/33-trade-warfare.md §3",
                    Probe),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("GameVars+Alliance", "AddTradeWarfare"),
            () => FixProbe.RequireField("GameVars+Alliance", "tradewarfare"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RaidLedgerSign), nameof(Before))));
        }

        private static void Before(int type, ref float value)
        {
            if (!FixState.IsOn(Id) || type != Raids || value <= 0f)
            {
                return;
            }

            value = 0f - value;
        }
    }
}
