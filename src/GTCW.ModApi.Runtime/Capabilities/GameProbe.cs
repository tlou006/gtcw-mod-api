using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace GTCW.ModApi.Runtime.Capabilities
{
    /// <summary>
    /// Reflection helpers for probes. Deliberately string-based: a probe that
    /// used <c>typeof</c> would turn a renamed game type into a build error
    /// here instead of a disabled feature on the player's machine.
    /// </summary>
    /// <remarks>
    /// The members named in probes are the same ones the signature manifest
    /// tracks (see plan.md, "Surviving game updates"), so the manifest should
    /// become the source of this list rather than a parallel document.
    /// </remarks>
    internal static class GameProbe
    {
        /// <summary>A game type by name, or null. Game code lives in the global namespace.</summary>
        public static Type? Type(string name) => AccessTools.TypeByName(name);

        /// <summary>Checks a type exists.</summary>
        public static ProbeResult RequireType(string typeName) =>
            Type(typeName) != null
                ? ProbeResult.Pass()
                : ProbeResult.Missing(typeName);

        /// <summary>
        /// Checks a method exists on a type, by name only. Overload-insensitive
        /// on purpose: a changed signature should be caught by the manifest
        /// diff with a readable message, not by a silent probe failure here.
        /// </summary>
        public static ProbeResult RequireMethod(string typeName, string methodName)
        {
            Type? type = Type(typeName);
            if (type == null)
            {
                return ProbeResult.Missing(typeName);
            }

            bool found = type
                .GetMethods(AccessTools.all)
                .Any(m => m.Name == methodName);

            return found
                ? ProbeResult.Pass()
                : ProbeResult.Missing($"{typeName}.{methodName}");
        }

        /// <summary>Checks a field exists on a type.</summary>
        public static ProbeResult RequireField(string typeName, string fieldName)
        {
            Type? type = Type(typeName);
            if (type == null)
            {
                return ProbeResult.Missing(typeName);
            }

            FieldInfo? field = type.GetField(fieldName, AccessTools.all);
            return field != null
                ? ProbeResult.Pass()
                : ProbeResult.Missing($"{typeName}.{fieldName}");
        }

        /// <summary>Every probe must pass. Reports the first failure.</summary>
        public static ProbeResult All(params Func<ProbeResult>[] probes)
        {
            foreach (Func<ProbeResult> probe in probes)
            {
                ProbeResult result = probe();
                if (!result.Ok)
                {
                    return result;
                }
            }

            return ProbeResult.Pass();
        }
    }
}
