using System;
using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime
{
    /// <summary>
    /// Fail-soft, as a mechanism rather than an aspiration. An exception must
    /// never cross back into game code from a postfix, and a badly behaved mod
    /// must cost the player that mod, not their game.
    /// </summary>
    internal static class FailSoft
    {
        /// <summary>After this many throws on one event, a subscriber is dropped.</summary>
        private const int ThrowBudget = 5;

        private static readonly Dictionary<string, int> Throws =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Runs <paramref name="body"/> and swallows anything it throws.
        /// <paramref name="who"/> identifies the mod or feature in the log, so a
        /// report names a culprit instead of a stack trace.
        /// </summary>
        /// <returns>False when the call threw.</returns>
        public static bool Guard(IModApiLog log, string who, Action body)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception ex)
            {
                int count = Throws.TryGetValue(who, out int previous) ? previous + 1 : 1;
                Throws[who] = count;

                if (count <= ThrowBudget)
                {
                    log.Error($"{who} threw ({count}/{ThrowBudget}): {ex}");
                }

                if (count == ThrowBudget)
                {
                    log.Error($"{who} has exhausted its throw budget and will be unsubscribed.");
                }

                return false;
            }
        }

        /// <summary>True once <paramref name="who"/> has thrown too often to keep calling.</summary>
        public static bool IsBanned(string who) =>
            Throws.TryGetValue(who, out int count) && count >= ThrowBudget;
    }
}
