using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace GTCW.BugFixes
{
    /// <summary>
    /// Holds the fixes, binds a config entry per register id, and applies only
    /// what is both enabled and applicable to the running build.
    /// </summary>
    internal sealed class FixRegistry
    {
        private readonly List<FixGroup> _groups = new List<FixGroup>();
        private readonly Dictionary<string, ConfigEntry<FixToggle>> _toggles =
            new Dictionary<string, ConfigEntry<FixToggle>>();
        private readonly Dictionary<string, ConfigEntry<bool>> _families =
            new Dictionary<string, ConfigEntry<bool>>();

        private readonly ConfigFile _config;
        private readonly ManualLogSource _log;

        public FixRegistry(ConfigFile config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        public void Add(FixGroup group)
        {
            _groups.Add(group);

            foreach (Fix fix in group.Fixes)
            {
                if (fix.Family != null && !_families.ContainsKey(fix.Family))
                {
                    _families[fix.Family] = _config.Bind(
                        "0. Families",
                        fix.Family,
                        false,
                        DescribeFamily(fix.Family));
                }

                // Section by class, so a player can read the file and take the
                // arithmetic repairs without opting into anything that moves a
                // tuned number. The id is the key, never the title: titles get
                // reworded, config files must not break when they do.
                string section = SectionFor(fix.Class);

                _toggles[fix.Id] = _config.Bind(
                    section,
                    fix.Id,
                    FixToggle.Default,
                    $"{fix.Title}\n" +
                    $"Patches: {group.Target}\n" +
                    $"Evidence: {fix.Source}\n" +
                    $"Save safety: {Describe(fix.Save)}\n" +
                    $"Default: {(fix.DefaultEnabled ? "on" : "off")}" +
                    (fix.Family == null ? string.Empty : $", or on with {fix.Family}"));
            }
        }

        /// <summary>
        /// Probes and applies. A group whose fixes are all off is not patched at
        /// all — the mod is then present and inert, which is the behaviour the
        /// compatibility boundary promises.
        /// </summary>
        public void ApplyAll(Harmony harmony)
        {
            var enabled = new List<string>();
            var skipped = new List<string>();

            foreach (FixGroup group in _groups)
            {
                foreach (Fix fix in group.Fixes)
                {
                    if (!IsWanted(fix))
                    {
                        skipped.Add($"{fix.Id} off");
                        continue;
                    }

                    ProbeResult probe = fix.Probe();
                    if (!probe.Ok)
                    {
                        // Loudly, but not fatally: a game update that moves one
                        // method must not stop the other fixes loading.
                        _log.LogWarning($"{fix.Id} cannot apply to this build: {probe.Reason}");
                        skipped.Add($"{fix.Id} unavailable");
                        continue;
                    }

                    enabled.Add(fix.Id);
                }
            }

            FixState.Seal(_log, enabled);

            int patched = 0;
            foreach (FixGroup group in _groups)
            {
                bool any = false;
                foreach (Fix fix in group.Fixes)
                {
                    if (FixState.IsOn(fix.Id))
                    {
                        any = true;
                        break;
                    }
                }

                if (!any)
                {
                    continue;
                }

                FixState.Guard($"patching {group.Target}", () => group.Apply(harmony));
                patched++;
            }

            Report(enabled, skipped, patched);
        }

        /// <summary>
        /// Whether the player has asked for this fix: their own word first, then
        /// the fix's family, then its shipped default.
        /// </summary>
        private bool IsWanted(Fix fix)
        {
            switch (_toggles[fix.Id].Value)
            {
                case FixToggle.On:
                    return true;
                case FixToggle.Off:
                    return false;
                default:
                    return fix.DefaultEnabled ||
                           (fix.Family != null &&
                            _families.TryGetValue(fix.Family, out ConfigEntry<bool> family) &&
                            family.Value);
            }
        }

        private static string DescribeFamily(string family) => family switch
        {
            Fix.Perks =>
                "Turn on every perk fix at once: perks then do what the manual and their tooltips say.\n" +
                "Each perk fix moves a combat or campaign quantity, so each one ships off; this is the " +
                "one decision that turns the family on.\n" +
                "Any individual GTCW-NNN below set to On or Off overrides this.\n" +
                "Covers the implemented rows of defect-register.md's perk section.",
            _ => $"Turns on every fix in the '{family}' family. Individual ids still override it.",
        };

        private void Report(List<string> enabled, List<string> skipped, int patched)
        {
            if (enabled.Count == 0)
            {
                _log.LogInfo(
                    $"No fixes enabled ({skipped.Count} available); no patches applied. " +
                    "The game runs exactly as it would without this mod.");
                return;
            }

            _log.LogInfo($"{enabled.Count} fixes active across {patched} patched methods:");
            foreach (string id in enabled)
            {
                _log.LogInfo($"  {id}");
            }

            if (skipped.Count > 0)
            {
                _log.LogInfo($"{skipped.Count} not active: {string.Join(", ", skipped.ToArray())}");
            }
        }

        private static string SectionFor(FixClass fixClass) => fixClass switch
        {
            FixClass.Code => "1. Code defects",
            FixClass.Data => "2. Data defects",
            FixClass.ManualDeviation => "3. Deviations from the manual",
            _ => "4. Not fixed here",
        };

        private static string Describe(SaveSafety save) => save switch
        {
            SaveSafety.Stateless => "nothing saved is touched; toggle freely",
            SaveSafety.ValueOnly => "changes a saved value, but saves load either way",
            SaveSafety.OneWay => "repairs saved state; turning it off later does not undo the repair",
            _ => "unknown",
        };
    }
}
