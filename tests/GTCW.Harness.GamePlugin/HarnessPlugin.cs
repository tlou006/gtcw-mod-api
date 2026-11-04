using BepInEx;
using GTCW.Harness.Game;

namespace GTCW.Harness.GamePlugin
{
    /// <summary>
    /// Hosts the harness inside the game. Loads after the bug-fix mod, so the
    /// mod has applied its patches from the runner's config by the time the
    /// harness checks them.
    /// </summary>
    /// <remarks>
    /// Does nothing unless the runner launched the game: every input arrives in
    /// environment variables (see <see cref="GameRunner"/>), so a copy of this
    /// plugin left in a real install would only log one line.
    /// </remarks>
    [BepInPlugin(PluginGuid, "GTCW Harness", "0.1.0")]
    [BepInDependency("gtcw.bugfixes", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class HarnessPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "gtcw.harness";

        private void Awake()
        {
            if (!GameRunner.Requested)
            {
                Logger.LogWarning("Not launched by GTCW.Harness.Runner; the harness does nothing.");
                return;
            }

            StartCoroutine(GameRunner.Drive(this));
        }
    }
}
