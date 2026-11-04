using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Engine;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;
using HarmonyLib;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-001 — objectives 26 and 27 each charge −0.20 Union national morale
    /// on the first pass after the 1864 election, so losing it costs −0.40 and
    /// drops morale under <c>breakmoraletrigger</c>.
    /// </summary>
    [FixTests("GTCW-001")]
    [Campaign("001", "A")]
    public sealed class ElectionDoubleChargeTests
    {
        /// <summary>
        /// Just under <c>lincolnmaintainmoraleabove</c>, the repro in
        /// IMPLEMENTATION.md: the branch where Lincoln loses.
        /// </summary>
        private const float StartMorale = 0.49f;

        [EngineTest]
        public void TheElectionCostsTwentyPointsNotForty() =>
            Verify.Fix(
                "GTCW-001",
                () => SweepFrom(StartMorale),
                defect: morale =>
                {
                    Expect.Near(StartMorale - 0.40f, morale, "Union morale after the election sweep");
                    Expect.Below(GamePrefs.breakmoraletrigger, morale, "morale against the campaign-ending trigger");
                },
                repaired: morale =>
                {
                    Expect.Near(StartMorale - 0.20f, morale, "Union morale after the election sweep");
                    Expect.AtLeast(GamePrefs.breakmoraletrigger, morale, "morale against the campaign-ending trigger");
                    Expect.Logged("suppressed a second 1864 election penalty", "the fix's own report");
                });

        /// <summary>
        /// The latch resets with each session, so loading a pre-election save
        /// charges the election once again rather than never.
        /// </summary>
        [EngineTest]
        public void EachSessionChargesTheElectionOnce()
        {
            Toggle.Only("GTCW-001");
            float first = SweepFrom(StartMorale);
            Session.Start();
            float second = SweepFrom(StartMorale);
            Expect.Near(StartMorale - 0.20f, first, "first session's election");
            Expect.Near(StartMorale - 0.20f, second, "second session's election, after a reload");
        }

        /// <summary>
        /// The repro and confirm IMPLEMENTATION.md writes for this fix, in the
        /// real game: the shipped objectives, swept by the real campaign-data
        /// pass. Unfixed, objective 1 — "Union national morale broken" — then
        /// marks the Union defeated.
        /// </summary>
        /// <remarks>
        /// The game does not end the campaign in the same pass: objective 1
        /// opens the defeat newspaper first and calls <c>SetEndOfCampaign</c>
        /// only once no newspaper is open, i.e. once a player has read it. So the
        /// measure is objective 1's own <c>accomplished</c>, the game's record of
        /// the loss; <see cref="Live.HoldSessionOpen"/> catches the ending itself
        /// should it come.
        /// </remarks>
        [CampaignTest]
        public IEnumerator TheElectionDoesNotLoseTheCampaignInTheRealGame()
        {
            yield return Live.JumpTo(13, 11, 1864);

            // Both objectives are chapter-gated, and the jump skipped the
            // chapter policies that would have moved the chapter on.
            CampaignObjective lincoln = Objective(26);
            Live.HoldChapter(lincoln.ObjectiveChapters[0]);
            Live.HoldSessionOpen = true;

            yield return Verify.FixLive<(float morale, bool unionLost)>(
                "GTCW-001",
                LiveElection,
                defect: r =>
                {
                    Expect.Near(StartMorale - 0.40f, r.morale, "Union morale after the election pass", 0.02f);
                    Expect.True(r.unionLost, "objective 1 records the Union's defeat");
                },
                repaired: r =>
                {
                    Expect.Near(StartMorale - 0.20f, r.morale, "Union morale after the election pass", 0.02f);
                    Expect.True(!r.unionLost, "objective 1 does not record a defeat");
                });
        }

        private static IEnumerator LiveElection(Box<(float, bool)> result)
        {
            Traverse.Create(typeof(GTCW.BugFixes.Fixes.ElectionDoubleCharge)).Field("_charged").SetValue(false);
            float election = new Tools.Date(9, 11, 1864).yearfraction + 0.002739726f;
            foreach (CampaignObjective objective in CampaignObjective.allcampaignobjectives.Where(o => o.UniqueObjectiveID == 26 || o.UniqueObjectiveID == 27))
            {
                // As if the last pass ran just before the election.
                objective.accomplished = false;
                objective.lastupdate = election - 0.01f;
            }

            CampaignObjective unionBroken = Objective(1);
            unionBroken.accomplished = false;
            GameVars.alliance[0].nationalmorale = StartMorale;
            yield return Live.NextCampaignPass();
            float morale = GameVars.alliance[0].nationalmorale;

            // Objective 1 reads the new morale on the pass after.
            yield return Live.NextCampaignPass();
            result.Value = (morale, unionBroken.accomplished);
            if (unionBroken.deactivated)
            {
                Report.Note("objective 1 was deactivated during the measurement");
            }

            GameVars.alliance[0].nationalmorale = 0.9f;
            unionBroken.accomplished = false;
        }

        private static CampaignObjective Objective(int id) =>
            CampaignObjective.allcampaignobjectives.SingleOrDefault(o => o.UniqueObjectiveID == id)
            ?? throw new ExpectationFailedException($"objective {id} is not loaded in this scenario");

        /// <summary>
        /// Runs objectives 26 and 27 on the first pass after the election, the
        /// way the campaign-data pass does, and returns Union morale.
        /// </summary>
        private static float SweepFrom(float morale)
        {
            // The threshold both objectives test, in Tools.Date's arithmetic.
            float election = new Tools.Date(9, 11, 1864).yearfraction + 0.002739726f;
            Clock.Set(13, 11, 1864);
            Expect.AtLeast(election, Clock.Now, "the game's clock against the election threshold");

            GameVars.alliance[0].nationalmorale = morale;
            float previousPass = election - 0.001f;
            Objective(26, previousPass).CheckIfAccomplished();
            Objective(27, previousPass).CheckIfAccomplished();
            return GameVars.alliance[0].nationalmorale;
        }

        private static CampaignObjective Objective(int id, float lastupdate)
        {
            CampaignObjective objective = Fakes.Make<CampaignObjective>();
            objective.UniqueObjectiveID = id;
            objective.ObjectiveChapters = new int[0];
            objective.ObjectiveScenario = new List<string> { GamePrefs.leveltoload };
            objective.validfromdate = new Tools.Date(1, 1, 1861);
            objective.validtodate = new Tools.Date(1, 1, 1866);
            objective.objectives = new List<object>();
            objective.precampaignobjectives = new List<CampaignObjective>();
            objective.lastupdate = lastupdate;
            return objective;
        }
    }
}
