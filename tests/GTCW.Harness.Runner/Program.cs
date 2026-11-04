using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GTCW.Harness.Runner
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Options options = Options.Parse(args);
                if (options.Command == "help")
                {
                    Console.WriteLine(Options.Usage);
                    return 0;
                }

                Settings settings = Settings.Load(options);
                var report = new RunReport();
                var clock = Stopwatch.StartNew();

                if (!options.NoBuild && options.Command != "clean")
                {
                    Build(settings);
                }

                string runFolder = Path.Combine(settings.HarnessDir, "runs", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(runFolder);

                switch (options.Command)
                {
                    case "stubs":
                        Console.WriteLine(EngineRuntime.Ensure(settings, Console.WriteLine));
                        return 0;
                    case "engine":
                        EngineTier.Run(settings, options, report, runFolder);
                        break;
                    case "campaign":
                    case "battle":
                        GameTier.Run(settings, options, report, runFolder, options.Command);
                        break;
                    case "all":
                        EngineTier.Run(settings, options, report, runFolder);
                        GameTier.Run(settings, options, report, runFolder, "campaign");
                        GameTier.Run(settings, options, report, runFolder, "battle");
                        break;
                    case "clean":
                        GameTier.Clean(settings);
                        return 0;
                    default:
                        throw new HarnessException($"unknown command '{options.Command}'.\n\n{Options.Usage}");
                }

                report.Print();
                string junit = Path.Combine(settings.RepoRoot, "TestResults", $"{options.Command}.xml");
                report.WriteJUnit(junit);
                Console.WriteLine($"{clock.Elapsed.TotalSeconds:0.0}s; logs in {runFolder}; JUnit in {junit}");
                return report.Failed == 0 ? 0 : 1;
            }
            catch (HarnessException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        /// <summary>
        /// Builds what the run deploys or launches. Project by project rather
        /// than the solution, because the solution includes this runner, whose
        /// own files are in use while it runs.
        /// </summary>
        private static void Build(Settings settings)
        {
            string[] projects =
            {
                Path.Combine("src", "GTCW.ModApi.Plugin", "GTCW.ModApi.Plugin.csproj"),
                Path.Combine("tests", "GTCW.Harness.GamePlugin", "GTCW.Harness.GamePlugin.csproj"),
                Path.Combine("tests", "GTCW.Harness.EngineHost", "GTCW.Harness.EngineHost.csproj"),
            };

            foreach (string project in projects)
            {
                Console.WriteLine($"building {project}");
                Shell.Result r = Shell.Run("dotnet",
                    new[] { "build", project, "-c", settings.Configuration, "--nologo", "-v", "quiet" },
                    settings.RepoRoot, TimeSpan.FromMinutes(10));
                if (r.ExitCode != 0)
                {
                    IEnumerable<string> errors = r.Output.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(20);
                    throw new HarnessException($"build of {project} failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
                }
            }
        }
    }
}
