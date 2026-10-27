using System;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Battle
{
    /// <summary>
    /// Raises <see cref="IBattleEvents"/> off the lifecycle, rather than off
    /// patches of its own.
    /// </summary>
    /// <remarks>
    /// There is nothing to patch for "the battle started": the game has no such
    /// method. What it has is a boot sequence whose last gate is frame 40, so
    /// <c>Battle.Started</c> is <see cref="Milestone.BattleIdle"/> under a
    /// clearer name. Deriving it from the lifecycle instead of duplicating the
    /// patch keeps one source of truth for when a battle is live.
    /// </remarks>
    internal sealed class BattleEventsService : IBattleEvents
    {
        private readonly IModApiLog _log;

        /// <remarks>
        /// Subscribes to the <c>After*</c> hooks rather than the public events,
        /// so these derived events always reach a mod *after* the lifecycle
        /// event they are concluded from. See
        /// <see cref="Lifecycle.LifecycleService.AfterMilestoneReached"/>.
        /// </remarks>
        public BattleEventsService(IModApiLog log, Lifecycle.LifecycleService lifecycle)
        {
            _log = log;

            lifecycle.AfterMilestoneReached += (session, milestone) =>
            {
                if (milestone == Milestone.BattleIdle)
                {
                    Raise(Started, session, nameof(Started));
                }
            };

            lifecycle.AfterSessionEnding += session =>
            {
                if (session.Kind == SessionKind.Battle)
                {
                    Raise(Ended, session, nameof(Ended));
                }
            };
        }

        public event Action<IGameSession>? Started;
        public event Action<IGameSession>? Ended;

        private void Raise(Action<IGameSession>? handlers, IGameSession session, string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                string owner = handler.Method.DeclaringType?.Assembly.GetName().Name ?? "unknown";
                string who = $"{owner}.{handler.Method.Name} on Battle.{eventName}";

                if (FailSoft.IsBanned(who))
                {
                    continue;
                }

                FailSoft.Guard(_log, who, () => ((Action<IGameSession>)handler)(session));
            }
        }
    }
}
