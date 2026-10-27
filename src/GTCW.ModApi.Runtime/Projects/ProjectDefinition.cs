using System;
using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Projects
{
    /// <summary>A mod's project definition, plus the id the broker gave it.</summary>
    internal sealed class ProjectDefinition : IProjectDefinition
    {
        public ProjectDefinition(string key) => Key = key;

        public string Key { get; }

        public string Name { get; set; } = string.Empty;
        public Side Sides { get; set; } = Side.None;
        public ProjectCategory Category { get; set; } = ProjectCategory.Politics;
        public float FirstLevelCost { get; set; }
        public bool Repeating { get; set; }
        public string Description { get; set; } = string.Empty;
        public string EffectText { get; set; } = string.Empty;
        public PanelColumn Column { get; set; } = PanelColumn.Left;
        public IList<string> Scenarios { get; } = new List<string>();
        public string? ImagePathSmall { get; set; }
        public string? ImagePathBig { get; set; }

        public event Action<IProjectEffectContext>? Completed;
        public event Action<IProjectEffectContext>? Refreshed;

        /// <summary>Assigned at the first campaign load; -1 until then.</summary>
        public int ProjectId { get; set; } = -1;

        internal Action<IProjectEffectContext>? CompletedHandlers => Completed;
        internal Action<IProjectEffectContext>? RefreshedHandlers => Refreshed;

        /// <summary>
        /// Rejects definitions the game would accept and then quietly ignore.
        /// Better a throw at registration than a project the player can never
        /// see and nobody can explain.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentException($"Project '{Key}' has no Name.");
            }

            if (Sides == Side.None)
            {
                throw new ArgumentException(
                    $"Project '{Key}' names no side. The panel only builds entries whose " +
                    "alliance list contains the player, so this would never appear.");
            }

            if (FirstLevelCost < 0f)
            {
                throw new ArgumentException($"Project '{Key}' has a negative FirstLevelCost.");
            }
        }

        /// <summary>Alliance indices, as the game's own record stores them.</summary>
        public List<int> AllianceIndices()
        {
            var list = new List<int>(2);
            if ((Sides & Side.Union) != 0)
            {
                list.Add(0);
            }

            if ((Sides & Side.Confederacy) != 0)
            {
                list.Add(1);
            }

            return list;
        }
    }

    /// <summary>Immutable context handed to an effect callback.</summary>
    internal sealed class ProjectEffectContext : IProjectEffectContext
    {
        public ProjectEffectContext(string key, int projectId, int alliance, int level)
        {
            Key = key;
            ProjectId = projectId;
            Alliance = alliance;
            Level = level;
        }

        public string Key { get; }
        public int ProjectId { get; }
        public int Alliance { get; }
        public int Level { get; }

        public Side Side => Alliance switch
        {
            0 => Side.Union,
            1 => Side.Confederacy,
            _ => Side.None,
        };
    }
}
