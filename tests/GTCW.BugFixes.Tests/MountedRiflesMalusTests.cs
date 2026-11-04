using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;
using HarmonyLib;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-028 — both mounted penalties become
    /// <c>(1 − base) × A[level] + base</c>, reading the declining
    /// <c>perk_mountedriflesmalusfactor</c> as the share of the penalty
    /// recovered, so Mounted rifles gets weaker at every level.
    /// </summary>
    [FixTests("GTCW-028")]
    [Battle]
    public sealed class MountedRiflesMalusTests
    {
        private const int MountedRifles = 10;
        private const int LevelIII = 2;

        private static bool _probing;
        private static float _factorRead = float.NaN;
        private static int _shotsLanded, _shotsTried;

        /// <summary>
        /// The precondition for every combat outcome in this tier: a headless
        /// battle actually fights. Shots that land and men that fall, in the
        /// game's own battle loop, with the AI commanding both sides.
        /// </summary>
        [BattleTest]
        public IEnumerator AHeadlessBattleFights()
        {
            var probe = new Harmony("gtcw.tests.shotcounter");
            probe.Patch(AccessTools.Method(typeof(Regiment), "FireBullet"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(MountedRiflesMalusTests), nameof(CountShot))));
            try
            {
                float before = Strength();
                Live.Speed = 30f;
                yield return Live.Until(() => _shotsLanded >= 50 && Strength() < before, 240f,
                    "50 shots to land and men to fall");
                Report.Note($"shots landed {_shotsLanded} of {_shotsTried} tried; men {before:0} -> {Strength():0}; " +
                            $"eodcycle {Live.EndOfDayCycle}; prompts closed {Live.PromptsClosed}; deployments ended {Live.DeploymentsEnded}");
            }
            finally
            {
                probe.UnpatchSelf();
            }
        }

        /// <summary>
        /// The reload half, at the call: the factor
        /// <c>UpdateFiringAndReloading</c> reads for a mounted unit holding
        /// Mounted rifles III, taken by a probe that runs after the fix's own
        /// prefix and skips the body so nothing fires. The mounted reload
        /// multiplier is then <c>(1 − base) × A + base</c>.
        /// </summary>
        [BattleTest]
        public IEnumerator MountedRiflesIIIRemovesMostOfTheReloadPenalty()
        {
            Regiment rider = AMountedUnit();
            float baseReload = GamePrefs.mountedreloadinfluencefactor;
            float[] shipped = GamePrefs.perk_mountedriflesmalusfactor;
            yield return Verify.FixLive<float>(
                "GTCW-028",
                multiplier =>
                {
                    float a = FactorReadBy(rider, "UpdateFiringAndReloading");
                    multiplier.Value = (1f - baseReload) * a + baseReload;
                    return Live.Frames(1);
                },
                defect: m => Expect.Near((1f - baseReload) * shipped[LevelIII] + baseReload, m, "mounted reload multiplier at level III, as shipped"),
                repaired: m => Expect.Near((1f - baseReload) * (1f - shipped[LevelIII]) + baseReload, m, "mounted reload multiplier at level III"));
        }

        /// <summary>The accuracy half, at <c>FireBullet</c>, the same way.</summary>
        [BattleTest]
        public IEnumerator MountedRiflesIIIRemovesMostOfTheAccuracyPenalty()
        {
            Regiment rider = AMountedUnit();
            float baseAccuracy = GamePrefs.mountedaccuracyinfluencefactor;
            float[] shipped = GamePrefs.perk_mountedriflesmalusfactor;
            yield return Verify.FixLive<float>(
                "GTCW-028",
                multiplier =>
                {
                    float a = FactorReadBy(rider, "FireBullet");
                    multiplier.Value = (1f - baseAccuracy) * a + baseAccuracy;
                    return Live.Frames(1);
                },
                defect: m => Expect.Near((1f - baseAccuracy) * shipped[LevelIII] + baseAccuracy, m, "mounted accuracy multiplier at level III, as shipped"),
                repaired: m => Expect.Near((1f - baseAccuracy) * (1f - shipped[LevelIII]) + baseAccuracy, m, "mounted accuracy multiplier at level III"));
        }

        /// <summary>A live cavalry regiment, mounted and holding Mounted rifles III for the test.</summary>
        private static Regiment AMountedUnit()
        {
            Regiment rider = BattleUnits.completeunitlist.FirstOrDefault(u => u != null && u.unittyp == 1 && u.strength > 0)
                             ?? throw new ExpectationFailedException("no cavalry regiment in this battle");
            Units.KeepFields(rider);
            rider.mounted = 1;
            Units.SetPerks(rider, (MountedRifles, LevelIII));
            Report.Note($"rider '{rider.name}'");
            return rider;
        }

        /// <summary>
        /// Calls <paramref name="method"/> on the unit with a last-priority
        /// probe that records <c>perk_mountedriflesmalusfactor[level]</c> as the
        /// body would read it — after the fix's prefix — and skips the body.
        /// </summary>
        private static float FactorReadBy(Regiment unit, string method)
        {
            var probe = new Harmony("gtcw.tests.malusprobe");
            probe.Patch(AccessTools.Method(typeof(Regiment), method),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(MountedRiflesMalusTests), nameof(ReadFactor))) { priority = Priority.Last });
            try
            {
                _factorRead = float.NaN;
                _probing = true;
                object[] args = method == "FireBullet" ? new object[] { 0 } : new object[0];
                Traverse.Create(unit).Method(method, args).GetValue();
                Expect.True(!float.IsNaN(_factorRead), $"the probe saw {method} run");
                return _factorRead;
            }
            finally
            {
                _probing = false;
                probe.UnpatchSelf();
            }
        }

        private static bool ReadFactor(Regiment __instance)
        {
            if (!_probing)
            {
                return true;
            }

            _factorRead = GamePrefs.perk_mountedriflesmalusfactor[__instance.GetPerkLevel(MountedRifles)];
            return false;
        }

        private static void CountShot(bool __result)
        {
            _shotsTried++;
            if (__result)
            {
                _shotsLanded++;
            }
        }

        private static float Strength() =>
            BattleUnits.completeunitlist.Where(u => u != null && u.unittyp <= 13).Sum(u => (float)u.strength);
    }
}
