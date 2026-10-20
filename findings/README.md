# Findings

Research write-ups, one file per topic. This is the knowledge output of goal 1
("collect information on game mechanics and behaviour") and the input to
goal 2 (the hook library).

## Conventions

**Two kinds of file:**

- **`NN-<topic>.md`** — detailed findings. Exhaustive, with signatures, tables
  and line references. Written to be *searched*, not read end to end.
- **`summary-<topic>.md`** — a summary over a group of detail files. Written to
  be *read*: the findings, their consequences, the traps, and what is still
  open. Start here.

Every detail file links to its summary and every summary lists what it covers,
so either direction works.

**Each file opens with:**

```markdown
**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** which assembly / tool / artifact the finding came from
**Summary:** link to the covering summary
```

**Describe findings, don't paste source.** Signatures, member names, field
layouts, line references and paraphrased idioms are fine, and short
illustrative snippets where prose genuinely cannot carry it. Bulk decompiled
bodies are not — they are the publisher's copyright and they rot on every game
update. Decompiled output lives in `decompiled/`, which is gitignored.

**Tie every finding to a build.** Line references shift when the game updates
or when a different decompiler version is used; treat them as "roughly here",
not as addresses. Record a new build in
[00-build-provenance](00-build-provenance.md) when the game updates.

**Keep open questions in the file they belong to.** Each one ends with an
"Open questions" section — that is the backlog, and it is more useful next to
the evidence than in a central list.

## Index

### Provenance

| File | Contents |
| --- | --- |
| [00-build-provenance](00-build-provenance.md) | Which build every other file was written against, and its hashes |

### Architecture — [summary](summary-architecture.md)

| File | Contents |
| --- | --- |
| [01-assembly-partition](01-assembly-partition.md) | Game code vs. third-party; scale; the exclusion list |
| [02-type-inventory](02-type-inventory.md) | The god classes and their roles |
| [03-scene-wiring](03-scene-wiring.md) | `GameController`, scene anchors, `SceneManagement` lifecycle |
| [04-version-identification](04-version-identification.md) | There is no version constant; hash the assembly |

### Data layer — [summary](summary-data-layer.md)

| File | Contents |
| --- | --- |
| [05-config-paths-and-overrides](05-config-paths-and-overrides.md) | Where config lives; whole-file override resolution |
| [06-config-file-format](06-config-file-format.md) | The exact format, the `Tools` helpers, `projects.dat` schema |
| [07-config-data-model](07-config-data-model.md) | The `Config/` data model, from the official ModdingTool and Quick Guide |
| [08-save-load-surface](08-save-load-surface.md) | `ImportExportUnitData`; the serialization contract |

### Effects — [summary](summary-effects.md)

| File | Contents |
| --- | --- |
| [09-policy-and-project-effects](09-policy-and-project-effects.md) | The effect dispatch sites; why new ids can be injected |

### Simulation — [summary](summary-simulation.md)

| File | Contents |
| --- | --- |
| [10-simulation-tick](10-simulation-tick.md) | How the game is driven: the frame counter, the boot state machines, the four scheduling patterns |

### Combat — [summary](summary-combat.md)

| File | Contents |
| --- | --- |
| [11-combat-resolution](11-combat-resolution.md) | The fire models, the weapon unit-system split, `firepower[3]`, stance, accuracy/casualty formulas, loss types |
| [12-melee](12-melee.md) | Melee entry/exit, the differential casualty formula, `meleestrength` as a display-only value, perk-id reuse |

### Morale — [summary](summary-morale.md)

| File | Contents |
| --- | --- |
| [13-morale-and-cohesion](13-morale-and-cohesion.md) | The 18 battle and 14 campaign morale slots, the rally/cap pipeline, `fightingspirit` as a politics→combat link, cohesion, and two unwired features |

### Logistics — [summary](summary-logistics.md)

| File | Contents |
| --- | --- |
| [14-supply-and-logistics](14-supply-and-logistics.md) | The two supply models, depots as economy buildings, the 5% shortage cliff |
| [15-attrition-and-condition](15-attrition-and-condition.md) | Condition, sickness, desertion, wounded recovery, fatigue and training |
| [16-enumerations](16-enumerations.md) | Cross-cutting reference: `unittyp`, branches, stance, ammo, loss types, supply slots, status and condition label tables, perk-id reuse |

### Politics — [summary](summary-politics.md)

| File | Contents |
| --- | --- |
| [17-politics-and-support](17-politics-and-support.md) | State support and its 42 reason codes, national morale and the loss condition, foreign intervention, recruitment, and the support-breakdown NaN bug |

### Economy — [summary](summary-economy.md)

| File | Contents |
| --- | --- |
| [18-economy](18-economy.md) | `IIP` nodes as local markets, S-curve pricing, boolean production recipes, the debt→rating→interest loop, and policies keyed to scene object names |

### Commanders — [summary](summary-commanders.md)

| File | Contents |
| --- | --- |
| [19-commanders-and-perks](19-commanders-and-perks.md) | The three perk namespaces and the full perk catalogue; the attribute pipeline; ranks and promotion; fame and defame; casualties and succession; `campaignranking` and the commander-in-chief; feuds; `commanders.txt` |

### Campaign AI — [summary](summary-ai-campaign.md)

| File | Contents |
| --- | --- |
| [20-ai-campaign](20-ai-campaign.md) | The 36-step job scheduler; the texture-derived zone map, the value map and the heatmaps; offensive and defensive operations; recruiting; AI personalities; the difficulty sliders and the ten hardcoded cheats; intervention as a script; two determinism problems |

### Battle AI — [summary](summary-ai-battle.md)

| File | Contents |
| --- | --- |
| [21-ai-battle](21-ai-battle.md) | The three `AIBattle` instances and their two clocks; the 30-slot micro task wheel; the macro stance ladder; `ai_stance` as a second stance enumeration; objective chains, flanks and reserves; `aistrategies.dat`; navmesh carving and the order-delay bypass; `FilterMap` as the engine-wide 2-D field type; what a stance does to your own units |

### Policies and projects — [summary](summary-policies-and-projects.md)

| File | Contents |
| --- | --- |
| [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) | All 117 policies and 125 projects by id, with chapters, prerequisites and effects; the reset-and-replay effect contract; the five ladders and the ten dead pref arrays; support and national-morale reason codes mapped to their writers; the state-index table; the AI's policy queues and project lotteries |

### Game state — [summary](summary-game-state.md)

| File | Contents |
| --- | --- |
| [23-game-state](23-game-state.md) | `GameVars.alliance[]` and `GameVars.nation[]` field maps; the two `nations.dat` formats and what a save does and does not overwrite; the 12-game-hour per-alliance master campaign tick; the three recompute idioms that decide whether a write survives; treasury, taxes and the subsidy trio; the national-morale/state-support coupling; the top-level statics and the two meanings of `currenttimefromstart` |

### Campaign objectives — [summary](summary-campaign-objectives.md)

| File | Contents |
| --- | --- |
| [24-campaign-objectives](24-campaign-objectives.md) | All 38 objectives by id with chapters, dates, prerequisites and prestige; the 1862 and 1864 elections, and the only loss condition in the game that is a date; the other half of the intervention trigger; national-morale codes 4–12/14 and support codes 7/8/41 mapped to their writing objectives; how objectives choose the map state set |

### Newspaper and events — [summary](summary-newspaper.md)

| File | Contents |
| --- | --- |
| [25-newspaper-and-events](25-newspaper-and-events.md) | The newspaper panel, its queue and its four producers; all 119 `historicalevents.dat` records with dates, images and the fifteen gating policies; the index-cursor scan and the game-day arithmetic that makes it safe; the eleven events no shipped campaign can reach; `endbattle` and `battleresulttype`; the procedural battle report and the two prefs that make its morale paragraph a constant |

## Planned

Per `plan.md` Phase 2, one file per subsystem, in order: **combat ✅** →
**morale ✅** → **supply ✅** → **politics ✅** (taken early, since state
support kept turning up as an input to everything else) → **economy ✅** →
**commanders ✅** → **AI ✅** (campaign and battle). Each joins a
`summary-<subsystem>.md`.

The AI turned out to be two subsystems rather than one, so the planned
`20-ai.md` became [20-ai-campaign](20-ai-campaign.md) plus
[21-ai-battle](21-ai-battle.md). **Phase 2 as planned is complete**, and the
policy and project catalogue it fed into is now written as
[22-policy-and-project-catalogue](22-policy-and-project-catalogue.md), and the
state those effects write to as
[23-game-state](23-game-state.md), and the last untouched campaign subsystem as
[24-campaign-objectives](24-campaign-objectives.md). The narrative channel both
of those left open is now
[25-newspaper-and-events](25-newspaper-and-events.md).

**Every major campaign and battle subsystem now has a findings file.** What
remains is narrower, and each item is recorded as an open question in the file
it belongs to:

- **`Messages`**, the small on-screen dispatch channel — 1,633 lines, ~45 typed
  ids, text assembled in code rather than authored. Split out of
  [25](25-newspaper-and-events.md), which covers the newspaper itself.
- **The DLC layer.** `DLC_WL` is 7,932 lines and every other file defers to it:
  the prestige economy, incidents, staff, companions, the entirely DLC-gated
  `CheckCurrentOrderUpdate` of [21](21-ai-battle.md) §14, the
  defensive-facing subsystem that [21](21-ai-battle.md) found is gated off in
  the base game, and the `Diary` channel, whose `config/diary.dat` is absent
  from this install ([25](25-newspaper-and-events.md) §3.3).
- **An asset dump.** Several questions now need one rather than more
  decompiling: the policy panel hierarchy
  ([22](22-policy-and-project-catalogue.md) §16),
  `Frontline2.FrontlineTextureDateToBeUsedFrom`
  ([24](24-campaign-objectives.md) §12), `PerksShownGroup`/`PerksShownFleet`
  ([19](19-commanders-and-perks.md)), and the `UI/Newspaper` sprite set
  ([25](25-newspaper-and-events.md) §9).
- **Phase 0 leftovers:** dnSpy, and `xl/vbaProject.bin` from the ModdingTool
  workbook.

After that, Phase 3 — the hook library itself — is the next body of work;
`plan.md` has the design.
