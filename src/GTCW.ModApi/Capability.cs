namespace GTCW.ModApi
{
    /// <summary>
    /// One unit of enable/disable. Every feature of the library is gated on a
    /// capability that is probed against the running game, so an unknown build
    /// degrades feature by feature instead of bricking every mod.
    /// </summary>
    /// <remarks>
    /// Values are never renumbered or removed — mods branch on these. A
    /// capability that stops being supported stays in the enum and reports
    /// <c>false</c> forever.
    /// </remarks>
    public enum Capability
    {
        /// <summary>Scene-scoped sessions and the boot milestone events.</summary>
        Lifecycle = 1,

        /// <summary>Battle start/end notifications.</summary>
        BattleEvents = 2,

        /// <summary>Per-unit notifications. Expensive; see the sampled tier.</summary>
        UnitEvents = 3,

        /// <summary>Post-parse, additive overrides of the <c>Config/</c> data files.</summary>
        ConfigOverrides = 10,

        /// <summary>Declarative contributions folded into the game's own passes.</summary>
        Modifiers = 20,

        /// <summary>
        /// Registration of new projects. The 56 <c>PLACEHOLDER</c> ids are free
        /// and no Unity assets are involved.
        /// </summary>
        ProjectRegistration = 30,

        /// <summary>
        /// Registration of new policies. Requires cloning a <c>Policy</c>
        /// GameObject, and is gated on three unknowns that need a live build —
        /// expect this to read <c>false</c> for some time.
        /// </summary>
        PolicyRegistration = 31,

        /// <summary>Side-car persistence alongside the game's own saves.</summary>
        Persistence = 40,

        /// <summary>Deferred work through the game's own batch-operation queue.</summary>
        Scheduling = 41,
    }

    /// <summary>
    /// What this build of the game actually supports. Query before using a
    /// feature; a mod that branches here keeps working on patch day.
    /// </summary>
    public interface ICapabilities
    {
        /// <summary>True when <paramref name="capability"/> probed successfully.</summary>
        bool Has(Capability capability);

        /// <summary>
        /// Why a capability is unavailable, for logging and for a mod that wants
        /// to tell the player something useful. Empty when it is available.
        /// </summary>
        string Explain(Capability capability);
    }
}
