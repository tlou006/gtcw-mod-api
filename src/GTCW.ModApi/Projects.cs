using System;
using System.Collections.Generic;

namespace GTCW.ModApi
{
    /// <summary>
    /// Which side a project is offered to. The game stores this as a list of
    /// alliance indices, and a project that names neither side **never appears
    /// in the panel** — the UI build loop requires the player's alliance.
    /// </summary>
    [Flags]
    public enum Side
    {
        None = 0,
        Union = 1,
        Confederacy = 2,
        Both = Union | Confederacy,
    }

    /// <summary>
    /// A project's category. This is one field doing two jobs: it is the
    /// subsidy pot the project is paid from <i>and</i> the panel filter it
    /// appears under.
    /// </summary>
    /// <remarks>
    /// The values are the game's own, proven by its filter code — each arm
    /// tests <c>subsidytype == n</c> against the matching filter name. The
    /// underlying array has eight slots; the last two are the publisher's
    /// abandoned categories, have no filter, and are deliberately not exposed.
    /// </remarks>
    public enum ProjectCategory
    {
        Politics = 0,
        Economy = 1,
        Agriculture = 2,
        Industry = 3,
        Military = 4,
        Diplomacy = 5,
    }

    /// <summary>Which column of the projects panel an entry is listed in.</summary>
    public enum PanelColumn
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// What a mod fills in to define a project. Every property has a working
    /// default except <see cref="Name"/> and <see cref="Sides"/>.
    /// </summary>
    public interface IProjectDefinition
    {
        /// <summary>The stable key this project was registered under.</summary>
        string Key { get; }

        /// <summary>Shown in the panel. Required.</summary>
        string Name { get; set; }

        /// <summary>
        /// Which side may appoint it. Required — a project with
        /// <see cref="Side.None"/> is silently invisible, so registration
        /// rejects it rather than letting you ship that.
        /// </summary>
        Side Sides { get; set; }

        /// <summary>The subsidy pot it is paid from, and its panel filter.</summary>
        ProjectCategory Category { get; set; }

        /// <summary>
        /// Cost of the first level, in the subsidy funding of
        /// <see cref="Category"/>. Later levels scale by the game's own
        /// exponent. Zero means free.
        /// </summary>
        float FirstLevelCost { get; set; }

        /// <summary>
        /// Whether it can be appointed more than once, each appointment adding
        /// a level. Non-repeating projects stop being appointable at level 1.
        /// </summary>
        bool Repeating { get; set; }

        /// <summary>Prose shown in the panel tooltip.</summary>
        string Description { get; set; }

        /// <summary>The effect summary line shown under the description.</summary>
        string EffectText { get; set; }

        /// <summary>Which column to list it in. Cosmetic.</summary>
        PanelColumn Column { get; set; }

        /// <summary>
        /// Scenario ids it is offered in, e.g. <c>"001"</c>. A project whose
        /// list excludes the running scenario is never appointable. Defaults to
        /// whatever the claimed slot already allowed, which is the base campaign.
        /// </summary>
        IList<string> Scenarios { get; }

        /// <summary>
        /// Optional sprite resource paths, as the game's own records use them
        /// (for example <c>"UI/ProjectImages/0_s"</c>). Left unset, the entry
        /// simply has no image, which is harmless.
        /// </summary>
        string? ImagePathSmall { get; set; }

        /// <inheritdoc cref="ImagePathSmall"/>
        string? ImagePathBig { get; set; }

        /// <summary>
        /// Fired once when the project is appointed, per side that appoints it.
        /// </summary>
        event Action<IProjectEffectContext> Completed;

        /// <summary>
        /// Fired when the game re-applies project effects: at campaign load,
        /// and again whenever any project is appointed.
        /// </summary>
        /// <remarks>
        /// <b>This is replayed, not ticked.</b> It can fire several times for
        /// one appointment and it fires again on every load, so it must set
        /// absolute values rather than accumulate. Adding to a running total
        /// here is the classic way to make a save drift every time it is
        /// reloaded.
        /// </remarks>
        event Action<IProjectEffectContext> Refreshed;
    }

    /// <summary>Who appointed the project, and at what level.</summary>
    public interface IProjectEffectContext
    {
        /// <summary>The registered key, for logging.</summary>
        string Key { get; }

        /// <summary>The numeric id the broker assigned. Not stable across mod sets.</summary>
        int ProjectId { get; }

        /// <summary>The side this invocation concerns.</summary>
        Side Side { get; }

        /// <summary>Raw alliance index, for callers that need the game's own numbering.</summary>
        int Alliance { get; }

        /// <summary>How many times it has been appointed by this side.</summary>
        int Level { get; }
    }

    /// <summary>
    /// Adds projects to the game. Projects are the one content type that needs
    /// no Unity assets: the panel clones one generic prefab per record, so a
    /// definition plus an effect callback is a complete project.
    /// </summary>
    public interface IProjectRegistry
    {
        /// <summary>
        /// Define a project under a stable key, conventionally
        /// <c>"yourmod.something"</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call this while your plugin loads. The game reads its project table
        /// once per campaign load and registration has to be complete before
        /// that, so a late call throws rather than silently doing nothing.
        /// </para>
        /// <para>
        /// The key is what you own; the numeric id is assigned from the slots
        /// the publisher left free and is <b>not</b> guaranteed stable across
        /// changes to the installed mod set.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Registration has closed, or the free slots are exhausted.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The key is already registered, or the definition is incomplete.
        /// </exception>
        void Register(string key, Action<IProjectDefinition> configure);

        /// <summary>
        /// How many project slots remain. The publisher left a fixed number
        /// free, and every mod draws from the same pool.
        /// </summary>
        int RemainingSlots { get; }

        /// <summary>
        /// The id assigned to a registered key, or -1 before assignment or if
        /// the key is unknown. Assignment happens at the first campaign load.
        /// </summary>
        int IdOf(string key);
    }
}
