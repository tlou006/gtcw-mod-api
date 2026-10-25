using System.Collections.Generic;
using GTCW.ModApi;

namespace GTCW.ModApi.Runtime.Lifecycle
{
    /// <summary>
    /// Maps the game's boot gates onto named milestones, per session kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The controllers' <c>Update</c> methods are boot state machines, not
    /// simulation: <c>CampaignController</c> has 55 <c>if (frame == N)</c>
    /// gates and <c>BattleController</c> 48, spanning frames 1–51. This table
    /// is the subset worth exposing, read out of those gates — the frame
    /// numbers stay in here and never reach a mod.
    /// </para>
    /// <para>
    /// Two gates do their work only on certain <c>currentoperation</c> values
    /// (frame 25 loads saved objects only when loading a save; the first frame-30
    /// gate skips the save paths). The milestone therefore means <i>the boot
    /// sequence has passed this point</i>, which is uniform, rather than
    /// <i>this specific work happened</i>, which is not.
    /// </para>
    /// </remarks>
    internal static class BootMilestones
    {
        /// <summary>
        /// Campaign gates. Frame 4 rather than 3 for the map: the sequence
        /// stalls by resetting the counter to 3 while <c>BFS.initialized &lt; 3</c>,
        /// so observing 4 from a postfix is the proof that the stall has
        /// released.
        /// </summary>
        public static readonly (int Frame, Milestone Milestone)[] Campaign =
        {
            (4, Milestone.MapReady),
            (8, Milestone.PoliciesReady),
            (12, Milestone.UnitsReady),
            (25, Milestone.SubsystemsLoaded),
            (30, Milestone.CampaignLive),
            (37, Milestone.ProjectsReady),
            (50, Milestone.CampaignIdle),
        };

        /// <summary>
        /// Battle gates. Frames 11–12 are the same regiment initialization as
        /// the campaign; 30 allocates and places units; 40 is the last gate.
        /// </summary>
        public static readonly (int Frame, Milestone Milestone)[] Battle =
        {
            (4, Milestone.MapReady),
            (12, Milestone.UnitsReady),
            (25, Milestone.SubsystemsLoaded),
            (30, Milestone.BattleDeployed),
            (40, Milestone.BattleIdle),
        };

        public static IReadOnlyList<(int Frame, Milestone Milestone)> For(SessionKind kind) =>
            kind == SessionKind.Campaign ? Campaign : Battle;
    }
}
