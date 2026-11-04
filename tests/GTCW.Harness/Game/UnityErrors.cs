using System;
using System.Collections.Generic;
using UnityEngine;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// Errors and exceptions the game logs to Unity, minus the ones every run of
    /// this build produces. Anything else during a test fails it: a test whose
    /// game threw underneath it has not shown anything.
    /// </summary>
    internal static class UnityErrors
    {
        /// <summary>
        /// Present in every run, harness or not: the shipped build looks for a
        /// <c>config/camp.dat</c> that does not exist (it is in the player's
        /// own Player.log too), and batch mode has no video shaders.
        /// </summary>
        private static readonly string[] Baseline =
        {
            "camp.dat",
            "Video shaders not found",
        };

        private static readonly List<string> Unexpected = new List<string>();
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>Returns and forgets what was logged since the last call.</summary>
        public static List<string> Drain()
        {
            var copy = new List<string>(Unexpected);
            Unexpected.Clear();
            return copy;
        }

        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
            {
                return;
            }

            foreach (string known in Baseline)
            {
                if (condition.IndexOf(known, StringComparison.Ordinal) >= 0)
                {
                    return;
                }
            }

            if (Unexpected.Count < 30)
            {
                string first = (stack ?? string.Empty).Split(new[] { '\n' })[0].Trim();
                string text = condition.Length > 300 ? condition.Substring(0, 300) : condition;
                Unexpected.Add($"{type}: {text}" + (first.Length > 0 ? " @ " + first : string.Empty));
            }
        }
    }
}
