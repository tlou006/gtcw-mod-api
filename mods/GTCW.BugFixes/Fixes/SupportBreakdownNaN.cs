using System;
using System.Reflection;
using GTCW.ModApi;
using HarmonyLib;
using Api = GTCW.ModApi.ModApi;

namespace GTCW.BugFixes.Fixes
{
    /// <summary>
    /// GTCW-002 — a support <em>loss</em> writes NaN into the breakdown tracker,
    /// and the guard that was meant to catch it cannot fire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GameVars+Nation.AdjustSupport</c> stores
    /// <c>Mathf.Pow(amount, 0.5f)</c>, which is NaN for a negative amount, then
    /// tests <c>== float.NaN</c> — always false in C#, because NaN is not equal
    /// to itself. The NaN never clears, so
    /// <c>Mathf.Abs(supportinfluencelast) &lt; 0.0001f</c> stays false and the
    /// matching <c>supportinfluence</c> slot is never reset again.
    /// </para>
    /// <para>
    /// The fix is <b>only</b> to make the author's own guard work:
    /// <c>float.IsNaN</c>. A sign-preserving root would be a more useful
    /// tracker and is deliberately <i>not</i> done here — the guard's existence
    /// is evidence of intent, the formula is not, and inventing one would make
    /// this a balance change wearing a bug fix's clothes.
    /// </para>
    /// </remarks>
    internal static class SupportBreakdownNaN
    {
        private const string Id = "GTCW-002";
        private const string Target = "GameVars+Nation:AdjustSupport";

        public static FixGroup Group() => new FixGroup(
            Target,
            new[]
            {
                new Fix(
                    Id,
                    "Stop a loss of state support poisoning its reason code in the support breakdown.",
                    FixClass.Code,
                    defaultEnabled: true,
                    SaveSafety.ValueOnly,
                    "code-findings/17-politics-and-support.md, the support-breakdown NaN bug",
                    Probe),
            },
            Apply);

        private static ProbeResult Probe() => FixProbe.All(
            () => FixProbe.RequireMethod("GameVars+Nation", "AdjustSupport"),
            () => FixProbe.RequireField("GameVars+Nation", "supportinfluencelast"));

        private static void Apply(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(Target)
                ?? throw new InvalidOperationException($"'{Target}' not found");

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(SupportBreakdownNaN), nameof(After))));

            // A save written before this fix was enabled still holds NaN in any
            // slot whose reason code has not fired since. Those heal on their
            // next adjustment, but a reason code that never fires again would
            // stay poisoned forever, so sweep once per campaign. CampaignLive is
            // the first milestone at which the save's own nation data is in
            // place.
            if (Api.Capabilities.Has(Capability.Lifecycle))
            {
                Api.Lifecycle.MilestoneReached += OnMilestoneReached;
            }
        }

        /// <summary>
        /// Clears the NaN the game has just written, if it wrote one.
        /// </summary>
        /// <remarks>
        /// Reads the field rather than recomputing from <c>amount</c>: the
        /// method reassigns that argument to the clamped delta, so recomputing
        /// would duplicate logic that is already on the line above.
        /// </remarks>
        private static void After(GameVars.Nation __instance, int _alliance, int typ)
        {
            if (!FixState.IsOn(Id))
            {
                return;
            }

            FixState.Guard(Id, () =>
            {
                // The method's own guard: it does nothing for alliances above 1.
                if (_alliance < 0 || _alliance >= 2)
                {
                    return;
                }

                float[,] last = __instance.supportinfluencelast;
                if (last == null ||
                    _alliance >= last.GetLength(0) ||
                    typ < 0 || typ >= last.GetLength(1))
                {
                    return;
                }

                if (float.IsNaN(last[_alliance, typ]))
                {
                    last[_alliance, typ] = 0f;
                }
            });
        }

        private static void OnMilestoneReached(IGameSession session, Milestone milestone)
        {
            if (!FixState.IsOn(Id) ||
                milestone != Milestone.CampaignLive ||
                session.Kind != SessionKind.Campaign)
            {
                return;
            }

            FixState.Guard(Id, () =>
            {
                GameVars.Nation[] nations = GameVars.nation;
                if (nations == null)
                {
                    return;
                }

                int cleared = 0;
                foreach (GameVars.Nation nation in nations)
                {
                    float[,]? last = nation?.supportinfluencelast;
                    if (last == null)
                    {
                        continue;
                    }

                    for (int a = 0; a < last.GetLength(0); a++)
                    {
                        for (int t = 0; t < last.GetLength(1); t++)
                        {
                            if (float.IsNaN(last[a, t]))
                            {
                                last[a, t] = 0f;
                                cleared++;
                            }
                        }
                    }
                }

                if (cleared > 0)
                {
                    FixState.Info($"{Id}: cleared {cleared} poisoned support-breakdown slots carried in this save.");
                }
            });
        }
    }
}
