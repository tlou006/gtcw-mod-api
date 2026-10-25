# `src/` — the hook library

A BepInEx mod library for the game, so other modders can extend it without
reversing it themselves. Design and rationale live in
[`plan.md`](../plan.md) Phase 3 and Phase 4; this file is how to build it.

## The four projects

| Project | Tier | What it is |
| --- | --- | --- |
| `GTCW.ModApi` | *(no refs)* | **The public, stable API.** Events, interfaces, DTOs, the `Capability` enum. What a mod references. |
| `GTCW.ModApi.Runtime` | game | **The volatile layer.** Harmony patches, publicized game-type access, capability probes, the contribution fold, the id broker, the config pipeline. The only assembly that knows real game types. |
| `GTCW.ModApi.Schemas` | *(no refs)* | Typed models for the `Config/` data files, generated from the schemas in `code-findings/`. Schemas, never game data. |
| `GTCW.ModApi.Plugin` | game | The BepInEx entry point. Bootstrap, build identification, the capability report, logging. |
| `samples/HelloWorldMod` | unity | What a mod looks like from the outside. |

The point of the split is that **a game update must break only `Runtime`.**
`GTCW.ModApi` cannot reference `Assembly-CSharp` at all, so a game type leaking
into the public API is a build failure rather than a review miss — there is a
`ForbidGameReferences` target that says so by name.

## Building

```powershell
# once: point the build at your own install
Copy-Item game.props.example game.props    # then edit <GameDir>
#   or:  $env:GTCW_GAME_DIR = "D:\...\Grand Tactician The Civil War (1861-1865)"

dotnet build GTCW.ModApi.sln
```

`GTCW.ModApi` and `GTCW.ModApi.Schemas` build with no game installed. The other
three need `GameDir`; the build fails with a readable message if it is unset or
wrong, rather than with four hundred "type not found" errors.

**The game is never vendored.** `game.props` is gitignored, every reference is
`Private="false"`, and the publicized assembly stays in
`obj/PublicizedAssemblies/`. Nothing in any `bin/` is the publisher's.

## Running it in the game

1. Install BepInEx 5.x (x64, Mono) into the game folder.
2. Run the game once so BepInEx generates its folders.
3. Copy `GTCW.ModApi*.dll` from
   `src/GTCW.ModApi.Plugin/bin/Debug/netstandard2.1/` into `BepInEx/plugins/`.
4. Copy `HelloWorldMod.dll` from
   `samples/HelloWorldMod/bin/Debug/netstandard2.1/` into `BepInEx/plugins/`
   too — without it the log cannot show an event reaching a mod, and BepInEx
   will report `1 plugin to load` instead of 2.
5. Read `BepInEx/LogOutput.log`.

A good startup looks like this:

```
[Info   :   BepInEx] Supports SRE: True
[Info   :GTCW ModApi] Game build recognised: Steam buildid 14703111 (2026-10-13).
[Info   :GTCW ModApi] Anchor check: all 10 expected game types present.
[Info   :GTCW ModApi] Capabilities on: BattleEvents, Lifecycle
[Warning:GTCW ModApi] Capability off: ConfigOverrides — not implemented in this release — ...
```

`Supports SRE: True` is worth checking first: without runtime IL generation
Harmony cannot patch at all, and every capability would report off.

Capabilities still reporting "not implemented" are correct — those features are
Phase 4. What the log proves is that the plugin loads, the build is identified,
the type names the research recorded are real, and the two built features
probed clean.

Then load a battle. The sample should print:

```
[Info   :Hello World] Session started: Battle
[Info   :Hello World] Battle reached MapReady
[Info   :Hello World] Battle reached UnitsReady
[Info   :Hello World] Battle reached SubsystemsLoaded
[Info   :Hello World] Battle reached BattleDeployed
[Info   :Hello World] Battle reached BattleIdle
[Info   :Hello World] Battle started — a mod is running inside the game.
```

Milestones must appear **in order** and **once each**. A repeat means the
frame-rewind dedupe is not holding; a milestone that never arrives means its
boot gate differs on this build from what `BootMilestones` records.

Launching a battle from inside a campaign should show the campaign session
ending before the battle session starts, with no overlap. If
`LifecycleService` logs *"A session was still open when another began"*, a
transition path is not covered by the four patch points.

## Gotchas already paid for

- **Reference tiers must live in `Directory.Build.targets`, not `.props`.**
  MSBuild imports `.props` before the project body, so a condition on a
  property the project sets evaluates false and the references are never
  added — and the build still succeeds. See the note in `plan.md` Phase 3.
- **BepInEx is not on nuget.org.** `BepInEx.Core` comes from
  `https://nuget.bepinex.dev/v3/index.json`, in the tracked `NuGet.config`.
- **CS0436 is expected and suppressed in `Runtime`.** `Krafs.Publicizer` and
  `MonoMod.Utils` each define `IgnoresAccessChecksToAttribute`; they are the
  same attribute by design.
