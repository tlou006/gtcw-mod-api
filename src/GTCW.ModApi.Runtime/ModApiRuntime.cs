using GTCW.ModApi;
using GTCW.ModApi.Runtime.Capabilities;
using GTCW.ModApi.Runtime.Lifecycle;

namespace GTCW.ModApi.Runtime
{
    /// <summary>
    /// Bootstraps the volatile layer and binds the public facade. Called once,
    /// by the BepInEx plugin.
    /// </summary>
    internal sealed class ModApiRuntime : IModApiHost
    {
        private readonly CapabilityRegistry _capabilities;
        private readonly LifecycleService _lifecycle;

        private ModApiRuntime(IModApiLog log)
        {
            Log = log;
            _capabilities = new CapabilityRegistry(log);
            _lifecycle = new LifecycleService(log);
        }

        public ICapabilities Capabilities => _capabilities;

        public ILifecycle Lifecycle => _lifecycle;

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
            _capabilities.Add(Feature.Planned(
                Capability.Lifecycle,
                "scene-scoped sessions and the seven boot milestones",
                "plan.md Phase 3, build-out step 4"));

            _capabilities.Add(Feature.Planned(
                Capability.BattleEvents,
                "Battle.Started / Battle.Ended",
                "plan.md Phase 3, build-out step 4"));

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

            _capabilities.Add(Feature.Planned(
                Capability.ProjectRegistration,
                "new projects in the 56 PLACEHOLDER ids",
                "plan.md Phase 4, effect registration"));

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
