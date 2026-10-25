using System;

namespace GTCW.ModApi
{
    /// <summary>
    /// A named point in the game's boot sequence. The game's controllers are
    /// boot state machines — <c>CampaignController.Update</c> is 55
    /// <c>if (frame == N)</c> gates — and the raw frame counter is unusable as
    /// a clock because it <b>rewinds</b> while the map builds. So the API
    /// exposes milestones and keeps the frame numbers to itself.
    /// </summary>
    public enum Milestone
    {
        /// <summary>The map is built. The boot sequence stalls here until it is.</summary>
        MapReady = 1,

        /// <summary>
        /// Policy effects have been applied for the first time. Any effect
        /// registration is already too late by this point.
        /// </summary>
        PoliciesReady = 2,

        /// <summary>Regiments exist. Nothing may read units before this.</summary>
        UnitsReady = 3,

        /// <summary>Every saved subsystem has loaded.</summary>
        SubsystemsLoaded = 4,

        /// <summary>Ownerships and frontlines are set; the campaign is live.</summary>
        CampaignLive = 5,

        /// <summary>Project effects have been applied.</summary>
        ProjectsReady = 6,

        /// <summary>Chapter updated, speed zero, waiting for the player.</summary>
        CampaignIdle = 7,
    }

    /// <summary>Which scene a session belongs to.</summary>
    public enum SessionKind
    {
        Campaign = 1,
        Battle = 2,
    }

    /// <summary>
    /// One scenario load. Bindings into game objects live here and nowhere
    /// else: every scene transition is a non-additive <c>LoadScene("Main")</c>,
    /// so anything cached across one points at a <b>destroyed</b> object.
    /// </summary>
    public interface IGameSession
    {
        /// <summary>Distinguishes campaign from battle; one driver serves both.</summary>
        SessionKind Kind { get; }

        /// <summary>False once the scene this session described has been torn down.</summary>
        bool IsAlive { get; }

        /// <summary>True once <paramref name="milestone"/> has been passed in this session.</summary>
        bool HasReached(Milestone milestone);
    }

    /// <summary>Session and milestone events.</summary>
    public interface ILifecycle
    {
        /// <summary>
        /// The live session, or null between scenes. Re-read it rather than
        /// holding it.
        /// </summary>
        IGameSession? Current { get; }

        /// <summary>A scenario has begun loading. Bindings are not resolved yet.</summary>
        event Action<IGameSession> SessionStarted;

        /// <summary>
        /// The scene is about to be torn down. Last chance to drop references;
        /// after this the session's objects are destroyed.
        /// </summary>
        event Action<IGameSession> SessionEnding;

        /// <summary>
        /// Fires once per session per milestone, in order. Subscribe before a
        /// session starts — a late subscriber does not receive milestones the
        /// current session has already passed.
        /// </summary>
        event Action<IGameSession, Milestone> MilestoneReached;
    }
}
