using HarmonyLib;

namespace GTCW.Harness.GameState
{
    /// <summary>
    /// The campaign clock, set the only way the game reads it: the UniStorm day
    /// and month and <c>BattleUnits.year</c>, which the real
    /// <c>GameVars.GetCampaignFloatingDate</c> combines. Setting those rather
    /// than patching the date keeps the game's own date arithmetic in the test —
    /// and that arithmetic differs from <c>Tools.Date</c>'s, which matters for
    /// thresholds like the 1864 election's.
    /// </summary>
    public static class Clock
    {
        /// <summary>
        /// Moves the clock. In the engine tier, where no scene exists, it first
        /// gives the game stand-in UniStorm and BattleUnits objects to read.
        /// </summary>
        public static void Set(int day, int month, int year, float hour = 12f)
        {
            BattleUnits? units = Units;
            if (units == null)
            {
                units = Fakes.Make<BattleUnits>();
                Traverse.Create(typeof(GameVars)).Field("bunits").SetValue(units);
            }

            UniStormWeatherSystem_C? weather = Weather;
            if (weather == null)
            {
                weather = Fakes.Make<UniStormWeatherSystem_C>();
                Traverse.Create(typeof(GameVars)).Field("uniStormSystem").SetValue(weather);
            }

            units.year = year;
            weather.monthCounter = month;
            weather.dayCounter = day;
            weather.Hour = hour;
        }

        /// <summary>What the game itself would report as the date now.</summary>
        public static float Now => GameVars.GetCampaignFloatingDate();

        public static BattleUnits? Units =>
            Traverse.Create(typeof(GameVars)).Field("bunits").GetValue<BattleUnits>();

        public static UniStormWeatherSystem_C? Weather =>
            Traverse.Create(typeof(GameVars)).Field("uniStormSystem").GetValue<UniStormWeatherSystem_C>();
    }
}
