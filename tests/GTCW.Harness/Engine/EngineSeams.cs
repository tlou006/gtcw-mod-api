using System;
using HarmonyLib;

namespace GTCW.Harness.Engine
{
    /// <summary>
    /// The engine tier's only departures from the real engine beyond the stubs:
    /// repeatable randomness and no UI.
    /// </summary>
    /// <remarks>
    /// With Unity stubbed, <c>Random.Range</c> would return 0 and silently zero
    /// any term it scales. A seeded generator, reset before every test, keeps
    /// the game's arithmetic meaningful and every run identical.
    /// </remarks>
    internal static class EngineSeams
    {
        private const int Seed = 18610412;
        private static Random _random = new Random(Seed);

        public static void Install(Harmony harmony)
        {
            Prefix(harmony, AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(float), typeof(float) }), nameof(RangeFloat));
            Prefix(harmony, AccessTools.Method(typeof(UnityEngine.Random), "Range", new[] { typeof(int), typeof(int) }), nameof(RangeInt));
            Prefix(harmony, AccessTools.PropertyGetter(typeof(UnityEngine.Random), "value"), nameof(Value));
            Prefix(harmony, AccessTools.Method(typeof(CampaignObjective), "LaunchNewspaperMessage"), nameof(Skip));
        }

        public static void Reseed() => _random = new Random(Seed);

        private static void Prefix(Harmony harmony, System.Reflection.MethodBase? target, string stub)
        {
            if (target == null)
            {
                throw new MissingMethodException($"engine seam target for {stub} not found");
            }

            harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(typeof(EngineSeams), stub)));
        }

        private static bool RangeFloat(float minInclusive, float maxInclusive, ref float __result)
        {
            __result = minInclusive + (float)_random.NextDouble() * (maxInclusive - minInclusive);
            return false;
        }

        private static bool RangeInt(int minInclusive, int maxExclusive, ref int __result)
        {
            __result = maxExclusive > minInclusive ? _random.Next(minInclusive, maxExclusive) : minInclusive;
            return false;
        }

        private static bool Value(ref float __result)
        {
            __result = (float)_random.NextDouble();
            return false;
        }

        private static bool Skip() => false;
    }
}
