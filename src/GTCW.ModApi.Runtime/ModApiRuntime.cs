using GTCW.ModApi;
using GTCW.ModApi.Runtime.Battle;
using GTCW.ModApi.Runtime.Capabilities;
using GTCW.ModApi.Runtime.Lifecycle;
using GTCW.ModApi.Runtime.Projects;
using HarmonyLib;

namespace GTCW.ModApi.Runtime
{
    /// <summary>
    /// Bootstraps the volatile layer and binds the public facade. Called once,
    /// by the BepInEx plugin.
    /// </summary>
    internal sealed class ModApiRuntime : IModApiHost
    {
        /// <summary>
        /// Harmony id. One instance for the whole library, so every patch can
        /// be attributed to us in Harmony's own diagnostics.
        /// </summary>
        private const string HarmonyId = "gtcw.modapi";

        private readonly CapabilityRegistry _capabilities;
        private readonly LifecycleService _lifecycle;
        private readonly BattleEventsService _battle;
        private readonly ProjectRegistry _projects;
        private readonly Harmony _harmony;

        private ModApiRuntime(IModApiLog log)
        {
            Log = log;
            _capabilities = new CapabilityRegistry(log);
            _lifecycle = new LifecycleService(log);
            _battle = new BattleEventsService(log, _lifecycle);
            _projects = new ProjectRegistry(log);
            _harmony = new Harmony(HarmonyId);
        }

        public ICapabilities Capabilities => _capabilities;

        public ILifecycle Lifecycle => _lifecycle;

        public IBattleEvents Battle => _battle;

        public IProjectRegistry Projects => _projects;

        public IModApiLog Log { get; }

        /// <summary>
        /// Identify the build, probe every feature, install what this build
        /// supports, and publish the facade mods talk to.
        /// </summary>
        /// <param name="log">Where the capability report goes.</param>
        /// <param name="managedFolder">The game's <c>Managed</c> folder.</param>
        public static ModApiRuntime Boot(IModApiLog log, string managedFolder)
        {
            var runtime = new ModApiRuntime(log);

            string? hash = BuildIdentity.Hash(managedFolder);
            string? known = BuildIdentity.Describe(hash);

            if (hash == null)
            {
                log.Warning(
                    "Could not read Assembly-CSharp.dll to identify the build. " +
                    "Continuing on probe results alone.");
            }
            else if (known != null)
            {
                log.Info($"Game build recognised: {known}.");
            }
            else
            {
                // Not an error. Identification is not gating — this is exactly
                // the case the probes exist for.
                log.Warning(
                    $"Unrecognised game build (SHA-256 {hash.Substring(0, 16)}…). " +
                    "Probing each feature individually; some may be unavailable.");
            }

            AnchorCheck.Run(log);

            runtime.RegisterFeatures();
            runtime._capabilities.ApplyAll();
            runtime._capabilities.Report();

            ModApi.Bind(runtime);
            return runtime;
        }

        /// <summary>
        /// The feature table. Everything is declared here even when unbuilt, so
        /// the startup report doubles as a roadmap and a mod gets a real reason
        /// rather than a bare false.
        /// </summary>
        private void RegisterFeatures()
        {
            _capabilities.Add(new Feature(
                Capability.Lifecycle,
                "scene-scoped sessions and the boot milestones",
                LifecyclePatches.Probe,
                () => LifecyclePatches.Apply(_harmony, _lifecycle, Log)));

            // Derived from Lifecycle rather than patched separately, so it is
            // available exactly when the lifecycle is.
            _capabilities.Add(new Feature(
                Capability.BattleEvents,
                "Battle.Started / Battle.Ended",
                () => _capabilities.Has(Capability.Lifecycle)
                    ? ProbeResult.Pass()
                    : ProbeResult.Fail("requires the Lifecycle capability, which is off"),
                () => { }));

            _capabilities.Add(Feature.Planned(
                Capability.UnitEvents,
                "per-unit notifications, sampled tier included",
                "plan.md Phase 4, event surface"));

            _capabilities.Add(Feature.Planned(
                Capability.ConfigOverrides,
                "post-parse additive overrides with per-mod attribution",
                "plan.md Phase 4, layered config overrides"));

            _capabilities.Add(Feature.Planned(
                Capability.Modifiers,
                "declarative contributions folded into the game's own passes",
                "plan.md Phase 4, the write path"));

            _capabilities.Add(new Feature(
                Capability.ProjectRegistration,
                "new projects, claiming the shipped PLACEHOLDER slots",
                ProjectPatches.Probe,
                () => ProjectPatches.Apply(_harmony, _projects, Log)));

            _capabilities.Add(Feature.Planned(
                Capability.PolicyRegistration,
                "new policies in ids 180–199, via GameObject cloning",
                "blocked on three live-build unknowns; plan.md Phase 4"));

            _capabilities.Add(Feature.Planned(
                Capability.Persistence,
                "side-car saves keyed off the save folder",
                "plan.md Phase 4, persistence"));

            _capabilities.Add(Feature.Planned(
                Capability.Scheduling,
                "deferred work through the game's batch-operation queue",
                "plan.md Phase 4, scheduling"));
        }
    }
}
