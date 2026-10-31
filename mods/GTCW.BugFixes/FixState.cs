using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace GTCW.BugFixes
{
    /// <summary>
    /// What a patch body is allowed to ask at runtime: whether a fix is on, and
    /// how to fail without taking the game down.
    /// </summary>
    /// <remarks>
    /// Static because Harmony patch methods are static. Written once during
    /// <see cref="FixRegistry.ApplyAll"/> and read-only afterwards.
    /// </remarks>
    internal static class FixState
    {
        private static readonly HashSet<string> Enabled = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Throws = new Dictionary<string, int>(StringComparer.Ordinal);
        private static ManualLogSource? _log;

        /// <summary>
        /// How many times one fix may throw before it is switched off for the
        /// rest of the session. A patch that throws every pass would otherwise
        /// fill the log and slow the game to a crawl.
        /// </summary>
        private const int ThrowBudget = 5;

        public static void Seal(ManualLogSource log, IEnumerable<string> enabledIds)
        {
            _log = log;
            Enabled.Clear();
            Throws.Clear();
            foreach (string id in enabledIds)
            {
                Enabled.Add(id);
            }
        }

        /// <summary>
        /// Whether a fix is enabled. A grouped patch calls this per fix, because
        /// the patch exists as soon as <em>any</em> of its fixes is on.
        /// </summary>
        public static bool IsOn(string id) => Enabled.Contains(id);

        /// <summary>
        /// The mod's own log. Fixes report through this rather than through
        /// <c>ModApi.Log</c>: a fix's noise belongs under the mod that chose to
        /// apply it, not under the library.
        /// </summary>
        public static void Info(string message) => _log?.LogInfo(message);

        /// <inheritdoc cref="Info"/>
        public static void Warn(string message) => _log?.LogWarning(message);

        /// <summary>
        /// Runs a patch body, swallowing anything it throws.
        /// </summary>
        /// <remarks>
        /// A bug-fix mod that crashes the game is worse than the bugs it fixes,
        /// and these patch bodies run inside the campaign's hottest pass. This
        /// duplicates <c>GTCW.ModApi.Runtime</c>'s internal <c>FailSoft.Guard</c>
        /// — recorded as API gap 2 in <c>IMPLEMENTATION.md</c>.
        /// </remarks>
        public static void Guard(string id, Action body)
        {
            if (Throws.TryGetValue(id, out int count) && count >= ThrowBudget)
            {
                return;
            }

            try
            {
                body();
            }
            catch (Exception ex)
            {
                count = count + 1;
                Throws[id] = count;
                _log?.LogError($"{id} threw ({count}/{ThrowBudget}): {ex}");

                if (count >= ThrowBudget)
                {
                    _log?.LogError($"{id} is disabled for the rest of this session.");
                }
            }
        }
    }
}
