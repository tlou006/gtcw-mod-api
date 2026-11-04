using System;
using System.Collections;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;
using UnityEngine;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-041 — in <c>BattleUnits.CalculateCommanderFame</c>,
    /// <c>CheckForGroupPerk</c> starts its walk at the unit's parent, so a
    /// holder of Embedded Reporters that sits under a formation without the
    /// perk never benefits itself; and the factor is indexed by the
    /// <em>asking unit's</em> level, so a Corps under a holder gets the level I
    /// factor whatever level the holder has reached. The fix's rule: the
    /// nearest holder, counting the unit itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The multiplier only applies to formations of type 15 and up, so these
    /// tests need Corps under Armies: scenario 001/E (spring 1864) has 62 Union
    /// Corps and 6 Armies; 001/A has no Corps under anything.
    /// </para>
    /// <para>
    /// <b>A finding the harness made.</b> At the top of the hierarchy
    /// <c>GrabNextParentUnit</c> returns the unit itself, so the walk's first
    /// step does test a top-level holder. The "holder is skipped" defect
    /// therefore exists only for holders that have a parent formation, and
    /// IMPLEMENTATION.md's first repro — "an army holding Embedded Reporters
    /// III … with no parent above it" — did not reproduce it.
    /// <see cref="ATopLevelHolderWasNeverSkipped"/> records that.
    /// </para>
    /// <para>
    /// Each measurement runs the game's own fame calculation over one live
    /// formation, as at the end of a battle, and reads its commander's fame
    /// change; commander and units are restored field by field afterwards.
    /// </para>
    /// </remarks>
    [FixTests("GTCW-041")]
    [Campaign("001", "E")]
    public sealed class EmbeddedReportersTests
    {
        private const int EmbeddedReporters = 14;
        private const int None = -1;
        private const int LevelOne = 0;
        private const int LevelIII = 2;

        private static float FactorI => GamePrefs.fameadjustmentembeddedreporters[LevelOne];

        private static float FactorIII => GamePrefs.fameadjustmentembeddedreporters[LevelIII];

        [CampaignTest]
        public IEnumerator AHolderUnderAnotherFormationGetsItsOwnReporters()
        {
            yield return Live.NextCampaignPass();
            (Regiment corps, Regiment army) = CorpsUnderAnArmy();
            Expect.True(FactorIII != 1f, $"Embedded Reporters III changes fame at all (factor {FactorIII})");

            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-041",
                box =>
                {
                    box.Value = (Fame(corps, (corps, None), (army, None)), Fame(corps, (corps, LevelIII), (army, None)));
                    return Live.Frames(1);
                },
                defect: r => { Expect.NonZero(r.without, "fame gain without the perk"); Expect.Near(r.without, r.with, "a Corps holder's own fame gain (unfixed its own perk is skipped)", Slack(r.without)); },
                repaired: r => { Expect.NonZero(r.without, "fame gain without the perk"); Expect.Near(r.without * FactorIII, r.with, $"a Corps holder's own fame gain (factor {FactorIII})", Slack(r.without)); });
        }

        [CampaignTest]
        public IEnumerator ACorpsUnderAHolderGetsTheHoldersLevel()
        {
            yield return Live.NextCampaignPass();
            (Regiment corps, Regiment army) = CorpsUnderAnArmy();
            Expect.True(FactorI != FactorIII, $"levels I and III differ ({FactorI} vs {FactorIII})");

            yield return Verify.FixLive<(float without, float with)>(
                "GTCW-041",
                box =>
                {
                    box.Value = (Fame(corps, (corps, None), (army, None)), Fame(corps, (corps, None), (army, LevelIII)));
                    return Live.Frames(1);
                },
                defect: r => { Expect.NonZero(r.without, "fame gain without the perk"); Expect.Near(r.without * FactorI, r.with, $"a Corps' fame gain under a level III holder (unfixed: the level I factor {FactorI})", Slack(r.without)); },
                repaired: r => { Expect.NonZero(r.without, "fame gain without the perk"); Expect.Near(r.without * FactorIII, r.with, $"a Corps' fame gain under a level III holder (factor {FactorIII})", Slack(r.without)); });
        }

        /// <summary>
        /// Not an off/on/off check: there is no defect to repair here. A
        /// top-level holder gets its factor with the fix off and with it on.
        /// </summary>
        [CampaignTest]
        public IEnumerator ATopLevelHolderWasNeverSkipped()
        {
            yield return Live.NextCampaignPass();
            Regiment army = Units.Formation(0, f => f.unittyp == 16 && Parent(f) == null);
            foreach (bool fixOn in new[] { false, true })
            {
                if (fixOn)
                {
                    Toggle.Only("GTCW-041");
                }
                else
                {
                    Toggle.AllOff();
                }

                float without = Fame(army, (army, None));
                float with = Fame(army, (army, LevelIII));
                Expect.NonZero(without, "fame gain without the perk");
                Expect.Near(without * FactorIII, with, $"a top-level Army holder's fame gain, fix {(fixOn ? "on" : "off")}", Slack(without));
                Report.Note($"top-level '{army.name}', fix {(fixOn ? "on" : "off")}: {without:R} -> {with:R}");
            }

            Toggle.AllOff();
        }

        /// <summary>
        /// Not an off/on/off check either: a unit that holds the perk itself
        /// is its own nearest holder, so it gets its own level whatever a
        /// formation above it holds. Unfixed, the game happens to do that (the
        /// walk finds the parent, and the factor is indexed by the unit's own
        /// level); the fix must not take it away. The first version of the fix
        /// did — it answered with the parent's level — and this test was red
        /// against it.
        /// </summary>
        [CampaignTest]
        public IEnumerator AHolderUnderAnotherHolderKeepsItsOwnLevel()
        {
            yield return Live.NextCampaignPass();
            (Regiment corps, Regiment army) = CorpsUnderAnArmy();
            Expect.True(FactorI != FactorIII, $"levels I and III differ ({FactorI} vs {FactorIII})");
            foreach (bool fixOn in new[] { false, true })
            {
                if (fixOn)
                {
                    Toggle.Only("GTCW-041");
                }
                else
                {
                    Toggle.AllOff();
                }

                string fix = fixOn ? "on" : "off";
                float without = Fame(corps, (corps, None), (army, None));
                Expect.NonZero(without, "fame gain without the perk");

                float higherUnderLower = Fame(corps, (corps, LevelIII), (army, LevelOne));
                Expect.Near(without * FactorIII, higherUnderLower, $"a level III Corps under a level I Army, fix {fix}", Slack(without));

                float lowerUnderHigher = Fame(corps, (corps, LevelOne), (army, LevelIII));
                Expect.Near(without * FactorI, lowerUnderHigher, $"a level I Corps under a level III Army, fix {fix}", Slack(without));

                Report.Note($"fix {fix}: none {without:R}; III under I {higherUnderLower:R}; I under III {lowerUnderHigher:R}");
            }

            Toggle.AllOff();
        }

        private static float Slack(float value) => Math.Abs(value) * 0.01f + 1e-6f;

        /// <summary>
        /// The fame <paramref name="unit"/>'s commander gains from one run of
        /// the game's fame calculation, with each listed formation holding
        /// Embedded Reporters at a level (0 = I) or, with <c>None</c>, not at all.
        /// </summary>
        private static float Fame(Regiment unit, params (Regiment formation, int level)[] perks)
        {
            GameVars.Commander commander = GameVars.commander[unit.commander];
            Units.KeepFields(commander);
            Units.KeepFields(unit);
            foreach ((Regiment formation, int level) in perks)
            {
                Units.SetPerks(formation, level == None ? (-1, 0) : (EmbeddedReporters, level));
            }

            try
            {
                // Neutral inputs: no experience gained this battle, no routs;
                // raw fame is then set by the casualty ratio and the level adjustment.
                commander.expbeforebattle = commander.exp;
                unit.numberofrouts = 0;
                commander.fame = 0f;

                var sides = new[] { new BattleUnits.AllUnitsOfAlliance(), new BattleUnits.AllUnitsOfAlliance() };
                sides[unit.alliance == 0 ? 0 : 1].unitsobjectrefreg.Add(unit);
                Clock.Units!.CalculateCommanderFame(0, sides);
                return commander.fame;
            }
            finally
            {
                Units.Restore();
            }
        }

        /// <summary>A Union Corps directly under an Army.</summary>
        private static (Regiment corps, Regiment army) CorpsUnderAnArmy()
        {
            foreach (Regiment unit in BattleUnits.completeunitlist.Where(u => u != null && u.alliance == 0 && u.unittyp == 15 && u.gameObject.activeInHierarchy))
            {
                Regiment? parent = Parent(unit);
                if (parent != null && parent.unittyp == 16)
                {
                    Report.Note($"corps '{unit.name}' under '{parent.name}'");
                    return (unit, parent);
                }
            }

            throw new ExpectationFailedException("no active Union Corps sits directly under an Army in this scenario");
        }

        /// <summary>The formation over a unit; null at the top, where the game's walk returns the unit itself.</summary>
        private static Regiment? Parent(Regiment unit)
        {
            GameObject? parent = BattleUnits.GrabNextParentUnit(unit.gameObject);
            Regiment? regiment = parent == null ? null : parent.GetComponent<Regiment>();
            return regiment != null && !ReferenceEquals(regiment, unit) ? regiment : null;
        }
    }
}
