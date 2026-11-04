using System;
using System.Collections.Generic;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.GameState;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-004 — <c>Mathf.Max</c> where <c>Mathf.Min</c> was meant pins every
    /// ship's rate of fire at the long-range 0.25. GTCW-006 — the turret bonus
    /// tests <c>withinturret == 1</c>, but it is a turret count, so ships whose
    /// guns share two turrets never get it.
    /// </summary>
    [FixTests("GTCW-004", "GTCW-006")]
    public sealed class NavalFiringStrengthTests
    {
        private const float LongRangeRate = 0.25f;

        [EngineTest]
        public void ClosingTheRangeRaisesTheRateOfFire()
        {
            int ship = FirstShip(withinturret => withinturret == 0);
            Verify.Fix(
                "GTCW-004",
                () => (far: RateAt(ship, MaxRange(ship) * 0.95f), near: RateAt(ship, 1f)),
                defect: r =>
                {
                    Expect.Near(LongRangeRate, r.far, "rate of fire at long range");
                    Expect.Near(LongRangeRate, r.near, "rate of fire at point-blank range");
                },
                repaired: r =>
                {
                    Expect.Near(1f, r.near, "rate of fire at point-blank range", 0.01f);
                    Expect.Below(0.3f, r.far, "rate of fire at long range");
                });
        }

        /// <summary>
        /// With every gun in range, the rate is the long-range rate scaled by the
        /// share of guns that count as turreted — and only the counting changes.
        /// </summary>
        [EngineTest]
        public void AllSixTurretShipsGetTheTurretBonus()
        {
            List<int> ships = TurretShips();
            Expect.Equal(6, ships.Count, "turret ships in the shipped data (IMPLEMENTATION.md says six)");
            Verify.Fix(
                "GTCW-006",
                () => ships.Select(s => RateAt(s, AllGunsInRange(s))).ToArray(),
                defect: rates =>
                {
                    for (int i = 0; i < ships.Count; i++)
                    {
                        Expect.Near(Expected(ships[i], c => c == 1), rates[i], Name(ships[i]) + " counting withinturret == 1");
                    }
                },
                repaired: rates =>
                {
                    for (int i = 0; i < ships.Count; i++)
                    {
                        Expect.Near(Expected(ships[i], c => c > 0), rates[i], Name(ships[i]) + " counting withinturret > 0");
                        Expect.True(rates[i] > LongRangeRate, Name(ships[i]) + " gets a bonus at all");
                    }
                });
        }

        /// <summary>
        /// The call already spent ammunition at the pinned rate, so the fix must
        /// refund the difference, or it silently changes ammunition endurance.
        /// </summary>
        [EngineTest]
        public void AmmunitionIsSpentAtTheCorrectedRate()
        {
            int ship = FirstShip(withinturret => withinturret == 0);
            const float factor = 10f;
            Verify.Fix(
                "GTCW-004",
                () =>
                {
                    Vessel vessel = Ship(ship);
                    float before = vessel.ammo;
                    vessel.GetFiringStrength(1f, reduceammo: true, ammoconsumptionfactor: factor);
                    return (spent: before - vessel.ammo, rate: vessel.strengthdata[3]);
                },
                defect: r => Expect.Near(PerRate(factor) * LongRangeRate, r.spent, "ammunition spent at the pinned rate"),
                repaired: r =>
                {
                    Expect.Near(1f, r.rate, "rate of fire at point-blank range", 0.01f);
                    Expect.Near(PerRate(factor) * r.rate, r.spent, "ammunition spent at the corrected rate", 1e-3f);
                });
        }

        /// <summary>Ammunition per unit of rate, as the game charges it with every gun in range.</summary>
        private static float PerRate(float factor) => 1f / GamePrefs.averagesalvopervessel * 1f * factor;

        private static float RateAt(int type, float distance)
        {
            Vessel vessel = Ship(type);
            vessel.GetFiringStrength(distance);
            return vessel.strengthdata[3];
        }

        private static Vessel Ship(int type)
        {
            Vessel vessel = Fakes.Make<Vessel>();
            vessel.ammo = 100f;
            vessel.condition = 1f;
            vessel.strengthdata = new float[6];
            vessel.type = type;
            return vessel;
        }

        private static float Expected(int type, Func<int, bool> counts)
        {
            List<GameVars.ShipType.WeaponSystems> systems = GameVars.shiptypes[type].weaponsystems;
            float guns = systems.Sum(w => (float)w.guns);
            float turreted = systems.Where(w => counts(w.withinturret)).Sum(w => (float)w.guns);
            return LongRangeRate * ((GamePrefs.rofimprovementturrets - 1f) * (turreted / guns) + 1f);
        }

        private static float MaxRange(int type) =>
            GameVars.shiptypes[type].weaponsystems.Max(w => GameVars.weapon[w.weaponid].range_yd);

        private static float AllGunsInRange(int type) =>
            GameVars.shiptypes[type].weaponsystems.Min(w => GameVars.weapon[w.weaponid].range_yd) * 0.5f;

        private static int FirstShip(Func<int, bool> everySystem) =>
            Enumerable.Range(0, GameVars.shiptypes.Count)
                .First(t => GameVars.shiptypes[t].weaponsystems.Count > 0 &&
                            GameVars.shiptypes[t].weaponsystems.All(w => everySystem(w.withinturret)));

        private static List<int> TurretShips() =>
            Enumerable.Range(0, GameVars.shiptypes.Count)
                .Where(t => GameVars.shiptypes[t].weaponsystems.Any(w => w.withinturret > 0))
                .ToList();

        private static string Name(int type) => GameVars.shiptypes[type].name;
    }
}
