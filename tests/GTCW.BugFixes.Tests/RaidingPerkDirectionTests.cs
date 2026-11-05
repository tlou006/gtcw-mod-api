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
    /// makes — after the fix's own prefixes have adjusted them — and skips
    /// applying them, so no unit is changed and every measurement starts from
    /// the same state. The supply each hit unit loses is measured against a
    /// stand-in stock of 1 per slot and the real stock put back. Unity's
    /// generator is reseeded before each round.
    /// </para>
    /// <para>
    /// The assertions are on exact ratios, with-perk over without: the reseed
    /// gives both rounds the same draws, and a perk does not change how many
    /// draws a round makes. The fix must apply each factor where the defective
    /// block applies it — the casualty factor to the casualties, the morale
    /// factor to the morale hit, neither to the supply loss. Both blocks
    /// compute <c>num3 *= Random.Range(num3 * f, num3 * (1 + f))</c>, which
    /// squares the casualty term (GTCW-061), so a factor applied any earlier
    /// than the casualties themselves comes out squared. The first version of
    /// the fix did that: the guards took ×4 casualties, a ×6 morale hit and ×4
    /// supply loss under Partisan III.
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

        /// <summary>Ratios are exact; this only absorbs float rounding.</summary>
        private const float Exact = 0.001f;

        private static bool _recording;
        private static readonly List<(int alliance, float amount)> Casualties = new List<(int, float)>();
        private static readonly List<(int alliance, float amount)> MoraleHits = new List<(int, float)>();
        private static readonly Dictionary<Regiment, float[]> Stocks = new Dictionary<Regiment, float[]>();

        private static float PartisanCasualty => GamePrefs.skirmishingpartisanbrigades[LevelIII];

        private static float PartisanMorale => GamePrefs.raidingmoralepartisanbrigades[LevelIII];

        private static float BushwhackerCasualty => GamePrefs.skirmishingbushwhackers[LevelIII];

        public sealed class Round
        {
            public float EnemyCasualties { get; set; }

            public float OwnCasualties { get; set; }

            public float EnemyMorale { get; set; }

            public float OwnMorale { get; set; }

            public float EnemySupply { get; set; }

            public override string ToString() =>
                $"enemy {EnemyCasualties:G4} men / {EnemyMorale:G4} morale / {EnemySupply:G4} supply, own {OwnCasualties:G4} / {OwnMorale:G4}";
        }

        [CampaignTest]
        public IEnumerator PartisanBrigadesHurtTheEnemyNotTheRaider()
        {
            yield return Live.NextCampaignPass();
            (Regiment raider, Regiment guard) = Contact();
            Expect.True(PartisanCasualty != 1f && PartisanMorale != 1f, $"Partisan Brigades III has factors to move ({PartisanCasualty}, {PartisanMorale})");
            yield return Verify.FixLive<(Round without, Round with)>(
                "GTCW-038",
                box =>
                {
                    box.Value = (Raid(raider, guard), Raid(raider, guard, PartisanBrigades));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.Near(1f, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Partisan III over none (unfixed: no effect)", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemyMorale, "enemy morale hit"), "enemy morale hit, Partisan III over none (unfixed: no effect)", Exact);
                    Expect.Near(PartisanCasualty, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties (unfixed: amplified)", Exact);
                    Expect.Near(PartisanMorale, Ratio(r, x => x.OwnMorale, "own morale hit"), "the raider's own morale hit (unfixed: amplified)", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemySupply, "enemy supply loss"), "enemy supply loss, Partisan III over none", Exact);
                },
                repaired: r =>
                {
                    Expect.Near(PartisanCasualty, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Partisan III over none", Exact);
                    Expect.Near(PartisanMorale, Ratio(r, x => x.EnemyMorale, "enemy morale hit"), "enemy morale hit, Partisan III over none", Exact);
                    Expect.Near(1f, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties, Partisan III over none", Exact);
                    Expect.Near(1f, Ratio(r, x => x.OwnMorale, "own morale hit"), "the raider's own morale hit, Partisan III over none", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemySupply, "enemy supply loss"), "enemy supply loss, Partisan III over none (no factor of its own)", Exact);
                });
        }

        [CampaignTest]
        public IEnumerator BushwhackersHurtTheEnemyNotTheRaider()
        {
            yield return Live.NextCampaignPass();
            (Regiment raider, Regiment guard) = Contact();
            Expect.True(BushwhackerCasualty != 1f, $"Bushwhackers III has a factor to move ({BushwhackerCasualty})");
            yield return Verify.FixLive<(Round without, Round with)>(
                "GTCW-039",
                box =>
                {
                    box.Value = (Raid(raider, guard), Raid(raider, guard, Bushwhackers));
                    return Live.Frames(1);
                },
                defect: r =>
                {
                    Expect.Near(1f, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Bushwhackers III over none (unfixed: no effect)", Exact);
                    Expect.Near(BushwhackerCasualty, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties (unfixed: amplified)", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemySupply, "enemy supply loss"), "enemy supply loss, Bushwhackers III over none", Exact);
                },
                repaired: r =>
                {
                    Expect.Near(BushwhackerCasualty, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), "enemy casualties, Bushwhackers III over none", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemyMorale, "enemy morale hit"), "enemy morale hit, Bushwhackers III over none (it has no morale factor)", Exact);
                    Expect.Near(1f, Ratio(r, x => x.OwnCasualties, "own casualties"), "the raider's own casualties, Bushwhackers III over none", Exact);
                    Expect.Near(1f, Ratio(r, x => x.EnemySupply, "enemy supply loss"), "enemy supply loss, Bushwhackers III over none", Exact);
                });
        }

        /// <summary>
        /// Not an off/on/off check: a raider holding both perks, with both
        /// fixes off and then both on. The two casualty factors multiply, on
        /// whichever side they land.
        /// </summary>
        [CampaignTest]
        public IEnumerator BothPerksMultiply()
        {
            yield return Live.NextCampaignPass();
            (Regiment raider, Regiment guard) = Contact();
            float both = PartisanCasualty * BushwhackerCasualty;
            foreach (bool fixOn in new[] { false, true })
            {
                if (fixOn)
                {
                    Toggle.Only("GTCW-038", "GTCW-039");
                }
                else
                {
                    Toggle.AllOff();
                }

                string fix = fixOn ? "on" : "off";
                (Round without, Round with) r = (Raid(raider, guard), Raid(raider, guard, PartisanBrigades, Bushwhackers));
                Expect.Near(fixOn ? both : 1f, Ratio(r, x => x.EnemyCasualties, "enemy casualties"), $"enemy casualties, both perks III over none, fixes {fix}", Exact);
                Expect.Near(fixOn ? PartisanMorale : 1f, Ratio(r, x => x.EnemyMorale, "enemy morale hit"), $"enemy morale hit, both perks III over none, fixes {fix}", Exact);
                Expect.Near(fixOn ? 1f : both, Ratio(r, x => x.OwnCasualties, "own casualties"), $"the raider's own casualties, both perks III over none, fixes {fix}", Exact);
                Expect.Near(fixOn ? 1f : PartisanMorale, Ratio(r, x => x.OwnMorale, "own morale hit"), $"the raider's own morale hit, both perks III over none, fixes {fix}", Exact);
                Expect.Near(1f, Ratio(r, x => x.EnemySupply, "enemy supply loss"), $"enemy supply loss, both perks III over none, fixes {fix}", Exact);
                Report.Note($"fixes {fix}: none ({r.without}); both ({r.with})");
            }

            Toggle.AllOff();
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

        /// <summary>One raid round by <paramref name="raider"/> holding exactly <paramref name="perks"/>, each at level III.</summary>
        private static Round Raid(Regiment raider, Regiment guard, params int[] perks)
        {
            List<Regiment.Perk> kept = raider.perks;
            List<GameObject> inRange = raider.unitrange.enemyinrange;
            int orders = raider.cavalryorders, ordered = raider.cavalryordersordered;
            Autocalc raid = raider.gameObject.AddComponent<Autocalc>();
            Casualties.Clear();
            MoraleHits.Clear();
            Stocks.Clear();
            var round = new Round();
            try
            {
                raider.perks = perks.Select(p => new Regiment.Perk { perk = p, perklevel = LevelIII }).ToList();
                raider.unitrange.enemyinrange = new List<GameObject> { guard.gameObject };

                raid.StartRaiding(raider);
                Traverse.Create(raid).Field("lastcycleupdate").SetValue(GameVars.currenttimefromstart - 2f * GamePrefs.cycleupdateforautocalcs);
                UnityEngine.Random.InitState(Seed);
                _recording = true;
                Traverse.Create(raid).Method("UpdateRaiding", new object[] { true }).GetValue();
                _recording = false;

                round.EnemySupply = Stocks
                    .Where(s => s.Key.alliance != raider.alliance)
                    .Sum(s => s.Key.supplyinstock.Sum(left => 1f - left));
            }
            finally
            {
                _recording = false;
                foreach (KeyValuePair<Regiment, float[]> stock in Stocks)
                {
                    stock.Key.supplyinstock = stock.Value;
                }

                Stocks.Clear();
                raider.perks = kept;
                raider.unitrange.enemyinrange = inRange;
                raider.cavalryorders = orders;
                raider.cavalryordersordered = ordered;
                if (raid != null)
                {
                    Object.Destroy(raid);
                }
            }

            round.EnemyCasualties = Casualties.Where(c => c.alliance != raider.alliance).Sum(c => c.amount);
            round.OwnCasualties = Casualties.Where(c => c.alliance == raider.alliance).Sum(c => c.amount);
            round.EnemyMorale = MoraleHits.Where(c => c.alliance != raider.alliance).Sum(c => Mathf.Abs(c.amount));
            round.OwnMorale = MoraleHits.Where(c => c.alliance == raider.alliance).Sum(c => Mathf.Abs(c.amount));
            return round;
        }

        private static bool RecordCasualties(Regiment __instance, float amount, ref int __result)
        {
            if (!_recording)
            {
                return true;
            }

            Casualties.Add((__instance.alliance, amount));

            // The round takes supply from the unit it has just hit. A stand-in
            // stock of 1 per slot keeps the loss well clear of the floor at 0
            // and of float rounding against a large stock.
            if (__instance.supplyinstock != null && !Stocks.ContainsKey(__instance))
            {
                Stocks[__instance] = __instance.supplyinstock;
                __instance.supplyinstock = Enumerable.Repeat(1f, __instance.supplyinstock.Length).ToArray();
            }

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
