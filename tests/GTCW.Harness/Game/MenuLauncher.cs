using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// Starts a scenario through the main menu's own code, so the boot that
    /// follows is the one a player gets.
    /// </summary>
    internal static class MenuLauncher
    {
        /// <summary>What the "Start Campaign" button does: <c>MainMenu.LaunchCampaign</c>.</summary>
        public static string Campaign(MainMenu menu, string map, string setup, int side)
        {
            int index = Find(menu, map, setup);
            Select(menu, index, side);
            menu.LaunchCampaign();
            return $"campaign {menu.GetLevel()}/{menu.GetSubLevel()} side {side}";
        }

        /// <summary>
        /// What the battle panel's start button does. The game has no single
        /// method for it, so this queues the same three batch operations that
        /// <c>PanelHandler</c> does.
        /// </summary>
        public static string Battle(MainMenu menu, string? map, string? setup, int side)
        {
            int index = Find(menu, map, setup);
            Select(menu, index, side);
            GameVars.playeralliance = side;

            // The game's own mode for an unattended battle: the AI commands
            // the player's side too. Set before the scene loads, because the
            // battle AI decides at start-up which groups it drives.
            GameVars.ai_vs_ai = true;
            GameVars.usedynamicstrategies = menu.GetAIMode() != 0f;
            GameVars.usedbonusbattles = menu.GetBonus();
            GameVars.casualtiesmodifier = GameVars.usedbonusbattles * GamePrefs.casualtiesmodifierbonus;
            GameVars.tutorialactive = false;

            List<SceneManagement.BatchOperation> ops = SceneManagement.batchoperation;
            ops.Add(new SceneManagement.BatchOperation(SceneManagement.bo_rebuildscene));
            ops.Add(new SceneManagement.BatchOperation(SceneManagement.bo_resetloadingscreen, 1, 115,
                menu.GetCommanderID(), -2, null, null, null, "", LoadingScreen.sound_loadingbattle[side]));
            ops.Add(new SceneManagement.BatchOperation(SceneManagement.bo_loadhistoricbattle, 1,
                menu.GetLevel(), menu.GetSubLevel()));
            return $"battle {menu.GetLevel()}/{menu.GetSubLevel()} side {side}";
        }

        private static void Select(MainMenu menu, int index, int side)
        {
            Traverse.Create(menu).Field("selectedscenario").SetValue(index);
            Traverse.Create(menu).Field("selectedside").SetValue(side);
        }

        private static int Find(MainMenu menu, string? map, string? setup)
        {
            var scenarios = Traverse.Create(menu).Field("scenarios").GetValue<IList>();
            if (scenarios == null || scenarios.Count == 0)
            {
                throw new ExpectationFailedException("the menu lists no scenarios");
            }

            if (map == null)
            {
                return 0;
            }

            var listed = new List<string>();
            for (int i = 0; i < scenarios.Count; i++)
            {
                Traverse s = Traverse.Create(scenarios[i]);
                string m = s.Field("mapid").GetValue<string>();
                string u = s.Field("setupid").GetValue<string>();
                listed.Add(m + "/" + u);
                if (m == map && (setup == null || u == setup))
                {
                    return i;
                }
            }

            throw new ExpectationFailedException($"scenario {map}/{setup} is not in the menu; it lists {string.Join(", ", listed)}");
        }
    }
}
