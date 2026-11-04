using System;
using System.IO;
using System.Linq;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// A throwaway copy of the game for one headless process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 16 GB of assets are not copied: <c>gtcwtest_Data</c> and
    /// <c>MonoBleedingEdge</c> are junctions into the install, which the game
    /// only reads. The renamed exe keeps <c>.claude/deploy.ps1</c>'s
    /// running-game check and the player's own game out of the way. Everything
    /// the game writes — data folders, BepInEx config, logs — is a copy,
    /// mirrored afresh from the install before every launch, because a run
    /// can change it (the spike's autosave emptied a scenario's Save folder).
    /// </para>
    /// <para>
    /// <c>UnityCrashHandler64.exe</c> is deliberately absent: the game crashes
    /// on every shutdown in batch mode, and the handler then lingers holding
    /// its own exe open. Unity runs without it.
    /// </para>
    /// </remarks>
    internal sealed class Sandbox
    {
        public const string ExeBase = "gtcwtest";

        private static readonly string[] DataFolders = { "Config", "Campaigns", "Battlefields", "Avatar", "Modding" };

        private static readonly string[] EngineFiles = { "UnityPlayer.dll", "winhttp.dll", "doorstop_config.ini", ".doorstop_version" };

        private Sandbox(string root) => Root = root;

        public string Root { get; }

        public string Exe => Path.Combine(Root, ExeBase + ".exe");

        public string Results => Path.Combine(Root, "harness-results.jsonl");

        public string PlayerLog => Path.Combine(Root, "player.log");

        public string BepInExLog => Path.Combine(Root, "BepInEx", "LogOutput.log");

        public static string SandboxesFolder(Settings settings) => Path.Combine(settings.HarnessDir, "sandboxes");

        public static Sandbox Prepare(Settings settings, int slot)
        {
            string root = Path.Combine(SandboxesFolder(settings), "s" + slot);
            Directory.CreateDirectory(root);
            var sandbox = new Sandbox(root);

            CopyIfChanged(Path.Combine(settings.HarnessGameDir, settings.ExeName), sandbox.Exe);
            foreach (string file in EngineFiles)
            {
                string source = Path.Combine(settings.HarnessGameDir, file);
                if (File.Exists(source))
                {
                    CopyIfChanged(source, Path.Combine(root, file));
                }
            }

            Junction(Path.Combine(root, ExeBase + "_Data"), Path.Combine(settings.HarnessGameDir, settings.DataFolderName));
            Junction(Path.Combine(root, "MonoBleedingEdge"), Path.Combine(settings.HarnessGameDir, "MonoBleedingEdge"));

            foreach (string folder in DataFolders)
            {
                Shell.Mirror(Path.Combine(settings.HarnessGameDir, folder), Path.Combine(root, folder));
            }

            Shell.Mirror(settings.BepInExCore, Path.Combine(root, "BepInEx", "core"));
            string patchers = Path.Combine(settings.HarnessGameDir, "BepInEx", "patchers");
            if (Directory.Exists(patchers))
            {
                Shell.Mirror(patchers, Path.Combine(root, "BepInEx", "patchers"));
            }

            Empty(Path.Combine(root, "BepInEx", "plugins"));
            Empty(Path.Combine(root, "BepInEx", "config"));
            foreach (string log in new[] { sandbox.Results, sandbox.PlayerLog, sandbox.BepInExLog, Path.Combine(root, "scenelog.dat") })
            {
                File.Delete(log);
            }

            return sandbox;
        }

        /// <summary>The library, the mod, the harness and the tests; and the mod's config.</summary>
        public void Deploy(Settings settings, string modConfig)
        {
            string plugins = Path.Combine(Root, "BepInEx", "plugins");
            foreach (string folder in new[] { settings.ModApiOutput, settings.PluginOutput })
            {
                foreach (string file in Directory.GetFiles(folder).Where(f => f.EndsWith(".dll", StringComparison.Ordinal) || f.EndsWith(".pdb", StringComparison.Ordinal)))
                {
                    File.Copy(file, Path.Combine(plugins, Path.GetFileName(file)), overwrite: true);
                }
            }

            File.Copy(modConfig, Path.Combine(Root, "BepInEx", "config", "gtcw.bugfixes.cfg"), overwrite: true);
        }

        /// <summary>
        /// Deletes a sandbox without ever following a junction: links first,
        /// each verified gone, then a refusal if any reparse point remains,
        /// and only then the rest. Stops at the first problem.
        /// </summary>
        public static void Delete(string root)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (string entry in Directory.GetDirectories(root))
            {
                if (IsReparsePoint(entry))
                {
                    Directory.Delete(entry, recursive: false);
                    if (Directory.Exists(entry))
                    {
                        throw new HarnessException($"could not remove junction {entry}; deleting nothing else");
                    }
                }
            }

            string? stray = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).FirstOrDefault(IsReparsePoint);
            if (stray != null)
            {
                throw new HarnessException($"{stray} is a junction or link; refusing to delete {root}");
            }

            Directory.Delete(root, recursive: true);
        }

        private static void Junction(string link, string target)
        {
            if (Directory.Exists(link))
            {
                if (!IsReparsePoint(link))
                {
                    throw new HarnessException($"{link} should be a junction to {target} but is a real folder; run 'clean' and retry");
                }

                string? current = new DirectoryInfo(link).LinkTarget;
                if (current != null && Path.GetFullPath(current).TrimEnd('\\').Equals(Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Directory.Delete(link, recursive: false);
            }

            Shell.Result r = Shell.Run("cmd", new[] { "/c", "mklink", "/J", link, target }, timeout: TimeSpan.FromSeconds(30));
            if (r.ExitCode != 0 || !IsReparsePoint(link))
            {
                throw new HarnessException($"could not create junction {link} -> {target}: {r.Output.Trim()}");
            }
        }

        private static void Empty(string folder)
        {
            if (Directory.Exists(folder))
            {
                if (Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).Any(IsReparsePoint))
                {
                    throw new HarnessException($"{folder} contains a junction; refusing to empty it");
                }

                Directory.Delete(folder, recursive: true);
            }

            Directory.CreateDirectory(folder);
        }

        private static void CopyIfChanged(string source, string destination)
        {
            var s = new FileInfo(source);
            var d = new FileInfo(destination);
            if (!d.Exists || d.Length != s.Length || d.LastWriteTimeUtc != s.LastWriteTimeUtc)
            {
                File.Copy(source, destination, overwrite: true);
            }
        }

        private static bool IsReparsePoint(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }
}
