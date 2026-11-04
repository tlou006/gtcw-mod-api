using System.Collections;
using System.Linq;
using GTCW.Harness;
using GTCW.Harness.Game;
using GTCW.Harness.GameState;
using HarmonyLib;
using UnityEngine;

namespace GTCW.BugFixes.Tests
{
    /// <summary>
    /// GTCW-003 — <c>Regiment.UpdateIIPSupplyLink</c> multiplies a resupply
    /// <em>time</em> by the Confiscation Act's <c>supplyenemysoilfactor</c>, so
    /// the act makes foraging on enemy soil slower where the manual, the
    /// appendix and the config comment all say faster.
    /// </summary>
    [FixTests("GTCW-003")]
    [Campaign("001", "A")]
    public sealed class ConfiscationActSupplyTests
    {
        /// <summary>
        /// One Union army on Confederate soil resupplying for one game day,
        /// with and without the act's factor: the ratio of the two is the act's
        /// effect, with everything else about the army held fixed.
        /// </summary>
        [CampaignTest]
        public IEnumerator TheActSpeedsForagingOnEnemySoil()
        {
            yield return Live.NextCampaignPass();
            Units.AtWar(0, 1);

            float act = GamePrefs.confiscationsupplyflow;
            Expect.True(act > 0f && act != 1f, $"confiscationsupplyflow is a real factor (it is {act})");

            Regiment army = Units.Formation(0, f => Units.Brigades(f).Any(b => b != null && b.unittyp == 0 && b.strength > 500));
            IIP enemySoil = Units.RichestNode(1);
            Units.MoveTo(army, enemySoil.transform.position + new Vector3(1f, 0f, 1f));
            Report.Note($"'{army.name}' on Confederate soil near {enemySoil.transform.position}; act factor {act}");

            yield return Verify.FixLive<float>(
                "GTCW-003",
                ratio =>
                {
                    float without = Resupply(army, 1f);
                    float with = Resupply(army, act);
                    Expect.True(without > 0f, $"the army resupplied at all on enemy soil ({without})");
                    ratio.Value = with / without;
                    return Live.Frames(1);
                },
                defect: r => Expect.Below(1f, r, "resupply with the act relative to without it (unfixed: slower)"),
                repaired: r => Expect.True(r > 1f, $"resupply with the act relative to without it should be faster; it is {r}"));
        }

        /// <summary>
        /// Supply the army's brigades take in over one game day from an empty
        /// store, under a given enemy-soil factor. Puts everything back.
        /// </summary>
        private static float Resupply(Regiment army, float factor)
        {
            GameVars.Alliance union = GameVars.alliance[army.alliance];
            Regiment[] brigades = Units.Brigades(army).Where(b => b != null && b.supplyinstock != null).ToArray();
            float[][] stock = brigades.Select(b => (float[])b.supplyinstock.Clone()).ToArray();
            Traverse timer = Traverse.Create(army).Field("lastsupplyupdate");
            float lastUpdate = timer.GetValue<float>();
            float savedFactor = union.supplyenemysoilfactor;
            try
            {
                union.supplyenemysoilfactor = factor;
                foreach (Regiment brigade in brigades)
                {
                    for (int i = 0; i < brigade.supplyinstock.Length; i++)
                    {
                        brigade.supplyinstock[i] = 0f;
                    }
                }

                timer.SetValue(Clock.Now - 1f / 365.25f);
                Traverse.Create(army).Method("UpdateIIPSupplyLink").GetValue();
                return brigades.Sum(b => b.supplyinstock.Sum());
            }
            finally
            {
                union.supplyenemysoilfactor = savedFactor;
                timer.SetValue(lastUpdate);
                for (int i = 0; i < brigades.Length; i++)
                {
                    brigades[i].supplyinstock = stock[i];
                }
            }
        }
    }
}
