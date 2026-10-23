# Approach

## Goals

1. **Document game mechanics** — recover how the simulation actually works
   (combat resolution, morale, supply, economy, AI) from the decompiled code.
2. **Ship a hook library** — a C# DLL that lets other modders subscribe to
   game events and change behavior, without each of them having to reverse
   the game themselves.

The two goals feed each other: Goal 1 produces the map, Goal 2 turns the map
into a stable surface other people can build on. Goal 1 output lives in
`code-findings/`; Goal 2 output lives in `src/`.

---

## What reconnaissance already established

These facts set the strategy, and all of them are favorable:

| Finding | Consequence |
| --- | --- |
| **Mono backend**, Unity 2021.3.16f1 | Managed CIL. Decompiles to near-source C#. No IL2CPP dumping, no Ghidra, no address math. |
| **Not obfuscated** — 6,717 readable PascalCase identifiers, intact lambda names (`>b__887_0`) | Original type, method and field names survive. Comprehension is reading, not puzzle-solving. |
| **Official ModdingTool** — 53 sheets, each declaring the `Config/` file it writes, with field schemas in its header rows | Modding is sanctioned, **and the data model is largely handed to us.** Most schemas no longer need reversing out of the game's parsers. [Details](code-findings/07-config-data-model.md). |
| **Policy and project *effects* are hardcoded** — data files carry only descriptions, costs and prerequisites | **New policies/projects with real effects are impossible via data modding.** Only code patching reaches them, which makes this the hook library's headline capability. |
| **Scenario overrides replace whole files**, with no field-level merge | Field-level additive overrides are genuinely new capability, not a reimplementation — and it explains why data mods conflict so badly today. |
| **Prefs are scale-dependent**: weapon ranges tuned to map size, lethality to unit echelon | No weapon number means anything without stating its scale. Affects every combat note. |
| **Some mechanics live in Unity prefabs**, not code or config (building names, ranking, levels — via the named `Cbuilding` script) | A hard ceiling on data-only modding, and a direct argument for the hook library. Also leaks a `C`-prefix class convention. |
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

- [x] Install .NET SDK (10.0.401) — needed for both `ilspycmd` and building
      the hook library.
- [x] Install ILSpy CLI: `dotnet tool install -g ilspycmd` (11.1.0.9782).
      Note it is **not** in the ILSpy MSI, which ships the GUI only.
- [x] **ILSpy** GUI (11.1.0.9782). Versions recorded in
      [`tools/README.md`](tools/README.md).
- [ ] Install **dnSpy** — still open. Wanted for live debugger attach, which
      `ilspycmd` cannot do.
- [x] Export the whole assembly to a browsable tree:
      ```powershell
      ilspycmd "game\The Civil War (1861-1865)_Data\Managed\Assembly-CSharp.dll" `
        -p -o decompiled\Assembly-CSharp
      ```
      `-p` emits a `.csproj` with one file per type — which is what makes the
      result searchable with Grep/ripgrep like any normal codebase.
      `Assembly-CSharp-firstpass.dll` was exported too and turned out to be
      **pure Unity Standard Assets — ignore it from here on.**
- [x] Confirm `decompiled/` is gitignored (verified with `git check-ignore -v`
      against real exported paths) and never commit the output.
- [x] **Read `Modding/ModdingTool Quick Guide.pdf`** (via OCR text). No
      column-level schemas, but it supplies the *semantics* the workbook
      can't: what each file controls, which effects are hardcoded, scenario
      layout, and several stated limits on modding. Essential reading.
- [x] **Inventory the ModdingTool workbook** — done, and it paid off: 53 sheets
      each declaring the config file they write, with field schemas in their
      header rows. See
      [`code-findings/07-config-data-model.md`](code-findings/07-config-data-model.md).
- [ ] **Extract `xl/vbaProject.bin` (218 KB, `Module1`).** The export macros
      are the developers' own serializer for every `Config/` file — field
      order included. VBA source is RLE-compressed per MS-OVBA, so this needs
      a real decompressor (`oletools`/`olevba`, or a CFB parser). Highest
      remaining return on effort in Phase 0, and it may settle most of the
      open schema questions outright.

      > **Downgraded in priority.** The parsers themselves turned out to give
      > exact field types, widths and order (see Phase 1 results), so the VBA
      > is no longer the only route to the schemas. It remains the best source
      > for the *dual-unit* question (yd/m, lb/kg, ft/s–m/s) and localization
      > ids, since those live in the export macros' formatting, not in the
      > game's reader.

**Exit criteria:** can run a ripgrep query across decompiled C# and get hits.
**Met** — 476 files, ~248k lines, greppable.

---

## Phase 1 — Map the assembly (breadth before depth)

**Goal:** know what exists and where, before studying anything closely.

Results are in [`code-findings/`](code-findings/README.md) — see [summary-architecture](code-findings/summary-architecture.md), [summary-data-layer](code-findings/summary-data-layer.md) and [summary-effects](code-findings/summary-effects.md).

- [x] Inventory types by size; identify the god-classes. Found: `Regiment`
      (28.5k lines) is the one the `b__887_0` lambda index pointed at, followed
      by `BattleUnits`, `GamePrefs`, `GameVars`, `AICampaign`.
- [x] ~~Test the `C`-prefix hypothesis first.~~ **Tested and disproved** —
      only `CBuilding` and `CBuildingIcon` carry it. The real partition is
      **global namespace = game code, namespaced = third-party**, with a
      ~40-type exclusion list for third-party code that leaked into the global
      namespace (UniStorm, EasyRoads3D, SimpleLOD). Cheap test, wrong
      hypothesis, right answer in one query — which is what the step was for.
- [x] **Anchor on the campaign scene graph.** Confirmed: 202 call sites do
      `GameObject.Find("GameController").GetComponent<T>()`. The component set
      is `BattleUnits`, `BattlefieldSetup`, `BattleUI`, `Economy`,
      `SpottedEnemy`, `Smoke`, `FogOfWar`, `AIBattle`. Secondary anchors: `UI`,
      `UICampaign`, `Cam_Pivot`, `SoundEngine`, `ProceduralObjects`.
- [x] Classify every major type into: **simulation**, **UI**, **data loading**,
      **serialization**, **AI**, **rendering/glue**. Done by evidence (UI-type
      references, IO sites, rendering types): ~67 UI-dominant, ~26
      rendering/glue, 50 with file IO, ~20 simulation/AI.

      > **The buckets overlap heavily, and that is the finding.** `Policies`,
      > `Projects`, `Statistics`, `Finances`, `Production` and `Trade` each hold
      > simulation logic *and* drive their own panels; `Projects` also holds a
      > config parser. So there is rarely a clean "model" to bind to — an
      > argument for the ModApi exposing purpose-built accessors rather than
      > game types, which is the `Bindings` boundary already specified.
      >
      > It also means the simulation/UI split is a poor filter for Goal 1:
      > reading a panel class to learn what a value *means* is often faster
      > than reading the simulation, because the UI is where values get
      > labelled.
- [x] Find the entry points. **There is no `Main`** — standard Unity scene
      load. `EntryPoint` is a false friend (a *battlefield* reinforcement entry
      point). The lifecycle hub is `SceneManagement`, whose static
      `Load*`/`Exit*`/`SaveAll` methods are the Phase 4 event surface.
- [x] Find the per-frame tick loop proper. Done — see
      [summary-simulation](code-findings/summary-simulation.md). **There is no fixed
      timestep**: 192 of 211 game types use `Update()`, only two use
      `FixedUpdate` and `Regiment.FixedUpdate` is an empty stub. The
      controllers' `Update` methods are **boot state machines** (~50 numbered
      frame gates), not simulation; the real driver is `BattleUnits.Update`,
      branching on `IsCampaign`, plus `Regiment.Update` per unit.
      `GameVars.frame++` happens in exactly one place, `MainMenuControls`.
      Four scheduling mechanisms found, incl. a config-tunable macro-economy
      interval (`GamePrefs.updatetimeforcampaigndata`). Game time comes from
      **UniStorm**, the third-party weather asset.
- [x] Find the `Config/` parsers. Done, and better than hoped: paths are plain
      relative string literals, the format is fully decoded (count header,
      line-per-field, `;`-separated fixed-width list fields, `\n`/`\t` escapes),
      and `projects.dat` is verified field-by-field against the shipped file.
      Three `Tools` helpers encode the whole convention.
- [x] Find the save/load path. `ImportExportUnitData` — ~25 paired
      `Save*`/`Load*` methods, one per subsystem, plus the game's own
      `CheckFileIntegrity` and a float save-version file.
- [x] Write up the findings — split across `code-findings/01`–`09` with three summaries.

**Exit criteria:** you can answer "where would I look to change X?" for any X.
**Met.** ✅ Phase 1 complete — all nine items closed, results in
[`code-findings/`](code-findings/README.md) across 11 detail files and four summaries.

Two items were closed by disproving their premise rather than by confirming
it: the `C`-prefix convention does not exist, and there is no fixed timestep.
Both reframed later phases, which is what the breadth-first pass was for.

---

## Phase 2 — Document mechanics (Goal 1)

**Goal:** `code-findings/` explains the simulation well enough to predict its behavior.

Run two tracks in parallel, cheapest first:

### Track A — Data-driven mechanics (cheap, do first)

The `Config/` files already name and quantify much of the design — and the
ModdingTool sheets supply most of the schemas, so **the work here is no longer
schema recovery but semantics**: for each file, confirm the field order against
its parser, then document what each field actually *does* to the simulation.

That reframing matters: field *names* are cheap now, but a name like
`Production Factor` or `HA` tells you nothing about how the value is consumed.
The parser tells you the shape; only the consuming code tells you the meaning.

Open questions inherited from the ModdingTool inspection, worth resolving
early because they affect every downstream number:

- ~~Which unit of each dual-unit pair is serialized (yd vs m, lb vs kg, ft/s vs
  m/s)? Guessing wrong misscales every ballistic value.~~ **Resolved, and the
  premise was wrong** — see
  [11-combat-resolution](code-findings/11-combat-resolution.md) §2. **Both** units
  are serialized as separate fields (`range_yd` *and* `range_m`), and the
  layers read different ones: the **battle** layer is metric, the **campaign,
  naval and UI** layers are imperial, and several imperial fields are parsed
  and never read. Nothing keeps the pairs in sync, so editing one silently
  desynchronizes the simulation from the UI and the AI's own valuations. The
  ModApi should expose one logical value per quantity and write both fields.
- Are the bare numeric header cells localization string IDs? If so, UI text is
  addressed by ID, which the hook library must respect to add strings.

Priority order, by how much mechanics they expose:

| File | KB | Why it matters |
| --- | --- | --- |
| `unitprefs.txt` | 68 | Unit stats — the core combat inputs |
| `weapons.txt` | 31 | Weapon stats; pairs with combat resolution |
| `policies.dat` | 144 | Campaign-layer political/economic effects |
| `aistrategies.dat` | 22 | AI behavior, by named map location |
| `historicalevents.dat` | 136 | Event scripting and triggers |
| `campaignprefs.txt` | 125 | **Promoted.** The designated balance surface for hardcoded policy effects — likely a large table of tuning constants keyed to them |
| `battleprefs.txt` | 41 | Battle tuning; **rescales with map size** (4×4 vs 8×8 miles) |
| `commanders.txt` | 544 | **Schema decoded** — 1,906 historical officers, 66 lines each; see [19](code-findings/19-commanders-and-perks.md) §10. Perks live in three separate `Perk*Tooltips.txt` files, not here |
| `projects.dat`, `buildingtypes.dat`, `resources.dat` | 59 | Economy model |

> Document the **schema and the meaning**, with representative values to make
> ranges clear. Don't bulk-copy these files into `code-findings/` — they're the
> publisher's data, and `.gitignore` deliberately keeps them out of the repo.

### Track B — Code-driven mechanics (the real reversing)

For each subsystem, write `code-findings/NN-<subsystem>.md` covering: inputs, the
formula or algorithm, where the constants come from (hardcoded vs `Config/`),
and the edge cases.

Suggested order — combat first, since it's what modders most want to change:

1. ✅ **Combat resolution** — done, see
   [summary-combat](code-findings/summary-combat.md). **There are two independent
   damage models**, and melee makes three: manual battles simulate real projectiles with ballistics
   and per-block hits; `Autocalc` computes casualties in closed form from a
   stance matrix and multiplicative stat influences. They share only
   `Regiment.SufferCasualties` and read different weapon fields — firepower is
   *linear* in one and *square-rooted* in the other — so a combat mod must
   change both or they diverge. Almost every combat constant is already in
   `GamePrefs`, which means balance is a data-modding problem and the hook
   library's combat value is **structural** change plus keeping the two models
   consistent. Melee turned out to be a **third** model again — block-to-block,
   purely differential, and absent from `Autocalc` entirely. `firepower[3]` is
   indexed by **ammunition type** (solid shot / shell / canister), and
   auto-resolve reaches the same index via a stance-derived closeness band.
2. ✅ **Morale and cohesion** — done, see
   [summary-morale](code-findings/summary-morale.md). Two systems write one `[0,1]`
   value: 18 effect slots in battle, 14 in the campaign, each a **rate**
   applied inline and scaled by elapsed game time, over a continuous rally
   recovery. Losses (never gains) are amplified by a factor experience and
   leadership **halve but cannot remove**. The decisive finding is
   `fightingspirit`: a hard morale cap in *both* combat models, seeded from
   `GameVars.nation[state].support[alliance]` — **state political support caps
   how well a unit can fight**, so any mod touching support is a combat mod.
   Cohesion is separate and simple: an exponential approach to a target set by
   terrain (only while moving), movement and units passing through, with drill
   reducing disruption rather than speeding recovery.

   **Two unwired features found**, both verified by exhaustive reference
   search: (a) `cohesionbyfire` has no increment path anywhere, and the three
   shipped `unitprefs.txt` values for infantry fire, artillery fire and cavalry
   shock disrupting cohesion are **parsed and never read** — only the decay
   half of the feature exists; (b) campaign morale slot 8 (high attrition) is
   computed and shown in the UI but **never applied** to morale. A modder
   tuning the cohesion values would see nothing happen.
3. ✅ **Supply and logistics** — done, see
   [summary-logistics](code-findings/summary-logistics.md). Two models again:
   battlefield supply is a **multiplicative chain up the command hierarchy to a
   map-edge `EntryPoint`**, cut by any enemy unit standing on the line (a real
   geometric raycast); campaign supply state is the **minimum** across the four
   stock types (small-arms ammo, artillery ammo, provisions, forage), fed by
   depots that hold actual economy goods — so **local prices throttle
   resupply**. Shortage never damages units directly: it degrades `condition`,
   which raises fatigue and lowers accuracy, reload speed and drill rate. And
   it bites on a **hardcoded 5% cliff**, not a gradient.

   Closed two questions from the morale pass: **fatigue** (recovery is
   unconditional; accrual comes from melee scaled by `inmelee` and from
   marching via `terrainfatigue[currentterrain]`, multiplied by low condition)
   and **training** (accumulated drill, distinct from experience, boosted by
   project 118 and scaled by `conditionmen`). Also resolved **`unittyp`
   completely** — `unitprefs.txt` declares all 19 types by name, which
   retroactively explains every `<= 13`, `== 5` and `{1,2,4}` guard across
   three subsystems. Collected that and six other sets in
   [16-enumerations](code-findings/16-enumerations.md).

   Two cross-layer couplings worth carrying into the economy and politics
   passes: **building hospitals makes the army fight better** (treating wounded
   raises state support, which caps `fightingspirit`), and **the AI raids
   logistics using the same `GetClosestDepotsAndTowns` lookup** the player's
   units use to find it.
4. ✅ **Campaign economy** — done, see
   [summary-economy](code-findings/summary-economy.md). Three layers. **`IIP` nodes**
   are a spatial network of local markets — nine node types, each with its own
   stock, prices, transport capacity and trade ledger, where IIPs, buildings,
   towns *and armies* all trade as counterparties. **`Resource`** gives goods a
   recipe expressed as a **boolean expression** over four inputs (0 = OR,
   1 = AND), which the parser expands into alternative recipes that
   `CheapestPreGoods` then resolves **at local prices** — so substitution is
   real and a factory has no fixed bill of materials. **Macro** turns deficit
   and debt into a credit-rating notch, which sets the interest rate, which
   compounds the debt; `ratingmodifier` is exactly what the policy switch
   adjusts, closing a policy → rating → interest → treasury loop.

   Pricing is a logistic curve on scarcity (`scurvepricingC1/C2`), so
   **moderate shortages move prices more than severe ones**, and transactions
   are priced on their own average rather than the starting stock — correct
   slippage. Note `minpricereached` is a **quantity**, not a price: the stock
   level at which price bottoms out, and the stock scale used everywhere else.

   Closed two open questions: **`IIP`** is an infrastructure node that is also
   a market (so a depot with no `closestiip` has nothing to buy from), and
   **`Alliance.credibility`** is an inverted credit-rating notch.

   Found the worst modding trap so far: transport-capacity policies key off
   **Unity scene object names** (`"Trade_SantaFe"`, `"Trade_Butterfield"`, …),
   so renaming a trade node silently disables the policy and a new trade route
   cannot receive these effects at all. Policy 30 shows the better pattern —
   membership of a `northernroutes` list.
5. ✅ **Politics and prestige** — done out of order, see
   [summary-politics](code-findings/summary-politics.md). Three variables, each with
   its own accounting array: **state support** (per state × alliance, 42
   hardcoded reason codes, 23 of them policies), **national morale** (per
   alliance, 35 codes, and the campaign-loss condition via
   `breakmoraletrigger`), and **foreign intervention levels** (per alliance ×
   foreign power, almost entirely a policy game, with power 3 gated on 7 June
   1863 *and* policy 144).

   Promoted ahead of the economy because state support kept appearing as an
   input elsewhere, and it reaches further than expected: **volunteers scale
   linearly with it and drop to zero below `statesupportrecruiting`**, drafts
   are hard-gated, it caps battle morale through `fightingspirit`, it
   multiplies desertion, and `minnationalmoralesurrender` makes units surrender
   outright. Politics is an input to the war, not a reaction to it.

   Two traps for modders: **policy 30 keys off five state *names* as strings**,
   so renaming a state silently disables it; and **the war balance is computed
   from ten UI `Slider.value` fields** rather than from the model, so anything
   reading it headlessly gets whatever was last displayed.

   Also found a clean first target for the hook library: the support-breakdown
   tracker is written as `Mathf.Pow(amount, 0.5f)`, which is **NaN for any
   support loss**, and guarded by `== float.NaN`, which is **always false in
   C#**. The "what is hurting our support" UI therefore returns nothing, ever.
   A postfix recomputing it as `sign(amount) * sqrt(|amount|)` fixes a visible
   bug with zero balance impact.
6. ✅ **Commanders and perks** — see
   [summary-commanders](code-findings/summary-commanders.md). The perk blocker is
   gone: there are **three perk namespaces** (19 battle, 18 army campaign, 11
   fleet), selected by one line — `unittyp <= 13` gets **one** battle perk,
   anything higher gets **four** campaign or fleet perks. The id "collisions"
   recorded earlier were cross-namespace and never ambiguous at runtime.

   The five attributes all run through one pipeline:
   `raw × outOfDuty(0.5) × (0.5 + 0.5·exp) × (1 + 0.33·(superior − self))`, so
   **a green commander operates at half his nominal attributes** and
   experience only ever brings him up to the number on his card. And
   **leadership, administration and cunning never change in the base game**
   except through military schools in the commander's home state — which also
   closes the `militaryschools[]` and `printingworks[]` questions from the
   politics pass.

   Rank is data-driven: every shipped commander has `rank = -1` and rises on
   the authored `dateofpromotion[]` schedule in `commanders.txt` (1,906
   researched officers, 66 lines each, spot-checked against history).
   **Rank is never lowered, by any code path.** `ranks.txt` rows 0–9 are dead
   data — fighting types are remapped to the Brigade row before lookup — and
   the Union is capped at Major General in code, not data.

   The find of the pass is **feuds**: a subordinate whose initiative differs
   sharply from his superior's, and who is the more famous of the two, is
   handed to a *third* AI controller and may march off at the nearest enemy.
   Political commanders are twice as likely to; volunteers half as likely.

   Three useful negatives: **the president is purely cosmetic**, the
   **engineer branch of arms has no mechanical effect**, and the perk
   "limited by policies and technologies" gating promised by the UI
   **does not exist** (`IsPerkUnlocked()` returns `true` and is never called).
7. **AI** — turned out to be two subsystems, so it gets two files.

   7a. ✅ **Campaign AI** — see
   [summary-ai-campaign](code-findings/summary-ai-campaign.md). A **36-step job
   machine per faction**, one step per call, over seven AI alliances; the same
   staggering idiom as the economy and the commander round-robin. Three spatial
   representations: strategic zones **read from a texture asset** on
   `Battlemap/Borders`, a 20×20 blurred influence grid, and raw positions.

   Three facts dominate. **AI throughput scales with the square root of game
   speed** (`floor(sqrt(gamespeed))` passes per frame), so fast-forwarding
   gives the AI less thinking time per game-day. **It plans against where units
   are going, not where they are** — every position read is
   `GetLastTransmittedPathPos(ignoreorderdelay: true)`, including for enemy
   units, so it intercepts a march before the order has been transmitted. And
   **fog of war is one-directional**: the shared detection guard exempts
   `alliance == playeralliance` from concealment, so the player's units are
   never hidden and the AI's are.

   The value map is **purely economic** — transport capacity (weight 1.0),
   corporate turnover (0.00001), population (0.0001), plus a flat 750 for a
   capital. No terrain, no defensibility. It attacks when it can assemble
   **0.8× to 1.0× the enemy's morale-weighted strength** (commander initiative
   is the dial), with `aiagressiveness[0] = 0` making chapter 0 a data-enforced
   phoney war, and a **cubic anti-stall term** that forces action after about a
   year of immobility.

   Eight named doctrines per side plus two crisis doctrines, and
   **`GetAIPersonality` re-decides on every call** — under fiscal or manpower
   stress the AI swaps its whole agenda. Its economic brain is literally the
   player's auto-manage autopilot.

   Difficulty is two sliders (which answers the `casualtiesmodifier`
   provenance question) plus **ten hardcoded cheat constants on `GameVars`** —
   of which **two are discarded reads** and **one has an inverted guard**, so
   it only applies in AI-vs-AI games.

   Two problems worth fixing in the hook library: the campaign is **not
   reproducible** (three `Random.InitState(DateTime.Now.Millisecond)` calls
   inside the AI tick reseed the global RNG from the wall clock), and the
   codebase has a **repo-wide off-by-one** — 39 instances of
   `Random.Range(0, list.Count - 1)` against 36 correct ones, so the last
   candidate in a list is never chosen.

   7b. **Battle AI ✅** — [21-ai-battle](code-findings/21-ai-battle.md),
   [summary-ai-battle](code-findings/summary-ai-battle.md). `AIBattle` is **three
   components on `GameController`**: the enemy AI, the **player's own
   auto-pilot** (any formation you give a stance to), and the feud controller.
   It runs two lanes on different clocks — a **game-time-gated macro lane**
   (0.015 game hours) and a **frame-gated 30-slot micro task wheel**, so
   fast-forwarding a battle starves the unit-level AI and leaves the
   army-level AI untouched. There are **two stance enumerations in opposite
   orders** (`stance`/`macroai` vs `ai_stance`). `Config/aistrategies.dat` is
   176 records × 42 lines, keyed to **scene GameObject names** with an
   **absolute battle-clock hour** per objective; a scripted chain is a
   prologue that reverts to a **greedy nearest-objective** engine with no
   value weighting. Two unlisted AI advantages: long AI moves **skip the order
   delay**, and the AI **temporarily carves the navmesh** to route around
   covered crossings. `FilterMap` turns out to be the engine-wide 2-D field
   type, shared with the campaign zone map and **ten `Economy` maps** — which
   closes the `UpdateFilterMaps` question in code-findings/18.

8. **Policies and projects ✅** — [22-policy-and-project-catalogue](code-findings/22-policy-and-project-catalogue.md),
   [summary-policies-and-projects](code-findings/summary-policies-and-projects.md).
   Taken next because it was the last subsystem the ModApi's effect-registration
   design was blocked on. **117 policies and 125 projects**, built two
   completely different ways: a policy is a `MonoBehaviour` whose config file
   only *overrides* it, while a project's config record *is* the project — and
   56 project ids ship as `PLACEHOLDER`. `LaunchPolicyEffect` is **replayed
   every tick** after a thirteen-field reset, which is the contract any hook
   must honour. The five policy ladders each do exactly one thing, and
   **ten pref arrays describing their further effects are parsed and never
   read** — the single biggest "documented but absent" finding in the project
   so far. Also resolved: support reason codes 9–31 and national-morale codes
   5, 20–31 mapped to their writing policies (three codes have labels and no
   writer); the 53-entry state-index table that decodes every hardcoded
   `GameVars.nation[n]`; chapters 0–3 and the fact that **Militia Act I is the
   act that starts the war**; and the twenty AI doctrines' actual agendas.

9. **Game state ✅** — [23-game-state](code-findings/23-game-state.md),
   [summary-game-state](code-findings/summary-game-state.md). The state every effect
   above writes to, and therefore the ModApi's core data surface:
   `GameVars.alliance[]` (8, 183 fields each) and `GameVars.nation[]`
   (52–54 depending on scenario, **never hardcode 53**). `nations.dat` has an
   authored format and a save format under one filename, which fixes exactly
   which scenario edits survive an existing save. The master campaign tick is
   `UpdateAllianceData`'s round-robin, so "replayed every tick" means **once
   per 12 game hours per alliance** — and three recompute idioms
   (double-buffered sweep, reset-then-accumulate, trailing average) decide
   whether a hook's write survives at all. Two of them make a direct write
   futile, which is the single most important fact for the ModApi's write path.

10. **Campaign objectives ✅** — 
    [24-campaign-objectives](code-findings/24-campaign-objectives.md),
    [summary-campaign-objectives](code-findings/summary-campaign-objectives.md).
    Data-driven like projects, not prefab-authored like policies: **38 records,
    20 lines each**, and a new objective with a town list needs no code at all.
    Holds the campaign's end conditions — including the **1864 election**, the
    only loss condition in the game that is a date rather than a threshold —
    and the half of the foreign-intervention trigger code-findings/20 could not
    reach. Closes the last reason-code gaps: national morale 4–12 and 14,
    support 7, 8 and 41.

11. **Newspaper and historical events ✅** —
    [25-newspaper-and-events](code-findings/25-newspaper-and-events.md),
    [summary-newspaper](code-findings/summary-newspaper.md). The game's one
    full-screen narrative channel: one queued panel with **four producers** —
    the 119-record `historicalevents.dat` catalogue, the policy records, the
    objective records, and a procedurally written battle report. Closes the
    open question shared by code-findings/22 and code-findings/24. Pins `endbattle` and
    `battleresulttype` (now in
    [16-enumerations](code-findings/16-enumerations.md)), finds **eleven authored
    events no shipped campaign start can reach**, and finds that two prefs set
    two orders of magnitude out of range make the morale paragraph of every
    battle newspaper a constant string.

**Phase 2 is complete.** Every major campaign and battle subsystem has a
findings file, and so does the narrative channel on top of them. What remains
before Phase 3 is narrower and recorded as open questions in the files
themselves: the `Messages` dispatch channel, the 7,932-line `DLC_WL` layer, and
a handful of questions that now need a Unity asset dump rather than more
decompiling.

**Method:** the `Get*Tooltip` / `Get*Details` methods are the shortcut. A game
that shows the player "why" a number is what it is must *compute the
breakdown*, so those methods enumerate the real contributing factors in one
place. Start every subsystem from its tooltip method and work outward.

**Exit criteria per subsystem:** you could reimplement it from the note alone.

---

## Phase 2b — Translate the code into mechanics (Goal 1)

**Goal:** `mechanics-findings/` explains the game well enough to play it
better, and to tell a modder which numbers are worth touching.

`code-findings/` is written to be searched by someone about to patch a method.
It is the wrong artifact for the other two audiences this project has: a player
who wants to know what the simulation rewards, and a modder deciding where to
spend a hook. [`mechanics-findings/`](mechanics-findings/README.md) is that
second layer — same facts, player's units, decisions stated.

The translation is cheap because the hard part is done: every planned file is
backed by a `code-findings` file that already exists. The conventions and the
planned index are in [`mechanics-findings/README.md`](mechanics-findings/README.md).

1. **[`ui-vs-simulation.md`](mechanics-findings/ui-vs-simulation.md) ✅** — the
   numbers and words the interface gets wrong. Eight discrepancies, the
   strongest being the campaign morale penalty the army panel reports at
   **−17.3 %/week and the simulation never applies**, the support-loss
   breakdown that **returns an empty string in every possible game**, and the
   in-game perk tooltip that **describes a gating system which does not
   exist**. Also: the strategic assessment is computed from its own slider
   widgets rather than from the model, and has an **uncovered band** that
   returns a blank verdict. Balanced by a verified §4 — every gameplay option
   in the in-game panel is wired, and all 87 weapons' imperial/metric pairs
   agree to the last digit, so the shipped UI does not lie about ranges. That
   check also found **`weight_powder_kg` holds grams**, now recorded in
   [11](code-findings/11-combat-resolution.md) §2.
2. **[`the-war-economy.md`](mechanics-findings/the-war-economy.md) ✅** — taken
   out of order at the user's request, since the economy is the subsystem the
   community finds most opaque. Eleven sections. The translation needed real
   new tracing, which closed two of
   [18](code-findings/18-economy.md)'s open questions and added a production
   section it never had:

   - **the economy cycle is a seven-point curve in each scenario's
     `battledata.dat`** — identical in all six sublevels, shared by both
     sides, with exactly two consumers, and `×20` interest leverage makes it
     the highest-leverage number in the economy;
   - **the interest base inverts and floors out from ~Nov 1863**, so $50 M of
     debt in 1861 costs ~10.3 % while $500 M in 1864 costs ~2.3 %;
   - **`lossestradewarfare` is forgone *tariff revenue*, not lost trade
     value** — it scales with your own tariff rate and conflates blockade with
     node damage;
   - **projects 113 and 114 test the loop counter against a resource id**, so
     114 does nothing at all and 113 keeps only its recruitment half, while
     both are advertised by the project panel;
   - **project 104 omits `id_manufactory`**, the one building type that makes
     Provisions and Uniforms, where three other industry groupings include it;
   - **administration cost counts a tax as charged when its slider is above
     zero, not when its cap is** — so every alliance pays a surcharge from day
     one for two taxes that collect nothing until the Revenue Acts.

   Also pins the price curve, the trade-agreement matrix, the harvest
   calendar's `-1`-means-no-modifier sentinel, and the production
   specialisation bonus, all with shipped numbers.

3. **[`production-and-workforce.md`](mechanics-findings/production-and-workforce.md) ✅**
   — the supply side, written to close the workforce hole item 2 left. Needed
   more new tracing than item 2 did, and it closed
   [18](code-findings/18-economy.md)'s largest open question:

   - **the labour map is a 32×32 byte field** painted `+√population` by towns
     and `−√workers × 5` by buildings, one cell each, with one blur pass;
     a level-4 plantation (−1.10) outweighs a city of 100,000 (+0.63);
   - **slave labour bypasses the map entirely** — the `1 × slaveRatio` term is
     unconditional — while the building's *drain* ignores slavery, so a
     plantation depresses its neighbours at full strength and is itself exempt;
   - **recruitment costs five workers a man, permanently**: the recruited count
     is only decremented by a disband, and only by the survivors, so the
     killed, the deserted and a disbanded unit's wounded never come back;
   - **fertility is applied squared** (the cache and the sample are both
     multiplied in), so 0.8 land yields 0.64;
   - **the foundation probability's subsidy term is dead** (`num5 ≡ 0f`) and its
     corporate-tax term reads the **slider**, not the effective rate, paying
     +35 % from turn one — which **corrects item 2's advice** about zeroing that
     slider;
   - **mines and ports never auto-found or auto-upgrade** (`aiplacement` is
     false), every productive building is funded from just the Agriculture and
     Industry subsidy categories and waits for full funding, and **private
     construction, recruitment and weapon orders all switch off together at
     notch 15 (CCC+)**;
   - and the loop back: slack labour is read as **unemployment** and cuts
     private wealth 1.6 → 0.9, while population growth runs inversely to it.

   Also fixed: an errant `perl -0777` with a wide character re-encoded
   `the-war-economy.md` as Latin-1 mid-session; reverted from the previous
   commit and the two edits redone with byte-safe splices.

4. **[`manual-vs-code.md`](mechanics-findings/manual-vs-code.md) ✅** — the
   official manual used as a yardstick, which is the cheapest confirmation
   available: where it and the code agree a finding is confirmed from both
   ends, and where they disagree one of them is misleading players. Six
   passes: economy, combat, supply, commanders, politics, naval. The headline
   is that **the manual's prose is reliable and its data tables are where it
   has drifted** — the pre-war policy appendix matched the shipped prefs 10 for
   10, while four production recipes, one producer and four weapon stats did
   not.

   The economy pass found **project 98 Market Reform does nothing at all**
   (both its level lookup and its pref read are discarded statements), and a
   sweep proved that failure mode is exhausted across all project prefs.

   The combat pass needed substantial new tracing, which went back into
   [11](code-findings/11-combat-resolution.md),
   [12](code-findings/12-melee.md), [13](code-findings/13-morale-and-cohesion.md)
   and [07](code-findings/07-config-data-model.md):

   - **the accuracy cascade is inert at or below 1.0.** The computed accuracy
     reaches exactly one consumer, the aim-scatter radius, where
     `Mathf.Max(accuracy/k, 1/k)` floors the divisor at 1 — and the blend's
     ceiling is 1.105, so for the 34 land weapons rated 1.0 or below (every
     standard rifle-musket) the weapon's accuracy, experience, morale, fatigue,
     gun condition, artillery zeroing and four perks change nothing about where
     the shots land;
   - **fatigue does not slow reloading.** `fatigueinfluenceonreloading` is read
     once and never assigned; the config value meant for it is parsed into a
     neighbouring field which a later line overwrites;
   - **a full `GamePrefs` sweep** — 1,972 fields — shows that is the **only**
     gameplay constant in the game read but never loaded, and turns up ~145
     loaded-but-never-read candidates, eight confirmed by hand;
   - **`weapon.bayonet` is parsed and never read**, so the manual's Bayonet
     column describes a dead field; Rebored Muskets are listed as
     bayonet-equipped and fight at the carbine `melee` value;
   - **the formation enumeration** falls out of the `unitprefs.txt` labels
     (0 Line, 1 Skirmish, 2 Attack Column, 3 March Column, 4 Square), closing
     [12](code-findings/12-melee.md)'s first open question, and with the
     shipped modifiers it shows formation is worth roughly ten times the weapon
     in a melee;
   - **a unit under the Charge order needs ~3× the enemy's numbers** before the
     micro-AI will go in — `microaitriggerforcharge` = 3 against a ratio whose
     quality terms cancel between equals;
   - **superior commanders can only become casualties in auto-resolve**, and a
     unit's own commander is rolled twice there, so auto-resolving roughly
     doubles the risk to brigade commanders;
   - **two defects in the weapon panel's own arithmetic**: every weight is
     reported 2 lb light, and the accuracy label array lost its first rung
     while keeping a clamp sized for it, so 0.8 and 0.9 share a word, "Good"
     is never printed, and accuracy ≥ 1.4 throws.

   A third pass took the **supply, depot, raiding and readiness** chapters. The
   prose held up best of any pass — the readiness restriction ladder matches
   the code gate for gate — and the tracing it needed closed
   [14](code-findings/14-supply-and-logistics.md)'s largest open question and
   added two subsystems that file never had:

   - **the depot/local split, resolved.** Depots first, foraging against the
     remaining deficit, with shipped rates that work out to **0.9 days to
     refill from a level-3 depot against 3.2 days of foraging next door and
     24.3 days at distance 2,000** — and ammunition 3.3× slower than food on
     every path;
   - **an army attaches to the best node, not the nearest**: the score is
     `capacity / distance / price`, so with a railroad worth **×11** a railhead
     outranks a plain junction eleven times further away;
   - **transport capacity never enters the supply *rate***. It decides node
     selection and how fast the economy restocks the node, so an army feels it
     through prices — true as an outcome, false as the mechanism the manual
     describes;
   - **forced march cuts resupply to zero.** The eligibility gate returns after
     consumption has been applied, and the config's intended 25 % cap
     (`maxsupplyratioretreat`) is never read;
   - **the Confiscation Act has its sign inverted** — the manual, the config
     label and the pref name all promise +15 % supply on enemy soil, and the
     factor multiplies the resupply *time*, making it 13 % worse;
   - **readiness, traced for the first time**: hardcoded quartile bands, the
     restriction ladder, −8.5/yr marching in a loyal state against −25/yr in a
     hostile one, and recovery divided by `N^0.43` so **splitting an army into
     corps to rest it is worth about 60 % more recovery per day**;
   - **raiding is a third supply source**, converting plundered goods into the
     raider's own stock, with guarding subtracted directly and floored at 0.1;
   - **ten subsidy-effect constants are shipped and never read**, because
     Transport, Trade War and Civil Order were removed as subsidy categories —
     `GameVars` still names their ids with an `x` suffix and reuses two of
     them. The manual lists only the six that exist, so this one is a modder's
     trap rather than a lie to players.

   A fourth pass took the **commander** chapters, whose 6 × 3 attribute-effects
   matrix is the densest data table in the manual — about forty claims. Most
   hold, several understate the code, and checking them needed a full consumer
   map of the five attribute getters, now
   [19](code-findings/19-commanders-and-perks.md) §2.6:

   - **four matrix cells are wrong**, and three of them are attribution
     mix-ups rather than missing features: *order delays* are not touched by
     initiative (no commander attribute appears on any line in the assembly
     mentioning delay, courier, bugle or processing), *flanking* is driven by
     initiative rather than cunning, *rallying* is the commander-nearby morale
     bonus rather than a recovery-rate term, and the enemy being *"more
     cautious vs. a very famous commander"* has no implementation at all —
     `forcebalance` is force × morale × experience and fame has no consumer in
     `AIBattle`, `AICampaign` or `Autocalc`;
   - **administration does nothing for drill or for campaign supply**, two more
     matrix cells; its real supply consumer is the battlefield model only;
   - **your fog-of-war estimate of the enemy is computed from the enemy's own
     generals.** `CalculateApproximation` reads the *enemy* commander's
     initiative and cunning and the *enemy's* average intelligence, and the
     multiplier it produces is applied by `TransformNumber` to every enemy
     figure the game shows — 104 call sites in `Statistics` alone. The same
     line computes the intelligence term as `0.3 − intel × 0.3` where its own
     label describes `1 − intel × 0.7`, so the estimate is up to 3.3× tighter
     than designed with no scouting and exactly correct with full scouting;
   - **the matrix understates several real effects**: leadership raises naval
     firepower by up to 30 %, initiative widens the command and combat range
     of every campaign formation, administration doubles an admiral's ship
     limit and damps the fatigue penalty from poor condition, and fame reduces
     desertions at the same weight as administration;
   - **the unhappiness test weights leadership four times fame**
     (`leadership × 2` against `fame / 2`) against the unit's own experience,
     which is the mechanism behind the manual's remark that veterans resent
     unknown commanders — and a defamed commander fails it outright;
   - **one `[deactivated]` config label lies.** `factorcommanderexpattr` is
     tagged deactivated and is live in every attribute getter, so a green
     commander really does operate at half his nominal attributes. The six
     other `[deactivated]` labels checked across six passes are all honest.

   A fifth pass took the **politics** chapters — national morale, state
   support, the Policies panel, Projects, Diplomacy and Interventions — plus
   the four tables of Appendix A that §6 had not reached. The prose held up
   again: fourteen checkable claims confirmed, including the loss threshold,
   support as a linear multiplier on policy research, the transitive
   deactivation cascade and the whole Militia Act I story. **Of thirty-five
   Appendix A entries bearing on support, morale, relations or the treasury,
   thirty-one match the shipped prefs exactly.** What the pass turned up went
   into [17](code-findings/17-politics-and-support.md),
   [22](code-findings/22-policy-and-project-catalogue.md) and
   [24](code-findings/24-campaign-objectives.md):

   - **the 1864 election charges its penalty twice.** Objectives 26 and 27
     both carry the tooltip *"Union national morale -20"* and both fire
     −0.20 in the same objective sweep, so a Union player who is under 50
     national morale on 9 November 1864 drops below 10 and loses the campaign
     on the next ten-minute tick. The 4 March 1865 inauguration branch that
     reads like a grace period is unreachable from either side of the test,
     and objective 27's copy of it is dead code — a correction to
     [24](code-findings/24-campaign-objectives.md) §3.2;
   - **the 1862 midterms are inert.** The −0.01-per-week national morale
     penalty is gated to chapter 1, which ends the day after the election
     date, so its reachable total is three tenths of a point;
   - **European relations cannot go negative.** Each side's per-power
     accumulator is floored at zero, so a relations-damaging Act is free when
     your relations are already at zero — and the Union starts every scenario
     at zero with all three powers. The manual's worked diplomacy example gets
     the right answer because the Confederacy's undocumented starting +10 with
     Britain supplies exactly the points the Union's Tariff Act fails to
     subtract;
   - **the Strategy panel's "European Relations" row is the intervention
     probability**, divided by a per-chapter requirement — so it reads 0 for
     both sides for the whole of Chapter I whatever the accumulators hold, the
     same lead shows a different number in each chapter, and the Union's
     figure is Spain alone. There is no dice roll behind it: the trigger is
     `>= 1f` with no `Random` in the path, and France's practical window is
     seventeen months;
   - **three routing errors in the intervention script**: objective 24's
     −0.10 lands on the Union's own accumulator, so *both* sides' major
     victories push Europe toward the Confederacy; the pullback penalty is
     charged to the intervening power's opponent rather than its patron
     (harmless only because `timesincewar` is never reset, making intervention
     one-shot); and the monthly intelligence report prints Britain's figure
     under France's name for the Confederacy and France's under Spain's name
     for the Union;
   - **a documented mechanic is shipped off.** The hostile-territory movement
     penalty the Support map filter describes is a blend against
     `adjustmentmovingspeedsupport0`, which ships at **1.0** — the identity —
     against a config label declaring `[<1]`. State support still costs
     readiness 2.9× faster, which is the whole of the real effect;
   - **"National Support" is a population-weighted average over the towns you
     own**, not over your loyal states, so capturing enemy cities lowers it
     and therefore slows your own policy research; and "Loyal States" counts a
     *share* of the two sides' support where the recruiting list reads the same
     pref as an absolute level;
   - **national morale tracks private wealth's *change*, not its level** —
     reason code 34 is a derivative against a monthly snapshot, so flat wealth,
     however high, pays nothing;
   - **three Acts still promise a subsidy category that was cut.** Letters of
     Marque, Legal Blockade and Organized Blockade Running all advertise
     raising the Trade War subsidy cap; two of the prefs are dead and the third
     was repurposed into a 25 % reduction in the blockade of your own ports —
     a better effect than the one advertised, and undocumented;
   - **a state's loyalty is decided once**, by a bare `support[0] >
     support[1]` in the single call that activates Militia Act I, and nothing
     afterwards revisits it. Ties go to the Confederacy.

   A sixth pass took the **naval** chapters and **Appendix E**, and with them
   the last unchecked data table in the manual. It needed a subsystem this
   folder had never covered, now
   [26-naval-combat](code-findings/26-naval-combat.md) with
   [summary-naval](code-findings/summary-naval.md): **fleet actions, fort
   bombardments and transport interceptions are a third damage model**,
   resolved entirely in `Autocalc` on a 21-minute campaign cycle, sharing
   nothing with either land path.

   The data came out better than any file checked so far. All 41 ship records
   are internally consistent — `guns` equals the sum of the weapon systems'
   guns and `turrets` equals the sum of their `withinturret` values — and
   Appendix E gets **all 34 armament lines** and **29 of its 34 stat blocks**
   exactly right, against six wrong numbers. What the pass found is in the
   consumers, not the data:

   - **naval rate of fire is a constant.** The term blends a long-range and a
     close-combat factor on `Mathf.Max(1f, distance / averageRangeOfInRangeGuns)`
     — and since every gun in that average passed `range >= distance`, the
     ratio can never exceed 1, so the `Max` pins it at exactly 1 and the blend
     always returns the long-range 0.25. **Closing the range buys no rate of
     fire**, and the shipped close-combat value of 1.00 is unreachable;
   - **three of the six turret ships get no turret bonus.** The ×1.4
     rate-of-fire bonus tests `withinturret == 1`, but the field is a turret
     *count*, so the Ocean Casemate Ironclad, Ironclad Turret Ship and River
     Monitor — each with a weapon system across two turrets — score zero
     turret guns while the panel still shows their guns as turret-mounted;
   - **the fleet Firepower figure is computed at point-blank range**, and the
     function that hands it to a battle takes the real fleet distance and never
     reads it — so the number that sets the victory balance is range-blind
     while the damage rolls are not;
   - **armour gates penetration rather than reducing damage**, and **target
     size is absent from hit probability** — it enters as `ceil(size)` entries
     in the target lottery and as `size × 50` hull strength, which roughly
     cancel. Three of the four clauses in the manual's one paragraph on naval
     combat point at the wrong mechanism;
   - **two mechanics the manual never mentions**: only **seven ships a side**
     fire per tick in a sea battle (three on a river) while the firepower
     comparison sums the whole fleet, and a **raiding fleet forces a
     blockading fleet to fight on even numbers**, culling the blockader's
     vessels out of the engagement until it has no more than the raider;
   - **seven ship types are in no appendix and in no project** — the European
     powers' hulls, including a 92-gun Screw Ship-of-the-Line — and **ship
     capture is unrestricted by type**, so beating an intervention squadron is
     the one way to own one. The Ship-of-the-Line is the eighth, and the
     appendix marks it honestly as *"Not buildable"*;
   - **Appendix F checks out** where it can: the scenario letters are exact and
     all sixteen battlefield ids match, but three playable historical battles —
     Champion Hill, Stones River and the regiment-scale 1st Manassas — are
     missing from its list, and the four config files it calls "the most
     important" are not where any of the six passes' data errors live.

**Pass 7 — `mechanics-findings/time-and-speed.md`** (first item off the planned
index). Translating [10](code-findings/10-simulation-tick.md) needed the file
itself extended by five findings, and corrected two published claims:

- **Battle and campaign do not share the day length.** `battleprefs.txt` sets
  `battledaylengthinmins = 120`, `campaignprefs.txt` sets the same field to
  **1.25**, under identical label text, and the campaign loader runs second —
  so a game day is **240/gamespeed real minutes in a battle and
  2.5/gamespeed in the campaign**. findings/10 §5 had claimed there was no
  campaign override.
- **That makes the historical-event cursor tight rather than safe.** A
  campaign game day at speed 50 is 3.0 real seconds, i.e. `3 × fps` frames, so
  the one-record-per-frame sweep of 119 records needs about **40 fps**, not the
  0.41 fps findings/25 §2.4 published from the battle value. Below 40 the tail
  of the catalogue — nearly the whole late war — is never tested. Corrected in
  findings/25 and `summary-newspaper.md`.
- **`Time.deltaTime` has no consumer in any simulation class**: 18 of 251
  files, 155 occurrences, 113 of them inside UniStorm, and the rest are two fps
  overlays, three light fades and unreferenced MeshAnimator demo code. Every
  simulation rate is a game-time delta, so a slow machine samples the same war
  rather than running a slower one.
- **`Application.targetFrameRate = 60`**, set once in `MainMenuControls.Awake`
  — but Unity ignores it whenever the player's VSync option sets
  `vSyncCount = 1`, so frame-paced work runs 2.4× faster on a 144 Hz monitor.
- **The battle end-of-day cycle is a seven-state machine** that fast-forwards
  the night at `SetGameSpeed(1000f, overrideminmax: true)` — the only place the
  clamp is deliberately bypassed — and pauses at dawn leaving `gamespeed == 1`.
  Daylight is 9 game hours in January and 16 in July, so a July battle day is
  160 real minutes at 1× against January's 90.
- **`UpdateEndOfMonthFunctions` is a day-counter rollover test**, not a month
  threshold: it fires when `dayCounter` *decreases*. It is the fifth scheduling
  mechanism and the only monthly one, and it is where both ministerial reports
  and every "last month" figure come from.
- **Pause stops the clock, not the frame.** `gamepaused` has thirteen readers;
  every frame-paced rolling sweep in the campaign branch keeps running, so a
  paused campaign keeps converging its derived panels.
- **The speed ladder is a UI convention.** The +/− keys step by 1.0, so every
  integer speed below the array tail is reachable and the panel highlights none
  of them — added to `ui-vs-simulation.md` as §1.10, with the day-length
  collision as its eighth label trap.

**Pass 8 — `mechanics-findings/why-units-break.md`** (second item off the
planned index). [13](code-findings/13-morale-and-cohesion.md) had the structure
right and almost none of the numbers, so the pass filled in every shipped rate
and corrected five published claims:

- **Net morale losses are multiplied by `5 − experience − leadership`.** Both
  influence prefs ship at 1.0, so every per-hour figure in the config
  understates the real loss by **3× to 5×**. `UnitPanel` sums the same slot
  values and prints *"Overall change per hour"* **without** the amplifier, so
  the panel's own total is wrong by that factor whenever a unit is losing. The
  earlier note described the mechanism but never collected the multiplier.
- **The `fightingspirit` formula was wrong.** The raw
  `support[alliance]` assignment quoted as the rule is a one-shot **seed**; the
  live derivation is four steps — a lerp `1 − (1 − support) × 0.20` for
  volunteers and `× 0.40` for drafts (so 0 % support caps at **0.80 / 0.60**,
  not at 0), minus Bounties, then `min` against the **parent formation's mean**,
  then three commander terms as **differentials against the side's own
  average** with per-level weights (fame rising 0.05→0.20, leadership falling
  0.20→0.05, experience 0.33 throughout), then −0.30 for enemy soil. Two prefs
  that would sharpen the curve are marked `-> inactive` and unread.
- **`startedwithlowmorale` is campaign-battle-only** (`currentoperation == 3`)
  and caps at **0.30** — above the 0.25 rally line but **below the 0.50 melee
  crack level**, so such a unit can rally yet can never hold contact. The
  earlier "can never recover past the break point" was imprecise both ways.
- **The two crack thresholds are 0.25 and 0.50**, so **contact doubles the
  break threshold**, and `moralealertlevel` is also 0.50 — the blinking morale
  icon means precisely "this unit breaks if anything reaches it".
- **A melee rout rallies on distance, a fire rout on morale.** `routoutofmelee`
  clears at 40 units of separation whatever the morale, setting it to 0.26;
  a unit shelled out of position must climb from the **0** that `Rout()`
  assigns, which at `0.30/hr × [1 − (1 − exp) × 0.66]` is 2 h 27 m green and
  50 m veteran.

New findings in the same pass:

- **Artillery's cover discount is inverted.** Slot 8 subtracts
  `0.75 × (1 − covervalue)`, and `covervalue` rises with cover — the same field
  slot 5 uses to *add* morale for being in cover. A unit in full cover takes
  the whole artillery morale penalty and one in the open keeps a quarter of it,
  the opposite of the pref label. Live and panel-visible.
- **`currentterrain` is decoded** from `battleprefs.txt`'s 14 named terrain
  meshes, which prices every crossing: Grassland and Road free, Bridge and
  Pontoon 0.90, Fences/Walls 0.60, Creek 0.55, Ford 0.40, and **River forces
  cohesion to exactly 0** through a ceiling that bypasses the training term.
- **`rallyvalue` resolved**: `basemoraleincrease`, latched at unit creation and
  never raised — and parsed from **two files with two units** (0.3/hr in
  `unitprefs.txt`, 0.02/day in `campaignprefs.txt`), the second instance of the
  `battledaylengthinmins` collision. The commander's rally order is a separate
  +0.20/hr channel that is cancelled by any move order and re-targets itself at
  the worst-off non-moving unit in the command.
- **`moraleincreaseeffects[3]` resolved** — it is the *"Commander rallying"*
  row in the unit panel.
- **`GetTraining()` writes to the field it reads**, adding a
  `cumulativeeffectsonce` bonus on every call, so in a DLC scenario a player
  unit's drill ratchets to its 1.0 clamp within a few frames. Base-game-safe.
- **A formation's unbearable-losses trigger is the maximum over its brigades**,
  so a corps's Loss Resilience bar shows its toughest brigade.
- **`fightingspirit` is overwritten wholesale in standalone historical
  battles** by `Mathf.Min(sideinformation[side].fightingspirit)` — a `Mathf.Min`
  with one argument, so an assignment, not a cap.
- **`Experience()` traced**: 0.0002 per enemy casualty the unit inflicts, so a
  brigade must inflict **5,000 casualties** to go from green to elite.

**Next:** `what-kills-your-men.md`, the third item on the planned index
([11](code-findings/11-combat-resolution.md),
[15](code-findings/15-attrition-and-condition.md)) — and the natural one,
because this pass left **fatigue** and **where drill comes from** as its two
open questions and both live in 15. The standing code-side backlog is
unchanged: the ~137 unverified dead prefs from the
[07](code-findings/07-config-data-model.md) sweep, a reachability audit of the
remaining project effects, the four subsystems findings/26 §10 leaves open
(transport interception, ship escape, blockade efficiency, ship construction),
whether Unity's `maximumDeltaTime` is overridden in `globalgamemanagers`, and
whether any other frame-paced rolling sweep can miss its reset window at
campaign speed 50.

**Exit criteria per file:** a reader who has never opened `code-findings/` can
act on it, and every claim in it can be traced back to a section that supports
it.

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
| Game references | **Resolved from each developer's own install**, never vendored | See [Distribution boundary](#distribution-boundary). No game DLL enters the repo or the release. |
| Target framework | `netstandard2.1` (match Unity 2021.3) | ABI compatibility with the game's Mono runtime. |
| Language | C# | Required by the goal, and native to the ecosystem. |

### Referencing the game without shipping it

The library must compile against `Assembly-CSharp.dll` but must never carry a
copy of it. Point MSBuild at a path the developer supplies, and fail the build
with a clear message when it's absent:

```xml
<!-- Directory.Build.props (tracked) -->
<Project>
  <!-- game.props is gitignored; it sets <GameDir>. Env var also works. -->
  <Import Project="game.props" Condition="Exists('game.props')" />
  <PropertyGroup>
    <GameDir Condition="'$(GameDir)' == ''">$(GTCW_GAME_DIR)</GameDir>
    <GameManaged>$(GameDir)\The Civil War (1861-1865)_Data\Managed</GameManaged>
  </PropertyGroup>

  <Target Name="CheckGameDir" BeforeTargets="ResolveReferences">
    <Error Condition="!Exists('$(GameManaged)')"
           Text="Game not found. Set GameDir in game.props or the GTCW_GAME_DIR environment variable to your install directory. The game is not distributed with this repo." />
  </Target>

  <ItemGroup>
    <Reference Include="Assembly-CSharp" HintPath="$(GameManaged)\Assembly-CSharp.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$(GameManaged)\UnityEngine.CoreModule.dll" Private="false" />
  </ItemGroup>
</Project>
```

`Private="false"` is the load-bearing attribute — it stops MSBuild copying the
game's DLLs into `bin/`, which is exactly how game assemblies accidentally end
up inside a release zip. `Krafs.Publicizer` then publicizes into `obj/` at
build time, so the publicized reference is a local build artifact that is
never committed and never shipped.

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
      and compare against known-good hashes (`code-findings/00-build-provenance.md`).
      Mismatch → loud warning, continue in degraded mode. Never hard-crash
      someone's game over a version bump.

      > **Hashing is not just the convenient option, it is the only one.**
      > Phase 1 found there is no version constant in the assembly: the game
      > parses its own version out of a `TextMeshProUGUI` label in the campaign
      > UI (`MainMenuControls.currentversion`). That is far too late and too
      > fragile to gate on. Read it if available, but only to phrase the
      > mismatch in the user's own terms.
- [ ] Land **one** end-to-end event (suggest `Battle.Started` — easy to trigger,
      easy to verify) and prove it reaches a sample mod.
- [ ] Establish **fail-soft** as a rule: a failed patch disables that one
      feature and logs; it never takes down the game or the other patches.

### A deterministic test harness, for free

`debug.txt` can boot straight into a specific battle or campaign scenario at
startup, and its "Debug Army Setup" mode loads the four ArmySetup files
(`battledata.dat`, `groups.dat`, `nations.dat`, `regiments.dat`) from
`Config/ArmySetup/` — exactly the files the ModdingTool emits.

That's a **developer-supported path to a reproducible scenario**, which is
normally the hardest part of testing game hooks. Set it up early:

- [ ] Author a minimal two-unit battle via the ModdingTool; boot directly into
      it through `debug.txt`.
- [ ] Use it as the fixture for every hook test, so runs are comparable rather
      than dependent on clicking through menus identically each time.

Many `debug.txt` flags are marked `[deactivated]`, so confirm which still work
in the shipped build before relying on them.

**Exit criteria:** sample mod prints on battle start, from a clean install,
booting into a fixed scenario without manual UI steps.

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

- [x] Reach the config parsers (Phase 1) — done; they are plain `StreamReader`
      sequences against relative paths, so post-parse interception is
      straightforward Harmony work with no Unity-specific machinery.
- [ ] Intercept post-parse and expose the parsed models.
- [ ] Typed models per data file, generated from the Phase 2 schemas.
- [ ] Conflict detection and a resolution policy.

**The game has no field-level merge** — a scenario copy of a prefs file
*replaces* the global one wholesale. **Verified in code**: the idiom
throughout `GamePrefs` is to build the scenario-local path and, if the file is
absent, reassign the path to `Config/` — one path wins entirely
(`loadcampaigncontrols`, `loadunitprefs`, `loadcontrols`). `loadcontrols` also
forces the `Config/` fallback when `SceneManagement.currentoperation` is 3 or
8, which is the mechanism behind the scale-dependent prefs finding in
`code-findings/07-config-data-model.md`. So mod overrides add a layer the game lacks entirely, rather than
extending an existing merge:

```
Config/ (global)  ─replaced by─►  scenario file  ─merged by us─►  mod overrides
     └──────── the game's two-entry chain ───────┘   └─ new capability ─┘
```

- [ ] Slot the override layer after the game's own resolution, so a mod patch
      applies to whichever file actually won. Modders then reason about one
      rule: *the effective value, whatever its source*.
- [ ] Validate before injecting. The game's data layer fails hard — a missing
      or malformed file makes a scenario fail to load — so overrides must be
      checked and rejected individually rather than inheriting that behavior.
      Phase 1 explains *why* it fails hard: `int.Parse`/`float.Parse`/
      `bool.Parse` with no guards, and field offsets that shift silently when a
      line count is wrong. Reuse the game's own
      `ImportExportUnitData.CheckFileIntegrity` as a reference for what it
      considers valid. **One exception to copy carefully:**
      `Tools.GetIntPartitions` uses `int.TryParse` and silently *drops*
      unparseable entries — so a typo'd id in a list field is invisible today.
      Our validator should report it instead.

### Effect registration — the capability nothing else provides

Policy and project *effects* are hardcoded; the data files hold only
descriptions, costs and prerequisites. A modder can add a project today and it
will appear, be researchable, and do nothing.

That makes effect registration the library's headline feature, not a
nice-to-have:

```csharp
GTCW.ModApi.Projects.Register("Repeating Rifles", effect => {
    effect.OnCompleted += ctx => ctx.Nation.UnlockWeaponClass("Repeater");
});
```

- [ ] Map which hardcoded effects are already balanceable via
      `campaignprefs.txt` (the designated balance surface) versus which need
      patching outright. That split defines the work. Phase 1 confirmed the
      split is real: some switch arms read their magnitude from `GamePrefs`
      (e.g. `machinegunprojectstock`, `project_propagandainfl`) while others
      use literals.
- [x] **Find the effect dispatch site for policies and projects, and establish
      whether new IDs can be injected or only existing ones intercepted.**
      Found, and the answer is the good one:

      | | Site | Shape |
      | --- | --- | --- |
      | Policies, one-shot | `Policies.LaunchPolicyEffect(Policy, int alliance, float timediff, bool startupset)` | ~1,200-line `switch` on `policy.UniquePolicyID`, 117 arms |
      | Policies, teardown | `Policies.DeactivatePolicyEffect(Policy, int alliance)` | mirror |
      | Policies, recurring | `Policies.UpdatePolicyEffects(int alliance, float timediff, bool startsetup)` | per-tick |
      | Projects, one-shot | `Projects.UpdateOneTimeProjectsEffects(int projectid, int alliance)` | `switch`, 12 arms |
      | Projects, recurring | `Projects.UpdateProjectEffects()` | `switch` inside an alliance loop |

      **Neither file has a single `default:` arm.** An unrecognised id falls
      through, the game still accepts/researches/tracks the policy or project,
      and nothing happens. So **new ids can be injected**, and a Harmony
      *postfix* supplies their behaviour — composing with the game's switch
      instead of replacing it. No transpiler, and none of the 117 existing arms
      need reimplementing.

      Policy ids also carry a **`+100` offset convention** for the second side
      (`case 0: case 100:` → Union / Confederate instances of one policy), so a
      registered effect must handle both id blocks or key off `alliance`.
- [x] **Resolve the prefab question — was the gating unknown.** Answered in
      [22-policy-and-project-catalogue](code-findings/22-policy-and-project-catalogue.md)
      §15. **Projects need no prefab at all**: `projects.dat` is the whole
      definition and ids 20–29 and 42–87 ship as `PLACEHOLDER`. **Policies
      cannot be added from config** — `policies.dat` only overrides ids the
      scene already carries — but `Policy.InitializeStatics()` is
      `public static` and rebuilds `allpolicies` from every `Policy` component
      under the `UI` GameObject, so cloning a policy GameObject there and
      calling that method is the registration route. Three things still need a
      live build before the ModApi commits to a public policy-registration
      surface: whether the clone is found, whether `ButtonTooltipRef` and
      `markedasgrandpolicy` survive cloning, and what the panel does with a
      button it has no layout slot for.
- [x] Harvest the id → effect catalogue into
      [code-findings/22-policy-and-project-catalogue.md](code-findings/22-policy-and-project-catalogue.md).
      All 117 policies and 125 projects, with chapters, prerequisites,
      reason codes, the state-index table and the AI agendas. One correction to
      the plan: `Projects.GetProjectEffect` is a real rosetta stone, but
      `Policies.UpdateAllTooltips` is **not** — it only appends research-speed
      text, so the policy half had to be read out of the 1,200-line switch.
      Also note the effect contract: `UpdatePolicyEffects` resets thirteen
      fields and **replays every policy's effect each tick**, so a postfix must
      either guard on `alreadyactivatedacts` or scale by `timediff`.

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
- [ ] Package per the [Distribution boundary](#distribution-boundary): our DLLs,
      docs and sample source only. Modders build against their own install.
- [ ] State the game requirement plainly in the README and on every release
      page, so nobody expects a playable download.
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
3. **Keep every note tied to a build** via `code-findings/00-build-provenance.md`.
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
| Game assembly swept into a release artifact | Low but serious | `Private="false"` on all game references; release-packaging filename check |

---

## Distribution boundary

**The hook library ships alone. Everyone who uses it supplies their own game.**
This is a hard constraint on the design, not a disclaimer bolted on at release.

### What ships, and what never does

| Ships in the release | Never ships |
| --- | --- |
| `GTCW.ModApi.dll` (public API) | `Assembly-CSharp.dll` or any game assembly |
| `GTCW.ModApi.Bindings.dll`, `GTCW.ModApi.Plugin.dll` | Publicized reference assemblies (build artifacts in `obj/`) |
| Docs, API reference, sample mod **source** | Decompiled C# source, in whole or in part |
| The signature manifest (names and hashes only) | `Config/` data files, or default data extracted from them |
| Our `LICENSE`, covering our code only | Assets: textures, audio, meshes, maps |

Three consequences that are easy to get wrong:

- **The signature manifest stores names and hashes, not bodies.** Method
  signatures and IL hashes are the fingerprints we need to detect drift; they
  are not the game's code, and the manifest must never grow into a dump of it.
- **Typed config models are schemas, not data.** Phase 4's override system
  defines the *shape* of `weapons.txt` and reads values from the user's own
  install at runtime. We ship no default values and no copy of any `.dat` or
  `.txt` from `Config/`. A mod that wants to change a number expresses it as a
  delta against whatever the player already has.
- **Sample mods ship as source, with no bundled game data**, so a sample can
  never become a backdoor for redistributing content.

### Who needs a copy of the game

| Role | Needs the game? | Why |
| --- | --- | --- |
| Contributor to this repo | **Yes** | Compiles against `Assembly-CSharp.dll` from their own install |
| Modder building against the API | **Yes** | References `GTCW.ModApi.dll`, runs against their own install |
| Player running a mod | **Yes** | Obviously — the library only exists inside a running game |

Nobody obtains any part of the game *through us*, in any of those roles. The
library is a lens on software the user already owns.

### How this is enforced, not just intended

- `.gitignore` keeps `game/`, `decompiled/` and extracted output out of the
  repo entirely — already verified against real paths.
- The build resolves game references from a developer-supplied `GameDir` with
  `Private="false"`, so no game DLL is copied into `bin/` and none can be
  swept into a release archive.
- Publicized assemblies are generated into `obj/` per build — local artifacts
  by construction.
- BepInEx injects at runtime via UnityDoorstop. **No game file is modified on
  disk**, so there is no patched binary that anyone could be tempted to
  redistribute, and uninstalling means deleting our files.
- [ ] Add a release-packaging check that fails if any game-owned filename
      (`Assembly-CSharp*`, `*.assets`, `UnityPlayer*`, `global-metadata*`)
      appears in the artifact. Cheap, and it catches the one mistake that
      actually matters.

### Conduct

Single-player strategy game with an official, developer-shipped ModdingTool —
modding is sanctioned, and there's no anti-cheat or multiplayer integrity
concern to navigate. Remaining diligence:

- Read the EULA's reverse-engineering and modding clauses before public
  release, and respect any distribution limits it sets.
- Keep `code-findings/` descriptive: explain mechanisms and document schemas rather
  than pasting decompiled source. A note should be readable without the
  original code beside it.
- The developers have been modding-friendly; engaging their community early is
  worth more than operating quietly.

---

## Immediate next steps

1. Read `Modding/ModdingTool Quick Guide.pdf` — may hand over the data model.
2. Install .NET SDK + `ilspycmd`; export `decompiled/Assembly-CSharp`.
3. Set Steam to update-on-launch only.
4. Phase 1 namespace/type inventory → `code-findings/` (done).
5. Locate the `Config/` parsers — best single return on effort, since it
   unlocks both the data schemas (Goal 1) and the override system (Goal 2).
