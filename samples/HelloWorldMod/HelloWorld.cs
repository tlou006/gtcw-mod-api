using BepInEx;
using GTCW.ModApi;

namespace HelloWorldMod
{
    /// <summary>
    /// The smallest useful mod: reports what the library can do on this build,
    /// then logs each milestone of the next campaign or battle load.
    /// </summary>
    [BepInPlugin("gtcw.sample.helloworld", "Hello World", "0.1.0")]
    [BepInDependency("gtcw.modapi", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class HelloWorld : BaseUnityPlugin
    {
        private void Awake()
        {
            // Registration happens here, at plugin load — not in response to a
            // game event. The game applies policy effects at boot gate 8, long
            // before any session event a mod can see, so anything registered
            // later is already too late.

            if (!ModApi.IsInitialised)
            {
                Logger.LogError("GTCW.ModApi did not load; nothing to do.");
                return;
            }

            // Branch on capabilities rather than assuming. This is what keeps a
            // mod alive across a game patch that breaks one feature.
            if (ModApi.Capabilities.Has(Capability.Lifecycle))
            {
                ModApi.Lifecycle.SessionStarted += session =>
                    Logger.LogInfo($"Session started: {session.Kind}");

                ModApi.Lifecycle.MilestoneReached += (session, milestone) =>
                    Logger.LogInfo($"{session.Kind} reached {milestone}");

                ModApi.Lifecycle.SessionEnding += session =>
                    Logger.LogInfo($"Session ending: {session.Kind}");
            }
            else
            {
                Logger.LogWarning(
                    "Lifecycle unavailable: " +
                    ModApi.Capabilities.Explain(Capability.Lifecycle));
            }

            if (ModApi.Capabilities.Has(Capability.BattleEvents))
            {
                ModApi.Battle.Started += session =>
                    Logger.LogInfo("Battle started — a mod is running inside the game.");

                ModApi.Battle.Ended += session =>
                    Logger.LogInfo("Battle ended.");
            }
            else
            {
                Logger.LogWarning(
                    "Battle events unavailable: " +
                    ModApi.Capabilities.Explain(Capability.BattleEvents));
            }
        }
    }
}
