using System;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Lifecycle
{
    /// <summary>
    /// Owns the current session and raises the milestone events. The Harmony
    /// patches on the game's boot sequence call into here; mods see only
    /// <see cref="ILifecycle"/>.
    /// </summary>
    internal sealed class LifecycleService : ILifecycle
    {
        private readonly IModApiLog _log;
        private GameSession? _current;

        public LifecycleService(IModApiLog log) => _log = log;

        public IGameSession? Current => _current;

        public event Action<IGameSession>? SessionStarted;
        public event Action<IGameSession>? SessionEnding;
        public event Action<IGameSession, Milestone>? MilestoneReached;

        /// <summary>
        /// Raised after every <see cref="MilestoneReached"/> subscriber has run.
        /// </summary>
        /// <remarks>
        /// Runtime services that <i>derive</i> an event from a milestone — such
        /// as <c>Battle.Started</c> from <see cref="Milestone.BattleIdle"/> —
        /// subscribe here rather than to the public event. Otherwise the order
        /// a mod observes depends on subscription order: the runtime subscribes
        /// during bootstrap, before any mod exists, so its derived event would
        /// fire from inside the first handler and reach the mod *before* the
        /// milestone that caused it. Observed in the first live run, where
        /// "Battle started" was logged ahead of "Battle reached BattleIdle".
        /// </remarks>
        internal event Action<IGameSession, Milestone>? AfterMilestoneReached;

        /// <summary>
        /// Raised after every <see cref="SessionEnding"/> subscriber has run.
        /// Same reasoning as <see cref="AfterMilestoneReached"/>.
        /// </summary>
        internal event Action<IGameSession>? AfterSessionEnding;

        /// <summary>Called from the scenario-load patch.</summary>
        internal void BeginSession(SessionKind kind)
        {
            if (_current != null)
            {
                // A session that was never ended means a transition path we do
                // not patch yet. Close it rather than leaking bindings.
                _log.Warning("A session was still open when another began; closing it.");
                EndSession();
            }

            _current = new GameSession(kind);
            Raise(SessionStarted, _current, nameof(SessionStarted));
        }

        /// <summary>Called from the teardown patch, before the scene is destroyed.</summary>
        internal void EndSession()
        {
            GameSession? session = _current;
            if (session == null)
            {
                return;
            }

            Raise(SessionEnding, session, nameof(SessionEnding));
            AfterSessionEnding?.Invoke(session);
            session.Kill();
            _current = null;
        }

        /// <summary>
        /// Called from the boot-gate patches. Idempotent per session, because
        /// <c>GameVars.frame</c> rewinds while the map builds and a gate can
        /// therefore be observed more than once.
        /// </summary>
        internal void ReachMilestone(Milestone milestone)
        {
            GameSession? session = _current;
            if (session == null || !session.MarkReached(milestone))
            {
                return;
            }

            Action<IGameSession, Milestone>? handlers = MilestoneReached;
            if (handlers != null)
            {
                foreach (Delegate handler in handlers.GetInvocationList())
                {
                    string who = Describe(handler, nameof(MilestoneReached));
                    if (FailSoft.IsBanned(who))
                    {
                        continue;
                    }

                    FailSoft.Guard(_log, who, () =>
                        ((Action<IGameSession, Milestone>)handler)(session, milestone));
                }
            }

            // Derived events last, so a mod always sees the milestone before
            // anything concluded from it.
            AfterMilestoneReached?.Invoke(session, milestone);
        }

        private void Raise(Action<IGameSession>? handlers, IGameSession session, string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                string who = Describe(handler, eventName);
                if (FailSoft.IsBanned(who))
                {
                    continue;
                }

                FailSoft.Guard(_log, who, () => ((Action<IGameSession>)handler)(session));
            }
        }

        /// <summary>Names the subscriber in log output, so a throw blames a mod, not us.</summary>
        private static string Describe(Delegate handler, string eventName)
        {
            string owner = handler.Method.DeclaringType?.Assembly.GetName().Name ?? "unknown";
            return $"{owner}.{handler.Method.Name} on {eventName}";
        }
    }
}
