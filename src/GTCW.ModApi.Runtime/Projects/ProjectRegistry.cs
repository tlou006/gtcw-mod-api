using System;
using System.Collections.Generic;
using System.Linq;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Projects
{
    /// <summary>
    /// Holds mod project definitions and brokers their numeric ids.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game keys projects off a bare <c>int</c>, and the only ids available
    /// are the slots the publisher shipped as <c>PLACEHOLDER</c> records. Mods
    /// name a string key; this decides which slot each one gets.
    /// </para>
    /// <para>
    /// <b>Why slots are claimed rather than appended.</b> The shipped table has
    /// <c>projectid == list index</c> for all 125 records, and
    /// <c>IsProjectAvailableInScenario</c> exploits that with a direct
    /// <c>loadedprojects[projectid]</c> before falling back to a search.
    /// Appending records with ids past the end would leave that fast path
    /// indexing a record that is not the one asked for, and an id at or past
    /// <c>Count</c> would throw outright. Overwriting a placeholder preserves
    /// the invariant and the record count.
    /// </para>
    /// <para>
    /// Allocation is ordered by key, so a fixed set of mods always produces the
    /// same assignment. It is <b>not</b> stable when the mod set changes, which
    /// is why a save that references a mod project needs the id mapping
    /// persisted alongside it — the Persistence capability, not yet built.
    /// Until then the mapping is logged at every load so a mismatch is at least
    /// diagnosable.
    /// </para>
    /// </remarks>
    internal sealed class ProjectRegistry : IProjectRegistry
    {
        private readonly IModApiLog _log;
        private readonly Dictionary<string, ProjectDefinition> _byKey =
            new Dictionary<string, ProjectDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, ProjectDefinition> _byId =
            new Dictionary<int, ProjectDefinition>();

        /// <summary>Set once the game has read its table and ids are fixed.</summary>
        private bool _closed;

        /// <summary>Free slots found in the shipped table, in id order.</summary>
        private int _freeSlots;

        public ProjectRegistry(IModApiLog log) => _log = log;

        public int RemainingSlots => Math.Max(0, _freeSlots - _byKey.Count);

        public int IdOf(string key) =>
            _byKey.TryGetValue(key, out ProjectDefinition? def) ? def.ProjectId : -1;

        public void Register(string key, Action<IProjectDefinition> configure)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A project key is required.", nameof(key));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (_closed)
            {
                throw new InvalidOperationException(
                    $"Project registration is closed: the game has already read its project " +
                    $"table, so '{key}' would never appear. Register while your plugin loads, " +
                    "not in response to a game event.");
            }

            if (_byKey.ContainsKey(key))
            {
                throw new ArgumentException($"Project '{key}' is already registered.", nameof(key));
            }

            var def = new ProjectDefinition(key);
            configure(def);
            def.Validate();

            _byKey.Add(key, def);
        }

        /// <summary>
        /// Definitions in allocation order. Ordinal by key so the result does
        /// not depend on which mod happened to load first.
        /// </summary>
        public IEnumerable<ProjectDefinition> InAllocationOrder() =>
            _byKey.Values.OrderBy(d => d.Key, StringComparer.Ordinal);

        /// <summary>Called by the import patch once it knows the real slot count.</summary>
        public void NoteFreeSlots(int count) => _freeSlots = count;

        /// <summary>Records the slot a definition was given.</summary>
        public void AssignId(ProjectDefinition def, int id)
        {
            def.ProjectId = id;
            _byId[id] = def;
        }

        /// <summary>Closes registration and logs the mapping.</summary>
        public void Close()
        {
            _closed = true;

            if (_byKey.Count == 0)
            {
                return;
            }

            foreach (ProjectDefinition def in InAllocationOrder())
            {
                _log.Info($"Project '{def.Key}' -> id {def.ProjectId} (\"{def.Name}\")");
            }
        }

        /// <summary>A registered project by id, or null if the id is not ours.</summary>
        public ProjectDefinition? ById(int id) =>
            _byId.TryGetValue(id, out ProjectDefinition? def) ? def : null;

        public bool HasAny => _byKey.Count > 0;

        /// <summary>
        /// Forgets the id assignments without forgetting the definitions, so a
        /// second campaign load re-runs allocation against a freshly imported
        /// table rather than trusting ids from the previous session.
        /// </summary>
        public void ResetAssignments()
        {
            _byId.Clear();
            foreach (ProjectDefinition def in _byKey.Values)
            {
                def.ProjectId = -1;
            }
        }
    }
}
