using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-004 and GTCW-006 — two defects in <c>Vessel.GetFiringStrength</c>,
    /// and therefore <b>one</b> patch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>GTCW-004.</b> The rate-of-fire blend is
    /// <c>Mathf.Max(1f, distance / num4)</c>, where <c>num4</c> is the
    /// gun-count-weighted mean range of the guns that are <em>in</em> range.
    /// Every one of those passed <c>range_yd &gt;= distance</c>, so the ratio
    /// cannot exceed 1 and the clamp raises it to exactly 1 — always. Every ship
    /// in every engagement fires at <c>navalbattlerofmaxdistance</c> (0.25), the
    /// close-combat rate of 1.00 is unreachable, and closing the range buys no
    /// rate of fire. The clamp wanted <c>Mathf.Min</c>.
    /// </para>
    /// <para>
    /// <b>GTCW-006.</b> The turret bonus counts guns with
    /// <c>withinturret == 1</c>, but <c>withinturret</c> is the number of
    /// turrets a weapon system is spread across, not a flag — and the ship
    /// panel's own figure counts <c>&gt; 0</c>. The three ships whose guns sit in
    /// two turrets get no bonus while the interface still calls their guns
    /// turret-mounted.
    /// </para>
    /// <para>
    /// Both are repaired by recomputing the method's own <c>num7</c> from inputs
    /// that are still available afterwards, and rescaling the result. Two
    /// independent postfixes would each rescale the other's output; one patch
    /// computes the corrected value once from whichever toggles are on.
    /// </para>
    /// </remarks>
    internal static class NavalFiringStrength
    {
        private const string RateOfFire = "GTCW-004";
        private const string TurretTest = "GTCW-006";
        private const string Target = "Vessel:GetFiringStrength";

        /// <summary>Both fixes share one patch, so they share a throw budget.</summary>
        private const string GuardLabel = "GTCW-004/006";

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    RateOfFire,
                    "Closing the range increases a ship's rate of fire, up to four times the long-range " +
                    "rate. Unfixed, every ship always fires at the long-range rate.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "mechanics-findings/manual-vs-code.md §38",
                    Probe),
                new Fix(
                    TurretTest,
                    "All six turret ships get the turret rate-of-fire bonus, not just the three whose guns " +
                    "share a single turret.",
                    FixClass.Code,
                    defaultEnabled: false,
                    SaveSafety.Stateless,
                    "mechanics-findings/manual-vs-code.md §39",
                    Probe),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("Vessel", "GetFiringStrength"),
            () => FixProbe.RequireField("Vessel", "strengthdata"),
            () => FixProbe.RequireField("GameVars+ShipType", "weaponsystems"),
            () => FixProbe.RequireField("GamePrefs", "navalbattlerofmaxdistance"),
            () => FixProbe.RequireField("GamePrefs", "navalbattlerofmindistance"),
            () => FixProbe.RequireField("GamePrefs", "rofimprovementturrets"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(NavalFiringStrength), nameof(Before))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(NavalFiringStrength), nameof(After))));
        }

        /// <summary>
        /// Records whether the body will run at all.
        /// </summary>
        /// <remarks>
        /// The method has four early returns, and two of their conditions are
        /// mutated by the body itself: <c>reduceammo</c> can take <c>ammo</c>
        /// from positive to zero, so a postfix testing <c>ammo &lt;= 0f</c> would
        /// wrongly conclude it had bailed. Evaluating the guards on entry is
        /// exact and costs four comparisons.
        /// </remarks>
        private static void Before(Vessel __instance, out bool __state)
        {
            __state = __instance.ammo > 0f
                   && __instance.condition >= GamePrefs.minshipconditionfighting
                   && __instance.constructiontimer <= 0f
                   && __instance.timeofarrival <= 0f;
        }

        private static void After(
            Vessel __instance,
            float distance,
            bool reduceammo,
            float ammoconsumptionfactor,
            bool __state,
            ref float __result)
        {
            bool rof = FixState.IsOn(RateOfFire);
            bool turrets = FixState.IsOn(TurretTest);

            if (!__state || (!rof && !turrets))
            {
                return;
            }

            float result = __result;
            FixState.Guard(GuardLabel, () =>
            {
                result = Correct(__instance, distance, reduceammo, ammoconsumptionfactor, rof, turrets, result);
            });
            __result = result;
        }

        /// <summary>
        /// Returns the corrected firing strength, and rewrites the panel figures
        /// and the ammunition the call spent at the wrong rate. Returns the
        /// value it was given if nothing needs correcting.
        /// </summary>
        private static float Correct(
            Vessel vessel,
            float distance,
            bool reduceammo,
            float ammoconsumptionfactor,
            bool rof,
            bool turrets,
            float result)
        {
            List<GameVars.ShipType> types = GameVars.shiptypes;
            if (types == null || vessel.type < 0 || vessel.type >= types.Count)
            {
                return result;
            }

            List<GameVars.ShipType.WeaponSystems> systems = types[vessel.type].weaponsystems;
            if (systems == null)
            {
                return result;
            }

            // Re-walk the weapon systems exactly as the method does. A handful
            // of entries per ship, so the cost is irrelevant beside being sure
            // the numbers match.
            float gunsInRange = 0f;       // num2
            float gunsInTurretsGame = 0f; // num3 as the game counts it
            float gunsInTurretsFixed = 0f;
            float rangeWeighted = 0f;     // num4
            float gunsTotal = 0f;

            foreach (GameVars.ShipType.WeaponSystems system in systems)
            {
                if (GameVars.weapon == null ||
                    system.weaponid < 0 ||
                    system.weaponid >= GameVars.weapon.Length)
                {
                    return result;
                }

                float range = GameVars.weapon[system.weaponid].range_yd;
                if (range >= distance)
                {
                    gunsInRange += system.guns;

                    if (system.withinturret == 1)
                    {
                        gunsInTurretsGame += system.guns;
                    }

                    if (system.withinturret > 0)
                    {
                        gunsInTurretsFixed += system.guns;
                    }

                    rangeWeighted += range * system.guns;
                }

                gunsTotal += system.guns;
            }

            float inRangeShare = gunsInRange / Mathf.Max(1f, gunsTotal);   // num5
            rangeWeighted /= Mathf.Max(1f, gunsInRange);

            float turretShareGame = gunsInTurretsGame / Mathf.Max(1f, gunsInRange);
            float turretShareFixed = gunsInTurretsFixed / Mathf.Max(1f, gunsInRange);

            float asShipped = Blend(
                Mathf.Max(1f, distance / Mathf.Max(1f, rangeWeighted)),
                turretShareGame);

            float corrected = Blend(
                rof ? Mathf.Min(1f, distance / Mathf.Max(1f, rangeWeighted))
                    : Mathf.Max(1f, distance / Mathf.Max(1f, rangeWeighted)),
                turrets ? turretShareFixed : turretShareGame);

            if (Mathf.Approximately(asShipped, corrected))
            {
                return result;
            }

            // Ammunition was already spent at the wrong rate, before the
            // low-ammo branch below could overwrite num7 — so this correction
            // applies either way. It does not retroactively re-evaluate whether
            // the different consumption would have crossed the low-ammo
            // threshold on this same call; the next call sees the corrected
            // ammo and decides for itself.
            if (reduceammo && GamePrefs.averagesalvopervessel > 0f)
            {
                vessel.ammo -= 1f / GamePrefs.averagesalvopervessel
                             * inRangeShare
                             * (corrected - asShipped)
                             * ammoconsumptionfactor;
            }

            // At or below the low-ammo trigger the game discards the blend
            // entirely and fires at navalbattleroflowammo, so there is nothing
            // to correct in the strength. Tested against the post-decrement
            // ammo, which is what the game tests.
            if (vessel.ammo <= GamePrefs.lowonammotriggerfleets)
            {
                return result;
            }

            if (turrets && vessel.strengthdata != null && vessel.strengthdata.Length > 2)
            {
                vessel.strengthdata[2] = gunsInTurretsFixed;
            }

            if (vessel.strengthdata != null && vessel.strengthdata.Length > 3)
            {
                vessel.strengthdata[3] = corrected;
            }

            // The result is strictly proportional to num7 — weapon strength,
            // condition, the commander factor and the battery-runner perk are
            // all independent of it — so a ratio is exact rather than a
            // reimplementation.
            if (asShipped != 0f)
            {
                result *= corrected / asShipped;

                if (vessel.strengthdata != null && vessel.strengthdata.Length > 5)
                {
                    vessel.strengthdata[5] = result;
                }
            }

            return result;
        }

        /// <summary>
        /// The game's own rate-of-fire expression, with its two inputs made
        /// explicit so the shipped and corrected values come from one formula.
        /// </summary>
        private static float Blend(float distanceFactor, float turretShare)
        {
            float rate = GamePrefs.navalbattlerofmaxdistance * distanceFactor
                       + GamePrefs.navalbattlerofmindistance * (1f - distanceFactor);

            return rate * ((GamePrefs.rofimprovementturrets - 1f) * turretShare + 1f);
        }
    }
}
