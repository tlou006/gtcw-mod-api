using System;

namespace GTCW.Harness
{
    /// <summary>The three places a test can run.</summary>
    public enum Tier
    {
        /// <summary>No Unity engine: the game's code against stubbed Unity, in a plain .NET Framework process.</summary>
        Engine,

        /// <summary>The real game, windowless, with a campaign loaded through the menu's own code.</summary>
        Campaign,

        /// <summary>The real game, windowless, with a standalone battle loaded through the menu's own code.</summary>
        Battle,
    }

    /// <summary>
    /// The register ids a test class covers. The coverage check fails every
    /// catalogued fix that no class names here, or in <see cref="ManualOnlyAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class FixTestsAttribute : Attribute
    {
        public FixTestsAttribute(params string[] ids) => Ids = ids;

        public string[] Ids { get; }
    }

    /// <summary>
    /// A fix the harness cannot exercise, and why. Counts for coverage so the
    /// gap is written down rather than silently absent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ManualOnlyAttribute : Attribute
    {
        public ManualOnlyAttribute(string id, string reason)
        {
            Id = id;
            Reason = reason;
        }

        public string Id { get; }

        public string Reason { get; }
    }

    /// <summary>Base for the per-tier method attributes.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class TierTestAttribute : Attribute
    {
        protected TierTestAttribute(Tier tier) => Tier = tier;

        public Tier Tier { get; }
    }

    /// <summary>
    /// Runs with no engine. A <c>void</c> method; shipped data is reloaded
    /// before it and before each phase of <see cref="Verify.Fix{T}"/>.
    /// </summary>
    public sealed class EngineTestAttribute : TierTestAttribute
    {
        public EngineTestAttribute() : base(Tier.Engine)
        {
        }
    }

    /// <summary>
    /// Runs inside a live headless campaign. An <c>IEnumerator</c> method run as
    /// a coroutine; the class's <see cref="CampaignAttribute"/> picks the scenario.
    /// </summary>
    public sealed class CampaignTestAttribute : TierTestAttribute
    {
        public CampaignTestAttribute() : base(Tier.Campaign)
        {
        }
    }

    /// <summary>
    /// Runs inside a live headless battle. An <c>IEnumerator</c> method run as a
    /// coroutine; the class's <see cref="BattleAttribute"/> picks the scenario.
    /// </summary>
    public sealed class BattleTestAttribute : TierTestAttribute
    {
        public BattleTestAttribute() : base(Tier.Battle)
        {
        }
    }

    /// <summary>
    /// The campaign a class's campaign tests share. One game process boots it
    /// once and runs every campaign test in the class against it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CampaignAttribute : Attribute
    {
        public CampaignAttribute(string map = "001", string setup = "A", int side = 0)
        {
            Map = map;
            Setup = setup;
            Side = side;
        }

        public string Map { get; }

        public string Setup { get; }

        /// <summary>0 Union, 1 Confederacy.</summary>
        public int Side { get; }
    }

    /// <summary>
    /// The standalone battle a class's battle tests share. Null map and setup
    /// take the first battle the menu lists.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BattleAttribute : Attribute
    {
        public BattleAttribute(string? map = null, string? setup = null, int side = 0)
        {
            Map = map;
            Setup = setup;
            Side = side;
        }

        public string? Map { get; }

        public string? Setup { get; }

        public int Side { get; }
    }
}
