using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using GTCW.ModApi.Runtime;
using UnityEngine;

namespace GTCW.ModApi.Plugin
{
    /// <summary>
    /// Loads the library into the game. BepInEx injects at runtime through
    /// UnityDoorstop, so no game file is modified on disk.
    /// </summary>
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public sealed class ModApiPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var log = new BepInExLog(Logger);

            try
            {
                ModApiRuntime.Boot(log, ManagedFolder());
                log.Info($"{PluginInfo.Name} {PluginInfo.Version} ready.");
            }
            catch (Exception ex)
            {
                // The outermost fail-soft boundary. A broken library must leave
                // the player with a working game, not a crash on the splash
                // screen.
                log.Error($"Bootstrap failed; the library is inert this session. {ex}");
            }
        }

        /// <summary>
        /// The game's <c>Managed</c> folder, for build identification.
        /// </summary>
        /// <remarks>
        /// <c>Application.dataPath</c> is the <c>_Data</c> folder. The game's
        /// own config loading does not use it — paths there are relative string
        /// literals resolved against the install root — but it is the right
        /// anchor for finding an assembly.
        /// </remarks>
        private static string ManagedFolder() =>
            Path.Combine(Application.dataPath, "Managed");

        /// <summary>Adapts BepInEx's logger to the public log interface.</summary>
        private sealed class BepInExLog : IModApiLog
        {
            private readonly ManualLogSource _source;

            public BepInExLog(ManualLogSource source) => _source = source;

            public void Info(string message) => _source.LogInfo(message);

            public void Warning(string message) => _source.LogWarning(message);

            public void Error(string message) => _source.LogError(message);
        }
    }
}
