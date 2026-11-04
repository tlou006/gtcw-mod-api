using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace GTCW.Harness.Engine
{
    /// <summary>
    /// The engine tier, start to finish. Called by GTCW.Harness.EngineHost once
    /// the loader can see the stubbed engine and the tests.
    /// </summary>
    /// <remarks>
    /// Arguments: <c>--data</c> (the runner's copy of the install's data
    /// folders, used as the working directory), <c>--results</c> (the JSON-lines
    /// file to append to), optional <c>--filter</c> and <c>--sabotage</c>.
    /// Exit code 0 when everything passed, 1 when a test failed, 2 when the
    /// tier could not run at all.
    /// </remarks>
    public static class EngineEntry
    {
        public static int Run(string[] args)
        {
            var arguments = new Arguments(args);
            if (arguments["--write-config"] is string configPath)
            {
                RegistryChecks.WriteConfig(configPath, on: arguments["--toggles"] == "on");
                return 0;
            }

            using var results = new ResultsFile(arguments.Require("--results"));
            int passed = 0, failed = 0;

            try
            {
                Directory.SetCurrentDirectory(arguments.Require("--data"));
                FixControl.Sabotaged = arguments["--sabotage"];
                if (FixControl.Sabotaged != null)
                {
                    results.Info(Tier.Engine, $"sabotage: {FixControl.Sabotaged} will stay off whatever tests ask");
                }

                LogCapture.Install();
                Assembly tests = Assembly.Load(TestCatalog.TestAssemblyName);

                var seams = new Harmony("gtcw.harness.seams");
                EngineSeams.Install(seams);
                World.Load();

                // The registry checks apply and remove patches of their own,
                // so they run before the harness's own copy is applied, each
                // against a fresh stand-in host.
                Record(results, "Harness.Inert", Array.Empty<string>(), () =>
                {
                    StandInHost.BindNew();
                    Throw(RegistryChecks.Inert(Directory.GetCurrentDirectory()));
                }, ref passed, ref failed);

                Record(results, "Harness.Defaults", Array.Empty<string>(), () =>
                {
                    StandInHost.BindNew();
                    Throw(RegistryChecks.Defaults(Directory.GetCurrentDirectory()));
                }, ref passed, ref failed);

                StandInHost.BindNew();
                var fixes = new Harmony("gtcw.harness.fixes");
                Record(results, "Harness.ApplyAll", Array.Empty<string>(), () => Throw(FixControl.ApplyAll(fixes)),
                       ref passed, ref failed);

                Record(results, "Harness.Coverage", Array.Empty<string>(), () =>
                {
                    List<string> uncovered = TestCatalog.Uncovered(tests);
                    if (uncovered.Count > 0)
                    {
                        Expect.Fail("catalogued fixes with no test and no [ManualOnly]: " + string.Join(", ", uncovered));
                    }
                }, ref passed, ref failed);

                Action fresh = () =>
                {
                    World.Reload();
                    EngineSeams.Reseed();
                    Session.Start();
                };
                Verify.BeforePhase = fresh;

                foreach (TestCase test in TestCatalog.Find(tests, Tier.Engine, arguments["--filter"]))
                {
                    Record(results, test.Name, test.Fixes, () =>
                    {
                        fresh();
                        object? instance = test.Method.IsStatic ? null : Activator.CreateInstance(test.Type);
                        test.Method.Invoke(instance, null);
                    }, ref passed, ref failed);
                }
            }
            catch (Exception ex)
            {
                results.Error(Tier.Engine, "engine tier", Failures.Describe(ex));
                results.Done(Tier.Engine, passed, failed + 1);
                return 2;
            }

            results.Done(Tier.Engine, passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void Record(
            ResultsFile results,
            string name,
            string[] fixes,
            Action body,
            ref int passed,
            ref int failed)
        {
            LogCapture.Clear();
            Report.Clear();
            Toggle.AllOff();
            var clock = Stopwatch.StartNew();
            string? message = null;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                message = Failures.Describe(ex);
            }
            finally
            {
                Toggle.AllOff();
            }

            if (message == null)
            {
                passed++;
            }
            else
            {
                failed++;
            }

            results.Test(Tier.Engine, name, fixes, message == null ? "passed" : "failed", clock.ElapsedMilliseconds,
                         message, Report.Notes, message == null ? Array.Empty<string>() : LogCapture.Captured.Take(40));
        }

        private static void Throw(List<string> problems)
        {
            if (problems.Count > 0)
            {
                Expect.Fail(string.Join("; ", problems));
            }
        }
    }
}
