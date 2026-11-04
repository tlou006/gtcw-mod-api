using System;
using System.Globalization;

namespace GTCW.Harness.Runner
{
    internal sealed class Options
    {
        public const string Usage =
@"GTCW.Harness.Runner <command> [options]

commands:
  engine     no-engine tier: the game's code against stubbed Unity (seconds)
  campaign   headless campaign tier: the real game, windowless (minutes)
  battle     headless battle tier: the real game, windowless (minutes)
  all        engine, then campaign, then battle
  stubs      only (re)build the engine runtime under tests/obj/EngineRuntime
  clean      delete every sandbox and the engine data copy (junction-safe)

options:
  --filter <text>         only tests whose name or fix id contains text
  --sabotage <GTCW-NNN>   keep that fix off whatever tests ask (proves tests notice)
  --parallel <n>          game processes at once (default 2)
  --timeout <seconds>     per game process (default 900)
  --no-build              skip building the harness and the mods first
  --configuration <c>     Debug (default) or Release
  --game-dir <path>       install to run from (overrides HarnessGameDir)
  --harness-dir <path>    sandbox root (overrides HarnessDir)";

        public string Command { get; private set; } = string.Empty;

        public string? Filter { get; private set; }

        public string? Sabotage { get; private set; }

        public int Parallel { get; private set; } = 2;

        public int TimeoutSeconds { get; private set; } = 900;

        public bool NoBuild { get; private set; }

        public string Configuration { get; private set; } = "Debug";

        public string? GameDir { get; private set; }

        public string? HarnessGameDir { get; private set; }

        public string? HarnessDir { get; private set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new HarnessException($"{a} needs a value.\n\n{Usage}");
                switch (a)
                {
                    case "--filter": options.Filter = Next(); break;
                    case "--sabotage": options.Sabotage = Next(); break;
                    case "--parallel": options.Parallel = Math.Max(1, int.Parse(Next(), CultureInfo.InvariantCulture)); break;
                    case "--timeout": options.TimeoutSeconds = Math.Max(60, int.Parse(Next(), CultureInfo.InvariantCulture)); break;
                    case "--no-build": options.NoBuild = true; break;
                    case "--configuration": options.Configuration = Next(); break;
                    case "--compile-dir": options.GameDir = Next(); break;
                    case "--game-dir": options.HarnessGameDir = Next(); break;
                    case "--harness-dir": options.HarnessDir = Next(); break;
                    case "-h":
                    case "--help":
                        options.Command = "help";
                        break;
                    default:
                        if (a.StartsWith("-", StringComparison.Ordinal) || options.Command.Length > 0)
                        {
                            throw new HarnessException($"unexpected argument '{a}'.\n\n{Usage}");
                        }

                        options.Command = a;
                        break;
                }
            }

            if (options.Command.Length == 0)
            {
                options.Command = "help";
            }

            return options;
        }
    }
}
