# Approach

## Goals

1. **Document game mechanics** — recover how the simulation actually works
   (combat resolution, morale, supply, economy, AI) from the decompiled code.
2. **Ship a hook library** — a C# DLL that lets other modders subscribe to
   game events and change behavior, without each of them having to reverse
   the game themselves.

The two goals feed each other: Goal 1 produces the map, Goal 2 turns the map
into a stable surface other people can build on. Goal 1 output lives in
`notes/`; Goal 2 output lives in `src/`.

---

## What reconnaissance already established

These facts set the strategy, and all of them are favorable:

| Finding | Consequence |
| --- | --- |
| **Mono backend**, Unity 2021.3.16f1 | Managed CIL. Decompiles to near-source C#. No IL2CPP dumping, no Ghidra, no address math. |
| **Not obfuscated** — 6,717 readable PascalCase identifiers, intact lambda names (`>b__887_0`) | Original type, method and field names survive. Comprehension is reading, not puzzle-solving. |
| **Official ModdingTool** (`Modding/ModdingTool_1.11.xlsm` + guide) | Modding is sanctioned by the developers. Also documents data formats for free — read before reversing. |
| **`Config/` is ~2 MB of plain-text data** — weapons, unitprefs, policies, commanders, historicalevents, aistrategies | Much of "game mechanics" is externalized data, not code. Cheap to document without decompiling anything. |
| **Config format is positional** (count header, then records addressed by line index) | Brittle and conflict-prone. Two mods editing `weapons.txt` collide. **This is the hook library's killer feature** — see Phase 4. |
| **`debug.txt` exposes a built-in developer mode**, many flags marked `[deactivated]` | Dormant dev/editor code paths likely still exist in the shipped assembly. High-value hook targets. |
| `Newtonsoft.Json` bundled | Some serialization is JSON; saves/config may be partly JSON. |
| `TriangleNET`, `MIConvexHull`, `DelaunayER` bundled | Procedural terrain or region partitioning, not hand-authored maps. |

`Assembly-CSharp.dll` is only 3.4 MB — a single-evening breadth pass is
realistic, not a multi-week slog.

---

## Phase 0 — Toolchain and workspace

**Goal:** a greppable decompiled source tree and a buildable project skeleton.

- [ ] Install .NET SDK (check: `dotnet --version`) — needed for both `ilspycmd`
      and building the hook library.
- [ ] Install ILSpy CLI: `dotnet tool install -g ilspycmd`
- [ ] Install **dnSpy** (interactive browsing + live debugger attach) and
      **ILSpy** GUI. Record versions in `tools/README.md`.
- [ ] Export the whole assembly to a browsable tree:
      ```powershell
      ilspycmd "game\The Civil War (1861-1865)_Data\Managed\Assembly-CSharp.dll" `
        -p -o decompiled\Assembly-CSharp
      ```
      `-p` emits a `.csproj` with one file per type — which is what makes the
      result searchable with Grep/ripgrep like any normal codebase.
- [ ] Confirm `decompiled/` is gitignored (it is) and never commit the output.
- [ ] Read `Modding/ModdingTool Quick Guide.pdf` and inventory the `.xlsm`
      sheet names. **Do this before writing any decompiler notes** — it may
      document half the data model and save days.

**Exit criteria:** can run a ripgrep query across decompiled C# and get hits.

---

## Phase 1 — Map the assembly (breadth before depth)

**Goal:** know what exists and where, before studying anything closely.

- [ ] Inventory namespaces and types by size; identify the god-classes (the
      `b__887_0` lambda index implies at least one class with ~900 methods).
      Those are the simulation cores and the eventual hook targets.
- [ ] Classify every major type into: **simulation**, **UI**, **data loading**,
      **serialization**, **AI**, **rendering/glue**. UI is the bulk of most
      Unity games and is mostly noise for Goal 1 — but it matters for Goal 2,
      since mods want to add UI.
- [ ] Find the entry points and the tick loop: `Update`/`FixedUpdate` on the
      main controllers, the turn-advance method, the battle-tick method.
      Everything in the simulation hangs off these.
- [ ] Find the `Config/` parsers. Since the format is positional, these are
      sequential readers — and each one is effectively a **schema definition**
      for its data file. Mapping parser → file gives you the whole data model
      almost for free.
- [ ] Find the save/load path. Determines what mods can safely touch.
- [ ] Write `notes/01-architecture.md`: the map, the god-classes, the tick
      flow, the data-loading flow.

**Exit criteria:** you can answer "where would I look to change X?" for any X.

---

## Phase 2 — Document mechanics (Goal 1)

**Goal:** `notes/` explains the simulation well enough to predict its behavior.

Run two tracks in parallel, cheapest first:

### Track A — Data-driven mechanics (cheap, do first)

The `Config/` files already name and quantify much of the design. For each
file: identify its parser, recover the field-per-index schema, and document
what each column *means*.

Priority order, by how much mechanics they expose:

| File | KB | Why it matters |
| --- | --- | --- |
| `unitprefs.txt` | 68 | Unit stats — the core combat inputs |
| `weapons.txt` | 31 | Weapon stats; pairs with combat resolution |
| `policies.dat` | 144 | Campaign-layer political/economic effects |
| `aistrategies.dat` | 22 | AI behavior, by named map location |
| `historicalevents.dat` | 136 | Event scripting and triggers |
| `campaignprefs.txt` / `battleprefs.txt` | 125 / 41 | Global tuning constants |
| `commanders.txt` | 544 | Commander traits/perks and their effects |
| `projects.dat`, `buildingtypes.dat`, `resources.dat` | 59 | Economy model |

> Document the **schema and the meaning**, with representative values to make
> ranges clear. Don't bulk-copy these files into `notes/` — they're the
> publisher's data, and `.gitignore` deliberately keeps them out of the repo.

### Track B — Code-driven mechanics (the real reversing)

For each subsystem, write `notes/NN-<subsystem>.md` covering: inputs, the
formula or algorithm, where the constants come from (hardcoded vs `Config/`),
and the edge cases.

Suggested order — combat first, since it's what modders most want to change:

1. **Combat resolution** — fire resolution, hit probability, damage, casualty
   allocation. (`Weapon`, `Accuracy`, `Reload`, `Damage`, `Casualt` all appear
   in the string table.)
2. **Morale and cohesion** — morale change sources, rout/break thresholds,
   recovery. The decisive system in this genre.
3. **Supply and logistics** — depots, supply range, attrition. (`Supply`
   appears 46×; `GetClosestDepotsAndTowns` is a confirmed real method.)
4. **Campaign economy** — production, trade, demand. (`GetHighestDemand`,
   `GetDemandProductionList`, `GetEconomyTooltip` confirmed.)
5. **Politics and prestige** — (`GetPrestigeEarnedDetails` confirmed.)
6. **Commander traits / experience** — (`GetEffectsOnFitness`,
   `GetInfluencesOnWeight` confirmed.)
7. **AI** — strategic and battlefield decision-making.

**Method:** the `Get*Tooltip` / `Get*Details` methods are the shortcut. A game
that shows the player "why" a number is what it is must *compute the
breakdown*, so those methods enumerate the real contributing factors in one
place. Start every subsystem from its tooltip method and work outward.

**Exit criteria per subsystem:** you could reimplement it from the note alone.

---

## Phase 3 — Hook library foundation (Goal 2)

**Goal:** a loadable DLL that runs code inside the game and exposes a first
stable event.

### Technical decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Mod loader | **BepInEx 5.x** (Mono branch) | The mature standard for Unity Mono. Bundles HarmonyX, uses UnityDoorstop injection, no game files modified. MelonLoader is the alternative but is weaker for Mono-era Unity. |
| Patching | **HarmonyX** (ships with BepInEx) | Runtime IL patching — prefix/postfix/transpiler. No patched game DLLs to redistribute, which keeps us legally clean. |
| Private member access | **`Krafs.Publicizer`** (MSBuild) | Compile against a publicized `Assembly-CSharp` reference so we can reach private members with normal typed code instead of reflection strings. Publicizes at build time only — **never redistributed**. |
| Target framework | `netstandard2.1` (match Unity 2021.3) | ABI compatibility with the game's Mono runtime. |
| Language | C# | Required by the goal, and native to the ecosystem. |

### Architecture: isolate volatility

The whole design rests on one principle — **game updates must break only one
layer.** Three layers, strictly separated:

```
┌─────────────────────────────────────────────────┐
│  GTCW.ModApi            (public, STABLE)        │  ← what modders reference
│  events, interfaces, DTOs. No game types leak.  │
├─────────────────────────────────────────────────┤
│  GTCW.ModApi.Bindings   (internal, VOLATILE)    │  ← the only layer that
│  Harmony patches, publicized game-type access,  │     knows real game types
│  translation from game state → stable DTOs      │
├─────────────────────────────────────────────────┤
│  GTCW.ModApi.Plugin     (BepInEx entry point)   │
│  bootstrap, version gate, logging, config       │
└─────────────────────────────────────────────────┘
```

A modder referencing `GTCW.ModApi` never touches a game type. When the game
updates, `Bindings` is repaired and dependent mods keep working unrecompiled.
If game types leak into the public API, every mod breaks on every patch — so
treat that boundary as inviolable.

### Build out

- [ ] Scaffold the three projects under `src/`, plus `samples/HelloWorldMod`.
- [ ] Verify BepInEx loads against this build; get a log line out of the game.
- [ ] Implement the **version gate**: on startup, hash `Assembly-CSharp.dll`
      and compare against known-good hashes (`notes/00-build-provenance.md`).
      Mismatch → loud warning, continue in degraded mode. Never hard-crash
      someone's game over a version bump.
- [ ] Land **one** end-to-end event (suggest `Battle.Started` — easy to trigger,
      easy to verify) and prove it reaches a sample mod.
- [ ] Establish **fail-soft** as a rule: a failed patch disables that one
      feature and logs; it never takes down the game or the other patches.

**Exit criteria:** sample mod prints on battle start, from a clean install.

---

## Phase 4 — API surface (Goal 2, the substance)

Driven by Phase 2's findings — you can only expose what you understand.

### Event surface

| Area | Events |
| --- | --- |
| Lifecycle | `Game.Initialized`, `Campaign.Loaded`, `Campaign.Saving`, `Turn.Advancing/Advanced` |
| Battle | `Battle.Started/Ended`, `Battle.Tick` |
| Units | `Unit.Damaged`, `Unit.MoraleChanged`, `Unit.Routed`, `Unit.Destroyed` |
| Economy | `Production.Calculating`, `Trade.Resolved` |
| Commanders | `Commander.Promoted`, `Commander.TraitApplied` |

Two event flavors, and the distinction matters:

- **Notification** (`…ed`, past tense) — read-only, cannot be cancelled. Safe.
- **Mutation** (`…ing`, present participle) — carries a mutable args object so
  a mod can adjust a value or cancel. Powerful and dangerous; every one needs
  an explicit contract for what happens when two mods both write.

### Layered config overrides — the feature worth building

The single most valuable thing this library can offer. Right now, data modding
means editing positional text files in `Config/`, so **two mods touching the
same file cannot coexist** — and a game patch silently overwrites both.

Intercept config loading and let mods register typed, additive overrides:

```csharp
// Instead of hand-editing line 47 of weapons.txt
GTCW.ModApi.Data.Weapons.Patch("6-pounder Field Gun", w => w.Reload *= 0.9f);
```

Benefits: mods stack, overrides survive game patches, conflicts are detectable
and reportable, and nothing on disk is modified. This alone justifies the
library's existence.

- [ ] Reach the config parsers (Phase 1) and intercept post-parse.
- [ ] Typed models per data file, generated from the Phase 2 schemas.
- [ ] Conflict detection and a resolution policy.

### Also worth investigating

- [ ] **Re-enable the dormant debug mode.** `debug.txt` shows numerous
      `[deactivated]` dev flags. If those code paths still exist, exposing them
      gives modders a dev console and scenario-loading tools nearly free.
- [ ] UI injection points, so mods can add panels rather than only change numbers.

---

## Phase 5 — Release

- [ ] XML doc comments on every public member; generate API reference.
- [ ] `docs/getting-started.md` — scaffold a mod in 10 minutes.
- [ ] Two or three sample mods, from trivial to realistic.
- [ ] **Semantic versioning**, enforced strictly. Mods depend on this; breaking
      the public API without a major bump breaks the ecosystem.
- [ ] Ship the API DLL only. **Never redistribute game assemblies**, publicized
      references, or decompiled source — modders build against their own copy.
- [ ] Publish to Nexus/Thunderstore and the game's modding community.

---

## Surviving game updates

The main long-term risk — Steam already updated this build on 2026-10-13, the
day analysis started. Mitigations, in order of value:

1. **Set Steam to "Only update when launched"** so the build under analysis
   doesn't change mid-investigation.
2. **Signature manifest.** Keep a tracked JSON file listing every method the
   library patches, with its full signature and an IL hash. A verification tool
   checks the manifest against any `Assembly-CSharp.dll` and reports exactly
   which patches drifted. Turns "the mod is broken, somewhere" into a
   30-second diff — and it's the highest-leverage piece of tooling in this
   whole plan.
3. **Keep every note tied to a build** via `notes/00-build-provenance.md`.
4. **Version-gate at runtime**, degrade loudly rather than crashing.
5. Prefer patching **stable, semantically-named** methods over volatile
   internals; avoid transpilers (IL-offset-fragile) where a prefix/postfix works.

---

## Risks

| Risk | Severity | Mitigation |
| --- | --- | --- |
| Game update breaks patches | High | Signature manifest; thin volatile layer |
| God-classes make clean hook points scarce | Medium | Patch at semantic boundaries, not convenience points |
| Mutation events let mods corrupt saves | Medium | Notification-first; mutation events documented as unsafe; test save round-trips |
| Mod conflicts on the same hook | Medium | Priority ordering + conflict reporting |
| Public API ossifies too early | Medium | Mark unstable areas `[Experimental]` until Phase 2 covers them |
| Decompiled code leaks into the repo | Low but serious | `.gitignore` covers it; notes describe findings rather than pasting source |

---

## Scope and conduct

Single-player strategy game with an official modding tool — modding is
sanctioned, and there's no anti-cheat or multiplayer integrity concern to
navigate. Even so:

- The repo ships **original work only**: notes, schemas, and the hook library.
- No decompiled source, game assemblies, or extracted assets are committed or
  redistributed — `.gitignore` enforces this and it shouldn't be relaxed.
- The hook library requires users to own the game; it patches at runtime in
  memory and modifies nothing on disk.
- Check the EULA's modding/reverse-engineering clauses before public release.

---

## Immediate next steps

1. Read `Modding/ModdingTool Quick Guide.pdf` — may hand over the data model.
2. Install .NET SDK + `ilspycmd`; export `decompiled/Assembly-CSharp`.
3. Set Steam to update-on-launch only.
4. Phase 1 namespace/type inventory → `notes/01-architecture.md`.
5. Locate the `Config/` parsers — best single return on effort, since it
   unlocks both the data schemas (Goal 1) and the override system (Goal 2).
