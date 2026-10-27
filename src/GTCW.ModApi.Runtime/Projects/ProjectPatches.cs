using System;
using System.Collections.Generic;
using System.Reflection;
using GTCW.ModApi;
using GTCW.ModApi.Runtime.Capabilities;
using HarmonyLib;

// The game's types live in the global namespace, and this file's own namespace
// is `...Runtime.Projects`, which shadows the game's `Projects` class outright.
// Alias it explicitly. Any future subsystem namespace named after a game type
// will need the same, which is a good reason to reach for the alias rather than
// rename the game's concept.
using GameProjects = global::Projects;

namespace GTCW.ModApi.Runtime.Projects
{
    /// <summary>
    /// Makes registered projects real: claims a shipped <c>PLACEHOLDER</c> slot
    /// for each one, then routes the game's two effect dispatchers back to the
    /// mod that registered it.
    /// </summary>
    /// <remarks>
    /// Neither dispatcher has a <c>default:</c> arm, so an id the game does not
    /// know falls straight through and does nothing. A postfix supplies the
    /// behaviour and the game's own arms are untouched — no transpiler, and
    /// none of the existing effects reimplemented.
    /// </remarks>
    internal static class ProjectPatches
    {
        /// <summary>The publisher's marker for an unused project record.</summary>
        private const string PlaceholderName = "PLACEHOLDER";

        private static ProjectRegistry? _registry;
        private static IModApiLog? _log;

        public static ProbeResult Probe() => GameProbe.All(
            () => GameProbe.RequireType("Projects+LoadedProjects"),
            () => GameProbe.RequireMethod("Projects+LoadedProjects", "Import"),
            () => GameProbe.RequireMethod("Projects", "UpdateOneTimeProjectsEffects"),
            () => GameProbe.RequireMethod("Projects", "UpdateProjectEffects"),
            () => GameProbe.RequireField("GameVars", "alliance"));

        public static void Apply(Harmony harmony, ProjectRegistry registry, IModApiLog log)
        {
            _registry = registry;
            _log = log;

            Patch(harmony, "Projects+LoadedProjects:Import", postfix: nameof(AfterImport));
            Patch(harmony, "Projects:UpdateOneTimeProjectsEffects", postfix: nameof(AfterOneTimeEffects));
            Patch(harmony, "Projects:UpdateProjectEffects", postfix: nameof(AfterProjectEffects));
        }

        private static void Patch(Harmony harmony, string target, string postfix)
        {
            MethodInfo original = AccessTools.Method(target)
                ?? throw new InvalidOperationException($"'{target}' not found");

            harmony.Patch(
                original,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ProjectPatches), postfix)));
        }

        // --- claiming slots -----------------------------------------------------

        /// <summary>
        /// Rewrites one placeholder record per registered project. Runs on every
        /// campaign load, because the game re-imports the table each time.
        /// </summary>
        private static void AfterImport(List<GameProjects.LoadedProjects> __result)
        {
            ProjectRegistry? registry = _registry;
            IModApiLog? log = _log;
            if (registry == null || log == null || __result == null)
            {
                return;
            }

            // Detect placeholders by the publisher's own marker rather than by a
            // hardcoded id range, so the pool tracks the shipped data if a patch
            // adds or spends one.
            var free = new List<GameProjects.LoadedProjects>();
            foreach (GameProjects.LoadedProjects record in __result)
            {
                if (string.Equals(record.projectname, PlaceholderName, StringComparison.Ordinal))
                {
                    free.Add(record);
                }
            }

            registry.NoteFreeSlots(free.Count);

            if (!registry.HasAny)
            {
                registry.Close();
                return;
            }

            registry.ResetAssignments();

            int next = 0;
            foreach (ProjectDefinition def in registry.InAllocationOrder())
            {
                if (next >= free.Count)
                {
                    log.Error(
                        $"Out of project slots: the shipped table has {free.Count} free and more " +
                        $"were registered. '{def.Key}' and any after it are not installed.");
                    break;
                }

                GameProjects.LoadedProjects slot = free[next++];

                // Assign the id only if the fill succeeded. A half-written slot
                // with a live id would be worse than no project: it would be
                // appointable and its effects would fire against fields the mod
                // never finished setting.
                if (FailSoft.Guard(log, $"project '{def.Key}'", () => Fill(slot, def)))
                {
                    registry.AssignId(def, slot.projectid);
                }
                else
                {
                    slot.projectname = PlaceholderName;
                    slot.alliances = new List<int>();
                    log.Error($"Project '{def.Key}' failed to install; its slot was returned to placeholder.");
                }
            }

            registry.Close();
        }

        /// <summary>
        /// Overwrites a placeholder's fields in place. The id is left alone:
        /// it is the slot's identity and the table's index invariant depends
        /// on it.
        /// </summary>
        private static void Fill(GameProjects.LoadedProjects slot, ProjectDefinition def)
        {
            slot.projectname = def.Name;
            slot.alliances = def.AllianceIndices();
            slot.subsidytype = (int)def.Category;
            slot.subsidycostfirstlevel = def.FirstLevelCost;
            slot.repeating = def.Repeating;
            slot.usedgroup = (int)def.Column;
            slot.prosadescr = def.Description;
            slot.effectsdescr = def.EffectText;

            if (def.ImagePathSmall != null)
            {
                slot.imagepath_small = def.ImagePathSmall;
            }

            if (def.ImagePathBig != null)
            {
                slot.imagepath_big = def.ImagePathBig;
            }

            if (def.Scenarios.Count > 0)
            {
                slot.applicablescenarios = new List<string>(def.Scenarios);
            }

            // datefrom is deliberately left as the placeholder's empty pair. A
            // non-null date makes CheckProjectUnlocks grant the project free at
            // new-campaign setup, which is not what "available from" sounds
            // like and is not something a mod should get by accident.
            slot.requiredpolicyids ??= new List<int>();
            slot.requireddlcids ??= new List<int>();
        }

        // --- effects ------------------------------------------------------------

        /// <summary>One-shot, on appointment. Fires once per side.</summary>
        private static void AfterOneTimeEffects(int projectid, int alliance)
        {
            ProjectDefinition? def = _registry?.ById(projectid);
            if (def == null || _log == null)
            {
                return;
            }

            Action<IProjectEffectContext>? handlers = def.CompletedHandlers;
            if (handlers == null)
            {
                return;
            }

            Dispatch(handlers, def, alliance, "Completed");
        }

        /// <summary>
        /// Replayed. The game's own method walks every alliance's project list,
        /// so this walks it again to find ours; there is no argument to filter
        /// on.
        /// </summary>
        private static void AfterProjectEffects()
        {
            ProjectRegistry? registry = _registry;
            if (registry == null || _log == null || !registry.HasAny)
            {
                return;
            }

            GameVars.Alliance[] alliances = GameVars.alliance;
            if (alliances == null)
            {
                return;
            }

            for (int a = 0; a < alliances.Length; a++)
            {
                List<int>? projects = alliances[a]?.projects;
                if (projects == null)
                {
                    continue;
                }

                // Distinct ids only: a repeating project appears once per level,
                // and an effect that fired per level would scale with level
                // twice over.
                var seen = new HashSet<int>();
                for (int i = 0; i < projects.Count; i++)
                {
                    int id = projects[i];
                    if (!seen.Add(id))
                    {
                        continue;
                    }

                    ProjectDefinition? def = registry.ById(id);
                    Action<IProjectEffectContext>? handlers = def?.RefreshedHandlers;
                    if (def != null && handlers != null)
                    {
                        Dispatch(handlers, def, a, "Refreshed");
                    }
                }
            }
        }

        private static void Dispatch(
            Action<IProjectEffectContext> handlers,
            ProjectDefinition def,
            int alliance,
            string eventName)
        {
            IModApiLog log = _log!;
            var ctx = new ProjectEffectContext(def.Key, def.ProjectId, alliance, LevelOf(def, alliance));

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                string owner = handler.Method.DeclaringType?.Assembly.GetName().Name ?? "unknown";
                string who = $"{owner}.{handler.Method.Name} on {def.Key}.{eventName}";

                if (FailSoft.IsBanned(who))
                {
                    continue;
                }

                FailSoft.Guard(log, who, () => ((Action<IProjectEffectContext>)handler)(ctx));
            }
        }

        /// <summary>
        /// How many times this side has appointed the project. Counted the same
        /// way the game counts it, by occurrences in the alliance's list.
        /// </summary>
        private static int LevelOf(ProjectDefinition def, int alliance)
        {
            List<int>? projects = GameVars.alliance?[alliance]?.projects;
            if (projects == null)
            {
                return 0;
            }

            int level = 0;
            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] == def.ProjectId)
                {
                    level++;
                }
            }

            return level;
        }
    }
}
