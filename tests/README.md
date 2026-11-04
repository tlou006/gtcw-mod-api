# `tests/` — the test harness for GTCW.BugFixes

Tests every fix in [`mods/GTCW.BugFixes`](../mods/GTCW.BugFixes/) without anyone
playing the game, in three tiers:

| Tier | What runs | Time | For |
| --- | --- | --- | --- |
| **engine** | The game's code and shipped data in a plain .NET Framework 4.8 process, against Unity with every native method stubbed | seconds | Fixes that are arithmetic on game state |
| **campaign** | The real game, windowless (`-batchmode -nographics`), with a campaign started through the menu's own code | ~1 min per test class | Anything needing live units, the scheduler, the map |
| **battle** | The same, with a standalone battle and the AI commanding both sides | ~1–2 min per test class | Battle mechanics: morale, firing, reloading |

Every fix test follows `IMPLEMENTATION.md` §7 automatically: the same scenario
runs with the fix **off** (the defect must show), **on** (it must not), and
**off again** with the patch still applied (it must show again).

## Running it

```powershell
dotnet run --project tests/GTCW.Harness.Runner -- engine     # or campaign, battle, all
dotnet run --project tests/GTCW.Harness.Runner -- campaign --filter GTCW-001
dotnet run --project tests/GTCW.Harness.Runner -- help
```

The runner builds what it needs first (`--no-build` skips that), prints a
summary, writes JUnit XML to `TestResults/<command>.xml`, and keeps every
game's evidence — results, `player.log`, the BepInEx log — under
`%LOCALAPPDATA%\gtcw-harness\runs\<timestamp>\`. Exit code 0 means everything
passed, 1 that something failed, 2 that the harness could not run.

| Option | |
| --- | --- |
| `--filter <text>` | Only tests whose name or fix id contains the text |
| `--sabotage <GTCW-NNN>` | Keep that fix off whatever the tests ask — every test of it must then fail. The check that the tests notice a fix doing nothing |
| `--parallel <n>` | Game processes at once (default 2) |
| `--timeout <s>` | Per game process (default 900) |
| `clean` | Delete every sandbox and the engine data copy, junction-first |

### Configuration

Two paths in your gitignored `game.props` (see `game.props.example`):

- `HarnessGameDir` — an install **with BepInEx 5 (x64, Mono)**. Only ever read.
  The repo's `game/` copy cannot run (no BepInEx, partial assets), so on a typical
  setup this points at the Steam install while `GameDir` points at the copy.
- `HarnessDir` — where sandboxes and logs go; default `%LOCALAPPDATA%\gtcw-harness`.

`GTCW_HARNESS_GAME_DIR` and `GTCW_HARNESS_DIR` override both. The runner refuses
to start if the build's `Assembly-CSharp` and the install's differ (SHA-256).

## What it never does

- **Write to the install.** Every game runs in a sandbox: the 16 GB of assets are
  junctions, everything the game writes is a fresh copy mirrored before each
  launch. The runner snapshots the install before and after and fails the run
  (`Harness.InstallUntouched`) if anything changed.
- **Sit inside the repo or the game folder.** The runner refuses such a
  `HarnessDir`: a `git clean` or a recursive delete through a junction would
  reach the install. Sandboxes are deleted junction-first, and deletion stops at
  the first problem.
- **Collide with your game.** The sandboxed exe is renamed (`gtcwtest.exe`), so
  `.claude/deploy.ps1`'s running-game check and your own install are unaffected.
- **Ship.** Nothing here is deployed by `deploy.ps1`. The engine tier's stubbed
  Unity lives under `tests/obj/EngineRuntime/` like the Publicizer's output, and
  no game assembly is ever copied into a `bin/`.

## Checks every run makes

| Check | Tier | Fails when |
| --- | --- | --- |
| `Harness.Coverage` | engine | A fix in `FixCatalogue` has no test (or `[ManualOnly]` reason) |
| `Harness.Inert` / `Game.Inert` | engine / campaign | Every fix Off, yet the mod patched something |
| `Harness.Defaults` | engine | A fresh config enables a fix not marked default-on, or misses one |
| `Harness.ApplyAll` / `Game.ApplyAll` | engine / game | A fix's probe fails on this build |
| `Harness.NetFrameworkApis` | engine | The harness calls a member .NET Framework lacks (see below) |
| `Game.Boot` | game | The game logged an unexpected error while loading the scenario |
| `Harness.InstallUntouched` | game | The install changed during the run |

A test also fails if the game logs an error or exception while it runs, or a
fix reports a throw through `FixState.Guard` — beyond two known baseline errors
every run of this build produces (`config/camp.dat` missing; no video shaders
in batch mode).

## Writing a test

One file per fix group in `GTCW.BugFixes.Tests`, holding its tests for every
tier it can be tested in:

```csharp
[FixTests("GTCW-002")]
[Campaign("001", "A")]            // scenario for this class's campaign tests
public sealed class SupportBreakdownNaNTests
{
    [EngineTest]                  // void; shipped data reloaded before each phase
    public void ALossLeavesARealNumber() =>
        Verify.Fix("GTCW-002",
            () => { var s = AState(); s.AdjustSupport(0, -0.05f, 5); return s.supportinfluencelast[0, 5]; },
            defect:   v => Expect.NaN(v, "the tracker after a loss"),
            repaired: v => Expect.Near(0f, v, "the tracker after a loss"));

    [CampaignTest]                // IEnumerator; runs as a coroutine in the live game
    public IEnumerator ... { yield return Live.NextCampaignPass(); yield return Verify.FixLive<float>(...); }
}
```

What the harness gives a test:

| | |
| --- | --- |
| `Verify.Fix` / `Verify.FixLive` | The off / on / off protocol. `heldOn:` keeps other fixes on in all three phases |
| `Toggle.Only` / `Toggle.AllOff` | For tests that are not an off/on/off comparison |
| `Expect.Near/Equal/True/Below/AtLeast/NaN/NonZero/Logged` | Assertions that name what they measured |
| `Report.Note` | A line attached to the result — put the measured numbers here |
| `Clock.Set` / `Clock.Now` | The campaign clock, through the game's own date arithmetic |
| `Fakes.Make<T>()` | A Unity object with no engine behind it (engine tier only) |
| `Session.Start` / `Session.Reach` | Lifecycle events through the stand-in ModApi host (engine tier) |
| `Live.NextCampaignPass` / `GameDays` / `Until` / `Seconds` / `Frames` | Waits, each with a time limit |
| `Live.JumpTo` | Move the campaign clock and wait out the passes that absorb the jump |
| `Live.HoldChapter` / `HoldSessionOpen` / `Speed` | Chapter gating, swallowing the end of a campaign, game speed |
| `Units.Formation/Brigades/MoveTo/SetPerks/AtWar/KeepFields` | Finding and rearranging live units; undone after every test |

Rules the harness learned the hard way:

- **Guard every ratio against a zero baseline** (`Expect.NonZero`). Zero makes
  "unchanged" and "scaled by the factor" the same claim, and both pass — two
  early tests here passed that way.
- **Assert the expected value, not just a difference.** A test that compared two
  zeros once passed because the prefs had never loaded.
- **Set every value the scenario reads.** The live session carries state from
  one phase to the next; only the engine tier reloads.
- **After `Live.JumpTo`, set state, never before.** The first pass after a jump
  sees the whole jump as elapsed time; in the spike it took Union morale to 1.0.
- **Pick the scenario for the units you need.** 001/A (February 1861) has no
  war yet and no field army on the map — only artillery garrisons are active;
  001/E (spring 1864) has Corps under Armies.
- **Reseed for randomness.** `UnityEngine.Random.InitState(seed)` before each
  measured call makes with- and without-perk calls draw identically.
- **Probe, don't apply.** A last-priority Harmony prefix that records a call's
  arguments and skips it (see `RaidingPerkDirectionTests`) measures what a round
  would do without changing any unit.

### Engine tier: .NET Framework, not Mono

The engine tier runs the netstandard2.1 harness on the .NET Framework 4.8 CLR
(the game's `Assembly-CSharp` targets mscorlib 4.0). Most of netstandard2.1
works; a few members do not exist there — `string.Split(char, ...)` is the one
that bit — and throw `MissingMethodException` at the first call. Some *types*
are a trap too: Unity's `netstandard.dll` forwards `Stack<T>` and `Queue<T>` to
mscorlib, where Unity's Mono keeps them and .NET Framework does not.
`Harness.NetFrameworkApis` resolves every framework member the harness, tests,
mod and library use before the run, exactly as the engine host will bind them,
and names any that are missing. Use `Split(new[] { ',' })`, and a `List<T>`
where you wanted a stack.

### Finding what a scenario contains

Write a throwaway `[CampaignTest]` that puts counts in `Report.Note` (units by
type, which are active, who sits under whom) and run it alone with `--filter`.
`Units.Formation` also describes the candidates it rejected when nothing matches.

## Layout

| Project | Target | |
| --- | --- | --- |
| `GTCW.Harness` | netstandard2.1 | Everything tests see; the engine tier's machinery; the in-game driver (`Game/GameRunner`) |
| `GTCW.BugFixes.Tests` | netstandard2.1 | The tests |
| `GTCW.Harness.EngineHost` | net48 exe | Points the loader at the engine runtime and the tests, then calls `GTCW.Harness.Engine.EngineEntry.Run` |
| `GTCW.Harness.GamePlugin` | BepInEx plugin | Starts `GameRunner` when — and only when — the runner launched the game |
| `GTCW.Harness.Runner` | net8.0 exe | The entry point: stubs, sandboxes, launching, results |

The mod exposes its fix list as `FixCatalogue.Groups()` and grants
`InternalsVisibleTo` to `GTCW.Harness` and the tests, so they can switch fixes
with `FixState`; the library grants it to `GTCW.Harness` for the engine tier's
stand-in host. Neither changes what ships.
