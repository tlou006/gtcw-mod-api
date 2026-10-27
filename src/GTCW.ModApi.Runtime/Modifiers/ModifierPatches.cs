using System;
using System.Reflection;
using GTCW.ModApi;
using GTCW.ModApi.Runtime.Capabilities;
using HarmonyLib;

namespace GTCW.ModApi.Runtime.Modifiers
{
    /// <summary>
    /// Folds registered contributions into the game's own alliance pass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pass.</b> <c>BattleUnits.UpdateAllianceData</c> is the campaign's
    /// heartbeat. It processes <b>one alliance per beat</b>, round-robin, and
    /// refuses to advance until <c>updaterhythmalliancedata</c> (half a game
    /// day) has passed for that alliance. So each alliance is visited on its
    /// own half-day rhythm, which is exactly the cadence a rate wants.
    /// </para>
    /// <para>
    /// <b>Why a prefix and a postfix.</b> The elapsed time is a local variable,
    /// so it cannot be read from a postfix, and the method both overwrites
    /// <c>lastalliancedataupdate</c> and advances <c>alliancerunthrough</c>
    /// before returning. The prefix therefore captures the alliance index and
    /// the previous timestamp; the postfix reconstructs the interval from them.
    /// </para>
    /// <para>
    /// <b>Detecting whether the body ran.</b> The method returns early when not
    /// enough time has passed, and a postfix runs either way — folding on every
    /// early return would apply contributions at frame rate. The one assignment
    /// to <c>lastalliancedataupdate</c> in the body is the signal: if it differs
    /// from what the prefix captured, the body ran.
    /// </para>
    /// </remarks>
    internal static class ModifierPatches
    {
        /// <summary>
        /// The support-breakdown slot all ModApi contributions are credited to.
        /// </summary>
        /// <remarks>
        /// <c>supportinfluence</c> is <c>[10, 50]</c> and the highest code the
        /// game itself uses is 41, so 42 to 49 are free. Every contribution
        /// shares <b>one</b> code rather than taking one each: eight slots would
        /// otherwise be exhausted by the ninth mod, and an exhaustion failure
        /// here would be far more confusing than an aggregated line.
        /// </remarks>
        private const int ModReasonCode = 42;

        private static ModifierRegistry? _registry;
        private static IModApiLog? _log;
        private static ILifecycle? _lifecycle;

        /// <summary>Captured across the patched call.</summary>
        private struct PassState
        {
            public int Alliance;
            public float LastUpdate;
        }

        public static ProbeResult Probe() => GameProbe.All(
            () => GameProbe.RequireMethod("BattleUnits", "UpdateAllianceData"),
            () => GameProbe.RequireField("BattleUnits", "alliancerunthrough"),
            () => GameProbe.RequireField("GameVars", "nation"),
            () => GameProbe.RequireField("GameVars", "alliance"),
            () => GameProbe.RequireField("GameVars", "currenttimefromstart"),
            () => GameProbe.RequireMethod("GameVars+Nation", "AdjustSupport"));

        public static void Apply(
            Harmony harmony,
            ModifierRegistry registry,
            ILifecycle lifecycle,
            IModApiLog log)
        {
            _registry = registry;
            _lifecycle = lifecycle;
            _log = log;

            MethodInfo original = AccessTools.Method("BattleUnits:UpdateAllianceData")
                ?? throw new InvalidOperationException("'BattleUnits:UpdateAllianceData' not found");

            harmony.Patch(
                original,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModifierPatches), nameof(BeforePass))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ModifierPatches), nameof(AfterPass))));
        }

        private static void BeforePass(BattleUnits __instance, out PassState __state)
        {
            __state = default;

            int alliance = __instance.alliancerunthrough;
            GameVars.Alliance[] alliances = GameVars.alliance;
            if (alliances == null || alliance < 0 || alliance >= alliances.Length || alliances[alliance] == null)
            {
                __state.Alliance = -1;
                return;
            }

            __state.Alliance = alliance;
            __state.LastUpdate = alliances[alliance].lastalliancedataupdate;
        }

        private static void AfterPass(PassState __state)
        {
            ModifierRegistry? registry = _registry;
            if (registry == null || !registry.HasSupportModifiers || __state.Alliance < 0)
            {
                return;
            }

            // Campaign only. The same driver serves the battle scene, and state
            // support is not a battlefield concept.
            if (_lifecycle?.Current?.Kind != SessionKind.Campaign)
            {
                return;
            }

            GameVars.Alliance[] alliances = GameVars.alliance;
            if (alliances == null || __state.Alliance >= alliances.Length)
            {
                return;
            }

            float now = alliances[__state.Alliance].lastalliancedataupdate;
            if (now == __state.LastUpdate)
            {
                // Early return: not this alliance's turn yet.
                return;
            }

            float dt = now - __state.LastUpdate;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
            {
                // First beat of a session starts from a sentinel timestamp, so
                // the first interval is meaningless rather than small.
                return;
            }

            FoldSupport(registry, __state.Alliance, dt);
        }

        private static void FoldSupport(ModifierRegistry registry, int alliance, float dt)
        {
            IModApiLog log = _log!;

            Side side = alliance switch
            {
                0 => Side.Union,
                1 => Side.Confederacy,
                _ => Side.None,
            };

            // Support exists only for the two belligerents, and this beat
            // belongs to exactly one of them. Folding a Union contribution on
            // Britain's beat would apply it up to four times per half-day.
            if (side == Side.None)
            {
                return;
            }

            GameVars.Nation[] nations = GameVars.nation;
            if (nations == null)
            {
                return;
            }

            var ctx = new SupportContext { Side = side, Alliance = alliance, DeltaYears = dt };

            foreach (SupportModifier modifier in registry.SupportModifiers)
            {
                if ((modifier.Side & side) == 0)
                {
                    continue;
                }

                if (modifier.AppliesToAllStates)
                {
                    for (int s = 0; s < nations.Length; s++)
                    {
                        ApplyOne(log, modifier, ctx, nations, s, alliance, dt);
                    }
                }
                else if (modifier.StateIndex < nations.Length)
                {
                    ApplyOne(log, modifier, ctx, nations, modifier.StateIndex, alliance, dt);
                }
                else
                {
                    // The state count varies by scenario, so an index valid in
                    // one campaign can be out of range in another.
                    log.Warning(
                        $"Support contribution '{modifier.Key}' targets state {modifier.StateIndex}, " +
                        $"but this scenario has {nations.Length}. Skipped.");
                }
            }
        }

        private static void ApplyOne(
            IModApiLog log,
            SupportModifier modifier,
            SupportContext ctx,
            GameVars.Nation[] nations,
            int stateIndex,
            int alliance,
            float dt)
        {
            GameVars.Nation nation = nations[stateIndex];
            if (nation?.support == null || alliance >= nation.support.Length)
            {
                return;
            }

            ctx.StateIndex = stateIndex;
            ctx.CurrentSupport = nation.support[alliance];

            float rate = 0f;
            string who = $"{modifier.Key} support";

            if (FailSoft.IsBanned(who))
            {
                return;
            }

            if (!FailSoft.Guard(log, who, () => rate = modifier.RatePerYear(ctx)))
            {
                return;
            }

            if (rate == 0f || float.IsNaN(rate) || float.IsInfinity(rate))
            {
                if (float.IsNaN(rate) || float.IsInfinity(rate))
                {
                    log.Error($"Support contribution '{modifier.Key}' returned {rate}; ignored.");
                }

                return;
            }

            // Through the game's own writer, so the 0-1 clamp and the
            // recomputed delta apply exactly as they do to its own effects.
            nation.AdjustSupport(alliance, rate * dt, ModReasonCode);
        }
    }
}
