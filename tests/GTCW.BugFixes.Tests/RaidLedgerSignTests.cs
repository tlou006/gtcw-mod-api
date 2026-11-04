using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;
using HarmonyLib;
using UnityEngine;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-014 — <c>Regiment.UpdateRaids</c> books raid losses into the
    /// trade-war ledger's category 2 with a positive sign, where the category's
    /// convention (and its other caller) is negative.
    /// </summary>
    [FixTests("GTCW-014")]
    [Campaign("001", "A")]
    public sealed class RaidLedgerSignTests
    {
        private const int RaidCategory = 2;

        [EngineTest]
        public void APositiveRaidChargeIsBookedAsALoss() =>
            Verify.Fix(
                "GTCW-014",
                () => Book(10f),
                defect: delta => Expect.Near(10f, delta, "category 2 after a positive charge"),
                repaired: delta => Expect.Near(-10f, delta, "category 2 after a positive charge"));

        /// <summary>
        /// The precondition the fix rests on: the correctly signed caller
        /// (<c>CBuilding</c>) passes a negative value, which must come through
        /// unchanged.
        /// </summary>
        [EngineTest]
        public void TheCorrectlySignedCallerIsUntouched()
        {
            Toggle.Only("GTCW-014");
            Expect.Near(-10f, Book(-10f), "category 2 after a negative charge, fix on");
        }

        /// <summary>The ledger's own total, slot 0, must still be the sum of the others.</summary>
        [EngineTest]
        public void TheTotalStillAddsUp()
        {
            Toggle.Only("GTCW-014");
            Book(10f);
            float[] ledger = GameVars.alliance[0].tradewarfare;
            float sum = 0f;
            for (int i = 1; i < ledger.Length; i++)
            {
                sum += ledger[i];
            }

            Expect.Near(sum, ledger[0], "slot 0 against the sum of slots 1..n", 1e-2f);
        }

        /// <summary>
        /// The defective caller itself: a live Union formation on raid orders
        /// next to a Confederate trade node, raiding through the game's own
        /// <c>Regiment.UpdateRaids</c>. IMPLEMENTATION.md's repro: the victim's
        /// "losses through raids" figure falls instead of rising.
        /// </summary>
        [CampaignTest]
        public IEnumerator ARealRaidBooksTheVictimsLossAsALoss()
        {
            yield return Live.NextCampaignPass();

            // 001/A opens in February 1861, before Fort Sumter: the two sides
            // are not yet at war, and a raid needs an enemy. Undone afterwards.
            Units.AtWar(0, 1);

            IIP target = Units.RichestNode(1);
            Regiment raider = Units.Formation(0);
            Report.Note($"raider '{raider.name}' on trade node at {target.transform.position}");

            yield return Verify.FixLive<float>(
                "GTCW-014",
                delta => Raid(raider, target, delta),
                defect: d => Expect.True(d > 0f, $"the victim's category 2 rose by {d} instead of falling"),
                repaired: d => Expect.True(d < 0f, $"the victim's category 2 should fall; it moved by {d}"));
        }

        private static IEnumerator Raid(Regiment raider, IIP target, Box<float> delta)
        {
            Traverse unit = Traverse.Create(raider);
            Vector3 home = raider.transform.position;
            int orders = raider.cavalryorders;
            float bugle = raider.buglerange;
            object nodes = unit.Field("iipsinrange").GetValue();
            object buildings = unit.Field("buildingsinrange").GetValue();
            try
            {
                raider.cavalryorders = 2;
                raider.transform.position = target.transform.position;
                unit.Field("lastpositionupdateframe").SetValue(-1);
                unit.Field("iipsinrange").SetValue(new List<IIP> { target });
                unit.Field("buildingsinrange").SetValue(new List<CBuilding>());
                unit.Field("lastraidupdate").SetValue(Clock.Now - 0.01f);
                raider.buglerange = Math.Max(bugle, 50f);

                float[] ledger = GameVars.alliance[target.allianceowner].tradewarfare;
                float before = ledger[RaidCategory];
                unit.Method("UpdateRaids").GetValue();
                delta.Value = ledger[RaidCategory] - before;
            }
            finally
            {
                raider.transform.position = home;
                raider.cavalryorders = orders;
                raider.buglerange = bugle;
                unit.Field("iipsinrange").SetValue(nodes);
                unit.Field("buildingsinrange").SetValue(buildings);
                unit.Field("lastpositionupdateframe").SetValue(-1);
            }

            yield return null;
        }

        private static float Book(float value)
        {
            GameVars.Alliance union = GameVars.alliance[0];
            float before = union.tradewarfare[RaidCategory];
            union.AddTradeWarfare(RaidCategory, value);
            return union.tradewarfare[RaidCategory] - before;
        }
    }
}
