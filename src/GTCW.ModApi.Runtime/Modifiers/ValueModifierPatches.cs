using System;
using System.Reflection;
using GTCW.ModApi;
using GTCW.ModApi.Runtime.Capabilities;
using HarmonyLib;

namespace GTCW.ModApi.Runtime.Modifiers
{
    /// <summary>
    /// Applies value modifiers to the game's computed financial figures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These five are pure functions of alliance state, which is what makes
    /// them safe to postfix: there is no stored field to fight over, nothing
    /// resets them, and returning a different number has exactly the effect it
    /// looks like. They are the cheapest real capability in the library.
    /// </para>
    /// <para>
    /// They are also, per the research, the natural place to implement the ten
    /// dead subsidy and tax pref arrays without touching the 1,200-line policy
    /// switch.
    /// </para>
    /// </remarks>
    internal static class ValueModifierPatches
    {
        private static ModifierRegistry? _registry;
        private static IModApiLog? _log;

        /// <summary>
        /// Each target, with the set it consults and whether the game passes a
        /// category index.
        /// </summary>
        /// <remarks>
        /// <c>GetSubsidyCost</c> is named on both <c>Alliance</c> and
        /// <c>Projects.LoadedProjects</c>, where it means something entirely
        /// different — a project's price at a level. Hence the fully qualified
        /// target names, and why none of these is resolved by bare method name.
        /// </remarks>
        private static readonly (string Target, string Setter)[] Targets =
        {
            ("GameVars+Alliance:GetAdminCost", nameof(AfterAdminCost)),
            ("GameVars+Alliance:GetSubsidyCost", nameof(AfterSubsidyCost)),
            ("GameVars+Alliance:GetSubsidyFactor", nameof(AfterSubsidyFactor)),
            ("GameVars+Alliance:GetCurrentTaxValuePa", nameof(AfterTaxRevenue)),
            ("GameVars+Alliance:GetBalance", nameof(AfterBalance)),
        };

        public static ProbeResult Probe() => GameProbe.All(
            () => GameProbe.RequireField("GameVars", "alliance"),
            () => GameProbe.RequireMethod("GameVars+Alliance", "GetAdminCost"),
            () => GameProbe.RequireMethod("GameVars+Alliance", "GetSubsidyCost"),
            () => GameProbe.RequireMethod("GameVars+Alliance", "GetSubsidyFactor"),
            () => GameProbe.RequireMethod("GameVars+Alliance", "GetCurrentTaxValuePa"),
            () => GameProbe.RequireMethod("GameVars+Alliance", "GetBalance"));

        public static void Apply(Harmony harmony, ModifierRegistry registry, IModApiLog log)
        {
            _registry = registry;
            _log = log;

            foreach ((string target, string setter) in Targets)
            {
                MethodInfo original = AccessTools.Method(target)
                    ?? throw new InvalidOperationException($"'{target}' not found");

                harmony.Patch(
                    original,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ValueModifierPatches), setter)));
            }
        }

        // --- postfixes ----------------------------------------------------------
        //
        // Each takes the instance and the game's own result, and the index
        // parameter where the method has one. Harmony injects parameters by
        // name, so `subsidyid` and `taxid` must match the game's own signatures.

        private static void AfterAdminCost(GameVars.Alliance __instance, ref float __result) =>
            __result = Apply(_registry?.AdminCostSet, __instance, -1, __result);

        private static void AfterSubsidyCost(GameVars.Alliance __instance, int subsidyid, ref float __result) =>
            __result = Apply(_registry?.SubsidyCostSet, __instance, subsidyid, __result);

        private static void AfterSubsidyFactor(GameVars.Alliance __instance, int subsidyid, ref float __result) =>
            __result = Apply(_registry?.SubsidyFactorSet, __instance, subsidyid, __result);

        private static void AfterTaxRevenue(GameVars.Alliance __instance, int taxid, ref float __result) =>
            __result = Apply(_registry?.TaxRevenueSet, __instance, taxid, __result);

        private static void AfterBalance(GameVars.Alliance __instance, ref float __result) =>
            __result = Apply(_registry?.FinancialBalanceSet, __instance, -1, __result);

        // --- composition --------------------------------------------------------

        /// <summary>
        /// <c>final = base * product(multipliers) + sum(offsets)</c>, which is
        /// order-independent in both halves, so the result does not depend on
        /// which mod registered first.
        /// </summary>
        private static float Apply(
            ValueModifierSet? set,
            GameVars.Alliance instance,
            int index,
            float baseValue)
        {
            // The hot early-out. These functions are called from UI and AI code
            // and this postfix sits in front of all of it, so with nothing
            // registered it must cost a null check and a count.
            if (set == null || !set.HasAny || instance == null)
            {
                return baseValue;
            }

            int alliance = IndexOf(instance);
            Side side = alliance switch
            {
                0 => Side.Union,
                1 => Side.Confederacy,
                _ => Side.None,
            };

            if (side == Side.None)
            {
                return baseValue;
            }

            IModApiLog log = _log!;
            var ctx = new ValueContext
            {
                Side = side,
                Alliance = alliance,
                BaseValue = baseValue,
                Index = index,
            };

            float factor = 1f;
            float offset = 0f;

            foreach (ValueModifier modifier in set.Modifiers)
            {
                if ((modifier.Side & side) == 0)
                {
                    continue;
                }

                string who = $"{modifier.Key} {set.Label}";
                if (FailSoft.IsBanned(who))
                {
                    continue;
                }

                float value = 0f;
                if (!FailSoft.Guard(log, who, () => value = modifier.Value(ctx)))
                {
                    continue;
                }

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    log.Error($"{set.Label} modifier '{modifier.Key}' returned {value}; ignored.");
                    continue;
                }

                if (modifier.Kind == ValueModifierKind.Multiply)
                {
                    factor *= value;
                }
                else
                {
                    offset += value;
                }
            }

            float result = (baseValue * factor) + offset;
            return (float.IsNaN(result) || float.IsInfinity(result)) ? baseValue : result;
        }

        /// <summary>
        /// Which alliance an instance is. <c>Alliance</c> carries no index of
        /// its own, so this is a reference scan of the eight-entry array —
        /// cheap, and only reached once something is registered.
        /// </summary>
        private static int IndexOf(GameVars.Alliance instance)
        {
            GameVars.Alliance[] alliances = GameVars.alliance;
            if (alliances == null)
            {
                return -1;
            }

            for (int i = 0; i < alliances.Length; i++)
            {
                if (ReferenceEquals(alliances[i], instance))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
