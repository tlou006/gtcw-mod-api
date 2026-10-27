using BepInEx;
using GTCW.ModApi;

namespace HelloWorldMod
{
    /// <summary>
    /// The smallest useful mod: reports what the library can do on this build,
    /// then logs each milestone of the next campaign or battle load.
    /// </summary>
    [BepInPlugin("gtcw.sample.helloworld", "Hello World", "0.1.0")]
    [BepInDependency("gtcw.modapi", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class HelloWorld : BaseUnityPlugin
    {
        private void Awake()
        {
            // Registration happens here, at plugin load — not in response to a
            // game event. The game applies policy effects at boot gate 8, long
            // before any session event a mod can see, so anything registered
            // later is already too late.

            if (!ModApi.IsInitialised)
            {
                Logger.LogError("GTCW.ModApi did not load; nothing to do.");
                return;
            }

            // Branch on capabilities rather than assuming. This is what keeps a
            // mod alive across a game patch that breaks one feature.
            if (ModApi.Capabilities.Has(Capability.Lifecycle))
            {
                ModApi.Lifecycle.SessionStarted += session =>
                    Logger.LogInfo($"Session started: {session.Kind}");

                ModApi.Lifecycle.MilestoneReached += (session, milestone) =>
                    Logger.LogInfo($"{session.Kind} reached {milestone}");

                ModApi.Lifecycle.SessionEnding += session =>
                    Logger.LogInfo($"Session ending: {session.Kind}");
            }
            else
            {
                Logger.LogWarning(
                    "Lifecycle unavailable: " +
                    ModApi.Capabilities.Explain(Capability.Lifecycle));
            }

            if (ModApi.Capabilities.Has(Capability.ProjectRegistration))
            {
                ModApi.Projects.Register("helloworld.testing-project", p =>
                {
                    p.Name = "Testing Project";
                    p.Sides = Side.Both;
                    p.Category = ProjectCategory.Politics;

                    // Free and repeatable on purpose. This is a test fixture:
                    // it should be appointable the moment a campaign starts,
                    // without waiting for a subsidy pot to fill, and again
                    // afterwards so the level path can be exercised.
                    p.FirstLevelCost = 0f;
                    p.Repeating = true;

                    p.Scenarios.Add("001");
                    p.Scenarios.Add("002");

                    p.Description =
                        "GTCW.ModApi test fixture. Free, repeatable, and on appointment it " +
                        "grants a deliberately enormous support contribution so the write path " +
                        "can be seen working in seconds. Not a balanced project.";
                    p.EffectText = "+10 support per half game day, in every state.";

                    // Points at a sprite the game already has loaded, in the
                    // same form its own records use ("folder/name"). Left unset,
                    // the entry simply has no icon, which is harmless -- so this
                    // line is what demonstrates the field, not what makes the
                    // project work.
                    p.ImagePathSmall = "UI/ProjectImages/0_s";

                    p.Completed += ctx =>
                        Logger.LogInfo($"Project appointed by {ctx.Side} (level {ctx.Level}).");

                    // What makes the project worth having: appointing it buys a
                    // standing support contribution. Registered on completion,
                    // so it exists only for a side that actually appointed it.
                    p.Completed += ctx =>
                    {
                        if (!ModApi.Capabilities.Has(Capability.Modifiers))
                        {
                            return;
                        }

                        // Deliberately absurd, and the number is derived rather
                        // than guessed. The fold runs once per half game day
                        // per side, so dt = 0.5/365 years; a rate of 73/year is
                        // therefore exactly 0.1 of support per fold -- ten
                        // percentage points. Support saturates from zero in ten
                        // folds, which at top campaign speed (a game day is
                        // ~3 real seconds) is about fifteen seconds.
                        //
                        // A real mod would use something nearer 0.005f.
                        ModApi.Modifiers.Support.Add(
                            $"helloworld.testing.{ctx.Side}",
                            ctx.Side,
                            c =>
                            {
                                // One line per fold, not per state: the rate
                                // callback runs once for every state, so
                                // logging unconditionally would be ~50 lines a
                                // beat. State 0 stands in for the pass.
                                if (c.StateIndex == 0)
                                {
                                    Logger.LogInfo(
                                        $"fold: {c.Side} support in state 0 = " +
                                        $"{c.CurrentSupport:P1} (dt {c.DeltaYears:F5} yr)");
                                }

                                return 73f;
                            });

                        // Treasury, in currency per game year. Large enough to
                        // see in the finance panel within seconds.
                        ModApi.Modifiers.Treasury.Add(
                            $"helloworld.testing.treasury.{ctx.Side}",
                            ctx.Side,
                            _ => 100_000_000f);

                        // National morale is the only loss condition, so this
                        // one stays deliberately gentle even in a test fixture:
                        // +1 point per game year, which is visible over a few
                        // game months rather than seconds.
                        ModApi.Modifiers.NationalMorale.Add(
                            $"helloworld.testing.morale.{ctx.Side}",
                            ctx.Side,
                            _ => 0.01f);

                        Logger.LogInfo(
                            $"Contributions registered for {ctx.Side}: support, treasury, morale.");
                    };

                    // Replayed on load and on every appointment, so this must
                    // not accumulate anything.
                    p.Refreshed += ctx =>
                        Logger.LogInfo($"Project effects refreshed for {ctx.Side}.");
                });

                Logger.LogInfo($"Project registered; {ModApi.Projects.RemainingSlots} slots left.");
            }
            else
            {
                Logger.LogWarning(
                    "Project registration unavailable: " +
                    ModApi.Capabilities.Explain(Capability.ProjectRegistration));
            }

            if (ModApi.Capabilities.Has(Capability.BattleEvents))
            {
                ModApi.Battle.Started += session =>
                    Logger.LogInfo("Battle started — a mod is running inside the game.");

                ModApi.Battle.Ended += session =>
                    Logger.LogInfo("Battle ended.");
            }
            else
            {
                Logger.LogWarning(
                    "Battle events unavailable: " +
                    ModApi.Capabilities.Explain(Capability.BattleEvents));
            }
        }
    }
}
