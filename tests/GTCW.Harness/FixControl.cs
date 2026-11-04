using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using GTCW.BugFixes;
using HarmonyLib;

namespace GTCW.Harness
{
    /// <summary>
    /// The harness's hold on the mod: which fixes exist, and which are on.
    /// </summary>
    /// <remarks>
    /// Every patch group is applied once, at start-up, with every fix enabled —
    /// by the harness in the engine tier, by the real plugin (configured
    /// all-on) in the game tiers. Tests then decide what is on with
    /// <see cref="FixState"/>, exactly as the patch bodies ask it.
    /// </remarks>
    internal static class FixControl
    {
        private static ManualLogSource? _log;

        /// <summary>
        /// A fix that silently stays off whatever a test asks. Set only by
        /// <c>--sabotage</c>, to prove the tests notice a fix that does nothing.
        /// </summary>
        public static string? Sabotaged { get; set; }

        public static ManualLogSource Log => _log ??= Logger.CreateLogSource("GTCW Bug Fixes");

        public static IReadOnlyList<FixGroup> Groups() => FixCatalogue.Groups();

        public static IEnumerable<BugFixes.Fix> AllFixes() => Groups().SelectMany(g => g.Fixes);

        public static void Set(IEnumerable<string> ids) =>
            FixState.Seal(Log, ids.Where(id => !string.Equals(id, Sabotaged, StringComparison.Ordinal)).ToArray());

        /// <summary>
        /// Probes and patches every group, the way FixRegistry.ApplyAll does for
        /// the enabled ones. Returns one line per fix that cannot apply.
        /// </summary>
        public static List<string> ApplyAll(Harmony harmony)
        {
            var problems = new List<string>();
            var groups = Groups();
            var all = new List<string>();

            foreach (FixGroup group in groups)
            {
                foreach (BugFixes.Fix fix in group.Fixes)
                {
                    ProbeResult probe = fix.Probe();
                    if (probe.Ok)
                    {
                        all.Add(fix.Id);
                    }
                    else
                    {
                        problems.Add($"{fix.Id} cannot apply to this build: {probe.Reason}");
                    }
                }
            }

            FixState.Seal(Log, all);
            foreach (FixGroup group in groups)
            {
                try
                {
                    group.Apply(harmony);
                }
                catch (Exception ex)
                {
                    problems.Add($"patching {group.Target} threw: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Set(Array.Empty<string>());
            return problems;
        }
    }
}
