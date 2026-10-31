using System;
using HarmonyLib;

namespace GTCW.BugFixes
{
    /// <summary>
    /// One row of <c>defect-register.md</c>, as code.
    /// </summary>
    /// <remarks>
    /// The register is the specification and this is the implementation, so the
    /// two must stay aligned: <see cref="Id"/> is the join, and the config key a
    /// player sees is that same id. Renaming an id is a breaking change to
    /// somebody's config file.
    /// </remarks>
    internal sealed class Fix
    {
        public Fix(
            string id,
            string title,
            FixClass fixClass,
            bool defaultEnabled,
            SaveSafety save,
            string source,
            Func<ProbeResult> probe)
        {
            Id = id;
            Title = title;
            Class = fixClass;
            DefaultEnabled = defaultEnabled;
            Save = save;
            Source = source;
            Probe = probe;
        }

        /// <summary>Register id, <c>GTCW-NNN</c>. Also the config key.</summary>
        public string Id { get; }

        /// <summary>One line, in the player's terms, for the config file.</summary>
        public string Title { get; }

        public FixClass Class { get; }

        /// <summary>
        /// Whether this ships on. <b>Certainty decides whether we fix it; reach
        /// decides whether it is on.</b> A defect can be beyond doubt and still
        /// default off because it moves a quantity data mods author and balance
        /// against — see the compatibility boundary in <c>plan.md</c>.
        /// </summary>
        public bool DefaultEnabled { get; }

        public SaveSafety Save { get; }

        /// <summary>The findings file and section that established the defect.</summary>
        public string Source { get; }

        /// <summary>Whether this build still has what the fix patches.</summary>
        public Func<ProbeResult> Probe { get; }
    }

    /// <summary>
    /// One Harmony patch, and every fix that rides on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Grouping is not tidiness — it is required. Four of the seeded fixes live
    /// inside <c>BattleUnits.UpdateAllianceData</c> and two inside
    /// <c>Vessel.GetFiringStrength</c>, and both of those are repaired by
    /// recomputing a value the method got wrong. Two independent postfixes each
    /// rescaling the same result would compose into nonsense, so a method gets
    /// exactly one patch and the patch body asks
    /// <see cref="FixState.IsOn(string)"/> which parts of its job are turned on.
    /// </para>
    /// <para>
    /// A group with nothing enabled is never patched at all. That is what makes
    /// "zero-diff when idle" true rather than merely intended.
    /// </para>
    /// </remarks>
    internal sealed class FixGroup
    {
        public FixGroup(string target, Fix[] fixes, Action<Harmony> apply)
        {
            Target = target;
            Fixes = fixes;
            Apply = apply;
        }

        /// <summary>The patched method, <c>Type:Method</c>, for logging.</summary>
        public string Target { get; }

        public Fix[] Fixes { get; }

        /// <summary>
        /// Applies the patch. Called once, only when at least one member fix is
        /// enabled and has probed clean.
        /// </summary>
        public Action<Harmony> Apply { get; }
    }
}
