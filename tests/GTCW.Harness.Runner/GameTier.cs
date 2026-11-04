using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// The headless tiers: one windowless game process per test class, each in
    /// its own sandbox, a few at a time.
    /// </summary>
    internal static class GameTier
    {
        private sealed class Unit
        {
            public string Mode { get; init; } = string.Empty;

            public TestScan.TestClass? Class { get; init; }

            public string Config { get; init; } = string.Empty;

            public string Label => Class?.Name ?? "Inert";
        }

        public static void Run(Settings settings, Options options, RunReport report, string runFolder, string tier)
        {
            Console.WriteLine($"{tier} tier");
            string attribute = tier == "campaign" ? "CampaignTestAttribute" : "BattleTestAttribute";
            List<TestScan.TestClass> classes = TestScan.Classes(settings.TestsOutput, attribute, options.Filter);

            string runtime = EngineRuntime.Ensure(settings, line => Console.WriteLine("  " + line));
            string allOn = WriteModConfig(settings, runtime, Path.Combine(runFolder, "gtcw.bugfixes.all-on.cfg"), on: true);
            string allOff = WriteModConfig(settings, runtime, Path.Combine(runFolder, "gtcw.bugfixes.all-off.cfg"), on: false);

            var units = new List<Unit>();
            if (tier == "campaign" && string.IsNullOrEmpty(options.Filter))
            {
                // The real plugin, every fix Off, in the real game: nothing patched.
                units.Add(new Unit { Mode = "inert", Config = allOff });
            }

            units.AddRange(classes.Select(c => new Unit { Mode = tier, Class = c, Config = allOn }));
            if (units.Count == 0)
            {
                Console.WriteLine("  no tests match");
                return;
            }

            InstallSnapshot before = InstallSnapshot.Take(settings);
            var slots = new ConcurrentBag<int>(Enumerable.Range(1, Math.Min(options.Parallel, units.Count)));
            Parallel.ForEach(units, new ParallelOptions { MaxDegreeOfParallelism = options.Parallel }, unit =>
            {
                slots.TryTake(out int slot);
                try
                {
                    RunUnit(settings, options, report, runFolder, tier, unit, slot);
                }
                catch (Exception ex)
                {
                    lock (report)
                    {
                        report.AddError(tier, unit.Label, ex is HarnessException ? ex.Message : ex.ToString());
                    }
                }
                finally
                {
                    slots.Add(slot);
                }
            });

            List<string> changes = before.Differences(InstallSnapshot.Take(settings));
            report.AddTest(new TestRecord
            {
                Tier = tier,
                Name = "Harness.InstallUntouched",
                Outcome = changes.Count == 0 ? "passed" : "failed",
                Message = changes.Count == 0 ? null : $"the install changed during the run ({changes.Count}): " + string.Join(", ", changes.Take(20)),
            });
        }

        private static void RunUnit(Settings settings, Options options, RunReport report, string runFolder, string tier, Unit unit, int slot)
        {
            Console.WriteLine($"  [s{slot}] {unit.Label}: preparing sandbox");
            Sandbox sandbox = Sandbox.Prepare(settings, slot);
            sandbox.Deploy(settings, unit.Config);

            var environment = new Dictionary<string, string>
            {
                ["GTCW_HARNESS_MODE"] = unit.Mode,
                ["GTCW_HARNESS_RESULTS"] = sandbox.Results,
                ["GTCW_HARNESS_CLASS"] = unit.Class?.FullName ?? string.Empty,
                ["GTCW_HARNESS_FILTER"] = options.Filter ?? string.Empty,
                ["GTCW_HARNESS_SABOTAGE"] = options.Sabotage ?? string.Empty,
            };

            Console.WriteLine($"  [s{slot}] {unit.Label}: game running ({unit.Class?.Tests.Count ?? 0} tests)");
            var clock = Stopwatch.StartNew();
            Shell.Result result = Shell.Run(
                sandbox.Exe,
                new[] { "-batchmode", "-nographics", "-logFile", sandbox.PlayerLog },
                sandbox.Root,
                TimeSpan.FromSeconds(options.TimeoutSeconds),
                environment);

            string evidence = Path.Combine(runFolder, $"{tier}-{unit.Label}");
            Directory.CreateDirectory(evidence);
            foreach (string file in new[] { sandbox.Results, sandbox.PlayerLog, sandbox.BepInExLog })
            {
                if (File.Exists(file))
                {
                    File.Copy(file, Path.Combine(evidence, Path.GetFileName(file)), overwrite: true);
                }
            }

            bool done;
            lock (report)
            {
                done = report.Read(Path.Combine(evidence, Path.GetFileName(sandbox.Results)), unit.Label);
            }

            Console.WriteLine($"  [s{slot}] {unit.Label}: {(done ? "finished" : "did not finish")} after {clock.Elapsed.TotalSeconds:0} s");
            if (!done)
            {
                string why = result.TimedOut
                    ? $"timed out after {options.TimeoutSeconds} s and was killed"
                    : $"exited {result.ExitCode} without writing its closing record";
                lock (report)
                {
                    report.AddError(tier, unit.Label, $"{why}; evidence in {evidence}{Clues(evidence)}");
                }
            }
        }

        /// <summary>The last harness line and the first errors from the logs, for a run that died.</summary>
        private static string Clues(string evidence)
        {
            var clues = new List<string>();
            string bepinex = Path.Combine(evidence, "LogOutput.log");
            if (File.Exists(bepinex))
            {
                clues.AddRange(File.ReadLines(bepinex).Where(l => l.StartsWith("[Error", StringComparison.Ordinal) || l.StartsWith("[Fatal", StringComparison.Ordinal)).Take(5));
            }

            string player = Path.Combine(evidence, "player.log");
            if (File.Exists(player))
            {
                clues.AddRange(File.ReadLines(player).Where(l => l.Contains("Exception", StringComparison.Ordinal) && !l.Contains("camp.dat", StringComparison.Ordinal)).Take(5));
            }

            return clues.Count == 0 ? string.Empty : Environment.NewLine + "        " + string.Join(Environment.NewLine + "        ", clues);
        }

        /// <summary>
        /// The mod's config with every fix on (so every patch exists and tests
        /// switch fixes at run time) or every fix off (the inert check), written
        /// by the engine host through the mod's own registry.
        /// </summary>
        private static string WriteModConfig(Settings settings, string runtime, string path, bool on)
        {
            Shell.Result r = Shell.Run(settings.EngineHostExe,
                new[] { "--runtime", runtime, "--tests", settings.TestsOutput, "--write-config", path, "--toggles", on ? "on" : "off" },
                timeout: TimeSpan.FromMinutes(2));
            if (r.ExitCode != 0 || !File.Exists(path))
            {
                throw new HarnessException($"could not write the mod config {path}: {r.Output.Trim()}");
            }

            return path;
        }

        /// <summary>Deletes every sandbox, junction-first, and the engine data copy.</summary>
        public static void Clean(Settings settings)
        {
            string sandboxes = Sandbox.SandboxesFolder(settings);
            if (Directory.Exists(sandboxes))
            {
                foreach (string sandbox in Directory.GetDirectories(sandboxes))
                {
                    Console.WriteLine($"deleting {sandbox}");
                    Sandbox.Delete(sandbox);
                }
            }

            string engine = Path.Combine(settings.HarnessDir, "engine");
            if (Directory.Exists(engine))
            {
                Console.WriteLine($"deleting {engine}");
                Sandbox.Delete(engine);
            }
        }
    }
}
