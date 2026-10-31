using BepInEx;
using HarmonyLib;

// The facade class `ModApi` sits in a namespace of the same name, and this
// project's own namespace begins with `GTCW` — so a bare `ModApi` binds to the
// namespace `GTCW.ModApi` rather than the class, and `using GTCW.ModApi;` does
// not help. Any mod whose namespace starts with `GTCW.` needs this alias; mods
// outside it never see the problem.
using Api = GTCW.ModApi.ModApi;

namespace GTCW.BugFixes
{
    /// <summary>
    /// Entry point for the bug-fix mod.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate plugin with its own GUID, version and config file, which is
    /// the point: the library stays behaviour-neutral and the player chooses
    /// the repairs. See <c>plan.md</c>, "The library does not fix the game".
    /// </para>
    /// <para>
    /// The ModApi dependency is hard rather than soft even though most fixes
    /// are plain Harmony patches that could run without it. Two reasons: the
    /// library owns build identification, so without it a fix cannot know
    /// whether it is looking at the build its evidence was gathered on; and
    /// load order matters for anything hooked to a session milestone.
    /// </para>
    /// </remarks>
    [BepInPlugin(PluginGuid, "GTCW Bug Fixes", "0.1.0")]
    [BepInDependency("gtcw.modapi", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class BugFixesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "gtcw.bugfixes";

        private Harmony? _harmony;

        private void Awake()
        {
            if (!Api.IsInitialised)
            {
                Logger.LogError("GTCW.ModApi did not initialise; no fixes applied.");
                return;
            }

            var registry = new FixRegistry(Config, Logger);

            // Fixes register here, one line each, grouped by the method they
            // patch. Nothing is registered yet: IMPLEMENTATION.md specifies all
            // twenty-one rows and the order to build them in, and an empty
            // registry is the correct idle state rather than a placeholder —
            // the mod currently loads, reports that nothing is enabled, and
            // patches nothing.
            //
            //   registry.Add(Fixes.ElectionDoubleCharge.Group);   // GTCW-001
            //   registry.Add(Fixes.SupportBreakdownNaN.Group);    // GTCW-002
            //   ...

            _harmony = new Harmony(PluginGuid);
            registry.ApplyAll(_harmony);
        }

        private void OnDestroy()
        {
            // Unpatching on unload is not symmetry for its own sake: a fix that
            // could not be removed would make "uninstalling restores exactly
            // what you had" false, and that is a promise the compatibility
            // boundary makes.
            _harmony?.UnpatchSelf();
        }
    }
}
