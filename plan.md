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

**Pass 9 — `mechanics-findings/what-kills-your-men.md`** (third item off the
planned index). [15](code-findings/15-attrition-and-condition.md) had every
formula and almost no values; this pass supplied them all and turned them into
men per week.

- **The wounded pool is where most of the dying happens**, and the player
  controls it: mortality 0.66 against recovery 0.50 in the field (**57 % of
  wounded die**) versus 0.33 against 1.00 encamped (**25 %**), and 3 % with a
  level-3 hospital in range. Of 100 men hit by musket fire, **54 end up dead if
  the army marches on and 31 if it camps**.
- **The per-man casualty roll decoded**: wounded by default, then `deadratio`
  by **ammunition type** (musket 0.15, shell 0.20, solid 0.50, **canister
  0.60**), then a flat 0.15 missing that overwrites the killed result. Canister
  kills four times the share musketry does.
- **Disease solved to its steady state**: target 10 % of strength, ×1.66 forced
  march, ×1.80 below 50 °F, ×1.40 bad weather, **×0.50 encamped**; pool
  turnover 2.5 against 5.0, so one sick man in three dies. For a 3,000-man
  brigade that is **4 deaths/week in camp, 8 in the field and 35 on a forced
  march through a bad winter** — 7 %, 14 % and 60 % per year.
- **Desertion quantified**: `0.1 × drive² × strength × supportFactor`/yr, zero
  above 80 % morale under an average general, and **`supportFactor` never
  reaches ×1** — it runs ×5.0 at zero state support to ×3.0 at full, against a
  label implying the factor should vanish at full loyalty.
- **Condition netted out**: a fully drilled brigade's men **gain** condition on
  a forced march (+5/yr) while its guns always lose it (−3/yr), and the
  recovery line labelled *"when not moving"* is applied unconditionally, so
  ordinary marching costs nothing.
- **Fatigue**: a normal-pace road march makes a unit **fresher** (0.50 accrual
  against 0.60 recovery); melee exhausts it in **5 minutes** and recovery takes
  1 h 40 m; the speed penalty is nearly flat (40 % speed at full exhaustion).
- **Drill cannot be bought in the field** — 0.20/yr is five years to full. The
  overriding if-chain means **entrenching a camp cuts drill from 0.60 to 0.40**
  and **winter quarters are the worst camp to drill in** at 0.30.

New defects and decodings:

- **Exhausted troops do not reload more slowly.** `fatigueinfluenceonreloading`
  is read by `Regiment` ~9998 and **never parsed by any config file**; the line
  written for it lands in `fatigueforreloading`, which the next pref in the
  same loader overwrites sixteen lines later.
- **`hospitalinfluenceonmortality` ships at −3 against a documented `[-1 to 0]`
  range**, making hospitals twice as strong as their label allows — and the
  tooltip prints the raw coefficient, claiming a **300 % mortality reduction**
  where the real figure is 75 %.
- **"Disabled Soldiers" includes the killed and the captured**, and its own
  three-line breakdown omits both while adding deserters, so it cannot
  reconcile with its headline in either direction.
- **Deserters are absent from every readable loss total** on the campaign map:
  the `attrition_tmp` slip, and `grouplosses` summing only wounded + killed +
  prisoners in the campaign branch while the battle branch includes them.
- **`unittypesize[]` confirmed** as the config's *"Maximum Unit Size"*
  (infantry 3000, cavalry 2500, artillery 240, skirmishers 200, scouts 150) —
  the base the whole wounded model is keyed to.
- **`GetMedicalTreatmentValue`'s three modes decoded**, including the own-side
  colour test on the hospital filter map and the 0.4 / 0.65 / 1.0 level values.
- **The three camp flags resolved**: `encamped` and `entrenched` are null tests
  on `campbuilding` and `entrenchment`, and **winter quarters are a camp
  building** — which is why they carry the encamped wounded rates and the ×0.5
  sickness factor but the slower drill rate.

**Pass 10 — `mechanics-findings/supply-in-practice.md`** (fourth item off the
planned index). [14](code-findings/14-supply-and-logistics.md) was already the
best-documented subsystem in the folder, so this pass went after what it had
left out: the battlefield half, and the end-of-day batch.

- **Battlefield supply chains are rooted at corps.** `supplytoplevelbf` = 15,
  and the pref's own label decodes the echelon ladder (13 Brigade, 14 Division,
  15 Corps, 16 Army). **Distance barely matters** — `distance^0.1` weighted
  0.15 gives 0.92 even at 2,000 — while **depth and the commander do**: a
  brigade two echelons down under a mediocre administrator draws **72 %**,
  under a poor one 62 %, and detached a further ×0.5.
- **`overridesymbol` resolved** as an echelon override falling back to
  `unittyp`, plus two inert code slips in the same method (a dead
  `supplystate = -1f` store, and a `num`/`num2` copy-paste in the `flag` loop).
- **The corps wagon train**, which no file had covered: `groupsupplyreserve` =
  **1 / 1 / 2.5 / 2.5 man-days**, seeded at battle start and handed out each
  night as `deficit × that brigade's own supply state`, pro-rated when the pool
  is short. `EatProvisionsAndForage` takes **one full man-day per brigade per
  night**, so **two and a half nights of food with no other source** — which is
  what ends long battles rather than any explicit limit.
- **Interdiction is binary and judged at nightfall.** The handout is scaled by
  each brigade's own state, so a cut line means *no* resupply rather than slow
  resupply — and `CalculateSupplyLine` is only called under
  `eodcycle > 0 || supplyiconactive`, so **a line cut at midday and cleared by
  dusk costs nothing**, and mid-battle supply figures are stale unless the
  overlay is on. Routed and surrendering brigades are skipped entirely.
- **The end-of-day batch quantified.** All three terms of
  `CheckDesertionsAndMorale()` are flat per-night hits with no `elapsed`
  scaling: a fifth-power **casualty shock** (−0.23 at 30 % losses for green
  troops, **exactly zero** for a unit both elite and fully drilled, since both
  terms carry `(1 − stat)`); a seventh-power **supply hit** (nothing above 50 %
  average supply, −0.15 at 20 %, −0.70 at empty); and **desertion at
  `drive² × 0.2 × strength`**, which drops the campaign formula's base rate,
  its `supportFactor` *and* its `× elapsed`. A 3,000-man brigade at 50 % morale
  loses **54 men in one battle night** against about 0.03 on the campaign map —
  **roughly two thousand times the rate.**
- **`groupsupplystate[]` resolved** (§2a): `stock / relevantStrength /
  groupsupplyreserve`, so the formation figure is *days carried as a fraction
  of the reserve*, not a ratio against consumption — and the four code paths
  disagree about which unit types draw ammunition and forage, with allocation
  and consumption including the light types while the fill ratio excludes them.
- `eodcycle`'s state machine, open in findings/14 since the supply pass, is now
  closed by pointing at [10](code-findings/10-simulation-tick.md) §8.

**Pass 11 — `mechanics-findings/the-manpower-tap.md`** (fifth item off the
planned index). Taken as two halves: where men come from
([17](code-findings/17-politics-and-support.md),
[23](code-findings/23-game-state.md)) and what spends them — which closed the
largest named gap in the logistics subsystem.

- **Replacements have no system of their own.** `UpdateIIPSupplyLink` — the
  campaign resupply method — fills a formation's brigades back up from their
  home states' pools, with no player order, at **a full unit in 1.5 campaign
  years** at its supply node and 4.0 at map distance 2,000. That is **26 men a
  week** for a 2,000-man brigade. Now [14](code-findings/14-supply-and-logistics.md) §3b.
- **`prioritysteps` is {0, 0.5, 1.0}**, so the *low* reinforcement priority the
  panel offers as the first of three settings **switches replacements off
  entirely**.
- **Reforming is the only fast channel**: ×10 rate, supply treated as full, one
  month, returning a brigade at about **55 % of establishment**.
- **The winter reinforcement penalty applies in winter quarters**, because
  `GetWeatherAdjustment()`'s winter-quarters branch resets four of its five
  values and not that one — and `GetReinforcementspeedWinter()`, which
  implements the config label's rule at **1 %**, has **no callers anywhere**.
  Two implementations of a published rule, neither matching the label.
- **Both recruiting pools are lifetime quotas.** `volunteersrecruited` /
  `draftsrecruited` only ever grow: casualties are never credited back,
  disbanding refunds survivors only, and the scenario's starting order of
  battle is billed to its states at frame 41 of a new campaign.
- **The draft ships switched off.** All six authored campaign starts carry
  `mobilizationrate = {0, 0}` in every state, so a 1861 campaign cannot draft
  until a Draft Act passes; 1863 and 1864 pre-research theirs.
- **Drafting burns the support it is gated on** at −0.000016 per man, which is
  **625 men per point** — so a 90 %-support state yields about **37,500**
  drafted men before it crosses the 30 % gate and closes itself off. Volunteers
  cost no support at all.
- **The ninety-day men are modelled.** Contract expiry keeps
  `0.10 + 0.50 × support` of a unit (±20 %) and the leavers are gone from the
  pool as well as the unit, wounded included — and the Union's Militia Act I
  *shortens* contracts from 12 months to **3**. The Draft Acts' extension is
  Confederate-only: `draftresetcontractlength`, the Union's own {36, 36}, is
  parsed and never read.
- **The USCT support-floor exception leaks to the Confederacy** — its `flag` is
  declared outside the per-alliance loop in `UpdateRecruitData`.
- **The recruitment subsidy buys no recruits**: `additionalrecruitssubsidies`
  = 2, labelled as a doubling of volunteers under full funding, is used as the
  *slew rate* of a factor whose target has no subsidy term.
- **`aodemand` closed** ([23](code-findings/23-game-state.md) §13): written by
  `ResourceUsage.AddAODemand` from every priced requisition including
  replacements, consumed by `Economy` as per-goods national demand — so
  recruiting bids up its own equipment prices.

**Pass 12 — `mechanics-findings/how-you-lose.md`** (sixth item off the planned
index). Translating [17](code-findings/17-politics-and-support.md) and
[24](code-findings/24-campaign-objectives.md), and in the process closing the
last three unmapped national-morale reason codes — the three the player
generates by playing.

- **There is exactly one loss condition and no time limit.** Campaign
  objectives 0 and 1 are live in every chapter and test nothing but the other
  side's `nationalmorale` against `breakmoraletrigger = 0.25`. The chapter
  machine has three transitions and no fourth chapter, and the only
  date-driven end-of-campaign call in the assembly is unreachable. **A war
  neither side is losing runs indefinitely.**
- **Codes 1, 2 and 3 — battles, naval engagements, town occupation — are the
  smallest terms in the model.** Now [17](code-findings/17-politics-and-support.md)
  §3a with every shipped magnitude.
- **The exponent 0.3 is why battles do not decide campaigns.** The charge is
  `losses^0.3 × 0.0004`: **250 casualties cost 0.21 points and 50,000 cost
  1.03**, a 200× range in blood for 5× in morale, with no reference to army
  size — the one expression that computes starting strength feeds only a debug
  line. **Both sides are charged, the winner included**, and there is no
  victory bonus in the national-morale path at all.
- **There are two implementations of that formula** and the auto-calculator's
  is missing three of the live version's four multipliers: the surrender ×3,
  the Lieutenant General ×1.2 and the AI's difficulty discount. So
  auto-resolving charges the AI at full price and charges the player less for
  capitulating.
- **Towns charge the loser, never the captor**, at `−0.125 × importance/10` —
  and because `ceil` puts almost every town at importance 1, that is a flat
  **−1.25 points**, with only the single largest city in the game reaching
  −12.5. The expensive half is the state's support bleeding at **−1.00 per
  annum × the share of its towns occupied**.
- **`Town.topincometemp` is a high-water mark that never resets**, so every
  town's importance — and the morale cost of losing it — ratchets down as the
  war impoverishes the country.

- **The two continuous taps decide the war.** National morale converges on
  `averagesupport` at half the gap per year — a two-year time constant, so
  **over four years it ends up wherever your state support is** — and if
  support holds above 25 % the convergence alone can never lose you the
  campaign. The private-wealth term pays on month-over-month *growth* at
  10.0/yr, about **±0.4 points per point of growth in a month**.
- **The 1864 election is a pass/fail test at 50 points**, charged −20 twice
  from two objectives in one loop, which puts the Union under the floor
  immediately. Both tooltips promise "−20". New
  [ui-vs-simulation](mechanics-findings/ui-vs-simulation.md) §1.14, and
  [summary-campaign-objectives](code-findings/summary-campaign-objectives.md)
  corrected, which still carried the superseded "ends at the inauguration"
  reading.
- **Below 40 % national morale routed units surrender instead of rallying**,
  at `0.01 / (natmorale × armymorale)^1.5` per rout — 11 % at the gate and
  certain below a product of 0.046 — and a surrendered army triples that
  battle's own morale charge, which raises the next probability.
- **The difficulty slider gives the AI no morale head start.**
  `usedcampaignbonus`'s one-shot is applied in the battle scene's
  `battledata` import and overwritten by `OverwriteNationsData` in the next
  statement, every battle, for the whole campaign. It is also indexed by
  **battle seat** rather than alliance id — in a battle the AI initiated it
  would land on the player — and its volunteers line, scaled for a 0–50
  slider against a shipped maximum of 0.5, is worth +0.5 % rather than +50 %.
  Corrects [20](code-findings/20-ai-campaign.md) §9.1 and
  [summary-ai-campaign](code-findings/summary-ai-campaign.md).
- **Two of findings/24's eleven unattributed war-progress statics are
  located**: `Town.UpdateOwnership` writes `csacapturedtowns` and
  `confederacyinvaded`, each guarded on the town's state being loyal to the
  side losing it, so neither advances in a border state and neither is undone
  on recapture.
- Smaller: **the Union's Abolition act gives the Confederacy +10 national
  morale and the Union +5**; the war bar is normalised to the 25 % floor
  rather than being a ratio of the two percentages, which makes it the one
  readout here that is *more* informative than it looks; and two DLC
  town-capture events are unreachable because an `else if` repeats its `if`'s
  condition instead of negating it.

**Pass 13 — `mechanics-findings/policy-order.md`** (seventh item off the
planned index). Translating
[22](code-findings/22-policy-and-project-catalogue.md) and
[09](code-findings/09-policy-and-project-effects.md), and closing the subsidy
half of findings/22's open question 4 along the way.

- **The policy-slot counter is a lifetime budget, not a queue width.**
  `CanBeActivated` counts non-acts in `activatedpolicies` *regardless of
  progress*, and only acts are retired on completion — a finished ladder level
  has to stay, because that is where the replay architecture reads its effect
  from. With **20 non-act policies per side** and a cap of
  `7 + round(5 × the politics slider)`, a nation can hold at most 12 of 20:
  **the top of three of the five branches, never five.**
- **Lowering the politics subsidy deletes finished policies.**
  `UpdateSubsidyEffect` enforces the cap every alliance pass; in-research
  policies always score higher on `duration + (1 − progress)` and go first,
  but once everything is complete it cancels completed ladder levels — and
  `RemoveResearch` zeroes the **entire transitive closure** above whatever it
  removes. Completed acts are spared; completed ladder levels are not.
- **The subsidy factor is also the project-funding accrual rate**, which
  closes open question 4: `subsidyfunding[type]` accrues at
  `slider × basicsubsidiescost × ladder factor` per annum, exactly what the
  treasury pays, with `AppointProject` the only debit. So **politics is the
  only category funded at full rate in 1861** — no Politics ladder means its
  factor stays 1.0 at $10 M/yr while the other five sit at 0.1 and accrue
  $5 M/yr even at a maximum slider. Now [22](code-findings/22-policy-and-project-catalogue.md) §3.2b.
- **Completing a ladder level raises the bill, not just the ceiling.**
  `AdjustSubsidyFactor`'s slider-rescaling parameter defaults to off, so
  Military III at a 60 % slider takes military spending from $15 M to $30 M a
  year with no player action.
- **The policy panel prints two different day counts for the same policy.**
  `UpdateAllTooltips` divides by average support, by `1/N^1.1` and by the
  Administration Reform factor, and itemises all three in prose;
  `UpdateActivations`, which writes the policy's own `DurationText`, applies
  **none** of them. At 75 % support with three in research the number on the
  policy is **4.5× too optimistic**. Corrects
  [22](code-findings/22-policy-and-project-catalogue.md) §3.1, which took the
  tooltip's correctness to cover the panel; new
  [ui-vs-simulation](mechanics-findings/ui-vs-simulation.md) §1.15.
- **A project's `datefrom` gates nothing.** Neither `IsAppointable` nor the
  panel's list filter reads it. Its only consumer,
  `Projects.CheckProjectUnlocks`, runs **once at frame 32 of a new campaign**
  and *grants* every dated project whose date has passed — checking the
  category balance and **never debiting it**. So a 1861 campaign can buy
  Military Railroad in March 1861, and a Spring 1864 campaign is handed most
  of the dated list for free, because its `subsidies.dat` opens at
  6 / 8 / 3 / 16 / 12 / 4.8 $M against Spring 1861's all-zeros. Now §9.3.
- **The three pre-war keys, counted.** Five grand policies gate the upper half
  of a wartime ladder — Union 74/75/76, Confederate 175/176 — and you pick
  three. So **the Union must spend all three picks on ladder keys** to keep
  every branch reachable where the Confederacy needs two; Confederate
  Diplomacy IV has no pre-war requirement where the Union's does. The
  tooltips do say so, in capitals, which makes this arithmetic rather than
  disclosure.
- **Policy 73's effect was documented with the wrong sign.**
  `abolitionismcsasupport` ships at **−0.10**, so Support Abolitionism *lowers*
  Confederate support in Union states by ten points rather than raising it.
- Also priced for the player: every project's first-level cost from
  `projects.dat` against the `√level` curve, the six structural reforms worth
  about $8 M together, **Security Measures sinking half the Confederate navy
  and halving its weapon stocks**, and Indian Wars handing the enemy the same
  military-experience grant it gives you.

**Pass 14 — `mechanics-findings/commanders-worth-having.md`** (eighth item off
the planned index). Translating
[19](code-findings/19-commanders-and-perks.md), and in the process pricing
every one of the 48 perks — which turned up four shipped arrays that point the
wrong way.

- **The six campaign perks findings/19 left with an empty effect column all
  have real effects.** Siege Train, Engineers & Mechanics, Sappers and Miners,
  Partisan Brigades and Limelights all ship **×1.25 / 1.50 / 2.00** on siege
  strength, construction speed, siege assault, raiding casualties and night
  combat respectively, reached through `HasPerkParent` sites in `Autocalc` and
  `CBuilding`. Now [19](code-findings/19-commanders-and-perks.md) §1.7 with
  every array's values.
- **Balloon Corps lengthens your order delays.** `orderdelaysballooncorps`
  ships **1.25 / 1.10 / 1.10** against a multiplier on the delay, where the
  adjacent `fieldtelegraphorderdelays` ships 0.9 / 0.8 / 0.7. Taking the perk
  costs order speed at every level and levelling it only recovers part of the
  loss.
- **Bushwhackers gets worse as it levels.** `skirmishingbushwhackers` ships
  **1.25 / 1.20 / 1.10** as a multiplier on raiding casualties, while its
  support-drop sibling rises correctly — diminishing increments authored as
  absolute factors.
- **Field Telegraph conceals and the intelligence perk exposes.**
  `informationdelay` and `informationdelaybureauofmilitaryinformation` ship the
  same 1.0 / 0.75 / 0.5 and are applied to the same spot-mark probability with
  **opposite operators**. The probability is the chance the *player* is shown a
  last-seen mark for an enemy formation and the perk read is that formation's
  **own**, so Field Telegraph makes its holder half as easy to track and Bureau
  of Military Information makes it **twice** as easy.
- **Engineers & Mechanics is the one paired `HasPerkParent` site that does not
  stack** — both branches assign `constructiontimer`, so the parent's level
  overwrites the Corps' own.
- **One genuine cross-namespace perk ambiguity**, qualifying findings/19 §1.4:
  `Regiment` ~4700's raiding-target filter tests `HasPerk(6)` with no
  `unittyp == 17` guard, so campaign perk 6 (Foot Cavalry) satisfies the
  fleet-only Sea Raiders clause. The `IIP` copy is guarded and no shipped trade
  node is reachable by land, so it is latent.
- **`project_civilorderraids` is a second discarded-statement pref.**
  `Regiment` ~4661 and ~4663 compute Civil Order's raid factor as bare
  expressions and throw both away, immediately before the two `AdjustSupport`
  calls it was meant to scale — so **Civil Order protects you from the support
  cost of casualties and not from the support cost of raids**, although the
  panel advertises both. Corrects
  [22](code-findings/22-policy-and-project-catalogue.md) §12.3, which had
  Market Reform as the only instance, and the row in
  [policy-order](mechanics-findings/policy-order.md) §8.2.
- Also priced for the player: **Close support delivers its whole ×2.0 canister
  bonus at level I**, **Skilled laying III is the only perk that grants an
  immunity** (zero demoralization under artillery fire), **Mounted rifles takes
  the mounted-fire penalty from ×0.75 to ×0.2**, and **Supply Colliers is the
  only perk with a standing penalty** (−30 % fleet speed at level I).
- On the commander side, the numbers that change decisions: **leadership
  swings retreat losses from +6 % to +42 %**, **two years in grade alone
  promotes an AI officer** (`exp × 0.6 + fame × 0.525 + years × 0.5 > 1`),
  **every commander change costs 10 points of readiness to every attached
  unit**, **a feud is an hourly `|Δinitiative| + Δfame − 0.3`** so any visible
  gap makes one certain, invented replacement commanders roll **[0.1, 0.7]**
  where European ones roll **[0.5, 0.9]**, and it is **easier to become famous
  in a minor victory than a major one**.
- And a duplicate row for findings/19 in `code-findings/README.md` — two
  entries for the same file, the older one superseded — removed.

**Pass 15 — `mechanics-findings/fighting-the-ai.md`** (ninth item off the
planned index). Translating [20](code-findings/20-ai-campaign.md) and
[21](code-findings/21-ai-battle.md), and pricing the difficulty package for
the first time.

- **The difficulty slider's centre of gravity is a casualty discount, and it
  is not a combat modifier.** `casualtiesmodifier = bonus × 0.50`, and its
  single consumer is `Regiment.SufferCasualties` ~16249:
  `if (playeralliance != alliance) amount /= 1f + casualtiesmodifier`. Because
  it sits in the one entry point every loss passes through, **at Very Hard
  every non-player unit takes 20 % fewer casualties from combat, disease,
  desertion and raiding alike.** Now
  [20](code-findings/20-ai-campaign.md) §9.1 with the whole package tabulated:
  national morale ×(1−b) in the live path only, state support ÷(1+0.3b), and
  morale recovery, unit experience, military experience, readiness, weapon
  production and all spending at ×(1+0.3b).
- **The two sliders reach the player as five-step labels**, `ceil(x × 5) − 1`
  over the normalised position: **Very Easy → Very Hard** in 0.1 bands of
  `bonus`, **Calm → Very High** in 0.25 bands of `aimode`. The battle start
  menu shows the same `aimode` field as a binary "AI: historical" versus
  "AI: dynamic" instead.
- **The support-hit handicap leaks between the two sides.** `num67` in
  `ImportExportUnitData` ~3641–3648 is not reset between side 0 and side 1, so
  when the AI is evaluated second its support hit inherits the first side's
  **Civil Order project** multiplier as well as its own. One-directional —
  player first, AI second — and it compounds with project 107's raid half
  being inert entirely.
- Missing shipped values filled in:
  **`aiagressivenessoffensiveoperationfaction` = Union 1.0, Confederacy 0.5**,
  so the Confederate AI evaluates offensive operations at half the Union AI's
  rate; `winteroffensiveoperationsmodifier` = **0.05** against the defensive
  sibling's 0.2; `probdefensiveop` = **75** Union and **60** Confederate per
  annum, about one consideration every five and six days.
- Translated for the player, from material the code files already had: the AI
  sees **every formation on the map** and reads **destinations with the
  order-transmission delay bypassed**; **your concealment hides you from
  nobody** while the AI's hides it from you; **fast-forwarding starves the
  campaign AI to `√(game speed)` passes** and a battle's micro AI linearly,
  while leaving its army-level decisions untouched; **chapter 0 ships zero
  aggression** so the phoney war is data rather than code; the **cubic
  anti-stall term** makes a stalled AI army attack within a year of sitting
  still; the **capital-distance ÷ 3 reflex**; interceptions that cancel
  themselves at 40 % morale or a fifth of their strength; **reloading resetting
  every defensive operation's give-up baseline**; the two crisis doctrines that
  cancel the AI's own research; a battle stance ladder where **an initiative of
  1.0 divides every threshold by 1.7** and a general above 0.6 initiative
  **never retreats voluntarily**; and the eight exploitable battle defects —
  **artillery always on the left wing**, coin-flip reserve commitment, one
  reserve per tick, the left-wing unit never withdrawn, two cooldowns that
  never gate, a charge test whose strength weighting cancels, a feuding
  formation that can never assault, and a defensive-facing subsystem that has
  never run.
- Also noted for the player: **giving one of your own formations a stance
  enrols it in the player-side battle AI**, which will fight it for you but
  will **not** march to the sound of the guns — that flag ships `false` for
  your units only, while the enemy AI always runs it.

**Pass 16 — `mechanics-findings/events-and-the-press.md`** (tenth and last
item off the planned index, which completes the mechanics layer) **and
`code-findings/27-dispatches-and-the-message-log.md`** (763 lines, a new
findings file). Translating
[25](code-findings/25-newspaper-and-events.md), and closing its open question 2
by writing up `Messages`, `MessageLog` and `Dispatch` — the channel every
earlier pass had deferred.

- **The one fog-gated dispatch never waits for the sighting.**
  `useunitspotted` has exactly one caller in the whole assembly —
  `msg_enemyreinforcementsarrive` — and it passes `timedelay: 0f`, so
  `CheckDelayedMessages`' elapsed-time clause (`time + 0 <= Hour`, with `time`
  stamped as the current hour) is satisfied on the very next frame and the fog
  clause is never consulted. **Enemy reinforcements are announced by the
  commander's name, with formation type and approximate strength, through fog
  of war, in every battle.** The figures still pass the `approximationvalues`
  filter, so fog blurs the numbers and leaves the arrival public. One-argument
  fix. The same site also burns two issue numbers per dispatch, leaving a
  visible gap in the letterhead numbering.
- **The battle de-duplication window compares hour-of-day, not elapsed time.**
  `uniStormSystem.Hour − message.time <= checktimeoflatestmessage` (**0.5**
  game hours) goes negative once a battle crosses midnight, and a negative
  difference satisfies the test — so a dispatch raised at 18:00 on day one is
  treated as half an hour old until 18:30 on day two. Since the three
  ammunition warnings have **no hysteresis at all** (they re-arm at exactly the
  firing threshold, unlike the morale warning's 1.2×), this window is the only
  thing stopping them spamming — and after midnight it stops them entirely.
  The campaign branch uses `currenttimefromstart` and is correct.
- **The Quartermaster's Morning Report always says morale is "intact."** Its
  first sentence indexes `GameVars.desertiontextinfo` — a *desertion* scale —
  with `desertionslastnight × 10` on the player's **top formation**. That field
  is written in exactly one place, `CheckDesertionsAndMorale` ~8140, inside a
  `unittyp <= 13` guard, so on an Army or Army Group it is never written, stays
  0, and clamps to band 0. Four of the five strings are unreachable. The
  dispatch channel's exact counterpart to the newspaper's constant morale
  paragraph — and unlike that one, **not** a config fix.
- **The campaign supply warning tests two of the four supplies.**
  `msg_outofsupply` additionally requires
  `groupsupplystate[0] < 0.25 || groupsupplystate[3] < 0.25` — rifle ammunition
  or forage. Artillery ammunition and **provisions** are absent, although the
  dispatch body reports all four by name, so an army can starve of food with no
  warning raised.
- **The monthly intelligence report is near-perfect information, free.**
  Enumerated for the first time: every policy the enemy finished and every one
  it is still debating by name, its recruitment or credit crisis stated
  outright, every project completed, every Corps and Army raised **with the
  state it is meant to operate in**, every fleet raised, its armies' **exact**
  average morale and the month's change, the top three high-ranking officers
  lost by name, and its doctrine in plain English. **Only the recruited-men
  figure passes the fog filter** (`approximationvalues` = 50/100/500/1000/2000);
  everything else is printed exactly. And `lastmonthaistrategy` is **a
  high-water mark, not a doctrine** — five `AICampaign` sites raise it with
  `if (x < n) x = n` and only the month-end reset lowers it — so the sentence
  reports the most aggressive thing the AI did, with raiding masked by anything
  else and value 4 raised **while a sea-invasion force is still assembling**,
  which makes *"planning a naval invasion"* the one genuinely actionable
  warning in the game.
- **Log retention is ten game days, hardcoded, and enforced only while the log
  panel is open.** `GameVars.holdmessagesfordays = 10f` is a bare field
  initialiser with no loader and no other writer — in no config file and no
  options screen — and `MessageLog.CheckAutoRemoval` runs in the panel's
  `Update`, which `HidePanel`'s `SetActive(false)` stops. So dispatches are
  destroyed **the moment the player opens the log**, invisibly, and at campaign
  speed 50 ten game days is **thirty seconds of real time**. Untested: the
  panel-object assumption wants an asset dump.
- **Three fully authored dispatches have no writer** — ids 12, 13 and 14: the
  enemy proposing a cease-fire, warning of its own retreat, and accepting terms
  of surrender in words close to Lee's at Appomattox. All three are written as
  requests for the player's orders, and the first explicitly waits on a reply,
  so together they describe a negotiated-battle-end layer the game does not
  have.
- **46 declared ids, not the ~45 findings/25 estimated**, all 46 with a case
  and **no `default:` arm** — so an unknown type yields an empty letter rather
  than the newspaper's diagnostic string. Twenty-five ids inside the two ranges
  are undeclared (17–19; 52, 62–73, 77, 78, 80–86). The symbol index is
  `type + symboloffset`, with the campaign passing a hardcoded **−45**, so
  battle type 0 and campaign type 50 share atlas slot 5.
- Smaller findings: the **very-high-casualties dispatch has a dead twin**
  (`Regiment` ~8183 differs only in signing the parent, and its
  `!messagetriggered[4]` guard is already false), so the casualty warnings are
  signed by the **regiment** while every other warning is signed by the
  **corps** (`messagehighestunitlevel` = 15); the morale and rout warnings
  exist at exactly **two echelons, Division and Corps** — an Army, an Army
  Group and a brigade are all silent; delayed dispatches are **never
  de-duplicated in the campaign** (both time branches sit inside
  `if (!IsCampaign)`); the main duplicate test omits `targetdestination` while
  the delayed one includes it; `CheckAutoRemoval` skips the new head after
  removing index 0; the unread counter lags a full 50–100-record sweep;
  `Dispatch` prints the year as `year - 1860`; id 87 is the only one of the 46
  that assigns no tooltip; and `Load` guards a `Regiment[]` walk with a
  `GameObject[]` null test.
- **A mod seam that is really a trap:** `MessageLog.OpenMessage` writes a
  single space into `manualsender` for any dispatch that has neither a sender
  nor a signature, which defeats `Dispatch.UpdateContent`'s own
  `manualsender.Length > 1` test and sends it into
  `GameVars.commander[sender.commander]` with `sender == null`. No shipped
  dispatch reaches it — every senderless one carries a cabinet title — but a
  mod's will, as a `NullReferenceException` the first time the player opens it
  from the log.
- Also **resolved findings/25 §5.1's unexplained pair of flags**: `flag3` is
  cleared when a naval action had under
  `minshipsfornewspapernavalengagement` = 8 participants **or** when
  `flag2 && flag` — some fleet with `fleetorders == 1` (Blockade) and some with
  `fleetorders == 3` (Raid). So **a commerce raider running into a blockade
  gets no paper at any size**, which is the same pair that suppresses the naval
  national-morale adjustment ([17](code-findings/17-politics-and-support.md)
  §3a). And `flag3` gates more than the newspaper: it also gates the
  `OpenMessageOutright` that pops the battle-result dispatch open, so that
  action does not interrupt the player at all.
- Translated for the player: the newspaper as a **modal interrupt that pauses
  the war** whose options switch **deletes** papers rather than deferring them,
  including the one the end of the campaign waits for; **how much of the
  written history each start date can still reach** — 94 of 105 dated papers
  from Spring 1861 against **30 from Spring 1864**; the fifteen policies that
  are also event unlocks, **Indian Wars alone worth six papers** while handing
  the enemy +5 military experience, and a gate that fires on the enemy's
  research as readily as yours, which makes those papers a free readout of what
  the enemy has adopted; the four **AI crisis papers** as the enemy announcing
  that it has just thrown away its running research; the **late-war papers that
  quietly go missing at speed 50 below 40 fps**, with the workaround of
  dropping to speed 20 for the last year; the battle report's three-step
  captured-weapons scale and its constant morale sentence; and the 40 % morale
  warning landing **above** the 25 % fire-break line but **below** the 50 %
  contact one, so a division already in melee has passed its break threshold
  before the dispatch is written.
- Three new entries in `mechanics-findings/ui-vs-simulation.md` (§1.16–1.18)
  and three new label traps; "fifteen label traps" → "eighteen".
- `code-findings/summary-newspaper.md` retitled and extended with a dispatch
  section; its asset-dump list grew from three questions to five.
- **The planned mechanics index is now empty.** All ten translations are
  written.

**Pass 17 — `code-findings/28-project-effect-reachability.md`** (835 lines, a
new findings file; the first item of the code-side backlog). The reachability
audit [22](code-findings/22-policy-and-project-catalogue.md) §12.3 left
outstanding, run over all **69 real projects** of the 125 ids — 56 are
`PLACEHOLDER`.

- **Four projects cannot be appointed at all** in a base-game install.
  `IsAppointable` and the `datefrom` grant path both test
  `applicablescenarios` and `FulfillsDLC()`, and **`Campaigns/002` is the DLC
  campaign** (`DLC_WL` requires its directory among seventeen conditions
  before setting `dlc_active`; `MainMenu` ~1053 hides any `mapid == "002"`
  campaign without it). So **88** is DLC-gated and **89**, **123**, **124** are
  scenario-`002`-only. Each has a scenario-`001` twin — 90, 111, 112 — so the
  content is a parallel reform set per campaign rather than duplication.
- **Army Groups are a DLC feature, and the base game cannot unlock them.**
  `AllowsArmyGroups()` tests `projects.Contains(89) | projects.Contains(88)` —
  **not 90**, despite both 89 and 90 being named Organization Reform — and
  `GameVars.allowarmygroups`, whose field initialiser is `true` but which
  `DLC_WL` ~3457/~3464 **assigns** `true` with the DLC and **`false`** without,
  alongside `allowgarrisondeployment`. Both gates are shut without the DLC;
  with it, 88 is the base campaign's unlock and 89 the DLC campaign's. Closes
  the `allowarmygroups` open question in
  [19](code-findings/19-commanders-and-perks.md) and corrects
  [22](code-findings/22-policy-and-project-catalogue.md) §12.4, which read the
  flag as a per-alliance pref.
- **The ship-import market inverts itself, and it is the largest exploitable
  thing found so far.** Projects **38–41** are the only callers of
  `ShipType.SetAsImported`, which grants `Random.Range(1, 5)` — **1 to 4** —
  entries in `importonmarketforalliance`, a *stock of purchasable hulls*. Each
  hull spent at `BattleUnits` ~4853 is **paid from the treasury, consumes no
  goods and sets `constructiontimer = 0f`** — an instantly-completed ship.
  Then two defects compound in `ShipType.UpdateMarket`:
  - the restock probability is
    `1f / probabilityofshiponeuropeanmarkets * timer / tonnage * 2000f` with
    **`timer` the absolute campaign date** (`GetCampaignFloatingDate()` ≈
    1862) rather than the elapsed cycle, which inflates it by about 10⁶ — from
    an intended 0.0017–0.0119 per cycle to **1,600–11,000**, so the roll always
    succeeds, every `shiptypeupdatecycle` = 0.002 yr ≈ **0.73 days**;
  - the restock is gated on `HasImport(i)`, which requires
    `importonmarketforalliance.Contains(i)` — **stock must already exist** — so
    the `else` branch fires once the last hull is spent and the restock branch
    is never reachable again.
  Net effect: **keep one hull unbought and you receive a free, instantly-built
  ironclad roughly every three-quarters of a game day forever; buy the last one
  and that ship type is closed permanently.** The slots round-trip through the
  save one line each, so an unspent stock inflates every save. Untested in
  play; the test is stated in the file.
- **The AI's Military Education gate reads the wrong alliance, in the wrong
  direction.** The player's gate (`BuildingList` ~111) is correct — schools
  allowed = `GetProjectLevel(122)`. The AI's (`AICampaign` ~5785) is
  `alliance[num9].militaryschools >= alliance[num9].GetProjectLevel(122)` where
  `num9` is **the opponent**, set deliberately for the military-experience
  comparison two lines above. So the AI is **unconstrained by its own project**
  (0 >= 0 at campaign start), and **a player who buys Military Education and
  builds no school locks the AI out of military schools entirely**, since the
  test becomes "has the player built one".
- **`nation[].occupiedby` is a latch.** `GameVars.UpdateNationsData` ~7947
  assigns it only when **every** town in the state is held by one other
  alliance, and **never clears it** — the `num < 0` path `continue`s and
  `!flag` does nothing. So projects **93** and **94** need a state *totally*
  occupied, keep applying to states their owner has since lost, and never
  apply to states it has liberated. The same latch reaches
  `Regiment.IsOnEnemySoil` ~27160, so a liberated state permanently stops
  counting as enemy soil for whoever once overran it.
- **Project 107's casualties half leaks onto the enemy.** The `num67` reset at
  `ImportExportUnitData` ~3647 happens only in the ternary's *false* branch —
  when side 1 **is** the player. So when the player is the side stored first,
  **the player's own Civil Order level multiplies the enemy's casualty-driven
  support loss as well.** Sharpens
  [20](code-findings/20-ai-campaign.md) §9.2, which had the handicap half of
  the same leak: both factors touching `num67` carry across, and Civil Order
  is the larger at any level above zero.
- Confirmed and bounded, rather than new: **project 104 Subsidize Industry
  omits `id_manufactory`** and so reaches Iron Works and Foundries only, while
  the three other sites that group industrial buildings all include it; and
  **98 Market Reform and 107's raid half are the only two discarded-statement
  failures among all 33 project prefs** — the mechanical sweep is now
  exhaustive, which is what the previous three passes could not say.
- **All 33 `project_*` prefs tabulated** with shipped values and standing:
  **4 dead, 2 impaired** — 12 % and 6 %, the best available prior for the
  `~137` unverified candidates from the findings/07 sweep. Project **97** is
  the only one that is not a multiplier (−1 **rating notch**), and project
  **5** holds the only literal magnitudes in the system
  (`weaponstock[36] += 3000f`, `[86] += 2500f`) — so rebalancing projects is
  config work, not code work, which closes
  [09](code-findings/09-policy-and-project-effects.md) open question 3.
- Smaller findings from the same sweep: **`additionalrecruitssubsidies`**
  (labelled *"additional volunteers at full recruitment subsidies"*, shipped
  **2**) is used only as the numerator of a convergence rate, and nothing but
  project 109 writes `recruitmentprojectfactortarget` — so the doubling the
  label promises is applied nowhere, and 109's +5 %/level **ramps in over about
  73 days** at 1.0/yr; **project 115's gate is in the button, not the
  simulation** (`CBuildingIcon.Upgrade` has no project test, and the AI's
  construction path never reaches a supply depot, so it holds in practice);
  **`Weapon.GetAvailability(int alliance)` ignores its argument** and reads the
  player's availability, with `Weapon.AbleToOrder` gating the order button by
  **string equality** against two of its five return strings; and
  **project 101's `1 + level × (−0.5)` form is safe only because the project is
  non-repeating.**
- Translated for the player in `mechanics-findings/policy-order.md`: a new
  **§8.4** on the four import projects and the hull never to buy, the Army
  Groups DLC note in §8.1, four corrected rows in §8.2, four new rows in §8.3,
  two new short-version bullets and two new entries in §10 ("Eleven things" →
  "Thirteen"). Plus `ui-vs-simulation.md` **§1.19** — the import projects
  promise an open channel and deliver a metered one — and a nineteenth label
  trap.

**Pass 18 — `code-findings/29-dead-prefs-audit.md`** (503 lines, a new findings
file). The `~137 unverified dead prefs` from
[07](code-findings/07-config-data-model.md), finished. Of `GamePrefs`' **2,215**
distinct fields, **144 are parsed from a config file and never read** and **4**
are declared without even a loader line; 90 of the 144 are gameplay-facing.
Method: declarations minus qualified external references, then every candidate's
internal references classified as declaration / loader / read by hand — which
removed two false positives (`debugsloaded`, an internal latch, and
`coverobjects`, which sizes four arrays).

- **Spotted-enemy marks ignore your intelligence entirely, and so do both
  intelligence perks.** `campaignprefs.txt:656` ships
  `timefactorspottings` = 0.1 / 0.003 game years — *"Time factor for enemy
  spottings, based on intelligence"* — and **nothing reads it**. What
  `SpottedEnemy.UpdateMarks` ~245 builds its interval from is
  `spottingfactor[1]` and `[0]`, the per-unit-type **concealment** array from
  `unitprefs.txt`, shipping **−50** and **−60**. The product is always
  negative, `Mathf.Max(0.001f, num3)` clamps the denominator, and the
  mark-creation probability runs about **1,000×** intended — reaching its
  `maximumcreationprobability` ceiling of 0.9 after about **eight game hours**
  instead of 33 days at intelligence 0 or one day at intelligence 1. Field
  Telegraph's `*=` and the Bureau of Military Information's `/=`
  ([19](code-findings/19-commanders-and-perks.md) §1.7) are both swallowed by
  the same ceiling. The live gate on a new mark is distance:
  `minimumdistanceforspotmark` = 40. One-identifier fix.
- **Winter costs the AI ×0.2, not ×0.05.** `winteroffensiveoperationsmodifier`
  has no consumer anywhere; `CheckOffensiveMovements` ~4965 reads
  **`winterdefensiveoperationsmodifier`** into the aggression roll. Corrects
  [20](code-findings/20-ai-campaign.md) and `fighting-the-ai.md`, which have
  been publishing the twenty-fold figure since pass 15.
- **`[deactivated]` does not lie anywhere — the one apparent exception was a
  twin pref.** [19](code-findings/19-commanders-and-perks.md) had concluded
  that `factorcommanderexpattr`'s `[deactivated]` tag was wrong. The tag
  belongs to **`experienceinfluenceoncommanderattributes`**, a separate field
  read from `unitprefs.txt:1583` that also ships **0.5** and has exactly two
  references in the assembly. `factorcommanderexpattr` has its own untagged
  label at `campaignprefs.txt:427`, confirmed by its loader neighbours
  `strengthtriggertodisband` (0.05) and `expgaincommanderservice` (0.2). The
  mechanic — a green commander at half his printed attributes — is unchanged;
  what changes is that **the publisher's markers are now a reliable signal with
  no known exceptions**, which was worth more than the number.
- **Four twin-pref pairs** sit across two config files with one live and one
  dead member, two of them sharing an identical shipped value:
  `experienceinfluenceoncommanderattributes` / `factorcommanderexpattr`,
  `intelligenceinfluenceonreadiness` / `influenceofintelligenceonreadiness`,
  `draftresetcontractlength` / `draftresetcontractlengthcsa`, and
  `timefactorspottings` / `spottingfactor[]`. **Finding a field by its label is
  unsafe**; a tool must walk the loader in order.
- **Twenty-three dead prefs are one cut.** Beside the ten dead policy-ladder
  arrays and two Act add-ons [22](code-findings/22-policy-and-project-catalogue.md)
  §10/§10.1 already had, this pass found the other half: **eleven
  subsidy-category effect prefs** in one contiguous block of
  `campaignprefs.txt` (2654–2681). Their labels name **transportation**,
  **trade war**, **civil order** and a **funding policy** — none of which is
  among the shipped six categories — and `basicsubsidiescost` still carries
  **two trailing slots marked `n/a`**. So the subsidy system was designed with
  roughly ten categories and shipped with six, and the numbers describing what
  the cut four did are still in the file. The largest single trap in
  `campaignprefs.txt`.
- **A fort's level does not make it harder to take.** `forthitpoints` — a
  six-entry per-level table rising 0 → 500 → 1000 → 1300 → 2500 → **3000** — is
  unread. Fort damage goes through `CBuilding.Damage(float)` into `Condition`,
  the generic 0-to-1 building condition every building shares, and neither
  caller carries a level term: `Autocalc` ~3166 (siege assault,
  `fortdamageattackersize` 0.8–1.7 × timediff × attacker strength) and
  `Regiment` ~4636 (raiding, `raiddamage` 0.075). `fortcapacity` **is** live and
  does scale — 5,000 men at level 0 to **25,000** at level 5 — as does
  `setuptimesiege`. So upgrading a fort buys garrison and delay, not durability.
- **Three of the five `powerbalanceinfo*` tiers are unread** (minor victory
  0.7, major victory 0.8, minor defeat 0.3). The two survivors are not a tier
  ladder either: `…majordefeat` (0.2) gates one branch of
  `victorytypeforplayer` and `…criticalloss` (0.4) adds one sentence to the
  Major Defeat panel.
- **A defamed commander's experience cap is never lowered.**
  `increaseexpcapcommanderifveteran` (0.25) is live at `GameVars` ~2042;
  `decreaseexpcapcommanderifdefamed` (0.2), from the adjacent config line, is
  dead.
- **About 38 labels carry the publisher's own marker** — in four spellings
  (`[deactivated]`, `(deactivated)`, `-> deactivated`, `-> inactive`), so a
  sweep for the bracketed form alone misses eleven. That leaves roughly **110
  dead prefs with no marker at all**, which is where the traps are.
- Also newly confirmed dead and individually notable:
  `timetowithdrawgroupsifallunitsrouted` (0.75 game hr — a rally window before
  a wholly routed formation leaves the field, with no substitute mechanism),
  `influencenationalmoraleonoffensives` (1.0),
  `minimumsupportlevelfightingspirit` / `leveragefightingspiritsupportcap`
  (0.20 / 2.0, both tagged `-> inactive`, a richer two-stage morale ceiling
  than the one that ships), `weaponstandardizationexp` / `…lin` (5.0 / 8.0),
  and `biggersizeobjectivesonpapermap` (**2.5**) — the only orphan carrying a
  value.
- Cross-file updates: `summary-data-layer.md` extended;
  [07](code-findings/07-config-data-model.md)'s sweep conclusion replaced;
  [19](code-findings/19-commanders-and-perks.md)'s `[deactivated]` note
  corrected and its §1.7 perk analysis extended;
  [20](code-findings/20-ai-campaign.md)'s winter row corrected.
  Player-facing: `fighting-the-ai.md` **§4.4** (new) plus the winter
  corrections in four places, `commanders-worth-having.md` **§10.3** (new),
  `ui-vs-simulation.md` **§1.20** (new).

**Pass 19 — `code-findings/30-naval-subsystems.md`** (591 lines, a new findings
file). The four subsystems [26](code-findings/26-naval-combat.md) §10 left
open, plus the fifth question about the European-only ship types. Five of its
six open items closed; only the tooltip string table still needs an asset dump.

- **Every damaged ship escapes a fleet action.** `UpdateNavalBattle` ~1765
  gates the escape on `(float)Random.Range(0, 1) <= num14`, and
  `Random.Range(int, int)` is max-exclusive — the left side is **always 0**, so
  the test always passes. Any vessel below `minconditionshipescape` = **0.35**
  sails for harbour on the cycle it crosses the line, and the speed-weighted
  chance that should govern it — `(ownSpeed / enemyAvgSpeed) ^ 0.3 × elapsed ×
  365`, about **1.5 % per 21-minute cycle** at an even match — is computed and
  discarded. **This also explains why ship capture is rare**: capture needs
  `condition < conditionfordisabledships` (0.25) **and** `!IsActive()`, and the
  escape threshold sits above it, so a ship must fall from healthy to disabled
  inside one cycle to be taken. Qualifies
  [26](code-findings/26-naval-combat.md) §7.3. One character fixes it.
- **Shipyard congestion never slows a hull.** `Vessel` ~507 computes
  `b = Mathf.Min(1f, tonnageUnderConstruction / capacity)` and then divides by
  `Mathf.Max(1f, b)` — **two clamps in opposite directions that cancel**, so
  the factor is always 1. Capacity gates whether a ship may be *laid down*
  (`HasCapacity`), never how fast it builds. The third clamp-against-clamp in
  the naval code after the rate-of-fire distance ratio and the armour display
  band.
- **`highestportlevel` is a latch.** `UpdateShipProductionCapacity`'s reset
  loop clears six alliance fields and not that one, and the harbour loop only
  raises it — so **losing your best harbour does not lose you the ship types
  its level unlocked**. The capacity total does fall, so `HasCapacity` still
  bites; only `HasPortLevel` latches. Same shape as `nation[].occupiedby`
  ([28](code-findings/28-project-effect-reachability.md) §6.5).
- **Transport interception, end to end.** A fleet catching an embarked army
  (`currentmodeltype` 1 or 2, embarkation finished, within the fleet's bugle
  range) attaches an `Autocalc` of type 5. Three findings in it:
  - its closing term is `fleetdistancereduction × num` where the sibling
    `UpdateNavalBattle` ~1547 writes `× num × **750000f**`, so at
    `1.5 × 0.00004` per cycle against a start distance of `mapDistance × 65`
    **the distance never closes** and the hit chance stays pinned at
    `generalhitprobabilitybombardment[0]` = **0.2** instead of ramping to 1.0;
  - the `SetRetreatToDepot` loop sits **outside** the hit test, so **one fleet
    anywhere on a sea lane aborts any amphibious move it touches**, hit or not,
    and since that call sets `onretreat` the engine destroys itself — the whole
    interception is **one cycle and at most one casualty roll**. The convoy
    also moves at `fleetmovementfactorwhileunderfire` = 0.1 while attached;
  - `Autocalc.PickUnit` returns `list[Random.Range(0, list.Count - 1)]`, so
    **the last unit in the convoy can never be the casualty** — the same
    off-by-one as the battle AI's wing selection.
  Also: the admiral's **initiative lengthens** the opening range by 30 % and
  his **cunning shortens** it by 30 %, which given the (broken) distance ramp
  would have made a cunning admiral the better interceptor.
- **Blockade, in full.** A fleet's per-node value is
  `sqrt(groupraidingefficiency − mean(enemy guards)) × blockadeefficiencyfactor`
  (0.004), where the raiding term is `lowestshipspeed × combatvalue ×
  fleetblockadefactor (5.0) × (1 + 0.5 × cunning)`. Because the raiding term is
  constant inside the averaging loop it survives intact while **the defender's
  guards are divided by the number of nodes covered** — so **spreading a
  blockading fleet wide is free** and one guard squadron among four covered
  harbours costs the raider a quarter of its strength at each. Closing to
  combat range multiplies the result by **2.5**. Land cavalry on **Guard**
  counts as a blockade guard alongside fleets on **Blockade**, and **Sea
  Raiders** lets a fleet blockade trade nodes of powers it is not at war with.
  `blockadeefficiencyfactors` is a `float[4]`, not the two slots findings/26
  guessed, and all four slots are tooltip-only.
- **The European intervention fleets are real, named, and identical in all six
  campaign starts.** Joining `ships.dat` (23-line records: index, name,
  `attachedtofleetgroupid`, `attachedtopool`, `shiptype`, …) against
  `groups.dat` (32-line records: `groupid`, name, `tempgroupref`, `alliance`,
  …) gives five pre-placed fleets totalling **25 ships**, md5-identical across
  A–F: **French Intervention Fleet** (alliance 3), **Spanish Intervention
  Fleet** (4), **British Intervention Fleet** (2), **North American Squadron**
  (2) and **Western Coast Fleet** (3). Among them **five 92-gun Screw
  Ship-of-the-Line** — the largest hull in `shiptypes.dat` at 9,150 t — four
  80-gun Fast Screw Ship-of-the-Line, the **Warrior Class** that project 41
  otherwise sells for $20 M, and the ***Normandie*** the newspaper catalogue
  names in events 4 and 16. They are scenario data present from the first
  frame; the intervention script only decides whether their alliance goes to
  war.
- **`SetRetreatToDepot` carries 123 unreachable lines.** Every land unit
  returns at ~6689 after enqueuing a `Retreat.RetreatOrder`, so the second
  `if (unittyp != 17)` block at ~6696–6818 — a complete inline depot search
  over the retreat heat map, picking a Town, `CBuilding`, `IIP` or friendly
  `Regiment` — can never be entered. Superseded rather than lost: the live
  path is `Retreat.WorkDownRetreatOrders()`. For a **fleet**, lines 6819+ are
  live and end in **`DisintegrateUnit()`** on three paths — no harbour found,
  a harbour not on water, or a path crossing land — so **a fleet ordered to
  retreat with no reachable friendly harbour is destroyed**.
- Corrections to [26](code-findings/26-naval-combat.md): §7.2's
  `UpdateFortBombardment` does not exist and is **`UpdateFortNavyEngagement`**
  (~1157), whose own `fleetdistance` decrement is dead because it recomputes
  from live map positions a few statements later; the field is spelled
  **`blockadeefficencyvalue`**, one `i` short.
- Player-facing: `the-war-economy.md` **§4a** (new) — spreading a blockading
  fleet wide, the ×2.5 at combat range, and cavalry on Guard as an answer to a
  naval raider. The rest of the pass is naval enough to want its own
  translation, so `the-naval-war.md` is now the one item on the
  `mechanics-findings/` planned index.

**Pass 20 — `mechanics-findings/the-naval-war.md`** (538 lines, a new mechanics
file, and the last subsystem with no player-facing write-up). Translating
[26](code-findings/26-naval-combat.md) and
[30](code-findings/30-naval-subsystems.md), with the import projects from
[28](code-findings/28-project-effect-reachability.md) §4.2a and the blockade
economics from `the-war-economy.md` §4/§4a.

Nothing new was decompiled; the value is in connecting findings that were
spread across two code files into decisions a player can act on.

- The organising fact is the **condition ladder, and it is in the wrong
  order**: a ship is fully effective to 60 %, stops being able to ram below
  60 %, **leaves the action with certainty below 35 %**, and would only become
  capturable below 25 % — under the line at which it has already sailed away.
  So **fleet actions are damage-trading exercises that end with both squadrons
  intact**, the 2.5 % magazine explosion is the player's main source of
  outright kills, and prizes come from the margins rather than from battle.
- **Repairs are charged the moment a ship turns for port** — a fraction of its
  full build cost equal to the damage taken, plus a full resupply — and since
  the escape is automatic, that bill is the real price of a fleet action and
  cannot be declined.
- Translated for the first time as advice: **spread a blockading fleet as wide
  as its range allows** (the defender's guards are averaged across every node
  it covers, the fleet's own strength is not), **push blockaders inside bugle
  range for ×2.5**, **guard coastal trade nodes with cavalry** — which counts
  exactly as a fleet on Blockade would — **raid big blockades with small
  fleets**, since a raider forces the blockading squadron down to its own
  numbers at no political cost, and **park one gunboat on an enemy sea lane**,
  which denies amphibious movement through it entirely.
- The three gates on building a hull, with the two quirks that matter: **a
  blockaded or damaged harbour builds less** (condition × (1 − blockade) × the
  level's rated tonnage), and **losing your best harbour keeps the ship types
  its level unlocked** because the port-level test latches.
- **Still open and flagged as such**: what a hit actually takes off a ship, and
  therefore how long a fleet action lasts. The damage formula is known; its
  scale against a real squadron is not, and that is the one number a player
  would most want.
- `mechanics-findings/README.md`'s planned index is empty again — every
  `code-findings` subsystem now has a translation.

**Pass 21 — `code-findings/31-retreat.md`** (507 lines, a new findings file).
`Retreat`, 624 lines, the one campaign subsystem no earlier file had opened and
the item pass 19 promoted. It turns out to be two systems: a **queued
destination-finder** and the game's **only** mechanism that makes units
physically block movement, which `AICampaign` borrows for its own campaign
moves through a single global slot.

- **A cut-off unit ordered to withdraw marches toward the nearest enemy.**
  `GetRetreatDestinationLand` overwrites its `fromposition` parameter with the
  nearest enemy's position in step 1 (to compute the retreat direction) and
  then, if the candidate search finds nothing, **returns that parameter**. The
  caller's disintegrate check is
  `CompareVector3(retreatdestination, fromposition, 5f)`, and because the
  method takes the parameter by value the comparison is against whatever the
  **call site** passed. Four of the six land call sites pass the unit's own
  position (`Regiment` ~6533, `BattleUnits` ~2790/~2937, `Autocalc` ~1078), so
  the check misses and `WorkDownRetreatOrders` waypoints the unit at the enemy
  with `doublequick: true`. The two that pass the enemy's position
  (`Regiment` ~6559, `BattleUnits` ~14270) get the intended disintegration.
  The search ring is bounded — 35 to 300 map units from a probe 70 to the rear
  — so the no-candidate case is reachable, not theoretical.
- **The other branch of the same slip**: a unit with **no known enemy** never
  has `fromposition` overwritten, so the comparison matches and it is marked to
  disintegrate — a unit with nothing near it and nowhere to go dissolves rather
  than standing still.
- **Holding the right mouse button suspends every pending retreat in the
  game.** `if (retreatorder == null || Input.GetMouseButton(1) ||
  GameVars.frame < 50) return;` heads both `WorkDownRetreatOrders` and
  `PrepareAllianceObstacles`, so a player panning the camera freezes both
  sides' retreats for the duration of the drag. The only place found so far
  where an input state gates a simulation process.
- **`Retreat` owns the only mechanism that makes units physically block
  movement.** `PrepareAllianceObstacles(alliance)` enables a `navmeshobstacle`
  on every unit of every *other* alliance, sized at `blockrangecampaign` — a
  **hardcoded `30f` static with no loader**, so the width of an enemy army's
  shadow on the campaign map is not a pref — capped by that unit's own distance
  to its nearest enemy, so **two forces in contact barely block each other**,
  which is what lets a retreat squeeze out of a line. In battle the base is 75
  and formations (`unittyp > 13`) drop to 1, so HQ markers do not obstruct.
  Three filters decide whether a unit blocks at all: a strength comparison that
  lets weak enemies be walked past, a flag that forces garrisons to block, and
  a flag that excludes fleets.
- **The queue is coupled to AI movement traffic.** Only one alliance's retreats
  are processed per frame, `currentlyactivated` is a single global int that
  `AICampaign` ~1128 also claims for its movement orders, and **both the
  activate and the deactivate path call `IncreaseAllOrdersFrames`**, which
  pushes **every** pending order's ready-frame to `frame + 2`. So the nominal
  two-frame delay is a floor and a long AI movement queue postpones retreats
  two frames per order.
- **What breaking actually costs, quantified.** `Surrender()` takes
  `powratiosurrender` **0.6 ± 0.2** and then halves it twice —
  `powmultiplierexp` **0.5** on experience and `powmultiplierfightingspirit`
  **0.5** on fighting spirit — so prisoners run **40–80 %** for a green unit
  and **10–20 %** for a veteran at full fighting spirit, a **fourfold** spread
  that nothing in the interface mentions. `DisintegrateUnit` zeroes readiness
  for the formation and everything attached, then either relocates the
  survivors to a friendly town after **50–80 %** desertion at national morale 0
  falling to **30–50 %** at 100 (`desertionsdisintegrationnatmorale0/1`), or —
  with no town in reach — **deletes the formation outright**, with no casualty
  accounting at all.
- The **battle destination ladder**: retrace your own advance (only when
  routing out of melee, using the unit's own position history), else rally on
  the nearest parent HQ with **no enemy within `mindistancetoofferrallypoints`
  = 70**, else just move along the retreat angle. And a unit with no path
  surrenders **only if an enemy is within its own frontage**, which is why
  pocketed units often neither escape nor surrender.
- Campaign retreats are waypointed at **double-quick with `useorderdelay:
  false`**, so a retreat is the one order that bypasses commander order
  transmission; `onretreat` is toggled off and on around the call so the
  waypoint logic will accept it.
- Smaller: **`debug_navmeshoperationoff` guards only the deactivate path**, so
  setting it leaves every obstacle permanently enabled; the
  surviving-disintegration dispatch uses the **fleet-named** id 75 for land
  formations; the disintegration town search is unbounded (`99999f`) while the
  destination search is capped at 300; and `Retreat`'s own `Start()` and
  `Update()` are **both empty** — it is driven entirely from
  `BattleUnits.Update`.
- Player-facing: `why-units-break.md` **§7a** (new, with §7a.1–7a.3) and three
  new bullets in its §8. `summary-simulation.md` extended; findings/10, /30 and
  `summary-naval.md` cross-referenced.

**Next:** the code-side backlog is down to two items and one tool, and both
items are larger than a single pass.

- **The `DLC_WL` layer**, 7,932 lines — now clearly the biggest remaining
  unknown. Five passes have shown it owns more than its line count suggests:
  Army Groups and garrison deployment
  ([28](code-findings/28-project-effect-reachability.md) §3.1), the whole
  scenario-`002` project set, the `Diary` channel and
  `messageidstoignore` ([27](code-findings/27-dispatches-and-the-message-log.md)
  §13), the prestige economy, incidents, staff, companions, the career-panel
  battle outcome ([25](code-findings/25-newspaper-and-events.md) §4.1) and the
  defensive-facing battle subsystem that [21](code-findings/21-ai-battle.md)
  found gated off in the base game. It wants a scoping pass first — a type and
  entry-point inventory, like Phase 1 did for the assembly — before any
  subsystem write-up.
- **One asset dump pass**, the binding constraint on eight questions: the perk
  availability lists ([19](code-findings/19-commanders-and-perks.md)), the
  `Frontline2` state-set cut-over dates
  ([24](code-findings/24-campaign-objectives.md) §12), the `levelgraphics`
  project-level cap ([28](code-findings/28-project-effect-reachability.md) §10),
  the campaign map's unit-to-mile scale, the `UI/Newspaper` sprite set
  ([25](code-findings/25-newspaper-and-events.md) §9), the message-symbol atlas
  and whether `MessageLog` sits on the panel object its retention depends on
  ([27](code-findings/27-dispatches-and-the-message-log.md) §13), and the
  `Tooltip` string table ([26](code-findings/26-naval-combat.md) §10).

The tool worth building before the next pref question is **a label-to-field map
for all 2,215 prefs**, by simulating each loader in order
([29](code-findings/29-dead-prefs-audit.md) §10) — four passes have now
reconstructed parts of it by hand.

Phase 0 leftovers remain (dnSpy; `xl/vbaProject.bin` from the ModdingTool
workbook), and the release-packaging check in the Licensing section below is
still unwritten.

**Every subsystem of the base game now has a `code-findings` file and a
`mechanics-findings` translation.** What is left is the DLC, the assets, and
tooling.

**Exit criteria per file:** a reader who has never opened `code-findings/` can
act on it, and every claim in it can be traced back to a section that supports
it.

---

## Phase 3 — Hook library foundation (Goal 2)

**Goal:** a loadable DLL that runs code inside the game, survives a game
update without bricking, and exposes a first stable event.

### Technical decisions

| Decision | Choice | Why |
| --- | --- | --- |
| Mod loader | **BepInEx 5.x** (Mono branch) | The mature standard for Unity Mono. Bundles HarmonyX, uses UnityDoorstop injection, no game files modified. MelonLoader is the alternative but is weaker for Mono-era Unity. |
| Patching | **HarmonyX** (ships with BepInEx) | Runtime IL patching — prefix/postfix/transpiler. No patched game DLLs to redistribute, which keeps us legally clean. |
| Private member access | **`Krafs.Publicizer`** (MSBuild) | Compile against a publicized `Assembly-CSharp` reference so we can reach private members with normal typed code instead of reflection strings. Publicizes at build time only — **never redistributed**. |
| Game references | **Resolved from each developer's own install**, never vendored | See [Distribution boundary](#distribution-boundary). No game DLL enters the repo or the release. |
| Target framework | `netstandard2.1` | **Confirmed**, not assumed: `Managed/` ships `netstandard.dll`, so the project's Api Compatibility Level is .NET Standard 2.1. |
| Language | C# | Required by the goal, and native to the ecosystem. |
| Version strategy | **Capability probing**, hash as metadata | A hash gate alone bricks every mod on every Steam patch. See [Version resilience](#version-resilience-a-capability-registry-not-a-hash-gate). |
| Write model | **Contributions, not setters** | Forced by the three state idioms in [summary-game-state](code-findings/summary-game-state.md). See [the write path](#the-write-path-contributions-not-setters). |

### Referencing the game without shipping it

The library must compile against `Assembly-CSharp.dll` but must never carry a
copy of it. `Directory.Build.props` (tracked) holds the common settings and
imports a gitignored `game.props` for the local install path; the
`GTCW_GAME_DIR` environment variable works as a fallback. **Scaffolded and
building** — see [`src/README.md`](src/README.md).

Three **reference tiers**, so the layer boundary is enforced by the build
rather than by code review:

| Tier | Projects | Can see |
| --- | --- | --- |
| *(none)* | `GTCW.ModApi`, `GTCW.ModApi.Schemas` | nothing — they build with no game installed |
| `UsesUnityAssemblies` | `samples/*` | Unity only. A BepInEx plugin derives from `MonoBehaviour`, but a mod has no business touching `Assembly-CSharp` |
| `UsesGameAssemblies` | `GTCW.ModApi.Runtime`, `GTCW.ModApi.Plugin` | Unity + `Assembly-CSharp` |

A `ForbidGameReferences` target errors if `Assembly-CSharp` ever appears in a
project that did not ask for it, so the inviolable boundary is a build failure
rather than a review miss. Verified by deliberately violating it.

> **The tier conditions must live in `Directory.Build.targets`, not
> `.props`.** MSBuild imports `.props` *before* the project body and
> `.targets` *after* it, and the tier is chosen by a property the project
> itself sets. In `.props` the conditions evaluate false, the references are
> never added, and — because the first scaffolded code reached game types only
> through `AccessTools.TypeByName` — **the build still succeeds.** It looks
> like a working setup right up until the first line that names a game type.
> This cost a debugging cycle; it is written down so it costs nobody another.

`Private="false"` on each reference is the other load-bearing detail — it stops
MSBuild copying those DLLs into `bin/`, which is exactly how game assemblies
accidentally end up inside a release zip. `Krafs.Publicizer` then publicizes
into `obj/PublicizedAssemblies/` at build time, so the publicized reference is
a local build artifact that is never committed and never shipped. Both
confirmed on the scaffold: no game or Unity DLL appears in any `bin/`.

One more packaging detail worth recording: **BepInEx does not publish to
nuget.org.** `BepInEx.Core` (which brings HarmonyX) comes from
`https://nuget.bepinex.dev/v3/index.json`, added in the tracked
`NuGet.config`. `Krafs.Publicizer` is on nuget.org. Both are referenced
`PrivateAssets="all" ExcludeAssets="runtime"` so neither ships in the release.

### Architecture: isolate volatility

The whole design rests on one principle — **game updates must break only one
layer.** Four projects, strictly separated:

```
┌─────────────────────────────────────────────────┐
│  GTCW.ModApi            (public, STABLE)        │  ← what modders reference
│  events, interfaces, DTOs, capability enum.     │     NO game reference at all
│  No game types leak. No Harmony. No Unity sim.  │
├─────────────────────────────────────────────────┤
│  GTCW.ModApi.Runtime    (internal, VOLATILE)    │  ← the only layer that
│  Harmony patches, publicized game-type access,  │     knows real game types
│  capability probes, contribution folding,       │
│  id broker, config pipeline, DTO translation    │
├─────────────────────────────────────────────────┤
│  GTCW.ModApi.Schemas    (generated)             │
│  typed config models generated from the         │
│  Phase 2 schemas — schemas, never game data     │
├─────────────────────────────────────────────────┤
│  GTCW.ModApi.Plugin     (BepInEx entry point)   │
│  bootstrap, build identification, capability    │
│  report, logging, BepInEx config                │
└─────────────────────────────────────────────────┘
```

A modder referencing `GTCW.ModApi` never touches a game type. When the game
updates, `Runtime` is repaired and dependent mods keep working unrecompiled.
If game types leak into the public API, every mod breaks on every patch — so
treat that boundary as inviolable. The enforcement is mechanical rather than
cultural: **`GTCW.ModApi` has no reference to `Assembly-CSharp` at all**, so a
leak is a compile error rather than a code-review miss.

`Schemas` is split out from `Runtime` because the typed config models are
generated and large, and because they are the one artifact a modder may
legitimately want to reference directly.

### Version resilience: a capability registry, not a hash gate

Hashing `Assembly-CSharp.dll` is the only way to *identify* a build —
[01-assembly-partition](code-findings/01-assembly-partition.md) found no
version constant, and the game parses its own version out of a
`TextMeshProUGUI` label far too late to gate on. But identification is not the
same as gating, and a pure hash gate means **every Steam patch disables every
mod in the ecosystem until we ship**. Steam already moved this build on
2026-10-13, the day analysis started.

So invert it. A **feature** is the unit of enable/disable:

```csharp
sealed class Feature {
    public Capability Id;             // public enum, part of the stable API
    public Func<bool> Probe;          // does this build still support me?
    public Action Apply;              // install the Harmony patches
    public string DegradedMessage;    // what a modder is told if I am off
}
```

- Hash matches a known build → trust the recorded capability table, skip probes.
- Hash is unknown → **run every probe**, enable feature by feature, log a
  one-line capability report naming what is on and what is off.
- A probe failure or a throw during `Apply` disables **that feature only**.

Probes check for the members a feature actually patches — method exists, right
signature, field present — by reflection over the real assembly. That is the
same information the **signature manifest** in
[Surviving game updates](#surviving-game-updates) already tracks, so the
manifest becomes the probe table's source rather than a separate document.

Then `ModApi.Capabilities.Has(Capability.PolicyRegistration)` is a public
query, a mod can branch on it, and a patch-day breakage degrades to "config
overrides still work, policy registration is off" instead of a dead install.

- [ ] Public `Capability` enum, one value per feature.
- [ ] Probe per feature, generated from or checked against the signature manifest.
- [ ] Capability report in the log, and in `ModApi.Capabilities` for mods.
- [ ] Build identification by hash, against
      [00-build-provenance](code-findings/00-build-provenance.md). Mismatch →
      loud warning and probe-driven degraded mode. **Never hard-crash someone's
      game over a version bump.**

### Fail-soft as a rule

Not an aspiration — a mechanism. Every patch install is wrapped, every
subscriber invocation is wrapped, and the failure unit is the feature or the
mod, never the process.

- [ ] Wrap `Apply` per feature; a throw disables that feature and logs once.
- [ ] Wrap subscriber dispatch per mod; a throwing mod is reported by name and,
      after N throws on one event, unsubscribed rather than left to spam.
- [ ] Never let an exception cross back into game code from a postfix.

### Sessions, not frames

`GameController` is scene-scoped and every scenario transition is a
non-additive `LoadScene("Main")`, so a binding held across a transition points
at a **destroyed** object. And `GameVars.frame` **rewinds** during load
(`if (BFS.initialized < 3 && GameVars.frame > 3) GameVars.frame = 3;`), so it
is a program counter for the boot sequence, not a monotonic clock.

Both facts point the same way:

- A **`GameSession`** object is created per scenario load and invalidated on
  `bo_rebuildscene`. Every binding hangs off the session; nothing is cached
  statically. `GameVars` itself must be re-read through the static each time —
  scene changes do not clear it, but the arrays are rebuilt by
  `import_nations`, and the state count is **52, 53 or 54 depending on the
  scenario**. Never hardcode 53.
- Expose **named milestones**, never frame numbers. Modders ask for
  `OnUnitsReady`, not `frame >= 12`.

| Milestone | Boot gate | Why it matters |
| --- | --- | --- |
| `MapReady` | 3 | the sequence stalls here until the map is built |
| `PoliciesReady` | 8 | `UpdatePolicyEffects(startsetup: true)` — **effect registration must already be done** |
| `UnitsReady` | 11–12 | nothing may read units before this |
| `SubsystemsLoaded` | 25 | all saved subsystems in |
| `CampaignLive` | 30 | `currentoperation = 2` |
| `ProjectsReady` | 37 | `UpdateProjectEffects()` |
| `CampaignIdle` | 50 | chapter update, speed 0, ready for input |

Because `PoliciesReady` is frame 8, **registration happens at plugin load, not
on a game event.** That is an API contract, not an implementation detail:
`Register` calls after the first `PoliciesReady` must throw rather than
silently no-op.

- [ ] `GameSession` with per-scene binding resolution, invalidated on `bo_rebuildscene`.
- [ ] Milestone events derived from the boot gates; frame numbers stay internal.
- [ ] Registration deadline enforced with a clear exception message.

### A deterministic test harness — half of it is real

This section previously claimed `debug.txt` "can boot straight into a specific
battle or campaign scenario at startup", on the strength of the file's own
labels. **That is wrong for this build**, and the full reading is now in
[32-debug-army-setup](code-findings/32-debug-army-setup.md).

> **`loaddebugs()` reads the first seven lines of `debug.txt` into a throwaway
> variable** and parses the eighth. So "Scene Management Operation at start",
> the scenario id and the sub-level are consumed only to keep the line offsets
> right — exactly what their `[deactivated]` tags say. The army-setup flag on
> line 8 is live; **the scenario-at-start prefs are dead.**

So the flag replaces the *army data* of a scenario you load through the menus;
it does not load the scenario. Which splits the harness in two:

| Want | Status |
| --- | --- |
| Deterministic armies in a standalone battle | **available** via `debug_loaddebugarmysetup`, once its data files are current |
| Booting without UI steps | **not available from `debug.txt`** — needs a patch on `SceneManagement.LoadBattleScenario`, which the library can do itself |

The second one is no loss, as it turns out: we already patch that method for
the lifecycle, so driving it is a few lines in a dev-only feature rather than a
dependency on a dead pref.

The debug folder's data files also ship stale, in three different ways:

- [x] **`nations.dat` — fixed.** Was `8x40` alliance blocks against a current
      format of `8x42`; the two missing fields are the last of each block,
      `volunteersadj` and `contractlength`. `import_nations` reads a version but
      gates only on `!foldername.Contains("Save")`, so a `Battlefields` path
      always takes the branch and **no `version.dat` can rescue the file**.
      Fixed by [`scripts/fix-debug-nations.ps1`](scripts/fix-debug-nations.ps1),
      which merges the values out of a shipped reference, checks the alliance
      names align first, and backs up the original. The repo tracks the
      transformation, never the data.
- [x] **`regiments.dat` — fixed.** One field short per regiment, 38 against 39.
      Here a version gate *does* exist (`num >= 0.7802f`), but `num` falls back
      to the **current game version** when `version.dat` is absent, which it is,
      so the gate never fires. The field is **morale**, passed to
      `create_regiment` as its trailing `float morale = 1f`, so appending `1`
      per record reproduces what the file was written to mean rather than
      inventing data — and needs no reference file. Fixed by
      [`scripts/fix-debug-regiments.ps1`](scripts/fix-debug-regiments.ps1);
      799 to 820 lines.
- [x] **`ships.dat` — not needed.** It is absent and `ImportShips` opens it with
      no `File.Exists` guard, which looked like a third blocker. But
      `ImportShips` has one caller, `InitCampaignBattleUnits`, which belongs to
      the **campaign** scene; `InitBattleUnits` never calls it. A standalone
      battle — the only thing the flag applies to — never reads the file. No
      file added.
- [x] `groups.dat` was already current: 32 lines per group, same as `000/A`.
- [x] **`battledata.dat` needed nothing.** It was the last suspect on the
      strength of a line count differing from `000/A`, which was never
      evidence: `import_battledata` has no version gates and four count-driven
      variable-length loops, so two battles legitimately differ in length.
- [x] **The flag loads.** Verified live: 1st Manassas selected from the menus
      came up with the debug folder's own order of battle — "1st Brigade",
      "U.S. Cavalry", "Patterson's Artillery" — and a clean log. **So the
      fixture is usable**, and the stale-file count was two, not four.
- [ ] Optional refinement: author a *minimal* two-unit battle instead of the
      shipped 21-regiment Bull Run setup. Faster to load and simpler to reason
      about, but no longer blocking anything.

Method note worth keeping: on this format **a line count that divides evenly by
a record count is evidence, and a line count that merely differs is not.** The
two files that were broken were both caught by exact division; the two that
looked suspicious and were fine were the two with no such invariant.

A side note that narrows the flag's usefulness: every `IsCampaign` branch
redirects to `Campaigns/DebugArmySetup`, and **no such folder ships**. So the
flag is battle-only in practice, whatever its label implies.

**And one open question closed on the way.** `float.Parse` with no
`CultureInfo` was flagged as a possible correctness blocker for the whole
override layer. It is not one: `GameVars.SetCultureInfo()` forces
`InvariantCulture` from `MainMenuControls.Awake()`, the first thing the game
runs. The one thing the library must still do is pass `InvariantCulture`
explicitly in its own parsing, because a BepInEx plugin's `Awake` can run
before the game's.

### Build out, in shippable order

Each milestone is independently useful and independently verifiable.

1. [x] **Scaffold the four projects** under `src/`, plus
       `samples/HelloWorldMod`. All five build clean, the three reference tiers
       work, the boundary guard fires when violated, and no game DLL reaches
       `bin/`. See [`src/README.md`](src/README.md).
2. [x] **BepInEx loads; the build is identified; the anchor check passes.**
       Verified on a live run against BepInEx 5.4.23.6, Unity 2021.3.16.4200023,
       CLR 4.0.30319, Bits64/Windows, `Supports SRE: True` — so runtime IL
       generation works and Harmony patches rather than degrading. The build
       hash matched buildid 14703111, and **all ten anchor types resolved**,
       which confirms the global-namespace rule and the publicized reference
       against a running game rather than against the decompile.
3. [x] **Capability registry and the fail-soft wrapper** — built first, before
       anything depends on them: `CapabilityRegistry` (probe → apply → report,
       isolating each feature), `Feature.Planned` so unbuilt features report an
       honest reason instead of a bare false, `FailSoft` with a per-subscriber
       throw budget, and `AnchorCheck`, which verifies the ten types the library
       intends to patch are present under the names the research recorded.
4. [x] **`GameSession` plus milestones, and `Battle.Started` reaching a sample
       mod.** Verified live: a standalone battle, a campaign, and a battle
       launched from that campaign. All twelve milestones fired **in order and
       exactly once each**, both session kinds raised only their own, the
       campaign session ended before the battle session began with no overlap,
       and `Battle.Started` reached `HelloWorldMod` through the public API.
       Four patch points cover every transition — postfixes on the three `Load*Scenario`
       methods and a prefix on `RebuildScenes` — for the reasons now recorded
       in [03-scene-wiring](code-findings/03-scene-wiring.md), "Where to patch
       for session lifecycle". Milestones come from postfixes on
       `CampaignController.Update` and `BattleController.Update`, mapped through
       `BootMilestones`; `Battle.Started` is derived from the battle session's
       last boot gate rather than patched separately, because the game has no
       "battle started" method to patch.

       Two design points worth keeping:
       - **The frame rewind is handled by construction**, not by a special case.
         A postfix observes the counter *after* the stall line has run, so a
         value above 3 proves the map is ready; and each milestone fires at most
         once per session, so a rewind cannot duplicate one.
       - **Milestones are per session kind.** A battle has no `PoliciesReady`
         and a campaign no `BattleDeployed`, so the enum documents which kinds
         raise each value and a session raises only its own.

       And two defects the live run found, both fixed:
       - **A derived event outran the event it derives from.** `Battle.Started`
         reached the mod *before* `MilestoneReached(BattleIdle)`, because the
         runtime subscribes during bootstrap — before any mod exists — so its
         handler ran first and raised the derived event from inside it. Order
         was therefore a function of subscription order. The runtime's own
         services now subscribe to internal `After*` hooks raised once every
         public subscriber has run, and the ordering is a documented guarantee
         rather than an accident.
       - **Quitting the game ended no session.** The game's only
         `Application.Quit()` is a bare call and nothing implements
         `OnApplicationQuit` ([03-scene-wiring](code-findings/03-scene-wiring.md)),
         so the last session of every play session never ended. Now hooked on
         Unity's `Application.quitting`. **This is a constraint on Phase 4
         persistence**: side-car saves must hook `SaveAll`, not session
         teardown.

**Exit criteria:** sample mod prints on battle start, from a clean install,
booting into a fixed scenario without manual UI steps — and prints a correct
capability report when run against a deliberately mismatched assembly.

**Status: met, with one item deliberately left and one impossible as written.**

- The sample prints on battle start, through the public API, verified live.
- The capability report is correct against the known build.
- **Reproducible armies: done.** `debug_loaddebugarmysetup` loads after the two
  data-file repairs above, so every hook test can run against the same order of
  battle instead of whatever the tester happened to click.
- **"Without manual UI steps" is not achievable from `debug.txt`** — the
  scenario-at-start prefs are dead (above). Reaching it means driving
  `SceneManagement.LoadBattleScenario` from a dev-only feature of our own,
  which is cheap because the lifecycle already patches that method. Not done,
  and not blocking Phase 4.
- **The mismatched-assembly check is not done.** It needs a deliberately
  modified copy of `Assembly-CSharp.dll` to probe against, which is a
  half-hour of work whose value is proving the degraded path behaves. Worth
  doing before any public release, since that path is what every future game
  patch will exercise.

---

## Phase 4 — API surface (Goal 2, the substance)

Driven by Phase 2's findings — you can only expose what you understand.

### The write path: contributions, not setters

**The most important design decision in the library, and the least obvious.**
[summary-game-state](code-findings/summary-game-state.md) found three state
idioms, two of which make a direct write futile:

| Idiom | What happens to a mod's write | Fields |
| --- | --- | --- |
| **Double-buffered sweep** | a cursor walks one entity per frame into `<field>temp`; on wrap the published field is overwritten and `temp` zeroed. **The write is undone.** | `averagesupport`, `totalpopulation`, `privateincome`, `corporateprofits`, `volumeimports`, `tradevolume`, a dozen more |
| **Reset-then-accumulate** | `UpdateAllianceData` zeroes ~18 fields and refills them from `unitlist`; `UpdatePolicyEffects` zeroes thirteen. **The write survives ≤12 game hours.** | alliance aggregates, policy effect accumulators |
| **Trailing average** | `Tools.AddToAverage(…, 0.25, dt)` accepts the write; it simply decays | the rest |

And `UpdatePolicyEffects` **replays every active policy's effect on every
pass** — every 12 game hours per alliance, with `dt ≈ 0.5/365` campaign years
handed to each rate integration — so "apply once on activation" is the wrong
model even for effects.

If the public API exposes setters, every mod in the ecosystem is subtly broken
and nobody can tell why. So the API is **declarative**: a mod registers a
contribution, and the runtime folds it in at the right point in the game's own
pass.

```csharp
// not: nation.Support += 0.1f;
Modifiers.Support.Add("mymod.railbonus",
    scope:   Scope.Nation("Virginia"),
    perYear: ctx => 0.25f * ctx.Alliance.RailCapacity);
```

Properties this buys, none of which a setter API can offer:

- **Replay-safe.** Re-applied by construction, so the 12-hour reset is a
  feature instead of a bug.
- **Rate-correct.** Contributions are expressed per game year and scaled by the
  `dt` the pass already computed — copying
  `Economy.UpdateEconomyEstimates(num2)`, not the frame-modulo pattern. Cadence
  is frame-rate dependent and must never leak into a mod's arithmetic.
- **Composable.** Two mods contributing to one field sum instead of racing, and
  the pattern matches the game's own commutative idiom (Tariff Act and Free
  Trade each check the other and multiply).
- **Attributable.** Every contribution carries a mod id, so the log can explain
  a number and conflicts can name both culprits.

Fold at the chokepoints [23-game-state](code-findings/23-game-state.md) already
ranks: `Nation.AdjustSupport`, `Alliance.AdjustNationalMorale`,
`AddTradeWarfare`, `Alliance.AdjustTreasury`, the `GetSubsidyFactor` /
`GetSubsidyCost` / `GetCurrentTaxValuePa` / `GetAdminCost` / `GetBalance`
pure-function set, and `BattleUnits.UpdateAllianceData` for anything the pass
itself resets.

- [ ] Contribution registry with mod attribution and a documented fold order.
- [ ] One chokepoint end to end — **`AdjustSupport` is the cleanest** (single
      writer, already clamp-then-recompute).
- [ ] `ModApi.Unsafe.*` for direct mutation, documented as unsupported, with
      the double-buffered and reset-then-accumulate fields named in the XML
      docs so the warning reaches IntelliSense rather than only a guide.
- [ ] Never expose writes to `nation[].name` — it is mutated at runtime by
      `statesetname`, and everything that matches a state by string then
      silently stops matching.

### Event surface

| Area | Events |
| --- | --- |
| Lifecycle | `Game.Initialized`, `Session.Started/Ending`, the boot milestones, `Campaign.Loaded`, `Campaign.Saving` |
| Battle | `Battle.Started/Ended`, `Battle.Tick` |
| Units | `Unit.Damaged`, `Unit.MoraleChanged`, `Unit.Routed`, `Unit.Destroyed` |
| Economy | `Production.Calculating`, `Trade.Resolved` |
| Commanders | `Commander.Promoted`, `Commander.TraitApplied` |

**Three event tiers, and the distinction is load-bearing:**

- **Notification** (`…ed`, past tense) — read-only, cannot be cancelled. Safe.
- **Mutation** (`…ing`, present participle) — carries a mutable args object so
  a mod can adjust a value or cancel. Every one needs an explicit contract for
  what happens when two mods both write.
- **Sampled** — coalesced per tick or per N entities, **documented as lossy**.

The third tier exists because of a performance trap, not a design preference.
`Regiment.Update` is 458 lines running per unit per frame, there is no fixed
timestep, and modulo staggering keys off the *display* frame rate — so a 144 Hz
monitor runs every frame-paced subsystem 2.4× faster than a 60 Hz one. A naive
per-unit event dispatch costs units × frame rate delegate invocations and will
be blamed on the library.

- [ ] Per-unit-per-frame subscription only behind an opt-in that logs a
      performance warning naming the subscribing mod.
- [ ] Document main-thread-only, and **no determinism guarantee across
      machines** — that is a property of the game, not a limitation we can fix.

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

**The game has no field-level merge** — a scenario copy of a prefs file
*replaces* the global one wholesale. **Verified in code**: the idiom throughout
`GamePrefs` is to build the scenario-local path and, if the file is absent,
reassign the path to `Config/` — one path wins entirely
(`loadcampaigncontrols`, `loadunitprefs`, `loadcontrols`). `loadcontrols` also
forces the `Config/` fallback when `SceneManagement.currentoperation` is 3 or
8, which is the mechanism behind the scale-dependent prefs finding in
[07-config-data-model](code-findings/07-config-data-model.md). So mod overrides
add a layer the game lacks entirely, rather than extending an existing merge:

```
Config/ (global)  ─replaced by─►  scenario file  ─merged by us─►  mod overrides
     └──────── the game's two-entry chain ───────┘   └─ new capability ─┘
```

The pipeline is four stages, and **attribution is what makes it better than a
pile of Harmony patches**: `parse → collect (each patch tagged with its mod id)
→ validate → commit`.

- [x] Reach the config parsers (Phase 1) — done; they are plain `StreamReader`
      sequences against relative string literals, so post-parse interception is
      straightforward Harmony work with no Unity-specific machinery.
- [ ] Intercept **post-parse**, so a patch applies to whichever file actually
      won the game's own resolution. Modders then reason about one rule: *the
      effective value, whatever its source*.
- [ ] Typed models per data file, generated from the Phase 2 schemas, into
      `GTCW.ModApi.Schemas`.
- [ ] Conflict detection that names **both mods and the field**, with a
      documented resolution policy.
- [ ] Validate before injecting, rejecting bad overrides **individually**
      rather than inheriting the game's all-or-nothing failure.
      [06-config-file-format](code-findings/06-config-file-format.md) explains
      why it fails hard: unguarded `int.Parse` / `float.Parse` / `bool.Parse`,
      and field offsets that shift silently when a line count is wrong. Reuse
      `ImportExportUnitData.CheckFileIntegrity` as a reference for what the game
      considers valid. **Two exceptions to copy carefully:**
      `Tools.GetIntPartitions` uses `int.TryParse` and silently *drops*
      unparseable entries — so a typo'd id in a list field is invisible today,
      and our validator should **report** it instead; and entries that do not
      fill `partlen` exactly are dropped the same way.
- [x] **The culture question is settled, and favourably.** It was carried as a
      correctness blocker: `float.Parse` throughout the data layer is called
      with no `CultureInfo`, so a comma-decimal locale would read `1.5` as `15`.
      But `GameVars.SetCultureInfo()` forces `InvariantCulture` from
      `MainMenuControls.Awake()`, the first thing the game runs
      ([32-debug-army-setup](code-findings/32-debug-army-setup.md) §6).
- [ ] **Pass `InvariantCulture` explicitly in our own parsing anyway.** A
      BepInEx plugin's `Awake` can run *before* `MainMenuControls.Awake`, so the
      library cannot assume the game has set the culture by the time it parses
      anything. Cheap to do, and the failure it prevents is locale-dependent and
      therefore nearly unreportable.

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

**Ship projects first, policies second.** They are not the same difficulty:

| | Projects | Policies |
| --- | --- | --- |
| New id from config? | **yes** — `Projects.LoadedProjects.Import` is the whole definition | **no** — `policies.dat` only overrides ids the scene already carries |
| Free ids | **56** `PLACEHOLDER` slots (20–29, 42–87) | **20** (180–199) |
| Unity assets needed | **none** | a cloned `Policy` GameObject under `UI` |
| Live-build unknowns | none | three (below) |

So project registration is policy registration minus the hard part, with
nearly three times the id space. It proves the whole feature with no Unity
risk.

- [x] **Find the effect dispatch sites, and establish whether new ids can be
      injected or only existing ones intercepted.** Found, and the answer is the
      good one — the five dispatch sites are tabulated in
      [summary-effects](code-findings/summary-effects.md), and **not one of them
      has a `default:` arm.** An unrecognised id falls through, the game still
      accepts/researches/tracks the policy or project, and nothing happens. So
      **new ids can be injected**, and a Harmony *postfix* supplies their
      behaviour — composing with the game's switch instead of replacing it. No
      transpiler, and none of the 117 existing policy arms need reimplementing.
      Policy ids also carry the **`+100` offset convention** for the second side
      (`case 0: case 100:` → Union / Confederate instances of one policy), so a
      registered effect must handle both id blocks or key off `alliance`.
- [x] **Resolve the prefab question — was the gating unknown.** Answered in
      [22-policy-and-project-catalogue](code-findings/22-policy-and-project-catalogue.md)
      §15. Projects need no prefab. Policies cannot come from config, but
      `Policy.InitializeStatics()` is `public static` and rebuilds `allpolicies`
      from every `Policy` component under the `UI` GameObject, so **cloning a
      policy GameObject there and calling that method is the registration
      route** — inheriting the editor-authored UI wiring that cannot be rebuilt
      from data.
- [x] Harvest the id → effect catalogue into
      [22-policy-and-project-catalogue](code-findings/22-policy-and-project-catalogue.md).
      All 117 policies and 125 projects, with chapters, prerequisites, reason
      codes, the state-index table and the AI agendas. One correction to the
      plan: `Projects.GetProjectEffect` is a real rosetta stone, but
      `Policies.UpdateAllTooltips` is **not** — it only appends research-speed
      text, so the policy half had to be read out of the 1,200-line switch.
- [x] Map which effects are balanceable via config versus which need patching.
      **33 `project_*` prefs carry every project magnitude in the game**, with
      one exception (project 5 Hall's Carbines uses literals). So project
      rebalancing is config work and belongs to the override layer, not to
      effect registration.
- [x] **Projects: built and verified in game.** A registered project appears in
      the panel under its category filter, with its own name and description,
      priced in its subsidy pot. Postfixes on `Import`,
      `UpdateOneTimeProjectsEffects` and `UpdateProjectEffects`; the public
      surface is `ModApi.Projects.Register(key, configure)`.

      **Claim a `PLACEHOLDER` slot, do not append a record.** The shipped table
      has `projectid == list index` for all 125 records and
      `IsProjectAvailableInScenario` exploits it with a direct
      `loadedprojects[projectid]` before falling back to a search, so an
      appended id would make that fast path return the wrong record and an id
      at or past `Count` would throw. Slots are found by the publisher's
      `PLACEHOLDER` marker rather than a hardcoded range.

      Three fields decide whether a project is real, and getting any wrong
      yields one that looks registered and does nothing:
      - **`alliances` must contain the player's alliance** or the panel never
        builds an entry. Registration now rejects a definition naming no side.
      - **`subsidytype` *is* the category** — the filter arms test
        `subsidytype == n` against Politics…Diplomacy. One field, two jobs.
      - **`usedgroup` is only the panel column** (`ContentFoldersLeftRight`;
        shipped data uses 0 and 1).

      And two things the live run settled that the code could not:
      - **No `Tooltip.RegisterNewTooltip` is needed.** The panel reads
        `prosadescr` and `effectsdescr` straight off the record.
      - **An unset image is harmless.** `Resources.Load` returns null and a null
        `Image.sprite` renders as nothing, so art is genuinely optional.

      `datefrom` is deliberately left empty: a non-null date makes
      `CheckProjectUnlocks` grant the project **free** at new-campaign setup
      (frame 32, new campaigns only), which is not an availability gate and not
      something a mod should get by accident.
- [ ] Effect callbacks are wired but **thin until `Modifiers` lands**: a mod can
      run code on appointment and on refresh, but the library gives it no
      curated way to write game state yet. `Alliance.GetProjectLevel(int)` is
      the pull side worth postfixing when it does — one patch changes every
      consumer at once.
- [ ] **Pin the ids.** Allocation is by key order, so it is stable for a fixed
      mod set and not across changes to it. A save referencing a mod project
      needs the mapping persisted beside it, which is the `Persistence`
      capability. Until then the mapping is logged every load.
- [ ] **Policies:** the registration surface stays **out of the public API**
      until three things are tested on a live build: whether the clone is
      discovered under `UI`; whether `ButtonTooltipRef` and the other outward
      references survive `Instantiate` or still point at the original's
      objects; and what the panel does with a button it has no layout slot for.
      A fourth is already known from the code: `InitializeStatics` runs
      `ImportPolicyData` immediately afterwards, which **overwrites the clone's
      fields** if `policies.dat` carries that id. Note also that grand-policy
      status is decided by the **grandparent object's name**, not by a field.
- [ ] Respect the **replay contract**: `UpdatePolicyEffects` resets thirteen
      fields and replays every policy's effect each pass, so a registered effect
      must either guard on `alreadyactivatedacts` or scale by `timediff` — which
      is exactly what the contribution model does for free.
- [ ] Cover **teardown** (`DeactivatePolicyEffect`) and **recurrence**
      (`UpdatePolicyEffects`), not just the launch path, or an effect persists
      after its policy is removed.
- [ ] Text via `Tooltip.RegisterNewTooltip` — `public static`, appends to the
      live tooltip table.
- [ ] Teach the AI, or decide not to and say so.
      `Policies.CheckAIPolicyChange` and `AIPersonality.GetNextProjectRandom`
      are both small and data-driven; without them, mod content is player-only —
      a legitimate v1 choice, but it must be documented rather than discovered.

### The id broker

Both effect systems key off a bare `int`, and the space is small, shared and
unmanaged. Left alone, two mods pick 184 and the second player to install both
gets silent nonsense.

- **Policies: ids 180–199 only — twenty slots.** `maxpoliciesingame = 200`
  sizes `interventionpointspolicy`, which is indexed by policy id, and the
  highest shipped id is 179. This **supersedes** the earlier note that
  `requiredpolicyids` being three characters wide leaves "room above 199" — the
  field width is irrelevant when the array throws. The `+100`
  Union/Confederate convention may consume them in pairs, so budget ten
  logical policies, not twenty.
- **Projects: 56 `PLACEHOLDER` ids** (20–29, 42–87).

So the ModApi must allocate ids, not let modders choose them:

- [ ] Allocate from a **string key** (`"mymod.conscription-reform"`), never a
      number the modder picks.
- [ ] **Persist the key → id mapping in the save side-car.** Without this, a
      save referencing policy 184 means something different after the player
      installs another mod and allocation order shifts. This is the single most
      corruption-prone part of the design.
- [ ] Report exhaustion as a clear error naming which mods hold which slots, and
      investigate whether the policy array can be grown by a patch to lift the
      200 ceiling.

### Persistence: side-cars, never the game's files

Saves reconcile objects **by position with a float tolerance**
(`GetGameObjectFromPosition(..., float accuracy = 0.5f)`), not by stable id, so
anything appended to a game save file is fatal.

- [ ] Side-car files keyed off the save folder, hooked on
      `SceneManagement.SaveAll`.
- [ ] Every record carries **its own identifier**; never rely on position.
- [ ] A save must still load with the mod uninstalled.
- [ ] The id-broker table lives here.

### Scheduling

- [ ] **Defer work through `SceneManagement.batchoperation`** rather than
      starting coroutines — it inherits the game's ordering and pacing. The
      dispatch is 16 named ids with no default branch, so a mod id passes
      through harmlessly and a postfix on `UpdateBatchOperations` picks it up.
      Arguments are boxed `object`s cast unchecked, so a mod id must own its own
      argument discipline. Note the queue **drains before** `GameVars.frame++`,
      so an operation scheduled for frame N runs while the counter still reads
      N−1.
- [ ] **Rate-correct, never tick-correct.** `Economy.UpdateEconomyEstimates(num2)`
      takes the elapsed delta; that is the pattern to copy.
- [ ] Expose `GameVars.SetGameSpeed(float)` rather than anything in UniStorm —
      game time comes from the third-party weather asset, and `Time.deltaTime`
      has no consumer in any simulation class.

### Also worth investigating

- [ ] **Re-enable the dormant debug mode.** `debug.txt` shows numerous
      `[deactivated]` dev flags. If those code paths still exist, exposing them
      gives modders a dev console and scenario-loading tools nearly free.
- [ ] UI injection points, so mods can add panels rather than only change
      numbers. The policy-clone work is the first real test of whether the UI
      hierarchy tolerates additions at all, so sequence it after that.

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
