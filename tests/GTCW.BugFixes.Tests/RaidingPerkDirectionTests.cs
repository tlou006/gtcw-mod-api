using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using HarmonyLib;
using UnityEngine;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-038 and GTCW-039 — in <c>Autocalc.UpdateRaiding</c>, Partisan
    /// Brigades' and Bushwhackers' multipliers sit in the block that hurts the
    /// <em>raider's own</em> brigade; the block that hurts the raided enemy
    /// reads no perk at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each measurement is one real raid round: a live Union formation next to
    /// a live Confederate one, a raid engine started the way the game starts
    /// one (<c>AddComponent&lt;Autocalc&gt;().StartRaiding</c>), and the game's
    /// own <c>UpdateRaiding</c>. A probe registered after every other patch
    /// records each <c>SufferCasualties</c> and <c>SufferMoraleHit</c> the round
    /// makes — after the fix's own prefix has adjusted the morale hit — and
    /// skips applying them, so no unit is changed and every measurement starts
    /// from the same state. Unity's generator is reseeded before each round.
    /// </para>
    /// <para>
    /// The assertions are on ratios, with-perk over without, and on direction
    /// rather than on the perk's nominal factor: both blocks compute
    /// <c>num3 *= Random.Range(num3 * f, num3 * (1 + f))</c>, which squares the
    /// casualty term, so a factor lent to the enemy's block scales its
    /// casualties by its square. The measured ratios are reported.
    /// </para>
    /// </remarks>
    [FixTests("GTCW-038", "GTCW-039")]
    // 001/E (spring 1864): in 001/A the only active Union formations are fort garrisons
    // with artillery; the field armies are not yet on the map.
    [Campaign("001", "E")]
    public sealed class RaidingPerkDirectionTests
    {
        private const int PartisanBrigades = 12;
        private const int Bushwhackers = 13;
        private const int LevelIII = 2;
        private const int Seed = 1862;

        private static bool _recording;
        private static readonly List<(int alliance, float amount)> Casualties = new List<(int, float)>();
        private static readonly List<(int alliance, float amount)> MoraleHits = new List<(int, float)>();

        public sealed class Round
        {
            public float EnemyCasualties { get; set; }

            public float OwnCasualties { get; set; }

            public float EnemyMorale { get; set; }

            public float OwnMorale { get; set; }

            public override string ToString() =>
                $"enemy {EnemyCasualties:G4} men / {EnemyMorale:G4} morale, own {OwnCasualties:G4} / {OwnMorale:G4}";
        }

        [CampaignTest]
        public IEnumerator PartisanBrigadesHurtTheEnemyNotTheRaider()
        {
            yield return Live.NextCampaignPass();
            (Regiment raider, Regiment guard) = Contact();
            yield return Verify.FixLive<(Round without, Round with)>(
                "GTCW-038",
                box =>
                {
                    box.Value = (Raid(raider, guard, -1), Raid(raider, guard, PartisanBrigades));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.Near(1f, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Partisan III over none (unfixed: no effect)", 0.01f);
                    Expect.True(Ratio(r, x => x.OwnCasualties, "own casualties") > 1.01f, "the raider's own casualties are amplified (unfixed)");
                    Expect.True(Ratio(r, x => x.OwnMorale, "own morale hit") > 1.01f, "the raider's own morale hit is amplified (unfixed)");
                },
                repaired: r =>
                {
                    Expect.True(Ratio(r, x => x.EnemyCasualties, "enemy casualties") > 1.01f, "the enemy's casualties are amplified");
                    Expect.True(Ratio(r, x => x.EnemyMorale, "enemy morale hit") > 1.01f, "the enemy's morale hit is amplified");
                    Expect.Near(1f, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties, Partisan III over none", 0.01f);
                    Expect.Near(1f, Ratio(r, x => x.OwnMorale, "own morale hit"), "the raider's own morale hit, Partisan III over none", 0.01f);
                });
        }

        [CampaignTest]
        public IEnumerator BushwhackersHurtTheEnemyNotTheRaider()
        {
            yield return Live.NextCampaignPass();
            (Regiment raider, Regiment guard) = Contact();
            yield return Verify.FixLive<(Round without, Round with)>(
                "GTCW-039",
                box =>
                {
                    box.Value = (Raid(raider, guard, -1), Raid(raider, guard, Bushwhackers));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.Near(1f, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Bushwhackers III over none (unfixed: no effect)", 0.01f);
                    Expect.True(Ratio(r, x => x.OwnCasualties, "own casualties") > 1.01f, "the raider's own casualties are amplified (unfixed)");
                },
                repaired: r =>
                {
                    Expect.True(Ratio(r, x => x.EnemyCasualties, "enemy casualties") > 1.01f, "the enemy's casualties are amplified");
                    Expect.Near(1f, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties, Bushwhackers III over none", 0.01f);
                });
        }

        private static float Ratio((Round without, Round with) r, System.Func<Round, float> of, string what)
        {
            Expect.NonZero(of(r.without), what + " without the perk");
            return of(r.with) / of(r.without);
        }

        /// <summary>
        /// A Union formation that can raid, placed next to a Confederate
        /// formation that can be raided, with the sides at war. All undone after
        /// the test.
        /// </summary>
        private static (Regiment raider, Regiment guard) Contact()
        {
            Units.AtWar(0, 1);
            Regiment guard = Units.Formation(1, f =>
                f.gameObject.activeInHierarchy && !f.inbattle && !f.onretreat && f.garrisonreference == null &&
                Units.Brigades(f).Any(b => b != null && b.unittyp <= 2 && b.strength > 500));
            Regiment raider = Units.Formation(0, f =>
                f.gameObject.activeInHierarchy && !f.inbattle && !f.onretreat && f.groupraidingefficiency > 0f &&
                Units.Brigades(f).Any(b => b != null && (b.unittyp == 0 || b.unittyp == 1) && b.strength > 500));
            Units.MoveTo(raider, guard.transform.position + new Vector3(1f, 0f, 1f));
            Report.Note($"raider '{raider.name}' (raiding efficiency {raider.groupraidingefficiency:0}) beside '{guard.name}'");

            var probe = new Harmony("gtcw.tests.raidprobe");
            probe.Patch(AccessTools.Method(typeof(Regiment), "SufferCasualties"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RaidingPerkDirectionTests), nameof(RecordCasualties))) { priority = Priority.Last });
            probe.Patch(AccessTools.Method(typeof(Autocalc), "SufferMoraleHit"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RaidingPerkDirectionTests), nameof(RecordMoraleHit))) { priority = Priority.Last });
            Units.Keep(() => 0, _ => probe.UnpatchSelf());
            return (raider, guard);
        }

        /// <summary>One raid round by <paramref name="raider"/> holding only <paramref name="perk"/> at level III.</summary>
        private static Round Raid(Regiment raider, Regiment guard, int perk)
        {
            List<Regiment.Perk> perks = raider.perks;
            List<GameObject> inRange = raider.unitrange.enemyinrange;
            int orders = raider.cavalryorders, ordered = raider.cavalryordersordered;
            Autocalc raid = raider.gameObject.AddComponent<Autocalc>();
            Casualties.Clear();
            MoraleHits.Clear();
            try
            {
                raider.perks = perk < 0
                    ? new List<Regiment.Perk>()
                    : new List<Regiment.Perk> { new Regiment.Perk { perk = perk, perklevel = LevelIII } };
                raider.unitrange.enemyinrange = new List<GameObject> { guard.gameObject };

                raid.StartRaiding(raider);
                Traverse.Create(raid).Field("lastcycleupdate").SetValue(GameVars.currenttimefromstart - 2f * GamePrefs.cycleupdateforautocalcs);
                UnityEngine.Random.InitState(Seed);
                _recording = true;
                Traverse.Create(raid).Method("UpdateRaiding", new object[] { true }).GetValue();
            }
            finally
            {
                _recording = false;
                raider.perks = perks;
                raider.unitrange.enemyinrange = inRange;
                raider.cavalryorders = orders;
                raider.cavalryordersordered = ordered;
                if (raid != null)
                {
                    Object.Destroy(raid);
                }
            }

            return new Round
            {
                EnemyCasualties = Casualties.Where(c => c.alliance != raider.alliance).Sum(c => c.amount),
                OwnCasualties = Casualties.Where(c => c.alliance == raider.alliance).Sum(c => c.amount),
                EnemyMorale = MoraleHits.Where(c => c.alliance != raider.alliance).Sum(c => Mathf.Abs(c.amount)),
                OwnMorale = MoraleHits.Where(c => c.alliance == raider.alliance).Sum(c => Mathf.Abs(c.amount)),
            };
        }

        private static bool RecordCasualties(Regiment __instance, float amount, ref int __result)
        {
            if (!_recording)
            {
                return true;
            }

            Casualties.Add((__instance.alliance, amount));
            __result = (int)amount;
            return false;
        }

        private static bool RecordMoraleHit(Regiment unit1, float moraleeffect)
        {
            if (!_recording)
            {
                return true;
            }

            MoraleHits.Add((unit1 == null ? -1 : unit1.alliance, moraleeffect));
            return false;
        }
    }
}
