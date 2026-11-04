using System.Collections;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Engine;
using GTCW.Harness.Game;
using GTCW.ModApi;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-002 — <c>AdjustSupport</c> stores <c>Mathf.Pow(amount, 0.5f)</c>,
    /// NaN for a loss, and its <c>== float.NaN</c> guard can never fire, so the
    /// reason code's breakdown slot is never reset again.
    /// </summary>
    [FixTests("GTCW-002")]
    [Campaign("001", "A")]
    public sealed class SupportBreakdownNaNTests
    {
        /// <summary>An arbitrary reason code; the defect is the same for all of them.</summary>
        private const int Reason = 5;

        [EngineTest]
        public void ALossLeavesARealNumber() =>
            Verify.Fix(
                "GTCW-002",
                () =>
                {
                    GameVars.Nation state = AStateWithSupport();
                    state.AdjustSupport(0, -0.05f, Reason);
                    return state.supportinfluencelast[0, Reason];
                },
                defect: last => Expect.NaN(last, "the breakdown tracker after a loss"),
                repaired: last => Expect.Near(0f, last, "the breakdown tracker after a loss"));

        /// <summary>
        /// The confirm in IMPLEMENTATION.md: once the tracker is a real number,
        /// the reset it gates fires on the next adjustment.
        /// </summary>
        [EngineTest]
        public void TheGuardedResetFiresAgain() =>
            Verify.Fix(
                "GTCW-002",
                () =>
                {
                    GameVars.Nation state = AStateWithSupport();
                    state.supportinfluence[0, Reason] = 0f;
                    state.supportinfluencelast[0, Reason] = 0f;
                    state.AdjustSupport(0, -0.05f, Reason);
                    state.AdjustSupport(0, 0.01f, Reason);
                    return state.supportinfluence[0, Reason];
                },
                defect: total => Expect.Near(-0.04f, total, "the slot after a loss then a gain (never reset)"),
                repaired: total => Expect.Near(0.01f, total, "the slot after a loss then a gain (reset, then the gain)"));

        /// <summary>
        /// A save written before the fix carries NaN in slots whose reason
        /// never fires again; the fix sweeps them once the campaign is live.
        /// </summary>
        [EngineTest]
        public void TheCampaignLiveSweepClearsNaNCarriedInASave() =>
            Verify.Fix(
                "GTCW-002",
                () =>
                {
                    GameVars.Nation state = AStateWithSupport();
                    state.supportinfluencelast[1, 7] = float.NaN;
                    Session.Reach(Milestone.CampaignLive);
                    return state.supportinfluencelast[1, 7];
                },
                defect: last => Expect.NaN(last, "a NaN loaded from a save, after CampaignLive"),
                repaired: last => Expect.Near(0f, last, "a NaN loaded from a save, after CampaignLive"));

        /// <summary>
        /// The live campaign, after its own passes have run: the patch is
        /// applied by the real plugin and the loss goes through the game's Mono.
        /// </summary>
        [CampaignTest]
        public IEnumerator ALossInTheLiveCampaignLeavesARealNumber()
        {
            yield return Live.NextCampaignPass();
            yield return Verify.FixLive<float>(
                "GTCW-002",
                result =>
                {
                    GameVars.Nation state = AStateWithSupport();
                    state.AdjustSupport(0, -0.05f, Reason);
                    result.Value = state.supportinfluencelast[0, Reason];
                    return Live.Frames(1);
                },
                defect: last => Expect.NaN(last, "the breakdown tracker after a loss"),
                repaired: last => Expect.Near(0f, last, "the breakdown tracker after a loss"));
        }

        /// <summary>A shipped state with enough Union support that a 0.05 loss is not clamped.</summary>
        private static GameVars.Nation AStateWithSupport() =>
            GameVars.nation.First(n => n.support != null && n.support[0] > 0.2f);
    }
}
