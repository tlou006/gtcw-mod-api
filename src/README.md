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
4. Read `BepInEx/LogOutput.log`.

At this stage a good log looks like:

```
[Info   : GTCW ModApi] Game build recognised: Steam buildid 14703111 (2026-10-13).
[Info   : GTCW ModApi] Anchor check: all 10 expected game types present.
[Info   : GTCW ModApi] Capabilities on: (none)
[Warning: GTCW ModApi] Capability off: Lifecycle — not implemented in this release — plan.md Phase 3, build-out step 4
...
```

Every capability reporting "not implemented" is correct for this milestone —
the registry, the probes and the reporting are what step 3 delivers, and the
features themselves are step 4 onward. What the log proves is that the plugin
loads, the build is identified, and the type names the research recorded are
real.

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
