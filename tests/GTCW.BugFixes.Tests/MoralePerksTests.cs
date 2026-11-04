using System;
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
    /// GTCW-024, GTCW-025 and GTCW-027 — three battle perks whose morale
    /// effects are misapplied inside <c>Regiment.UpdateMorale</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each measurement sets the state one morale term reads on a live
    /// regiment of a live battle, calls the game's own <c>UpdateMorale</c>
    /// once over a tenth of an hour, and reads the term from
    /// <c>moraleeffects</c> — the breakdown the unit panel shows. The
    /// regiment is restored field by field afterwards.
    /// </para>
    /// <para>
    /// The block holding these terms only runs in daylight fighting
    /// (<c>BattleUnits.eodcycle == 0</c>). The test holds it there for its one
    /// call and puts it back, so these tests do not depend on the battle having
    /// reached the fighting stage; <c>MountedRiflesMalusTests.AHeadlessBattleFights</c>
    /// covers that.
    /// </para>
    /// </remarks>
    [FixTests("GTCW-024", "GTCW-025", "GTCW-027")]
    [Battle]
    public sealed class MoralePerksTests
    {
        private const int DeadlyVolley = 5;
        private const int IronDiscipline = 7;
        private const int Vanguard = 12;
        private const int LevelI = 0;
        private const int LevelII = 1;
        private const int LevelIII = 2;
        private const float Hours = 0.1f;

        /// <summary>
        /// GTCW-025: from level II the perk froze the decay of a nearby friendly
        /// rout's penalty instead of ignoring the penalty.
        /// </summary>
        [BattleTest]
        public IEnumerator IronDisciplineIIIgnoresANearbyRout()
        {
            Regiment unit = AnInfantryRegiment();
            const float penalty = -0.075f;
            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-025",
                box =>
                {
                    Func<Regiment, float> afterRout = u => u.friendlyroutednear;
                    box.Value = (Measure(unit, -1, 0, u => u.friendlyroutednear = penalty, afterRout),
                                 Measure(unit, IronDiscipline, LevelII, u => u.friendlyroutednear = penalty, afterRout));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.True(r.without > penalty, $"without the perk the penalty decays ({penalty} -> {r.without})");
                    Expect.Near(penalty, r.with, "the penalty under Iron discipline II (unfixed: frozen)");
                },
                repaired: r => Expect.Near(0f, r.with, "the penalty under Iron discipline II"));
        }

        /// <summary>
        /// GTCW-024: the perk's 0.5 was applied to the drill reduction of the
        /// heavy-casualties hit, and only from level III, instead of to the hit.
        /// </summary>
        [BattleTest]
        public IEnumerator DeadlyVolleyHalvesTheHeavyCasualtyHit()
        {
            Regiment unit = AnInfantryRegiment();
            float t = unit.GetTraining();
            float T = GamePrefs.moraleeffectonhighmomentarycasualties;
            float d = GamePrefs.drillreductionfactorforhighmomentarycasualties;
            float p = GamePrefs.perk_deadlyvolley_reductionmoralehit;
            Expect.True(t > 0f && p != 1f && T != 0f, $"the term can show the difference (training {t}, perk factor {p}, hit {T})");
            Report.Note($"training {t:0.###}, hit {T}, drill factor {d}, perk factor {p}");

            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-024",
                box =>
                {
                    box.Value = (Measure(unit, -1, 0, HeavyCasualties, u => u.moraleeffects[9]),
                                 Measure(unit, DeadlyVolley, LevelIII, HeavyCasualties, u => u.moraleeffects[9]));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.NonZero(r.without, "the heavy-casualties term without the perk");
                    Expect.Near(r.without + T * t * d * (1f - p), r.with, "the term under Deadly volley III, as shipped (perk scales the drill reduction)");
                },
                repaired: r =>
                {
                    Expect.NonZero(r.without, "the heavy-casualties term without the perk");
                    Expect.Near(r.without + (p - 1f) * T * (1f - t * d), r.with, "the term under Deadly volley III (perk scales the hit)");
                });
        }

        /// <summary>GTCW-027, level I's own benefit: no morale penalty for being detached.</summary>
        [BattleTest]
        public IEnumerator VanguardIRemovesTheDetachedPenalty()
        {
            Regiment unit = AnInfantryRegiment();
            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-027",
                box =>
                {
                    box.Value = (Measure(unit, -1, 0, u => u.detached = true, u => u.moraleeffects[15]),
                                 Measure(unit, Vanguard, LevelI, u => u.detached = true, u => u.moraleeffects[15]));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.NonZero(r.without, "the detached penalty without the perk");
                    Expect.Near(r.without, r.with, "the detached penalty under Vanguard I (unfixed: unchanged)");
                },
                repaired: r =>
                {
                    Expect.NonZero(r.without, "the detached penalty without the perk");
                    Expect.Near(0f, r.with, "the detached penalty under Vanguard I");
                });
        }

        /// <summary>GTCW-027, the other half: ignoring flanks is level II's benefit, not level I's.</summary>
        [BattleTest]
        public IEnumerator VanguardIDoesNotYetIgnoreBeingOutflanked()
        {
            Regiment unit = AnInfantryRegiment();
            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-027",
                box =>
                {
                    box.Value = (Measure(unit, -1, 0, u => u.outflanked = 3, u => u.moraleeffects[2]),
                                 Measure(unit, Vanguard, LevelI, u => u.outflanked = 3, u => u.moraleeffects[2]));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.NonZero(r.without, "the outflanked penalty without the perk");
                    Expect.Near(0f, r.with, "the outflanked penalty under Vanguard I (unfixed: level II's benefit early)");
                },
                repaired: r =>
                {
                    Expect.NonZero(r.without, "the outflanked penalty without the perk");
                    Expect.Near(r.without, r.with, "the outflanked penalty under Vanguard I");
                });
        }

        /// <summary>
        /// One melee record as heavy as the unit itself. The constructor stores
        /// its arguments only when given an opponent, so the fields are set
        /// directly — the first version of this test passed it null and
        /// measured a term of zero.
        /// </summary>
        private static void HeavyCasualties(Regiment unit)
        {
            var record = new Regiment.ReceivedMeleeCasualties(null, 0f, 0)
            {
                timeoffire = GameVars.currenttimefromstart,
                sufferedcasualties = Math.Max(1, unit.strength),
            };
            unit.receivedmeleecasualties = new List<Regiment.ReceivedMeleeCasualties> { record };
        }

        /// <summary>A healthy infantry regiment fighting in this battle.</summary>
        private static Regiment AnInfantryRegiment()
        {
            Regiment unit = BattleUnits.completeunitlist.FirstOrDefault(u =>
                                u != null && u.unittyp == 0 && u.strength > 100 && !u.isrouted && u.gameObject.activeInHierarchy &&
                                u.moraleeffects != null && u.moraleeffects.Length > 17)
                            ?? throw new ExpectationFailedException("no active infantry regiment in this battle");
            Report.Note($"regiment '{unit.name}', {unit.strength} men");
            return unit;
        }

        /// <summary>
        /// One call of the game's <c>UpdateMorale</c> on the unit, holding only
        /// the given perk, after <paramref name="arrange"/>; returns what
        /// <paramref name="read"/> takes from it. Everything is put back.
        /// </summary>
        private static float Measure(Regiment unit, int perk, int level, Action<Regiment> arrange, Func<Regiment, float> read)
        {
            BattleUnits battle = Clock.Units ?? throw new ExpectationFailedException("no BattleUnits in this scene");
            int dayCycle = battle.eodcycle;
            Units.KeepFields(unit);
            Units.SetPerks(unit, (perk, level));
            try
            {
                unit.morale = 0.9f;
                unit.isrouted = false;
                unit.inmelee = 0f;
                unit.friendlyroutednear = 0f;
                unit.enemyroutednear = 0f;
                unit.outflanked = 0;
                unit.detached = false;
                unit.receivedmeleecasualties = new List<Regiment.ReceivedMeleeCasualties>();
                arrange(unit);
                unit.lastmoraleupdate = GameVars.currenttimefromstart - Hours;
                battle.eodcycle = 0;
                Traverse.Create(unit).Method("UpdateMorale").GetValue();
                return read(unit);
            }
            finally
            {
                battle.eodcycle = dayCycle;
                Units.Restore();
            }
        }
    }
}
