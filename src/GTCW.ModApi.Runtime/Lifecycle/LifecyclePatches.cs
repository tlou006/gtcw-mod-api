using System;
using System.Reflection;
using GTCW.ModApi;
using GTCW.ModApi.Runtime.Capabilities;
using HarmonyLib;
using UnityEngine;

namespace GTCW.ModApi.Runtime.Lifecycle
{
    /// <summary>
    /// The Harmony patches behind <see cref="ILifecycle"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four patch points cover every transition, which took reading the real
    /// code to establish:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// The three <c>Load*Scenario</c> methods start a session. Every queued
    /// load funnels back into them — <c>UpdateBatchOperations</c>'
    /// <c>bo_loadcampaignscenario</c>, <c>bo_loadhistoricbattle</c>,
    /// <c>bo_loadcampaignbattle</c> and the two from-save variants all call
    /// one of these three — so patching them catches direct and deferred
    /// loads alike.
    /// </item>
    /// <item>
    /// <c>RebuildScenes</c> ends one. It holds the only non-additive
    /// <c>LoadScene("Main")</c> in the game and has exactly <b>one</b> caller,
    /// the batch queue, which fifteen sites feed. That makes it the single
    /// universal teardown chokepoint.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Why not the <c>Exit*Scenario</c> methods:</b> they only clear
    /// gameplay state and reset <c>currentoperation</c> — they destroy nothing.
    /// And <c>ExitCampaignBattleAndRebuildCampaign</c>, the battle→campaign
    /// return, never calls either of them. Patching those would have leaked a
    /// dead session on the most common transition in the game.
    /// </para>
    /// <para>
    /// A scenario load is <c>LoadSceneMode.Additive</c> on top of
    /// <c>Main</c>; only the teardown is non-additive. So a session's objects
    /// stay alive until <c>RebuildScenes</c>, and <c>SessionEnding</c> is
    /// raised from a prefix there while they still exist.
    /// </para>
    /// </remarks>
    internal static class LifecyclePatches
    {
        private static LifecycleService? _service;

        /// <summary>
        /// Probes the four patch targets and the frame counter. String-based on
        /// purpose: a rename must disable the feature on the player's machine,
        /// not throw inside a postfix.
        /// </summary>
        public static ProbeResult Probe() => GameProbe.All(
            () => GameProbe.RequireField("GameVars", "frame"),
            () => GameProbe.RequireMethod("SceneManagement", "LoadCampaignScenario"),
            () => GameProbe.RequireMethod("SceneManagement", "LoadBattleScenario"),
            () => GameProbe.RequireMethod("SceneManagement", "LoadCampaignBattleScenario"),
            () => GameProbe.RequireMethod("SceneManagement", "RebuildScenes"),
            () => GameProbe.RequireMethod("CampaignController", "Update"),
            () => GameProbe.RequireMethod("BattleController", "Update"));

        public static void Apply(Harmony harmony, LifecycleService service)
        {
            _service = service;

            Patch(harmony, "SceneManagement:LoadCampaignScenario",
                postfix: nameof(AfterLoadCampaign));
            Patch(harmony, "SceneManagement:LoadBattleScenario",
                postfix: nameof(AfterLoadBattle));
            Patch(harmony, "SceneManagement:LoadCampaignBattleScenario",
                postfix: nameof(AfterLoadBattle));
            Patch(harmony, "SceneManagement:RebuildScenes",
                prefix: nameof(BeforeRebuildScenes));
            Patch(harmony, "CampaignController:Update",
                postfix: nameof(AfterCampaignUpdate));
            Patch(harmony, "BattleController:Update",
                postfix: nameof(AfterBattleUpdate));

            // Quitting the game routes through no teardown at all: the only
            // Application.Quit() in the assembly is a bare call from the quit
            // button, and nothing implements OnApplicationQuit. So without this
            // the last session of every play session never ends, which the
            // first live run showed. Unity raises this before the OnDestroy
            // cascade, so a subscriber still sees live objects.
            Application.quitting += OnApplicationQuitting;
        }

        private static void Patch(
            Harmony harmony,
            string target,
            string? prefix = null,
            string? postfix = null)
        {
            MethodInfo original = AccessTools.Method(target)
                ?? throw new InvalidOperationException($"'{target}' not found");

            harmony.Patch(
                original,
                prefix: prefix == null ? null : Own(prefix),
                postfix: postfix == null ? null : Own(postfix));
        }

        private static HarmonyMethod Own(string name) =>
            new HarmonyMethod(AccessTools.Method(typeof(LifecyclePatches), name));

        // --- session boundaries -------------------------------------------------

        private static void AfterLoadCampaign() =>
            _service?.BeginSession(SessionKind.Campaign);

        private static void AfterLoadBattle() =>
            _service?.BeginSession(SessionKind.Battle);

        private static void BeforeRebuildScenes() =>
            _service?.EndSession();

        // EndSession is idempotent, so a quit that somehow follows a rebuild
        // closes nothing twice.
        private static void OnApplicationQuitting() =>
            _service?.EndSession();

        // --- boot milestones ----------------------------------------------------

        // Reached from a postfix, so the frame counter has already been through
        // the stall check: if it reads above 3 here, the map is genuinely ready.
        // MarkReached makes each milestone fire once per session, which is what
        // makes the rewind harmless rather than a source of duplicates.

        private static void AfterCampaignUpdate() => Advance(SessionKind.Campaign);

        private static void AfterBattleUpdate() => Advance(SessionKind.Battle);

        private static void Advance(SessionKind kind)
        {
            LifecycleService? service = _service;
            if (service == null || service.Current?.Kind != kind)
            {
                return;
            }

            int frame = GameVars.frame;

            foreach ((int gate, Milestone milestone) in BootMilestones.For(kind))
            {
                if (frame >= gate)
                {
                    service.ReachMilestone(milestone);
                }
            }
        }
    }
}
