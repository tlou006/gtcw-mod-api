using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GTCW.Harness
{
    /// <summary>
    /// The verification protocol from <c>mods/GTCW.BugFixes/IMPLEMENTATION.md</c>
    /// §7, as code: one scenario, run with the fix off, on, and off again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first run is the <b>repro</b>: the defect must show. The second is
    /// the <b>confirm</b>: it must not. The third is the <b>toggle-off</b>: with
    /// the patch still applied and the fix switched off, the defect must show
    /// again, which is what makes "turning it off restores the game" true.
    /// </para>
    /// <para>
    /// Each run starts from the same state: in the engine tier the shipped data
    /// is reloaded before every phase; in the game tiers the scenario has to set
    /// every value it reads, because the live session carries on.
    /// </para>
    /// </remarks>
    public static class Verify
    {
        /// <summary>Set by the host: whatever makes each phase start clean.</summary>
        internal static Action? BeforePhase { get; set; }

        /// <summary>
        /// Runs <paramref name="scenario"/> three times and checks each result.
        /// </summary>
        /// <param name="fixId">The fix being verified; it alone changes between phases.</param>
        /// <param name="scenario">Builds the situation and returns what it measured.</param>
        /// <param name="defect">Throws unless the measurement shows the defect.</param>
        /// <param name="repaired">Throws unless the measurement shows the repair.</param>
        /// <param name="heldOn">Fixes on in all three phases, for fixes that share a patch.</param>
        public static void Fix<T>(
            string fixId,
            Func<T> scenario,
            Action<T> defect,
            Action<T> repaired,
            params string[] heldOn)
        {
            T off = Phase(heldOn, scenario);
            Check($"repro ({fixId} off)", () => defect(off));

            T on = Phase(heldOn.Concat(new[] { fixId }), scenario);
            Check($"confirm ({fixId} on)", () => repaired(on));

            T again = Phase(heldOn, scenario);
            Check($"toggle-off ({fixId} off again, patch still applied)", () => defect(again));

            Report.Note($"{fixId}: off {Show(off)} | on {Show(on)} | off again {Show(again)}");
        }

        /// <summary>
        /// The game-tier form: the scenario is a coroutine that spans frames and
        /// puts its measurement in the <see cref="Box{T}"/> it is given.
        /// </summary>
        public static IEnumerator FixLive<T>(
            string fixId,
            Func<Box<T>, IEnumerator> scenario,
            Action<T> defect,
            Action<T> repaired,
            params string[] heldOn)
        {
            var off = new Box<T>();
            FixControl.Set(heldOn);
            yield return scenario(off);
            Check($"repro ({fixId} off)", () => defect(off.Value));

            var on = new Box<T>();
            FixControl.Set(heldOn.Concat(new[] { fixId }));
            yield return scenario(on);
            Check($"confirm ({fixId} on)", () => repaired(on.Value));

            var again = new Box<T>();
            FixControl.Set(heldOn);
            yield return scenario(again);
            Check($"toggle-off ({fixId} off again, patch still applied)", () => defect(again.Value));

            FixControl.Set(Array.Empty<string>());
            Report.Note($"{fixId}: off {Show(off.Value)} | on {Show(on.Value)} | off again {Show(again.Value)}");
        }

        private static T Phase<T>(IEnumerable<string> on, Func<T> scenario)
        {
            BeforePhase?.Invoke();
            FixControl.Set(on);
            try
            {
                return scenario();
            }
            finally
            {
                FixControl.Set(Array.Empty<string>());
            }
        }

        private static void Check(string phase, Action check)
        {
            try
            {
                check();
            }
            catch (ExpectationFailedException ex)
            {
                throw new ExpectationFailedException($"{phase}: {ex.Message}");
            }
        }

        private static string Show<T>(T value) => value switch
        {
            null => "null",
            float f => f.ToString("R"),
            string s => s,
            IEnumerable items => "[" + string.Join(", ", items.Cast<object>().Select(i => i is float x ? x.ToString("R") : i?.ToString())) + "]",
            _ => value.ToString(),
        };
    }

    /// <summary>Which fixes are on, for tests that are not an off/on/off comparison.</summary>
    public static class Toggle
    {
        /// <summary>Exactly these fixes on, every other one off.</summary>
        public static void Only(params string[] ids) => FixControl.Set(ids);

        /// <summary>Every fix off: the game as it ships, with the patches still applied.</summary>
        public static void AllOff() => FixControl.Set(Array.Empty<string>());
    }

    /// <summary>Carries a coroutine's measurement out to its caller.</summary>
    public sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }

    /// <summary>Free-form lines attached to the current test's result.</summary>
    public static class Report
    {
        private static readonly List<string> NotesList = new List<string>();

        internal static IReadOnlyList<string> Notes => NotesList;

        internal static void Clear() => NotesList.Clear();

        public static void Note(string line) => NotesList.Add(line);
    }
}
