using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// Runs the engine host once over every engine test.
    /// </summary>
    internal static class EngineTier
    {
        private static readonly string[] DataFolders = { "Config", "Campaigns", "Battlefields" };

        public static void Run(Settings settings, Options options, RunReport report, string runFolder)
        {
            Console.WriteLine("engine tier");
            string runtime = EngineRuntime.Ensure(settings, line => Console.WriteLine("  " + line));
            CheckFrameworkApis(settings, runtime, report);

            // The loaders open relative Config/... paths and some write logs, so
            // they run in a copy, never in the install.
            string data = Path.Combine(settings.HarnessDir, "engine", "data");
            foreach (string folder in DataFolders)
            {
                Shell.Mirror(Path.Combine(settings.HarnessGameDir, folder), Path.Combine(data, folder));
            }

            string results = Path.Combine(runFolder, "engine.jsonl");
            var arguments = new List<string>
            {
                "--runtime", runtime,
                "--tests", settings.TestsOutput,
                "--data", data,
                "--results", results,
            };
            if (options.Filter != null)
            {
                arguments.AddRange(new[] { "--filter", options.Filter });
            }

            if (options.Sabotage != null)
            {
                arguments.AddRange(new[] { "--sabotage", options.Sabotage });
            }

            string log = Path.Combine(runFolder, "engine.host.log");
            if (!File.Exists(settings.EngineHostExe) || !Directory.Exists(settings.TestsOutput))
            {
                throw new HarnessException($"engine host or tests not built ({settings.EngineHostExe}); run without --no-build.");
            }

            Shell.Result result = Shell.Run(settings.EngineHostExe, arguments, data, TimeSpan.FromMinutes(10), logFile: log);
            if (!report.Read(results, "engine"))
            {
                string tail = string.Join(Environment.NewLine + "        ",
                    result.Output.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).TakeLast(15));
                report.AddError("engine", "engine host",
                    (result.TimedOut ? "timed out" : $"exited {result.ExitCode}") +
                    $" without finishing; full output in {log}" + Environment.NewLine + "        " + tail);
            }
        }

        /// <summary>
        /// A harness or test calling a member .NET Framework lacks is a bug to
        /// fix; the mod or library doing so only limits this tier, so it is
        /// reported without failing the run.
        /// </summary>
        private static void CheckFrameworkApis(Settings settings, string runtime, RunReport report)
        {
            Dictionary<string, List<string>> missing = FrameworkApiCheck.Find(
                runtime,
                settings.TestsOutput,
                new[] { "GTCW.Harness", "GTCW.BugFixes.Tests", "GTCW.BugFixes", "GTCW.ModApi" });

            var ours = missing.Where(m => m.Key.StartsWith("GTCW.Harness", StringComparison.Ordinal) || m.Key.EndsWith(".Tests", StringComparison.Ordinal)).ToList();
            foreach (KeyValuePair<string, List<string>> pair in missing.Except(ours))
            {
                Console.WriteLine($"  note: {pair.Key} calls members .NET Framework lacks; engine tests reaching them will throw: " +
                                  string.Join(", ", pair.Value));
            }

            report.AddTest(new TestRecord
            {
                Tier = "engine",
                Name = "Harness.NetFrameworkApis",
                Outcome = ours.Count == 0 ? "passed" : "failed",
                Message = ours.Count == 0
                    ? null
                    : "members missing on .NET Framework 4.8: " +
                      string.Join("; ", ours.Select(p => p.Key + ": " + string.Join(", ", p.Value))),
            });
        }
    }
}
