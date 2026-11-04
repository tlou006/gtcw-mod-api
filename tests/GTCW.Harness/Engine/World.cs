using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;

namespace GTCW.Harness.Engine
{
    /// <summary>
    /// The shipped game data, loaded by the game's own loaders in the order
    /// <c>CampaignController.Awake</c> runs them, minus the scene.
    /// </summary>
    /// <remarks>
    /// Reloaded before every engine test and every protocol phase. It costs
    /// about 130 ms and makes tests independent: whatever a test did to an
    /// alliance or a nation is replaced by a fresh load. The working directory
    /// is the runner's copy of the install's data folders, because the loaders
    /// open relative <c>Config/...</c> paths and some of them write logs.
    /// </remarks>
    public static class World
    {
        public static string Map { get; private set; } = "001";

        public static string Setup { get; private set; } = "A";

        public static string Folder => "Campaigns/" + Map + "/" + Setup;

        public static void Load(string map = "001", string setup = "A")
        {
            Map = map;
            Setup = setup;

            GameVars.SetCultureInfo();

            // The game parses its version out of a UI label, and several loaders
            // choose a file format by it; outside the game it would read 0.
            string version = Path.Combine(Folder, "Save", "version.dat");
            if (!File.Exists(version))
            {
                version = Path.Combine("Campaigns", "001", "A", "Save", "version.dat");
            }

            MainMenuControls.currentversion = float.Parse(File.ReadAllText(version).Trim(), CultureInfo.InvariantCulture);

            GamePrefs.loaddebugs();
            GamePrefs.loadcontrols();
            GamePrefs.loadui();
            GamePrefs.loaduibattle();
            GamePrefs.leveltoload = map;
            GamePrefs.subleveltoload = setup;
            GamePrefs.LoadDLCWLConfig(Folder);
            GamePrefs.loadunitprefs(Folder);
            GamePrefs.loadcampaigncontrols(Folder);
            GameVars.ImportWeapons();
            GameVars.import_nations(Folder);
            GameVars.MinMaxRanks.ImportMinMaxRanks(Folder);
            GameVars.import_commanders(Folder + "/");
            GameVars.ImportShipTypes();
            GameVars.ImportBuildingTypes();

            ClearScene();
        }

        /// <summary>Reloads whatever was last loaded.</summary>
        public static void Reload() => Load(Map, Setup);

        /// <summary>
        /// Drops scene objects an earlier test may have faked, so a test that
        /// needs a clock or objectives sets them up itself.
        /// </summary>
        private static void ClearScene()
        {
            Traverse.Create(typeof(GameVars)).Field("bunits").SetValue(null);
            Traverse.Create(typeof(GameVars)).Field("uniStormSystem").SetValue(null);
            Traverse.Create(typeof(GameVars)).Field("uniStormObject").SetValue(null);
            CampaignObjective.allcampaignobjectives = new List<CampaignObjective>();
        }
    }
}
