using System;
using System.Collections;
using GTCW.Harness.GameState;
using HarmonyLib;
using UnityEngine;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// What a campaign or battle test does to the running game: wait, move the
    /// clock, keep the simulation going. Every wait is a coroutine step, used
    /// with <c>yield return</c>, and every one has a time limit that fails the
    /// test with a message rather than hanging the process.
    /// </summary>
    public static class Live
    {
        /// <summary>The game speed the harness holds the simulation at. Campaign default 50.</summary>
        public static float Speed { get; set; } = 50f;

        /// <summary>How many times the harness had to unpause the game; panels and videos pause it.</summary>
        public static int Unpauses { get; private set; }

        internal static bool Running { get; set; }

        /// <summary>
        /// While true, the game's end-of-campaign and end-of-battle calls are
        /// counted in <see cref="EndAttempts"/> and swallowed, so a test can
        /// drive a scenario into a losing state, assert that the game tried to
        /// end it, and carry on with the next phase in the same session.
        /// </summary>
        public static bool HoldSessionOpen { get; set; }

        /// <summary>End-of-campaign or end-of-battle calls swallowed since the last reset.</summary>
        public static int EndAttempts { get; set; }

        /// <summary>The speed <see cref="Speed"/> returns to before each test: 50 in a campaign, 30 in a battle.</summary>
        internal static float DefaultSpeed { get; set; } = 50f;

        /// <summary>
        /// Battle upkeep: close any modal prompt the way its Continue button
        /// does, and end deployment the way the Play button does.
        /// </summary>
        /// <remarks>
        /// A battle's day machine (<c>BattleUnits.eodcycle</c>) only advances
        /// while no prompt is open, and <c>FireBullet</c> refuses every shot
        /// while it is above zero. With nobody to click Continue on the
        /// deployment prompt, a headless battle would sit with both armies
        /// deployed and never fire — which is what the spikes saw.
        /// </remarks>
        internal static bool DriveBattle { get; set; }

        /// <summary>Prompts the harness closed in this battle.</summary>
        public static int PromptsClosed { get; private set; }

        /// <summary>Deployment phases the harness ended in this battle.</summary>
        public static int DeploymentsEnded { get; private set; }

        private static int? _heldChapter;

        /// <summary>
        /// Keeps <c>Policy.CurrentChapter</c> at a value for objectives that are
        /// chapter-gated. A clock jump skips the chapter policies that would
        /// normally move it, so a test that jumps to 1864 has to say which
        /// chapter it wants.
        /// </summary>
        public static void HoldChapter(int chapter) => _heldChapter = chapter;

        public static void ReleaseChapter() => _heldChapter = null;

        internal static void Reset()
        {
            _heldChapter = null;
            Speed = DefaultSpeed;
            HoldSessionOpen = false;
            EndAttempts = 0;
        }

        /// <summary>Harmony prefix on SetEndOfCampaign / SetEndOfBattle.</summary>
        internal static bool AllowSessionEnd()
        {
            if (!HoldSessionOpen)
            {
                return true;
            }

            EndAttempts++;
            return false;
        }

        /// <summary>Per-frame upkeep, run alongside the tests for the whole session.</summary>
        internal static IEnumerator Service()
        {
            while (true)
            {
                if (_heldChapter is int chapter)
                {
                    Policy.CurrentChapter = chapter;
                }

                if (DriveBattle)
                {
                    BattleUpkeep();
                }

                if (Running && GameVars.gamepaused)
                {
                    Unpauses++;
                    GameVars.SetGameSpeed(Speed, overrideminmax: true);
                }

                yield return null;
            }
        }

        /// <summary>The battle's day machine: 0 is daylight fighting; anything else stops every shot.</summary>
        public static int EndOfDayCycle => Clock.Units == null ? -1 : Clock.Units.eodcycle;

        private static void BattleUpkeep()
        {
            PanelHandler? panels = Traverse.Create(typeof(GameVars)).Field("panelhandler").GetValue<PanelHandler>();
            if (panels != null && panels.prompt_panelopen != -1)
            {
                panels.HidePanel(panels.panel, panels.panel_prompt);
                PromptsClosed++;
            }

            BattleUI? ui = Clock.Units == null ? null : Traverse.Create(Clock.Units).Field("bui").GetValue<BattleUI>();
            if (ui != null && ui.deploymentmodeactive)
            {
                GameVars.SetGameSpeed(Speed, overrideminmax: true);
                ui.SetActiveDeploymentPhase(active: false);
                DeploymentsEnded++;
            }
        }

        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        public static IEnumerator Seconds(float realSeconds)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        public static IEnumerator Until(Func<bool> condition, float timeoutSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end)
                {
                    throw new ExpectationFailedException($"timed out after {timeoutSeconds:0} s waiting for {what}");
                }

                yield return null;
            }
        }

        /// <summary>Lets the campaign clock run forward by a number of game days.</summary>
        public static IEnumerator GameDays(float days, float timeoutSeconds = 120f)
        {
            float target = Clock.Now + days / 365.25f;
            yield return Until(() => Clock.Now >= target, timeoutSeconds, $"{days} game days to pass");
        }

        /// <summary>
        /// When the campaign-data pass last ran (<c>BattleUnits.campaigndataupdate</c>):
        /// economy, politics, objectives — the pass most campaign fixes live in.
        /// </summary>
        public static float LastCampaignPass =>
            Traverse.Create(Clock.Units).Field("campaigndataupdate").GetValue<float>();

        /// <summary>Waits for the next campaign-data pass to complete.</summary>
        public static IEnumerator NextCampaignPass(float timeoutSeconds = 60f)
        {
            float last = LastCampaignPass;
            yield return Until(() => LastCampaignPass != last, timeoutSeconds, "the next campaign-data pass");
        }

        /// <summary>
        /// Moves the campaign clock and waits out the first two passes after it.
        /// </summary>
        /// <remarks>
        /// The first pass after a jump sees the whole jump as elapsed time and
        /// moves morale, money and support by years' worth — in the spike it
        /// took Union morale to 1.000. Set the scenario's own state after this
        /// returns, not before. Autosave, which a jump would otherwise trigger,
        /// is always off in a harness run.
        /// </remarks>
        public static IEnumerator JumpTo(int day, int month, int year)
        {
            Clock.Set(day, month, year);
            yield return NextCampaignPass();
            yield return NextCampaignPass();
        }
    }
}
