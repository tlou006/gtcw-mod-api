using System;

namespace GTCW.Harness
{
    /// <summary>A failed expectation. Its message is the whole report.</summary>
    public sealed class ExpectationFailedException : Exception
    {
        public ExpectationFailedException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Assertions. Every one names what it measured, because a failure report is
    /// read by someone who did not write the test.
    /// </summary>
    /// <remarks>
    /// Comparisons on floats always take a tolerance: the engine tier runs on the
    /// .NET Framework CLR and the headless tiers on the game's Mono, and the two
    /// are not guaranteed to round identically.
    /// </remarks>
    public static class Expect
    {
        public const float DefaultTolerance = 1e-4f;

        public static void Near(float expected, float actual, string what, float tolerance = DefaultTolerance)
        {
            if (float.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            {
                Fail($"{what}: expected {expected:R} (±{tolerance:R}), got {actual:R}");
            }
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!Equals(expected, actual))
            {
                Fail($"{what}: expected {expected}, got {actual}");
            }
        }

        public static void True(bool condition, string what)
        {
            if (!condition)
            {
                Fail(what);
            }
        }

        public static void Below(float limit, float actual, string what)
        {
            if (float.IsNaN(actual) || !(actual < limit))
            {
                Fail($"{what}: expected below {limit:R}, got {actual:R}");
            }
        }

        public static void AtLeast(float limit, float actual, string what)
        {
            if (float.IsNaN(actual) || !(actual >= limit))
            {
                Fail($"{what}: expected at least {limit:R}, got {actual:R}");
            }
        }

        /// <summary>
        /// The guard every ratio test needs: a baseline of zero makes
        /// "unchanged" and "scaled by the factor" the same claim, and both pass.
        /// </summary>
        public static void NonZero(float actual, string what)
        {
            if (float.IsNaN(actual) || Math.Abs(actual) < 1e-9f)
            {
                Fail($"{what} must be non-zero for the comparison to mean anything; it was {actual:R}");
            }
        }

        public static void NaN(float actual, string what)
        {
            if (!float.IsNaN(actual))
            {
                Fail($"{what}: expected NaN, got {actual:R}");
            }
        }

        public static void Logged(string fragment, string what)
        {
            if (!LogCapture.Contains(fragment))
            {
                Fail($"{what}: no log line contains \"{fragment}\"");
            }
        }

        public static void NotLogged(string fragment, string what)
        {
            if (LogCapture.Contains(fragment))
            {
                Fail($"{what}: a log line unexpectedly contains \"{fragment}\"");
            }
        }

        public static void Fail(string message) => throw new ExpectationFailedException(message);
    }
}
