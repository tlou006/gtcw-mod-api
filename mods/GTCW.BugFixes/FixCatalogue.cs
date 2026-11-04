using System.Collections.Generic;

namespace GTCW.BugFixes
{
    /// <summary>
    /// Every fix group this mod ships, in register order.
    /// </summary>
    /// <remarks>
    /// One list, read by the plugin and by the test harness in <c>tests/</c>, so
    /// a group that is registered is a group that is tested: the harness fails
    /// any id here that has no test. Adding a fix means one line here and a test
    /// file there.
    /// </remarks>
    internal static class FixCatalogue
    {
        public static IReadOnlyList<FixGroup> Groups() => new[]
        {
            // Wave 1. One line per patched method; a method with several fixes
            // on it appears once, because the patch is shared (see FixGroup).
            // IMPLEMENTATION.md has the remaining three waves.
            Fixes.ElectionDoubleCharge.Group(),   // GTCW-001
            Fixes.SupportBreakdownNaN.Group(),    // GTCW-002
            Fixes.ConfiscationActSupply.Group(),  // GTCW-003
            Fixes.NavalFiringStrength.Group(),    // GTCW-004, GTCW-006
            Fixes.RaidLedgerSign.Group(),         // GTCW-014

            // Wave 5 — the perks. Every one of these answers to the
            // `perksasdocumented` family switch as well as to its own id, and
            // all of them are off until one of the two is turned on.
            Fixes.MoralePerks.Group(),            // GTCW-024, 025, 027
            Fixes.MountedRiflesMalus.Group(),     // GTCW-028
            Fixes.AttritionPerkNamespace.Group(), // GTCW-035, 036
            Fixes.RaidingPerkDirection.Group(),   // GTCW-038, 039
            Fixes.EmbeddedReporters.Group(),      // GTCW-041
        };
    }
}
