using System;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Capabilities
{
    /// <summary>
    /// A capability, its probe, and the patches that implement it. The unit of
    /// enable/disable — a failed probe or a throwing <see cref="Apply"/>
    /// disables this feature and nothing else.
    /// </summary>
    internal sealed class Feature
    {
        public Feature(
            Capability id,
            string description,
            Func<ProbeResult> probe,
            Action apply)
        {
            Id = id;
            Description = description;
            Probe = probe;
            Apply = apply;
        }

        public Capability Id { get; }

        /// <summary>One line, for the capability report in the log.</summary>
        public string Description { get; }

        /// <summary>
        /// Does this build still support the feature? Probes check the members
        /// the patches target — method present, signature unchanged, field
        /// present — by reflection over the real assembly, so an unknown build
        /// is answered rather than guessed.
        /// </summary>
        public Func<ProbeResult> Probe { get; }

        /// <summary>Installs the Harmony patches. Called only when the probe passed.</summary>
        public Action Apply { get; }

        /// <summary>
        /// A capability this release does not implement yet. Registering it
        /// anyway means the startup report doubles as an honest roadmap, and a
        /// mod asking for it gets a real reason rather than a bare false.
        /// </summary>
        public static Feature Planned(Capability id, string description, string tracking) =>
            new Feature(
                id,
                description,
                () => ProbeResult.Fail($"not implemented in this release — {tracking}"),
                () => { });
    }

    /// <summary>Why a probe succeeded or failed. The failure text reaches the player.</summary>
    internal readonly struct ProbeResult
    {
        private ProbeResult(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }

        public bool Ok { get; }

        /// <summary>Empty when <see cref="Ok"/>; otherwise what was missing.</summary>
        public string Reason { get; }

        public static ProbeResult Pass() => new ProbeResult(true, string.Empty);

        public static ProbeResult Fail(string reason) => new ProbeResult(false, reason);

        /// <summary>Convenience for the common "this member is gone" case.</summary>
        public static ProbeResult Missing(string member) =>
            new ProbeResult(false, $"'{member}' not found in this build of Assembly-CSharp");
    }
}
