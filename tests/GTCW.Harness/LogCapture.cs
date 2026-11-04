using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace GTCW.Harness
{
    /// <summary>
    /// Every BepInEx log line written during the current test, so a test can
    /// assert on what a fix reported (GTCW-001 logs each suppression, for one).
    /// </summary>
    public static class LogCapture
    {
        private static readonly List<string> Lines = new List<string>();
        private static Listener? _listener;

        public static IReadOnlyList<string> Captured => Lines;

        internal static void Install()
        {
            if (_listener != null)
            {
                return;
            }

            _listener = new Listener();
            Logger.Listeners.Add(_listener);
        }

        internal static void Clear() => Lines.Clear();

        /// <summary>
        /// Error-level lines: a fix whose patch body threw reports it here,
        /// through <c>FixState.Guard</c>, and keeps the game running.
        /// </summary>
        internal static List<string> Errors()
        {
            var errors = new List<string>();
            foreach (string line in Lines)
            {
                if (line.StartsWith("[Error:", StringComparison.Ordinal) || line.StartsWith("[Fatal:", StringComparison.Ordinal))
                {
                    errors.Add(line);
                }
            }

            return errors;
        }

        /// <summary>
        /// What is worth attaching to a failure: everything except the game's
        /// own Unity console chatter, which BepInEx relays as "Unity Log" and
        /// which is mostly font warnings.
        /// </summary>
        internal static IEnumerable<string> Relevant()
        {
            foreach (string line in Lines)
            {
                if (line.IndexOf(":Unity Log]", StringComparison.Ordinal) < 0)
                {
                    yield return line;
                }
            }
        }

        public static bool Contains(string fragment)
        {
            foreach (string line in Lines)
            {
                if (line.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class Listener : ILogListener
        {
            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                if (Lines.Count < 2000)
                {
                    Lines.Add($"[{eventArgs.Level}:{eventArgs.Source.SourceName}] {eventArgs.Data}");
                }
            }

            public void Dispose()
            {
            }
        }
    }
}
