using System;
using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.Harness.Engine
{
    /// <summary>
    /// The engine tier's stand-in for <c>GTCW.ModApi.Runtime</c>: enough of the
    /// library for the fixes to probe it and subscribe to session events, and a
    /// way for tests to raise those events.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Capability.Lifecycle"/> is reported present, because it
    /// is the only capability any fix uses. Battle events, projects and
    /// modifiers are absent; a fix that starts needing one will fail its probe
    /// here, which is the right place to find out.
    /// </remarks>
    internal sealed class StandInHost : IModApiHost
    {
        public static StandInHost Current { get; private set; } = new StandInHost();

        private readonly StandInLifecycle _lifecycle = new StandInLifecycle();

        public ICapabilities Capabilities { get; } = new StandInCapabilities();

        public ILifecycle Lifecycle => _lifecycle;

        public IBattleEvents Battle => null!;

        public IProjectRegistry Projects => null!;

        public IModifierRegistry Modifiers => null!;

        public IModApiLog Log { get; } = new StandInLog();

        /// <summary>
        /// Binds a fresh host, dropping every subscription made against the
        /// last one, so patches applied twice do not hear events twice.
        /// </summary>
        public static StandInHost BindNew()
        {
            Current = new StandInHost();
            GTCW.ModApi.ModApi.Bind(Current);
            return Current;
        }

        public void StartSession(SessionKind kind) => _lifecycle.Start(kind);

        public void Reach(Milestone milestone) => _lifecycle.Reach(milestone);

        private sealed class StandInCapabilities : ICapabilities
        {
            public bool Has(Capability capability) => capability == Capability.Lifecycle;

            public string Explain(Capability capability) =>
                Has(capability) ? "present (engine-tier stand-in)" : "absent in the engine tier's stand-in host";
        }

        private sealed class StandInLog : IModApiLog
        {
            public void Info(string message) => FixControl.Log.LogInfo("[ModApi] " + message);

            public void Warning(string message) => FixControl.Log.LogWarning("[ModApi] " + message);

            public void Error(string message) => FixControl.Log.LogError("[ModApi] " + message);
        }

        private sealed class StandInLifecycle : ILifecycle
        {
            private StandInSession? _current;

            public IGameSession? Current => _current;

            public event Action<IGameSession>? SessionStarted;

            public event Action<IGameSession>? SessionEnding;

            public event Action<IGameSession, Milestone>? MilestoneReached;

            public void Start(SessionKind kind)
            {
                if (_current != null)
                {
                    _current.Alive = false;
                    SessionEnding?.Invoke(_current);
                }

                _current = new StandInSession(kind);
                SessionStarted?.Invoke(_current);
            }

            public void Reach(Milestone milestone)
            {
                if (_current == null)
                {
                    Start(SessionKind.Campaign);
                }

                _current!.Reached.Add(milestone);
                MilestoneReached?.Invoke(_current, milestone);
            }
        }

        private sealed class StandInSession : IGameSession
        {
            public StandInSession(SessionKind kind) => Kind = kind;

            public SessionKind Kind { get; }

            public bool Alive { get; set; } = true;

            public bool IsAlive => Alive;

            public HashSet<Milestone> Reached { get; } = new HashSet<Milestone>();

            public bool HasReached(Milestone milestone) => Reached.Contains(milestone);
        }
    }

    /// <summary>Session events for engine-tier tests, raised through the stand-in host.</summary>
    public static class Session
    {
        /// <summary>A new session: fixes holding per-session state reset, as after a load.</summary>
        public static void Start(SessionKind kind = SessionKind.Campaign) => StandInHost.Current.StartSession(kind);

        public static void Reach(Milestone milestone) => StandInHost.Current.Reach(milestone);
    }
}
