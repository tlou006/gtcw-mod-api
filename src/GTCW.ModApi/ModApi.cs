using System;

namespace GTCW.ModApi
{
    /// <summary>
    /// The entry point a mod talks to. Everything hangs off here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All members are main-thread only. The game runs its entire simulation on
    /// Unity's <c>Update</c> with no fixed timestep, so there is no background
    /// thread to call from and <b>no determinism guarantee across machines</b>.
    /// </para>
    /// <para>
    /// Registration (projects, policies, modifiers) must happen while your
    /// plugin loads, not in response to a game event: the game applies policy
    /// effects at boot gate 8, long before any session event a mod can see.
    /// </para>
    /// </remarks>
    public static class ModApi
    {
        private static IModApiHost? _host;

        /// <summary>Set once by the runtime during BepInEx bootstrap.</summary>
        internal static void Bind(IModApiHost host) =>
            _host = host ?? throw new ArgumentNullException(nameof(host));

        private static IModApiHost Host =>
            _host ?? throw new InvalidOperationException(
                "GTCW.ModApi is not initialised. The runtime binds it during BepInEx " +
                "bootstrap; if you are seeing this, GTCW.ModApi.Plugin did not load. " +
                "Declare a BepInDependency on it.");

        /// <summary>True once the runtime has bound itself.</summary>
        public static bool IsInitialised => _host != null;

        /// <summary>What this build of the game supports.</summary>
        public static ICapabilities Capabilities => Host.Capabilities;

        /// <summary>Scene-scoped lifecycle and the boot milestones.</summary>
        public static ILifecycle Lifecycle => Host.Lifecycle;

        /// <summary>Battle notifications.</summary>
        public static IBattleEvents Battle => Host.Battle;

        /// <summary>
        /// Adds projects to the game. Register while your plugin loads — see
        /// <see cref="IProjectRegistry.Register"/>.
        /// </summary>
        public static IProjectRegistry Projects => Host.Projects;

        /// <summary>
        /// The library's own log. Mods should prefer their BepInEx logger; this
        /// is for reporting through the ModApi's channel so capability and
        /// conflict messages stay together.
        /// </summary>
        public static IModApiLog Log => Host.Log;
    }

    /// <summary>
    /// Implemented by <c>GTCW.ModApi.Runtime</c>. Exists so the public layer can
    /// expose behaviour it does not implement, without referencing a game type.
    /// </summary>
    internal interface IModApiHost
    {
        ICapabilities Capabilities { get; }
        ILifecycle Lifecycle { get; }
        IBattleEvents Battle { get; }
        IProjectRegistry Projects { get; }
        IModApiLog Log { get; }
    }

    /// <summary>Minimal log surface, so the public layer needs no BepInEx reference.</summary>
    public interface IModApiLog
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }
}
