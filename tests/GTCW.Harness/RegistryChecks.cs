using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using GTCW.BugFixes;
using HarmonyLib;

namespace GTCW.Harness
{
    /// <summary>
    /// Two checks of the mod's own loading path rather than of any one fix,
    /// run through the real <c>FixRegistry</c> against a throwaway config file.
    /// </summary>
    internal static class RegistryChecks
    {
        public const string InertOwner = "gtcw.harness.inert";

        /// <summary>
        /// Every fix set Off: no patch may be applied. This is the compatibility
        /// boundary's promise that the mod present and idle changes nothing.
        /// </summary>
        public static List<string> Inert(string scratchFolder) =>
            Run(scratchFolder, "inert.cfg", allOff: true, (problems, registry) =>
            {
                var mine = Harmony.GetAllPatchedMethods()
                    .Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains(InertOwner) == true)
                    .Select(m => m.DeclaringType?.Name + "." + m.Name)
                    .ToList();
                if (mine.Count > 0)
                {
                    problems.Add("with every fix Off the registry still patched: " + string.Join(", ", mine));
                }
            });

        /// <summary>
        /// A fresh config: exactly the fixes marked default-on are enabled, and
        /// none of the off-by-default ones — the register's "reach decides
        /// whether it is on" rule, checked against the code.
        /// </summary>
        public static List<string> Defaults(string scratchFolder) =>
            Run(scratchFolder, "defaults.cfg", allOff: false, (problems, registry) =>
            {
                foreach (BugFixes.Fix fix in FixControl.AllFixes())
                {
                    if (FixState.IsOn(fix.Id) != fix.DefaultEnabled)
                    {
                        problems.Add($"{fix.Id}: default-enabled is {fix.DefaultEnabled} but a fresh config left it " +
                                     (FixState.IsOn(fix.Id) ? "on" : "off"));
                    }
                }
            });

        /// <summary>
        /// Writes the mod's config file with every fix and family on, or every
        /// one off, by binding it through the mod's own registry — so the
        /// sections and keys are whatever the mod really uses. The headless
        /// tiers install it in the sandbox before launching the game.
        /// </summary>
        public static void WriteConfig(string path, bool on)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ConfigFile config = Bound(path, out _);
            SetAll(config, on);
            config.Save();
        }

        private static ConfigFile Bound(string path, out FixRegistry registry)
        {
            var config = new ConfigFile(path, saveOnInit: false) { SaveOnConfigSet = false };
            registry = new FixRegistry(config, FixControl.Log);
            foreach (FixGroup group in FixControl.Groups())
            {
                registry.Add(group);
            }

            return config;
        }

        private static void SetAll(ConfigFile config, bool on)
        {
            foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> pair in config)
            {
                ConfigEntryBase entry = pair.Value;
                if (entry.SettingType == typeof(FixToggle))
                {
                    entry.BoxedValue = on ? FixToggle.On : FixToggle.Off;
                }
                else if (entry.SettingType == typeof(bool))
                {
                    entry.BoxedValue = on;
                }
            }
        }

        private static List<string> Run(
            string scratchFolder,
            string fileName,
            bool allOff,
            Action<List<string>, FixRegistry> check)
        {
            var problems = new List<string>();
            string path = Path.Combine(scratchFolder, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ConfigFile config = Bound(path, out FixRegistry registry);
            if (allOff)
            {
                SetAll(config, on: false);
            }

            var harmony = new Harmony(InertOwner);
            try
            {
                registry.ApplyAll(harmony);
                check(problems, registry);
            }
            finally
            {
                harmony.UnpatchSelf();
                FixControl.Set(Array.Empty<string>());
            }

            return problems;
        }
    }
}
