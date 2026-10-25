using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace GTCW.ModApi.Runtime.Capabilities
{
    /// <summary>
    /// Identifies which build of the game is running, by hashing
    /// <c>Assembly-CSharp.dll</c>.
    /// </summary>
    /// <remarks>
    /// Hashing is not the convenient option, it is the only one: there is no
    /// version constant anywhere in the assembly. The game parses its own
    /// version out of a <c>TextMeshProUGUI</c> label in the campaign UI, which
    /// is far too late to gate on — read it only to phrase a mismatch in the
    /// player's own terms.
    /// <para>
    /// Identification is not gating. An unknown hash means "run the probes",
    /// not "refuse to load".
    /// </para>
    /// </remarks>
    internal static class BuildIdentity
    {
        /// <summary>
        /// Hashes known to this release, from
        /// <c>code-findings/00-build-provenance.md</c>. Add an entry per build
        /// rather than relaxing the check.
        /// </summary>
        private static readonly Dictionary<string, string> KnownBuilds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Steam buildid 14703111, updated 2026-10-13, Unity 2021.3.16f1
                ["AE15E3E479F4D79B70E40CED42EB93A953569B8185E075B3873C7410462AE8C4"] =
                    "Steam buildid 14703111 (2026-10-13)",
            };

        /// <summary>SHA-256 of the running game assembly, or null if unreadable.</summary>
        public static string? Hash(string managedFolder)
        {
            string path = Path.Combine(managedFolder, "Assembly-CSharp.dll");
            if (!File.Exists(path))
            {
                return null;
            }

            using (var sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        /// <summary>A human label for a hash, or null when the build is unrecognised.</summary>
        public static string? Describe(string? hash) =>
            hash != null && KnownBuilds.TryGetValue(hash, out string label) ? label : null;
    }
}
