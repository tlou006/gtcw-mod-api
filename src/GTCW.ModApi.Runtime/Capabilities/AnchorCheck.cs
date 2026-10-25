using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Capabilities
{
    /// <summary>
    /// Verifies, at startup, that the types this library intends to patch are
    /// present under the names the research recorded.
    /// </summary>
    /// <remarks>
    /// This is the cheapest possible early-warning system and it earns its keep
    /// on day one: it proves the publicized reference resolved, confirms the
    /// "game code lives in the global namespace" rule against a live build, and
    /// on patch day it turns "the mod is broken, somewhere" into one line in the
    /// log. The anchors come from the hook-seam tables in
    /// <c>code-findings/</c> — mostly summary-game-state and
    /// 22-policy-and-project-catalogue §15.
    /// </remarks>
    internal static class AnchorCheck
    {
        /// <summary>Type name → why the library cares about it.</summary>
        private static readonly (string Type, string Why)[] Anchors =
        {
            ("SceneManagement", "scene lifecycle, SaveAll, the batch queue"),
            ("GameVars", "the static state surface: alliance[], nation[], frame"),
            ("GamePrefs", "every config-backed tunable"),
            ("BattleUnits", "the real per-frame driver for both scenes"),
            ("Regiment", "the campaign and battlefield unit"),
            ("Policies", "LaunchPolicyEffect, UpdatePolicyEffects, IsDeactivated"),
            ("Projects", "the two effect dispatchers and LoadedProjects.Import"),
            ("Policy", "the prefab-authored policy component"),
            ("Tools", "the list-field and date parsers"),
            ("ImportExportUnitData", "the save/load surface and CheckFileIntegrity"),
        };

        public static void Run(IModApiLog log)
        {
            var missing = new List<string>();

            foreach ((string type, string why) in Anchors)
            {
                if (GameProbe.Type(type) == null)
                {
                    missing.Add($"{type} ({why})");
                }
            }

            if (missing.Count == 0)
            {
                log.Info($"Anchor check: all {Anchors.Length} expected game types present.");
                return;
            }

            log.Warning(
                $"Anchor check: {missing.Count} of {Anchors.Length} expected game types are missing. " +
                "Either this is a different build than the research describes, or the game reference " +
                "did not resolve.");

            foreach (string entry in missing)
            {
                log.Warning($"  missing: {entry}");
            }
        }
    }
}
