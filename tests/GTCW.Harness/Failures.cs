using System;
using System.Linq;
using System.Reflection;

namespace GTCW.Harness
{
    /// <summary>Turns whatever a test threw into one readable line.</summary>
    internal static class Failures
    {
        public static string Describe(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            if (ex is ExpectationFailedException)
            {
                return ex.Message;
            }

            // Split(char[]), not Split(char): the engine tier runs on .NET
            // Framework, which lacks netstandard2.1's char overloads.
            string frames = string.Join(" | ", (ex.StackTrace ?? string.Empty)
                .Split(new[] { '\n' }).Select(l => l.Trim()).Where(l => l.Length > 0).Take(6));
            string inner = ex.InnerException == null
                ? string.Empty
                : $" (inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message})";
            return $"{ex.GetType().Name}: {ex.Message}{inner} at {frames}";
        }
    }
}
