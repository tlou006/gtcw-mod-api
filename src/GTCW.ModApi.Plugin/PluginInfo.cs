namespace GTCW.ModApi.Plugin
{
    /// <summary>
    /// Identity of the BepInEx plugin. Mods declare a dependency on
    /// <see cref="Guid"/>, so it is part of the public contract and must not
    /// change without a major version bump.
    /// </summary>
    public static class PluginInfo
    {
        public const string Guid = "gtcw.modapi";
        public const string Name = "GTCW ModApi";

        /// <summary>
        /// Semantic version, enforced strictly: mods depend on this, and
        /// breaking the public API without a major bump breaks the ecosystem.
        /// </summary>
        public const string Version = "0.1.0";
    }
}
