using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace GTCW.Harness.Runner
{
    /// <summary>A problem the user has to fix; printed without a stack trace.</summary>
    internal sealed class HarnessException : Exception
    {
        public HarnessException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Where everything is, and the refusals that keep the real install and
    /// the repo safe.
    /// </summary>
    /// <remarks>
    /// Paths come from game.props (baked in at build time), then environment
    /// variables, then the command line, each overriding the last.
    /// </remarks>
    internal sealed class Settings
    {
        private Settings(string repoRoot, string gameDir, string harnessGameDir, string harnessDir, string configuration)
        {
            RepoRoot = repoRoot;
            GameDir = gameDir;
            HarnessGameDir = harnessGameDir;
            HarnessDir = harnessDir;
            Configuration = configuration;
        }

        public string RepoRoot { get; }

        /// <summary>What the build compiled against.</summary>
        public string GameDir { get; }

        /// <summary>The install every run reads from. Never written.</summary>
        public string HarnessGameDir { get; }

        /// <summary>Sandboxes, the engine data copy and run logs. Outside the repo and the game.</summary>
        public string HarnessDir { get; }

        public string Configuration { get; }

        public string DataFolderName { get; private set; } = string.Empty;

        public string ExeName => DataFolderName.Substring(0, DataFolderName.Length - "_Data".Length) + ".exe";

        public string Managed => Path.Combine(HarnessGameDir, DataFolderName, "Managed");

        public string BepInExCore => Path.Combine(HarnessGameDir, "BepInEx", "core");

        public string TestsOutput => Path.Combine(RepoRoot, "tests", "GTCW.BugFixes.Tests", "bin", Configuration, "netstandard2.1");

        public string PluginOutput => Path.Combine(RepoRoot, "tests", "GTCW.Harness.GamePlugin", "bin", Configuration, "netstandard2.1");

        public string ModApiOutput => Path.Combine(RepoRoot, "src", "GTCW.ModApi.Plugin", "bin", Configuration, "netstandard2.1");

        public string EngineHostExe => Path.Combine(RepoRoot, "tests", "GTCW.Harness.EngineHost", "bin", Configuration, "net48", "GTCW.Harness.EngineHost.exe");

        public static Settings Load(Options options)
        {
            string repoRoot = Metadata("RepoRoot") ?? throw new HarnessException("RepoRoot was not baked into the runner; rebuild it.");
            string gameDir = options.GameDir ?? Environment.GetEnvironmentVariable("GTCW_HARNESS_COMPILE_DIR") ?? Metadata("GameDir") ?? string.Empty;
            string harnessGameDir = options.HarnessGameDir ?? Environment.GetEnvironmentVariable("GTCW_HARNESS_GAME_DIR") ?? Metadata("HarnessGameDir") ?? gameDir;
            string harnessDir = options.HarnessDir ?? Environment.GetEnvironmentVariable("GTCW_HARNESS_DIR") ?? Metadata("HarnessDir") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(harnessDir))
            {
                harnessDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gtcw-harness");
            }

            var settings = new Settings(
                Full(repoRoot),
                gameDir.Length == 0 ? string.Empty : Full(gameDir),
                Full(harnessGameDir),
                Full(harnessDir),
                options.Configuration);
            settings.Validate();
            return settings;
        }

        private void Validate()
        {
            if (!Directory.Exists(HarnessGameDir))
            {
                throw new HarnessException($"No game install at '{HarnessGameDir}'. Set HarnessGameDir in game.props (see game.props.example).");
            }

            DataFolderName = Directory.GetDirectories(HarnessGameDir, "*_Data")
                .Select(Path.GetFileName)
                .FirstOrDefault(d => File.Exists(Path.Combine(HarnessGameDir, d!, "Managed", "Assembly-CSharp.dll")))
                ?? throw new HarnessException($"'{HarnessGameDir}' has no <Game>_Data\\Managed\\Assembly-CSharp.dll.");

            if (!File.Exists(Path.Combine(BepInExCore, "BepInEx.dll")) || !File.Exists(Path.Combine(HarnessGameDir, "winhttp.dll")))
            {
                throw new HarnessException(
                    $"'{HarnessGameDir}' has no BepInEx 5 (x64, Mono). The harness copies its loader into each sandbox; " +
                    "install BepInEx into the game folder once, or point HarnessGameDir at an install that has it.");
            }

            string root = Path.GetPathRoot(HarnessDir) ?? string.Empty;
            if (string.Equals(root.TrimEnd('\\'), HarnessDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                throw new HarnessException($"HarnessDir '{HarnessDir}' is a drive root. Give it a folder of its own.");
            }

            foreach ((string label, string dir) in new[] { ("the repo", RepoRoot), ("HarnessGameDir", HarnessGameDir), ("GameDir", GameDir) })
            {
                if (dir.Length > 0 && (IsInsideOrEqual(HarnessDir, dir) || IsInsideOrEqual(dir, HarnessDir)))
                {
                    throw new HarnessException(
                        $"HarnessDir '{HarnessDir}' overlaps {label} ('{dir}'). Sandboxes hold junctions into the install, " +
                        "so they must live outside both the repo (git clean) and the game folder.");
                }
            }

            if (GameDir.Length > 0 && !string.Equals(GameDir, HarnessGameDir, StringComparison.OrdinalIgnoreCase))
            {
                string compiled = FindAssemblyCSharp(GameDir);
                string running = Path.Combine(Managed, "Assembly-CSharp.dll");
                if (compiled.Length > 0 && !Sha256(compiled).Equals(Sha256(running), StringComparison.Ordinal))
                {
                    throw new HarnessException(
                        "The build compiled against a different Assembly-CSharp than the harness would run:\n" +
                        $"  GameDir:        {compiled} ({Sha256(compiled).Substring(0, 16)})\n" +
                        $"  HarnessGameDir: {running} ({Sha256(running).Substring(0, 16)})\n" +
                        "Point both at the same build.");
                }
            }
        }

        public string AssemblyCSharpHash => Sha256(Path.Combine(Managed, "Assembly-CSharp.dll"));

        public static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        public static bool IsInsideOrEqual(string path, string folder)
        {
            string p = Full(path).TrimEnd('\\') + "\\";
            string f = Full(folder).TrimEnd('\\') + "\\";
            return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
        }

        private static string FindAssemblyCSharp(string gameDir)
        {
            if (!Directory.Exists(gameDir))
            {
                return string.Empty;
            }

            return Directory.GetDirectories(gameDir, "*_Data")
                .Select(d => Path.Combine(d, "Managed", "Assembly-CSharp.dll"))
                .FirstOrDefault(File.Exists) ?? string.Empty;
        }

        private static string Full(string path) => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

        private static string? Metadata(string key)
        {
            string? value = typeof(Settings).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
