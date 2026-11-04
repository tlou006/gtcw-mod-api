using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GTCW.BugFixes;
using HarmonyLib;
using UnityEngine;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// One headless game process, start to finish: check the mod loaded as
    /// configured, launch the class's scenario through the menu, run its tests
    /// as coroutines, write the results, quit.
    /// </summary>
    /// <remarks>
    /// Driven entirely by environment variables the runner sets, so a game
    /// started by hand with the plugin present does nothing at all:
    /// <c>GTCW_HARNESS_MODE</c> (inert, campaign or battle),
    /// <c>GTCW_HARNESS_RESULTS</c>, <c>GTCW_HARNESS_CLASS</c>,
    /// <c>GTCW_HARNESS_FILTER</c>, <c>GTCW_HARNESS_SABOTAGE</c>,
    /// <c>GTCW_HARNESS_TEST_TIMEOUT</c> (seconds).
    /// </remarks>
    public static class GameRunner
    {
        public const string ModeVariable = "GTCW_HARNESS_MODE";

        private const string ModOwner = "gtcw.bugfixes";

        public static bool Requested => Environment.GetEnvironmentVariable(ModeVariable) != null;

        public static IEnumerator Drive(MonoBehaviour host)
        {
            string mode = Environment.GetEnvironmentVariable(ModeVariable) ?? "campaign";
            string resultsPath = Environment.GetEnvironmentVariable("GTCW_HARNESS_RESULTS") ?? "harness-results.jsonl";
            string? className = Environment.GetEnvironmentVariable("GTCW_HARNESS_CLASS");
            string? filter = Environment.GetEnvironmentVariable("GTCW_HARNESS_FILTER");
            float testTimeout = float.TryParse(Environment.GetEnvironmentVariable("GTCW_HARNESS_TEST_TIMEOUT"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : 240f;
            Tier tier = mode == "battle" ? Tier.Battle : Tier.Campaign;

            var results = new ResultsFile(resultsPath);
            var counts = new int[2];
            LogCapture.Install();
            UnityErrors.Install();
            FixControl.Sabotaged = Environment.GetEnvironmentVariable("GTCW_HARNESS_SABOTAGE");

            // A test run must never write a save: a date jump triggers the
            // autosave, and a failing autosave locks AICampaign.dat and aborts
            // BattleUnits.Update every frame after, silently stopping the
            // campaign pass the tests depend on.
            var patches = new Harmony("gtcw.harness.game");
            patches.Patch(AccessTools.Method(typeof(GameVars), "CheckAutosave"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(GameRunner), nameof(Skip))));
            patches.Patch(AccessTools.Method(typeof(GameVars), "CheckAutosaveBattle"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(GameRunner), nameof(Skip))));
            // No video, ever. Without a GPU every load fails inside Windows Media
            // Foundation, the menu reloads its background loop every few frames,
            // and with two games running one of those failures eventually
            // crashes the process natively. The menu's loop passes
            // overridevideodeactivation, so the game's own disablevideos switch
            // is not enough; with nothing loaded, FinishedPlaying() is true and
            // every flow that waits on a video carries on.
            GameVars.disablevideos = true;
            patches.Patch(AccessTools.Method(typeof(VideoScreen), "LoadVideo"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(GameRunner), nameof(Skip))));
            MainMenuControls.videoscreen?.RemoveVideo();

            var hold = new HarmonyMethod(AccessTools.Method(typeof(Live), nameof(Live.AllowSessionEnd)));
            patches.Patch(AccessTools.Method(typeof(BattleUnits), "SetEndOfCampaign"), prefix: hold);
            patches.Patch(AccessTools.Method(typeof(BattleUnits), "SetEndOfBattle"), prefix: hold);

            if (mode == "inert")
            {
                Check(results, tier, "Game.Inert", counts, InertProblems);
            }
            else
            {
                Check(results, tier, "Game.ApplyAll", counts, ApplyAllProblems);
            }

            Toggle.AllOff();

            Exception? error = null;
            yield return CoroutineRunner.Run(
                Live.Until(() => UnityEngine.Object.FindObjectOfType<MainMenuControls>() != null && GameVars.frame >= 60, 240f, "the main menu"),
                250f, e => error = e);
            if (error != null || mode == "inert")
            {
                Finish(results, tier, counts, error, "main menu");
                yield break;
            }

            Assembly tests = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == TestCatalog.TestAssemblyName)
                             ?? Assembly.Load(TestCatalog.TestAssemblyName);
            Type? type = className == null ? null : tests.GetType(className);
            if (type == null)
            {
                Finish(results, tier, counts, new ExpectationFailedException($"test class '{className}' not found"), "setup");
                yield break;
            }

            yield return CoroutineRunner.Run(Boot(type, tier, results), 600f, e => error = e);
            if (error != null)
            {
                Finish(results, tier, counts, error, "boot");
                yield break;
            }

            Check(results, tier, "Game.Boot", counts, () => UnityErrors.Drain().Select(e => "during boot: " + e).ToList());

            if (tier == Tier.Battle)
            {
                Live.DefaultSpeed = 30f;
                Live.DriveBattle = true;
                GameVars.ai_vs_ai = true;
            }

            host.StartCoroutine(Live.Service());
            Live.Running = true;

            foreach (TestCase test in TestCatalog.Find(tests, tier, filter, type.FullName))
            {
                LogCapture.Clear();
                Report.Clear();
                UnityErrors.Drain();
                Live.Reset();
                Toggle.AllOff();

                var clock = Stopwatch.StartNew();
                Exception? failure = null;
                IEnumerator? body = null;
                try
                {
                    object? instance = test.Method.IsStatic ? null : Activator.CreateInstance(test.Type);
                    body = test.Method.Invoke(instance, null) as IEnumerator
                           ?? throw new InvalidOperationException($"{test.Name} must return IEnumerator");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (body != null)
                {
                    yield return CoroutineRunner.Run(body, testTimeout, e => failure = e);
                }

                Toggle.AllOff();
                try
                {
                    Units.Restore();
                }
                catch (Exception ex)
                {
                    failure ??= new InvalidOperationException("restoring units after the test failed: " + ex.Message, ex);
                }

                string? message = failure != null ? Failures.Describe(failure) : null;
                List<string> unity = UnityErrors.Drain();
                List<string> fixErrors = LogCapture.Errors();
                if (message == null && fixErrors.Count > 0)
                {
                    message = "a fix reported an error: " + string.Join(" | ", fixErrors.Take(5));
                }

                if (message == null && unity.Count > 0)
                {
                    message = "the game logged errors during the test: " + string.Join(" | ", unity.Take(5));
                }

                counts[message == null ? 0 : 1]++;
                results.Test(tier, test.Name, test.Fixes, message == null ? "passed" : "failed",
                             clock.ElapsedMilliseconds, message, Report.Notes,
                             message == null ? Array.Empty<string>() : LogCapture.Relevant().Take(40));
            }

            Live.Running = false;
            Finish(results, tier, counts, null, null);
        }

        private static IEnumerator Boot(Type type, Tier tier, ResultsFile results)
        {
            MainMenuControls controls = UnityEngine.Object.FindObjectOfType<MainMenuControls>();
            if (tier == Tier.Campaign)
            {
                CampaignAttribute scenario = type.GetCustomAttribute<CampaignAttribute>() ?? new CampaignAttribute();
                results.Info(tier, "launching " + MenuLauncher.Campaign(controls.CampaignMenu, scenario.Map, scenario.Setup, scenario.Side));
                yield return Live.Until(() => UnityEngine.Object.FindObjectOfType<CampaignController>() != null, 180f, "the campaign scene");
            }
            else
            {
                BattleAttribute scenario = type.GetCustomAttribute<BattleAttribute>() ?? new BattleAttribute();
                results.Info(tier, "launching " + MenuLauncher.Battle(controls.BattleMenu, scenario.Map, scenario.Setup, scenario.Side));
                yield return Live.Until(() => UnityEngine.Object.FindObjectOfType<BattleController>() != null, 180f, "the battle scene");
            }

            // The controller resets the frame counter in its Awake and runs its
            // boot gates on frames 1-50; 55 is past the last of them.
            float started = Time.realtimeSinceStartup;
            yield return Live.Until(() => GameVars.frame >= 55, 420f, "the scenario's boot gates (frame 55)");
            results.Info(tier, $"scenario live after {Time.realtimeSinceStartup - started:0} s; " +
                               $"{(BattleUnits.completeunitlist == null ? 0 : BattleUnits.completeunitlist.Count)} units");
        }

        /// <summary>Every fix must be on: the runner installed the all-on config.</summary>
        private static List<string> ApplyAllProblems() =>
            FixControl.AllFixes().Where(f => !FixState.IsOn(f.Id))
                .Select(f => $"{f.Id} is not on after the mod loaded with the all-on config (failed probe? see the BepInEx log)")
                .ToList();

        /// <summary>
        /// The real plugin, loaded with every fix Off, must have patched nothing:
        /// the compatibility boundary's promise, checked in the real game.
        /// </summary>
        private static List<string> InertProblems()
        {
            var problems = Harmony.GetAllPatchedMethods()
                .Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains(ModOwner) == true)
                .Select(m => $"patched with every fix Off: {m.DeclaringType?.Name}.{m.Name}")
                .ToList();
            problems.AddRange(FixControl.AllFixes().Where(f => FixState.IsOn(f.Id)).Select(f => $"{f.Id} is on with every fix Off"));
            return problems;
        }

        private static void Check(ResultsFile results, Tier tier, string name, int[] counts, Func<List<string>> problems)
        {
            var clock = Stopwatch.StartNew();
            string? message;
            try
            {
                List<string> found = problems();
                message = found.Count == 0 ? null : string.Join("; ", found);
            }
            catch (Exception ex)
            {
                message = Failures.Describe(ex);
            }

            counts[message == null ? 0 : 1]++;
            results.Test(tier, name, Array.Empty<string>(), message == null ? "passed" : "failed",
                         clock.ElapsedMilliseconds, message, Array.Empty<string>(), Array.Empty<string>());
        }

        private static void Finish(ResultsFile results, Tier tier, int[] counts, Exception? error, string? stage)
        {
            if (error != null)
            {
                results.Error(tier, stage ?? "harness", Failures.Describe(error) + Unity());
                counts[1]++;
            }

            results.Done(tier, counts[0], counts[1]);
            results.Dispose();
            Application.Quit(counts[1] == 0 ? 0 : 1);
        }

        private static string Unity()
        {
            List<string> unity = UnityErrors.Drain();
            return unity.Count == 0 ? string.Empty : " | unity: " + string.Join(" | ", unity.Take(5));
        }

        private static bool Skip() => false;
    }
}
