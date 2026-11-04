using System;
using System.Collections;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-035 and GTCW-036 — <c>Regiment.UpdateCampaignUnitStats</c> runs on
    /// brigades, so its <c>HasPerk(6)</c> and <c>HasPerk(8)</c> read the
    /// brigade's <em>battle</em> perks (Ace of Spade, Expert scouts) instead of
    /// the formation's army perks (Foot Cavalry, Ambulance Corps).
    /// </summary>
    /// <remarks>
    /// Each measurement is one real call over a quarter of a game year with
    /// Unity's random generator reseeded first, so the with-perk and
    /// without-perk calls draw identical randomness and differ only by the
    /// perk. The other attrition term is zeroed so the alliance's
    /// <c>attrition_tmp</c>, which only these two terms write, counts exactly
    /// the one being measured.
    /// </remarks>
    [FixTests("GTCW-035", "GTCW-036")]
    // 001/E (spring 1864): in 001/A the only active Union formations are fort garrisons
    // with artillery; the field armies are not yet on the map.
    [Campaign("001", "E")]
    public sealed class AttritionPerkNamespaceTests
    {
        private const int AceOfSpadeOrFootCavalry = 6;
        private const int ExpertScoutsOrAmbulanceCorps = 8;
        private const int LevelIII = 2;
        private const int Seed = 1861;

        [CampaignTest]
        public IEnumerator AmbulanceCorpsCutsTheFormationsWoundedDeaths()
        {
            yield return Live.NextCampaignPass();
            (Regiment army, Regiment brigade) = Pick();
            float factor = GamePrefs.woundedtokilledambulancecorps[LevelIII];
            yield return Verify.FixLive<(int without, int with)>(
                "GTCW-036",
                box =>
                {
                    box.Value = (WoundedDeaths(army, brigade, armyPerk: -1, brigadePerk: -1),
                                 WoundedDeaths(army, brigade, armyPerk: ExpertScoutsOrAmbulanceCorps, brigadePerk: -1));
                    return Live.Frames(1);
                },
                defect: r => { Measurable(r.without, "wounded deaths without the perk"); Expect.Equal(r.without, r.with, "wounded deaths with the army's Ambulance Corps III against without (unfixed it does nothing)"); },
                repaired: r => { Measurable(r.without, "wounded deaths without the perk"); Expect.Near(r.without * factor, r.with, $"wounded deaths with Ambulance Corps III (factor {factor})", Slack(r.without)); });
        }

        [CampaignTest]
        public IEnumerator ABrigadesExpertScoutsNoLongerCutWoundedDeaths()
        {
            yield return Live.NextCampaignPass();
            (Regiment army, Regiment brigade) = Pick();
            float factor = GamePrefs.woundedtokilledambulancecorps[LevelIII];
            yield return Verify.FixLive<(int without, int with)>(
                "GTCW-036",
                box =>
                {
                    box.Value = (WoundedDeaths(army, brigade, armyPerk: -1, brigadePerk: -1),
                                 WoundedDeaths(army, brigade, armyPerk: -1, brigadePerk: ExpertScoutsOrAmbulanceCorps));
                    return Live.Frames(1);
                },
                defect: r => { Measurable(r.without, "wounded deaths without the perk"); Expect.Near(r.without * factor, r.with, "wounded deaths of a brigade holding Expert scouts III (unfixed it gets the army perk's cut)", Slack(r.without)); },
                repaired: r => { Measurable(r.without, "wounded deaths without the perk"); Expect.Equal(r.without, r.with, "wounded deaths of a brigade holding Expert scouts III"); });
        }

        /// <summary>
        /// The repro needs a brigade on the double-quick. Whether an attached
        /// brigade's flag is ever set on the campaign map is the open question
        /// IMPLEMENTATION.md carries; this test sets it, so it proves the fix's
        /// arithmetic, not that the branch is reachable in play.
        /// </summary>
        [CampaignTest]
        public IEnumerator FootCavalryCutsTheFormationsForcedMarchSickness()
        {
            yield return Live.NextCampaignPass();
            (Regiment army, Regiment brigade) = Pick();
            float factor = GamePrefs.attritionfootcavalry[LevelIII];
            yield return Verify.FixLive<(int without, int with)>(
                "GTCW-035",
                box =>
                {
                    box.Value = (NewlySick(army, brigade, armyPerk: -1),
                                 NewlySick(army, brigade, armyPerk: AceOfSpadeOrFootCavalry));
                    return Live.Frames(1);
                },
                defect: r => { Measurable(r.without, "forced-march sickness without the perk"); Expect.Equal(r.without, r.with, "forced-march sickness with the army's Foot Cavalry III against without (unfixed it does nothing)"); },
                repaired: r =>
                {
                    Measurable(r.without, "forced-march sickness without the perk");
                    Expect.True(r.with < r.without, $"Foot Cavalry III (factor {factor}) should cut forced-march sickness: {r.with} vs {r.without}");
                });
        }

        /// <summary>
        /// A Union formation on the map and out of battle, neither inheriting
        /// perk 6 or 8, with an infantry brigade the method will process. The
        /// method returns at once when the formation is not active in the scene:
        /// in 001/A that is all nine "Department"/"Army" formations, and the
        /// first version of these tests picked one and measured zero. The
        /// divisions are active; their 1861 brigades are a few hundred strong.
        /// </summary>
        private static (Regiment army, Regiment brigade) Pick()
        {
            Regiment army = Units.Formation(0, f =>
                f.gameObject.activeInHierarchy && !f.inbattle &&
                !f.HasPerkParent(ExpertScoutsOrAmbulanceCorps) && !f.HasPerkParent(AceOfSpadeOrFootCavalry) &&
                Units.Brigades(f).Any(Processable));
            Regiment brigade = Units.Brigades(army).First(Processable);
            Report.Note($"army '{army.name}' (type {army.unittyp}), brigade '{brigade.name}' ({brigade.strength} men)");
            return (army, brigade);
        }

        private static bool Processable(Regiment b) =>
            b != null && b.unittyp == 0 && b.strength > 300 && b.losses != null &&
            HarmonyLib.Traverse.Create(b).Field("weather").GetValue() != null;

        /// <summary>A measurement of zero would let both the defect and the repair "pass".</summary>
        private static void Measurable(int value, string what) =>
            Expect.True(value > 0, $"{what} must be measurable for the comparison to mean anything; it was {value}");

        private static float Slack(int expected) => Math.Max(2f, expected * 0.03f);

        private static int WoundedDeaths(Regiment army, Regiment brigade, int armyPerk, int brigadePerk) =>
            Measure(army, brigade, armyPerk, brigadePerk, () =>
            {
                brigade.losses[0] = 5000;
                brigade.sick = 0;
                GamePrefs.sicknesstargetrate = 0f;
            });

        private static int NewlySick(Regiment army, Regiment brigade, int armyPerk) =>
            Measure(army, brigade, armyPerk, brigadePerk: -1, () =>
            {
                brigade.losses[0] = 0;
                brigade.sick = 0;
                brigade.doublequick = true;
            });

        /// <summary>
        /// One real <c>UpdateCampaignUnitStats</c> call, counted through
        /// <c>attrition_tmp</c>, with every touched value put back afterwards.
        /// </summary>
        private static int Measure(Regiment army, Regiment brigade, int armyPerk, int brigadePerk, Action arrange)
        {
            GameVars.Alliance side = GameVars.alliance[brigade.alliance];
            int[] losses = (int[])brigade.losses.Clone();
            int strength = brigade.strength, sick = brigade.sick;
            bool doublequick = brigade.doublequick;
            float last = brigade.lastcampaignstatsupdate;
            float rate = GamePrefs.sicknesstargetrate;
            var attrition = side.attrition_tmp;
            var returned = side.returnedtoduty_tmp;

            Units.SetPerks(army, (armyPerk, LevelIII));
            Units.SetPerks(brigade, (brigadePerk, LevelIII));
            try
            {
                arrange();
                brigade.lastcampaignstatsupdate = Clock.Now - 0.25f;
                UnityEngine.Random.InitState(Seed);
                var before = side.attrition_tmp;
                brigade.UpdateCampaignUnitStats(army, -1);
                return (int)Math.Round((double)(side.attrition_tmp - before));
            }
            finally
            {
                brigade.losses = losses;
                brigade.strength = strength;
                brigade.sick = sick;
                brigade.doublequick = doublequick;
                brigade.lastcampaignstatsupdate = last;
                GamePrefs.sicknesstargetrate = rate;
                side.attrition_tmp = attrition;
                side.returnedtoduty_tmp = returned;
                Units.Restore();
            }
        }
    }
}
