using System;
using System.Reflection;
using GTCW.ModApi;
using HarmonyLib;
using Api = GTCW.ModApi.ModApi;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-001 — the 1864 election charges its national-morale penalty twice,
    /// which turns the −20 both tooltips promise into −40 and ends the campaign.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Objectives 26 (<i>Lincoln's Second Term</i>) and 27 (<i>Demoralize Union
    /// Voters</i>) test the same date and each apply their own −0.20 under the
    /// same <c>lastupdate</c> guard. In the branch where Lincoln loses, both
    /// fire in the same sweep. Union morale then sits below
    /// <c>breakmoraletrigger</c> and objective 1 ends the campaign within one
    /// campaign-data pass — about ten game minutes.
    /// </para>
    /// <para>
    /// <b>Why this watches the charge instead of predicting it.</b> The obvious
    /// implementation replicates each objective's window test in a prefix and
    /// spoofs the pref to zero. That needs the fresh value of
    /// <c>deactivated</c>, which <c>CheckIfAccomplished</c> reassigns on its
    /// own first line — so a prefix reads the previous pass's value, and
    /// guessing wrong the other way would suppress the <em>only</em> charge
    /// rather than the second one. Marking which objective is executing and
    /// filtering at <c>AdjustNationalMorale</c> observes what actually happens,
    /// and needs no copy of the game's date arithmetic.
    /// </para>
    /// </remarks>
    internal static class ElectionDoubleCharge
    {
        private const string Id = "GTCW-001";

        /// <summary>Union national morale. Both penalties are charged to it.</summary>
        private const int Union = 0;

        /// <summary>The reason code both election objectives charge under.</summary>
        private const int ElectionReason = 8;

        /// <summary>
        /// Which objective's <c>CheckIfAccomplished</c> is on the stack, or 0.
        /// </summary>
        private static int _executing;

        /// <summary>
        /// Set once the election penalty has been charged in this campaign.
        /// Reset on every session start, so reloading a pre-election save
        /// starts clean.
        /// </summary>
        private static bool _charged;

        public static FixGroup Group() => new FixGroup(
            "CampaignObjective:CheckIfAccomplished + GameVars+Alliance:AdjustNationalMorale",
            new[]
            {
                new Fix(
                    Id,
                    "The 1864 election costs the Union 20 points of national morale, not 40. " +
                    "Unfixed, losing the election is an immediate campaign loss.",
                    FixClass.Code,
                    defaultEnabled: true,
                    SaveSafety.Stateless,
                    "mechanics-findings/manual-vs-code.md §32.2; code-findings/24-campaign-objectives.md §3.2",
                    Probe),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("CampaignObjective", "CheckIfAccomplished"),
            () => FixProbe.RequireField("CampaignObjective", "UniqueObjectiveID"),
            () => FixProbe.RequireMethod("GameVars+Alliance", "AdjustNationalMorale"),
            () => FixProbe.RequireField("GamePrefs", "lincolninfluenceunionmorale"),
            () => FixProbe.RequireField("GamePrefs", "demoralizevotersunionmoraleinfluence"),
            () => Api.Capabilities.Has(Capability.Lifecycle)
                ? ProbeResult.Pass()
                : ProbeResult.Fail(
                    "the latch needs session events to reset per campaign: " +
                    Api.Capabilities.Explain(Capability.Lifecycle)));

        private static void Apply(Harmony harmony)
        {
            Patch(harmony, "CampaignObjective:CheckIfAccomplished",
                prefix: nameof(BeforeObjective), finalizer: nameof(AfterObjective));

            Patch(harmony, "GameVars+Alliance:AdjustNationalMorale",
                prefix: nameof(BeforeMoraleChange));

            Api.Lifecycle.SessionStarted += OnSessionStarted;
        }

        private static void Patch(Harmony harmony, string target, string? prefix = null, string? finalizer = null)
        {
            MethodInfo original = AccessTools.Method(target)
                ?? throw new InvalidOperationException($"'{target}' not found");

            harmony.Patch(
                original,
                prefix: prefix == null
                    ? null
                    : new HarmonyMethod(AccessTools.Method(typeof(ElectionDoubleCharge), prefix)),
                finalizer: finalizer == null
                    ? null
                    : new HarmonyMethod(AccessTools.Method(typeof(ElectionDoubleCharge), finalizer)));
        }

        private static void OnSessionStarted(IGameSession session)
        {
            _charged = false;
            _executing = 0;
        }

        // --- marking which objective is running ---------------------------------
        //
        // Deliberately not gated on FixState.IsOn: this pair is pure
        // bookkeeping, the group is only patched when the fix is enabled, and a
        // prefix and finalizer that could disagree about whether to run is a
        // worse failure than two integer assignments per objective per pass.

        private static void BeforeObjective(CampaignObjective __instance, out int __state)
        {
            __state = _executing;
            _executing = __instance.UniqueObjectiveID;
        }

        /// <summary>
        /// A finalizer rather than a postfix, so an exception inside the
        /// objective sweep cannot leave the marker set and silently suppress an
        /// unrelated morale change for the rest of the session.
        /// </summary>
        private static void AfterObjective(int __state)
        {
            _executing = __state;
        }

        // --- the suppression ----------------------------------------------------

        /// <summary>
        /// Lets the first election charge through and drops the second.
        /// </summary>
        /// <returns>
        /// False to skip the original call, which is how the second −0.20 is
        /// prevented rather than refunded. Refunding would have to fight
        /// <c>AdjustNationalMorale</c>'s own clamp at zero.
        /// </returns>
        private static bool BeforeMoraleChange(GameVars.Alliance __instance, int type, float value)
        {
            if (!FixState.IsOn(Id) || type != ElectionReason)
            {
                return true;
            }

            // Only while one of the two election objectives is on the stack, and
            // only for a value handed straight from one of their two prefs.
            // Exact float comparison is right here: the caller passes the pref
            // itself, with no arithmetic in between.
            if (_executing != 26 && _executing != 27)
            {
                return true;
            }

            if (value != GamePrefs.lincolninfluenceunionmorale &&
                value != GamePrefs.demoralizevotersunionmoraleinfluence)
            {
                return true;
            }

            // Both objectives charge the Union. Anything else under this reason
            // code is not the election.
            if (GameVars.alliance == null ||
                GameVars.alliance.Length <= Union ||
                !ReferenceEquals(__instance, GameVars.alliance[Union]))
            {
                return true;
            }

            if (_charged)
            {
                FixState.Info(
                    $"{Id}: suppressed a second 1864 election penalty ({value:F3}) from objective " +
                    $"{_executing}; the first has already been applied.");
                return false;
            }

            _charged = true;
            FixState.Info($"{Id}: 1864 election penalty applied once ({value:F3}) by objective {_executing}.");
            return true;
        }
    }
}
