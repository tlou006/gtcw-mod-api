# Code findings

Research write-ups, one file per topic, answering **what the code does**. This
is the knowledge output of goal 1 ("collect information on game mechanics and
behaviour") and the input to goal 2 (the hook library).

Its companion is [`mechanics-findings/`](../mechanics-findings/README.md), which
translates the same facts into **what they mean as gameplay**. Write the
mechanism here; write the consequence for a player there, and source it back to
the section here that supports it.

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

**`findings/NN` in prose means a file in this folder.** The numbered series
lives only here, so the older shorthand ("as findings/19 has it") still
resolves unambiguously; `mechanics-findings/` files are named, not numbered, and
are always referred to by name.

**Keep open questions in the file they belong to.** Each one ends with an
"Open questions" section — that is the backlog, and it is more useful next to
the evidence than in a central list.

### Verification rules

Most corrections this folder has needed fall into a handful of repeating
mistakes. These rules exist to catch them before they are written down. Each
names the case that prompted it.

1. **A "works" claim names its effect site.** An effect works when the value
   changes an outcome — casualties, morale, speed, support, money — so cite the
   line where it enters that calculation, not the line that computes or
   multiplies it. Follow the variable to its last use: a value that is
   overwritten later in the method, or read only by UI, does nothing.
   *`meleestrength` is read only by the unit panel; `IIP.priceadjustment` only
   by a tooltip; `FightUnit` multiplies four perk bonuses into a value it then
   reassigns ([34](34-perks.md) §8).*
2. **An absence claim states its search.** "Never", "only", "no effect
   anywhere", "dead" and "unread" need the search behind them: the identifier,
   value or pattern, the scope searched, and the hit count. Search by name
   *and* by value. *The engineer-branch bonus was missed because the search
   was for reads of branch value 3, not for the pref labelled "engineers" —
   which reads 4 ([19](19-commanders-and-perks.md) §11).*
3. **Read pref values through the loader, not the label.** The loaders parse
   positionally, so a label is a claim about the value below it. Use
   [`scripts/simulate-prefs-loader.js`](../scripts/README.md) and quote the
   value with its file and line. Then check which file wins at runtime:
   battlefield and campaign folders can ship their own copies
   ([05](05-config-paths-and-overrides.md); [34](34-perks.md) §1.1 for seven
   battlefields with older perk values).
4. **Labels, names, tooltips and the manual are intent, not behaviour.**
   Treat them as a hypothesis for the code to confirm. Where they disagree,
   report both. *`increaseofmoralecapforcorrectbranch` scales a recovery
   rate; Pontoon Train's "movement cost" is a pathfinding cost; Flying
   Column's "maximum supplies" is a consumption divisor.*
5. **Ids are not global.** Before reading meaning into a small integer,
   establish its namespace: perk ids mean three tables depending on who holds
   the list ([34](34-perks.md) §7), `historicalbranch` is two enumerations
   ([16](16-enumerations.md)), `currentterrain` differs between battle and
   campaign meshes, and auto-resolve still uses an older perk order
   ([34](34-perks.md) §6). An unguarded `HasPerk(n)` is a finding until every
   holder type has been checked.
6. **Say how sure you are.** Unmarked means read directly from the code or
   data. Mark a number you calculated from verified values *(calculated)*, so
   a reader knows to check the arithmetic. Mark anything that needs the
   running game to confirm — timing, state machines, rates that depend on how
   often a method is called — **Untested**, the tag
   [`mechanics-findings/`](../mechanics-findings/README.md) uses for the same
   thing, and put it in "Open questions" with the test that would settle it.
   The hook library can log the values in-game.
7. **Search before writing; grep after correcting.** Before stating a
   mechanism, grep `code-findings/` and `mechanics-findings/` for its
   identifiers: it may already be documented, or contradicted. When a claim
   is corrected, grep the whole repo for every restatement — summaries,
   `mechanics-findings/`, `mechanics-findings-summaries/`,
   `defect-register.md`, `plan.md` — and fix or flag each copy in the same
   change. Say what changed where it changed ("An earlier version of this
   section said …"). Files that restate a finding should cite the section
   that backs it, so this grep finds them.

To re-check the folder in bulk, `scripts/replay-all-prefs.js` replays all six
config loaders into tables that `check-pref-quotes.js` (quoted pref values),
`check-dead-claims.js` ("dead" and "unread" claims) and `count-dead-prefs.js`
(the census in [29](29-dead-prefs-audit.md)) check against. See
[`scripts/README.md`](../scripts/README.md). They find candidates; every hit
still needs reading.

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
| [29-dead-prefs-audit](29-dead-prefs-audit.md) | Which of `GamePrefs`' 2,215 fields are real: **144 parsed from a config file and never read**, 4 declared and never loaded, 90 of the 144 gameplay-facing; the abandoned subsidy system that accounts for 23 of them and names four categories the game does not have; four twin-pref pairs across two config files, one of which had produced a wrong reading; and the five live defects the sweep exposed — spotted-enemy marks that ignore intelligence entirely because the formula reads a concealment array, the AI's winter offensive modifier reading the defensive pref, fort level that buys capacity but not durability, a defamed commander's experience cap that is never lowered, and three of five power-balance tiers unread |
| [08-save-load-surface](08-save-load-surface.md) | `ImportExportUnitData`; the serialization contract |
| [32-debug-army-setup](32-debug-army-setup.md) | What `debug.txt` can still do, read out of `loaddebugs` rather than its labels: **the scenario-at-start prefs are dead** — the first seven lines are read into a throwaway variable — so the debug flag replaces a battle's army data but cannot load the battle. Which files the flag redirects and under which condition, why `currentoperation == 3` always beats it, and the three ways the shipped `DebugArmySetup` folder is stale: `nations.dat` two fields short per alliance with **no version gate that can rescue it**, `regiments.dat` one field short per regiment where the gate exists but defaults to the current version, and a missing `ships.dat` read without an existence check. Also closes the culture question: `SetCultureInfo` forces `InvariantCulture` from the first `Awake` |

### Effects — [summary](summary-effects.md)

| File | Contents |
| --- | --- |
| [09-policy-and-project-effects](09-policy-and-project-effects.md) | The effect dispatch sites; why new ids can be injected |
| [28-project-effect-reachability](28-project-effect-reachability.md) | Whether each of the 69 real projects actually does what it claims: the four that cannot be appointed at all in a base-game install and why Army Groups are a DLC feature; the five dispatch mechanisms and what each reaches; the ship-import market, which grants 1–4 instantly-built hulls, restocks every three-quarters of a day because the probability is passed the absolute campaign date, and closes permanently once the last hull is spent; the twelve projects whose effect misses, in six classes — two discarded statements, two conditions comparing a loop counter against a resource id, a gate read from the opponent's alliance and inverted, a guard that omits the manufactory, a flag that never resets, and an accumulator carried across sides; and all 33 `project_*` prefs with their shipped values and standing |

### Simulation — [summary](summary-simulation.md)

| File | Contents |
| --- | --- |
| [10-simulation-tick](10-simulation-tick.md) | How the game is driven: the frame counter, the boot state machines, the five scheduling patterns, the day length each scene actually uses, the battle end-of-day state machine, what pausing stops, and the clock-paced / frame-paced census |
| [31-retreat](31-retreat.md) | The retreat system: a queued destination-finder and the game's only mechanism that makes units physically block movement, which `AICampaign` borrows for its own moves through a single global slot. The three-tier battle search — retrace your own advance, rally on a parent HQ with no enemy within 70, or just go backwards — and the five-source campaign search over towns, forts, depots, trade nodes and the nearest unrouted friendly unit, in a ring 35 to 300 from a probe 70 to the rear. **With no candidate it returns the nearest enemy's position, and four of the six call sites then send the unit there at double-quick**; a unit with no known enemy disintegrates instead. **Holding the right mouse button suspends every pending retreat**, and every obstacle rebuild pushes the whole queue back two frames. What execution costs: surrender at 40–80 % prisoners, or 14–28 % for a veteran at full fighting spirit, since fighting spirit halves it and experience cuts it by 30 %; disintegration at 30–80 % desertion by national morale with readiness zeroed, or outright deletion when no fallback town exists |

### Combat — [summary](summary-combat.md)

| File | Contents |
| --- | --- |
| [11-combat-resolution](11-combat-resolution.md) | The fire models, the weapon unit-system split, `firepower[3]`, stance, accuracy/casualty formulas, loss types |
| [12-melee](12-melee.md) | Melee entry/exit, the differential casualty formula, `meleestrength` as a display-only value, perk-id reuse |

### Morale — [summary](summary-morale.md)

| File | Contents |
| --- | --- |
| [13-morale-and-cohesion](13-morale-and-cohesion.md) | The 18 battle and 14 campaign morale slots with their shipped rates, the rally/cap pipeline and the 3-to-5× loss amplifier, the four-step `fightingspirit` derivation as a politics→combat link, the two crack thresholds and the melee-vs-fire rally asymmetry, cohesion with the decoded terrain table, and four unwired or mis-wired features |

### Logistics — [summary](summary-logistics.md)

| File | Contents |
| --- | --- |
| [14-supply-and-logistics](14-supply-and-logistics.md) | The two supply models with their shipped numbers, the depot/local split and the node-selection rule, the corps wagon train and the overnight handout, depots as economy buildings, transport capacity node by node, raiding as a third supply source, readiness, the 5% shortage cliff, the three flat per-night hits of the end-of-day batch, and **replacements, which come down the same pipe** — 26 men a week at best, none at all on low priority |
| [15-attrition-and-condition](15-attrition-and-condition.md) | Condition, sickness, desertion, wounded recovery, fatigue and drill — with every shipped rate, the establishment sizes the wounded model is keyed to, the decoded hospital modes, and four mis-wired features |
| [16-enumerations](16-enumerations.md) | Cross-cutting reference: `unittyp`, branches, stance, ammo, loss types, supply slots, status and condition label tables, perk-id reuse |

### Politics — [summary](summary-politics.md)

| File | Contents |
| --- | --- |
| [17-politics-and-support](17-politics-and-support.md) | State support and its 42 reason codes, national morale and the loss condition, the three continuous couplings (including private wealth as a derivative), state loyalty decided once on the day the war starts, foreign intervention with its zero floor and per-scenario starting relations, the intervention script's sign and routing errors, recruitment with the Militia and Draft Act ladders, contracts and the reenlistment roll, **the battle, naval and town-occupation writers with every shipped magnitude**, the nine consumers of national morale including the battle surrender roll, the difficulty one-shot that is discarded in campaign play, and the support-breakdown NaN bug |

### Economy — [summary](summary-economy.md)

| File | Contents |
| --- | --- |
| [18-economy](18-economy.md) | `IIP` nodes as local markets, S-curve pricing, boolean production recipes, the debt→rating→interest loop, and policies keyed to scene object names |
| [33-trade-warfare](33-trade-warfare.md) | **`tradewarfare[]` is a ledger, not a mechanic** — nothing in the simulation reads it; the damage it records is applied where it is computed. The categories named out of the game's own panel code (1 blockades and privateering, 2 raids, 3 repairs, 0 a recomputed total, 4–9 unused), and the negative-means-cost convention. **The defect: category 2 is written with both signs** — `Regiment.UpdateRaids` adds an unnegated positive where `CBuilding` negates, so raiding any enemy market node *reduces* the victim's displayed raid losses, corrupts the recomputed total and therefore the commerce-war slider. Plus a second gap in the same method: it never mirrors into the node's own `lossestradewarfare`, so the map tooltip omits regiment raids. Why this strikes `AddTradeWarfare` from the top tier of mod seams, and which upstream terms to use instead |

### Commanders — [summary](summary-commanders.md)

| File | Contents |
| --- | --- |
| [19-commanders-and-perks](19-commanders-and-perks.md) | The three perk namespaces and the full perk catalogue; the attribute pipeline and the full consumer map of all five attributes; ranks and promotion; fame and defame; casualties and succession; `campaignranking` and the commander-in-chief; feuds; the fog-of-war accuracy function; **every perk array's shipped magnitudes, with four that point the wrong way**; `commanders.txt` |
| [34-perks](34-perks.md) | A fresh, per-perk audit of all 48 manual perks against the code, with loader-verified values: **19 work as written, 9 differently, 20 have a dead, inverted or misdirected headline effect**. Siege-assault, skirmish and siege-night multipliers overwritten in `FightUnit`; Partisan Brigades and Bushwhackers scaling the raider's own losses; Sea Raiders' price effect tooltip-only; Ambulance Corps and Foot Cavalry testing the brigade's battle perk; eight cross-namespace collisions; Sharpshooters and Zouave swapped in auto-resolve; seven battlefields shipping the manual's older Engineering values; and where it corrects [19](19-commanders-and-perks.md) |

### Campaign AI — [summary](summary-ai-campaign.md)

| File | Contents |
| --- | --- |
| [20-ai-campaign](20-ai-campaign.md) | The 36-step job scheduler; the texture-derived zone map, the value map and the heatmaps; offensive and defensive operations; recruiting; AI personalities; the difficulty sliders with **every handicap priced and the casualty discount located in `SufferCasualties`**, and the ten hardcoded cheats, one of whose one-shots never takes effect in a campaign; intervention as a script; two determinism problems |

### Battle AI — [summary](summary-ai-battle.md)

| File | Contents |
| --- | --- |
| [21-ai-battle](21-ai-battle.md) | The three `AIBattle` instances and their two clocks; the 30-slot micro task wheel; the macro stance ladder; `ai_stance` as a second stance enumeration; objective chains, flanks and reserves; `aistrategies.dat`; navmesh carving and the order-delay bypass; `FilterMap` as the engine-wide 2-D field type; what a stance does to your own units |

### Policies and projects — [summary](summary-policies-and-projects.md)

| File | Contents |
| --- | --- |
| [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) | All 117 policies and 125 projects by id, with chapters, prerequisites and effects; the reset-and-replay effect contract; the five ladders with their subsidy tables, **the slot cap as a lifetime budget of twelve of twenty ladder levels and the cancellation cascade that enforces it**, **the subsidy factor as the project-funding accrual rate**, the ten dead pref arrays and the three Acts whose trade-war clause died with them; Militia Act I's before/after asymmetry; support and national-morale reason codes mapped to their writers; the state-index table; **project availability dates that gate nothing and grant free projects at a new campaign's frame 32**; and the AI's policy queues and project lotteries |

### Game state — [summary](summary-game-state.md)

| File | Contents |
| --- | --- |
| [23-game-state](23-game-state.md) | `GameVars.alliance[]` and `GameVars.nation[]` field maps; the two `nations.dat` formats and what a save does and does not overwrite; the 12-game-hour per-alliance master campaign tick; the three recompute idioms that decide whether a write survives; treasury, taxes and the subsidy trio; the national-morale/state-support coupling; the recruiting pools as lifetime quotas and the USCT exception that leaks to the Confederacy; the top-level statics and the two meanings of `currenttimefromstart` |

### Campaign objectives — [summary](summary-campaign-objectives.md)

| File | Contents |
| --- | --- |
| [24-campaign-objectives](24-campaign-objectives.md) | All 38 objectives by id with chapters, dates, prerequisites and prestige; the 1862 election's one-day window and the 1864 election's doubled penalty, which makes it an immediate loss; the other half of the intervention trigger; national-morale codes 4–12/14 and support codes 7/8/41 mapped to their writing objectives; how objectives choose the map state set; and the two war-progress statics written by the town-capture path |

### Newspaper and events — [summary](summary-newspaper.md)

| File | Contents |
| --- | --- |
| [25-newspaper-and-events](25-newspaper-and-events.md) | The newspaper panel, its queue and its four producers; all 119 `historicalevents.dat` records with dates, images and the fifteen gating policies; the index-cursor scan and the game-day arithmetic that makes it tight rather than safe; the eleven events no shipped campaign can reach; `endbattle` and `battleresulttype`; the procedural battle report and the two prefs that make its morale paragraph a constant |
| [27-dispatches-and-the-message-log](27-dispatches-and-the-message-log.md) | The other narrative channel: 46 code-written dispatch types, their 50 call sites and the seven shipped warning thresholds; the fog-gated dispatch whose one caller defeats its own gate, so enemy reinforcements are announced through fog of war; the battle de-duplication window that compares hour-of-day and so suppresses repeats for a whole day after midnight; the campaign supply warning that tests two of the four supplies; the Quartermaster's Morning Report, whose morale sentence is a constant; the monthly intelligence report's full disclosure list and its high-water-mark doctrine line; the ten-day log retention enforced only while the panel is open; and three fully authored dispatches the enemy can never send |

### Naval combat — [summary](summary-naval.md)

| File | Contents |
| --- | --- |
| [26-naval-combat](26-naval-combat.md) | The third damage model: `shiptypes.dat`'s 41 variable-length records and its inverted availability string; `armor` as a rating with a dead display band; fleet aggregation and the point-blank Firepower figure; `GetFiringStrength`, whose rate-of-fire term is a constant because its distance ratio can never exceed 1; the turret bonus that tests `withinturret == 1` and so skips three turret ships; the hit/penetrate/damage rolls, where armour gates penetration rather than reducing damage and size is absent from hit probability; ramming as an unrolled per-tick damage scale; the seven-ship combat line; raiders forcing blockaders to even numbers; and ship capture |
| [30-naval-subsystems](30-naval-subsystems.md) | The four subsystems findings/26 left open, plus one it did not. Transport interception end to end — the closing distance that never closes because it omits the ×750,000 its sibling carries, so the hit chance stays pinned at 0.2 a cycle; a retreat order outside the hit test, so one fleet on a sea lane aborts any amphibious move it touches; and a `PickUnit` that can never target the last unit in the convoy. **Every damaged ship escapes a fleet action**, because the roll is `Random.Range(0, 1)` on the int overload — which also explains why ship capture is rare, since the 0.35 escape threshold sits above the 0.25 disable threshold. Blockade efficiency in full, including why spreading a fleet across several nodes costs nothing while the enemy's guards are averaged across them, and that land cavalry on Guard defends a trade node against a fleet. Shipyard capacity's three gates, a `highestportlevel` that latches so losing your best port keeps the unlock, and congestion that never slows a hull because a `Mathf.Min` and a `Mathf.Max` cancel. 123 unreachable lines in `SetRetreatToDepot`, and the fleet branch that destroys a squadron with no reachable harbour. And the five named European intervention fleets — 25 ships, identical in all six starts, five of them 92-gun Screw Ship-of-the-Line |

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
[25-newspaper-and-events](25-newspaper-and-events.md), and its sibling — the
dispatch log — is
[27-dispatches-and-the-message-log](27-dispatches-and-the-message-log.md).

**Every major campaign and battle subsystem now has a findings file.** What
remains is narrower, and each item is recorded as an open question in the file
it belongs to:

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
  ([19](19-commanders-and-perks.md)), the `UI/Newspaper` sprite set
  ([25](25-newspaper-and-events.md) §9), and the message-symbol atlas
  ([27](27-dispatches-and-the-message-log.md) §13).
- **Phase 0 leftovers:** dnSpy, and `xl/vbaProject.bin` from the ModdingTool
  workbook.

Running alongside all of it: **the mechanics translation layer**, now
scaffolded as [`mechanics-findings/`](../mechanics-findings/README.md) with its
conventions and a planned index. It does not need more decompiling — every
planned file is backed by a file in here that already exists.

After that, Phase 3 — the hook library itself — is the next body of work;
`plan.md` has the design.
