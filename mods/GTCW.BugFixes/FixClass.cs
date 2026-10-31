namespace GTCW.BugFixes
{
    /// <summary>
    /// What kind of defect a fix addresses. The letters match
    /// <c>defect-register.md</c>, and the class is what decides whether a
    /// defect is ours to fix at all.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Code"/>, <see cref="Data"/> and
    /// <see cref="ManualDeviation"/> can appear on a shipped fix. The other
    /// four exist so a register row can be modelled and *declined* in code
    /// rather than quietly left out: a reader of this enum should be able to
    /// see why the mod does not touch cut content or AI handicaps.
    /// </remarks>
    internal enum FixClass
    {
        /// <summary>C — the code contradicts its own evident intent.</summary>
        Code,

        /// <summary>D — a shipped value contradicts its own config comment.</summary>
        Data,

        /// <summary>M — manual, appendix and config agree; the code does not.</summary>
        ManualDeviation,

        /// <summary>S — the code is the design; the manual is stale. Not ours.</summary>
        ManualStale,

        /// <summary>U — the simulation is right and the readout lies. Needs UI injection.</summary>
        Display,

        /// <summary>B — a deliberate asymmetry. Belongs in a balance mod.</summary>
        Asymmetry,

        /// <summary>X — unfinished rather than broken. Belongs in a restoration mod.</summary>
        Unfinished,
    }

    /// <summary>
    /// Whether enabling or disabling a fix is safe across a save boundary.
    /// Stated per fix because a player will turn these on and off.
    /// </summary>
    internal enum SaveSafety
    {
        /// <summary>
        /// Touches nothing that is written to a save. Toggle freely.
        /// </summary>
        Stateless,

        /// <summary>
        /// Changes a value that is saved, but the field exists either way, so a
        /// save written with the fix on still loads with it off and vice versa.
        /// Only the number differs.
        /// </summary>
        ValueOnly,

        /// <summary>
        /// Repairs state the save already holds. Disabling it afterwards does
        /// not put the old state back, because the repair was the point.
        /// </summary>
        OneWay,
    }
}
