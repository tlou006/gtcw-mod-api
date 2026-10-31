using System;
using HarmonyLib;

namespace GTCW.BugFixes
{
    /// <summary>
    /// Whether a fix can be applied to the build that is actually running.
    /// </summary>
    internal readonly struct ProbeResult
    {
        private ProbeResult(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }

        public bool Ok { get; }

        /// <summary>Why not, in a form worth putting in a log line.</summary>
        public string Reason { get; }

        public static ProbeResult Pass() => new ProbeResult(true, string.Empty);

        public static ProbeResult Fail(string reason) => new ProbeResult(false, reason);

        public static ProbeResult Missing(string what) =>
            new ProbeResult(false, $"'{what}' is not in this build");
    }

    /// <summary>
    /// String-based existence checks against the running assembly, so a probe
    /// never needs the type it is asking about to still exist at compile time.
    /// </summary>
    /// <remarks>
    /// This duplicates, in miniature, <c>GTCW.ModApi.Runtime</c>'s own
    /// <c>GameProbe</c> — which is <c>internal</c>, because it was written for
    /// the library rather than for mods. Duplicating it is the right call for
    /// now (reaching into the library's internals is the coupling the layering
    /// exists to prevent), but a game-aware mod wanting exactly this is
    /// evidence the public API is missing something. Recorded as API gap 1 in
    /// <c>IMPLEMENTATION.md</c>.
    /// </remarks>
    internal static class FixProbe
    {
        public static ProbeResult RequireMethod(string type, string method)
        {
            if (AccessTools.TypeByName(type) == null)
            {
                return ProbeResult.Missing(type);
            }

            return AccessTools.Method($"{type}:{method}") == null
                ? ProbeResult.Missing($"{type}:{method}")
                : ProbeResult.Pass();
        }

        public static ProbeResult RequireField(string type, string field)
        {
            Type? t = AccessTools.TypeByName(type);
            if (t == null)
            {
                return ProbeResult.Missing(type);
            }

            return AccessTools.Field(t, field) == null
                ? ProbeResult.Missing($"{type}.{field}")
                : ProbeResult.Pass();
        }

        /// <summary>
        /// All of them, stopping at the first failure. Lazy on purpose: a later
        /// check may name a type an earlier one proved absent.
        /// </summary>
        public static ProbeResult All(params Func<ProbeResult>[] checks)
        {
            foreach (Func<ProbeResult> check in checks)
            {
                ProbeResult result = check();
                if (!result.Ok)
                {
                    return result;
                }
            }

            return ProbeResult.Pass();
        }
    }
}
