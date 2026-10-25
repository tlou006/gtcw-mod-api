using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Lifecycle
{
    /// <summary>
    /// One scenario load. Every binding into the scene hangs off an instance of
    /// this and dies with it.
    /// </summary>
    /// <remarks>
    /// <c>GameController</c> is scene-scoped and every transition is a
    /// non-additive <c>LoadScene("Main")</c>, so a binding held across one does
    /// not go stale — it points at a <b>destroyed</b> object. Resolve after each
    /// scenario load, discard on <c>bo_rebuildscene</c>.
    /// <para>
    /// <c>GameVars</c> is the exception and the trap: its fields are static and
    /// survive the scene change, but <c>import_nations</c> rebuilds the
    /// <c>alliance</c> and <c>nation</c> arrays, and the state count is 52, 53
    /// or 54 depending on the scenario. Re-read through the static; never cache
    /// the array and never hardcode 53.
    /// </para>
    /// </remarks>
    internal sealed class GameSession : IGameSession
    {
        private readonly HashSet<Milestone> _reached = new HashSet<Milestone>();

        public GameSession(SessionKind kind) => Kind = kind;

        public SessionKind Kind { get; }

        public bool IsAlive { get; private set; } = true;

        public bool HasReached(Milestone milestone) => _reached.Contains(milestone);

        internal bool MarkReached(Milestone milestone) => _reached.Add(milestone);

        internal void Kill() => IsAlive = false;
    }
}
