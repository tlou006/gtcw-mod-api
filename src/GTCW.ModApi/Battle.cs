using System;

namespace GTCW.ModApi
{
    /// <summary>
    /// Battle notifications. Past tense throughout: these are read-only and
    /// cannot be cancelled, which is the <i>notification</i> tier of the event
    /// surface and the only tier that is unconditionally safe.
    /// </summary>
    public interface IBattleEvents
    {
        /// <summary>
        /// A battle is up and interactive: the map is built, regiments exist,
        /// both sides are deployed and the boot sequence has finished.
        /// </summary>
        /// <remarks>
        /// Raised when a battle session reaches <see cref="Milestone.BattleIdle"/>,
        /// for a standalone battle and for one launched from the campaign alike.
        /// Reloading a saved battle raises it too.
        /// </remarks>
        event Action<IGameSession> Started;

        /// <summary>
        /// The battle scene is being torn down. The session's objects are still
        /// alive when this runs and destroyed immediately afterwards, so read
        /// what you need now rather than holding a reference.
        /// </summary>
        event Action<IGameSession> Ended;
    }
}
