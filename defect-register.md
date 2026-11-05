# Defect register

Every defect and manual deviation the analysis has turned up, in one list, with
a stable id — so a fix can be built, toggled, released and argued about by
reference.

**Design and rationale:** [`plan.md` Phase 4b](plan.md#phase-4b--the-defect-register-and-the-bug-fixing-mod).
**Fixes:** `GTCW.BugFixes`, an opt-in mod built on the ModApi. The library
itself never changes game behaviour.
**Compatibility:** no fix may break a mod that edits the game's data files
([the boundary](plan.md#compatibility-boundary--existing-data-mods-keep-working)).
Two consequences bind every row below: **a fix is never a file rewrite** — class
D fixes are runtime overrides, so a data mod's edit of the same file is never
clobbered and nothing survives uninstall — and **certainty decides whether we
fix it, reach decides whether it is on.** A defect can be beyond doubt and still
ship off by default because it moves a quantity data mods author and balance
against.
**Build:** every row refers to the build in
[`code-findings/00-build-provenance.md`](code-findings/00-build-provenance.md).

> **Wave 1 is implemented.** GTCW-001, 002, 003, 004, 006 and 014 are written
> and building in `mods/GTCW.BugFixes/Fixes/`. 001, 002, 003 and 014 are
> `confirmed (headless)` by the [test harness](tests/README.md); 004 and 006 are
> tested without the engine only and still owe a confirm in the game.

> **The perk wave is implemented.** GTCW-024, 025, 027, 028, 035, 036, 038, 039
> and 041 are written, and all nine are `confirmed (headless)`; the rest of the
> perk family (GTCW-023 … GTCW-059) is recorded. All of it ships off unless the
> one family switch, `perksasdocumented`, is turned on.

> **State of this file.** First pass. It is seeded only from findings that
> already state a defect precisely enough to cite a mechanism. The three
> catalogue sweeps — the `code-findings` summaries, `manual-vs-code` parts I–VI,
> and `ui-vs-simulation` — are pending and tracked in `plan.md`. **Absence from
> this register means nobody has written the row yet, not that the game is
> correct there.**

---

## How a row works

| Field | Rule |
| --- | --- |
| **Id** | `GTCW-NNN`, allocated in order, **never reused and never renumbered.** Config toggles, release notes and commit messages cite it. |
| **Defect** | The claim in one line. The account stays in the findings file. |
| **Class** | One of the seven below. The class decides whether it is ours to fix. |
| **Source** | The findings file and section that established it. **No row without one.** |
| **Fix** | `patch`, `override`, `repair-on-load`, `contribution`, or `none`. **Never a file rewrite.** |
| **Default** | `on` only where the fix cannot change a tuned outcome — crashes, NaNs, dead guards, anything that only feeds a readout. Everything else `off`, however certain the defect. |
| **How** | The patch target, technique, repro and confirm for each id live in [`mods/GTCW.BugFixes/IMPLEMENTATION.md`](mods/GTCW.BugFixes/IMPLEMENTATION.md). |
| **Status** | The ladder below. |

### Classes

| Class | Meaning | Ours to fix? |
| --- | --- | --- |
| **C** | Code defect — contradicts its own evident intent | yes |
| **D** | Data defect — a shipped value contradicts its own config comment | yes, as an override |
| **M** | Manual deviation, **code** wrong | yes |
| **S** | Manual deviation, **manual** stale — documents cut or changed content | no: fix the manual |
| **U** | Display defect — simulation right, readout lies | blocked on UI injection |
| **B** | Deliberate asymmetry — AI handicap, difficulty discount | no: a balance mod |
| **X** | Unfinished rather than broken — dead prefs, cut content | no: a restoration mod |

### Status ladder

`recorded` → `classified` → `reproduced` → `fixed` → `confirmed`

Plus `blocked` (needs an API capability that does not exist yet) and `wontfix`
(classes S, B, X, and anything with no behavioural effect). **Nothing counts as
`fixed` without a repro, and nothing ships without a `confirmed`.**

**`confirmed (headless)`** — the fix's off / on / off test passes in the
[harness](tests/README.md)'s campaign or battle tier: the real game, the real
plugin, the real scheduler, windowless. That stands in for "seen working in
game" for the simulation. It does not look at the screen, so a fix whose effect
is a number on a panel still gets a look in the real UI before release; those
rows say so. A fix tested only in the engine tier (stubbed Unity) stays `fixed`.

---

## Class C and M — the fix mod's work

Ordered by what a fix is worth, not by id.

| Id | Defect | Class | Source | Fix | Status |
| --- | --- | --- | --- | --- | --- |
| GTCW-001 | **The 1864 election charges its penalty twice.** Objectives 26 and 27 each apply −0.20 Union national morale on the first pass after 10 Nov 1864; in the branch where Lincoln loses, both fire in the same pass. −40 instead of the −20 both tooltips promise, which puts morale under `breakmoraletrigger` and ends the campaign within one tick. | C | [manual-vs-code §32.2](mechanics-findings/manual-vs-code.md), [24](code-findings/24-campaign-objectives.md) §3.2 | patch | **confirmed (headless)** |
| GTCW-002 | **The support-loss breakdown is destroyed by a NaN.** `Mathf.Pow(negative, 0.5f)` makes every support *loss* write NaN into `supportinfluencelast`, and the guard is `== float.NaN`, which is always false in C#. The NaN survives the decay and is cleared only by the next *gain* on the same reason code in that state (the tracker is assigned, not accumulated), so a slot is poisoned from each loss until the next gain — for the rest of the campaign on codes that only ever cost support. | C | [17](code-findings/17-politics-and-support.md) §(the NaN defect) | patch | **confirmed (headless)**; breakdown panel to check by eye |
| GTCW-003 | **The Confiscation Act makes enemy-soil foraging worse.** Manual, appendix and config comment all say supply efficiency on enemy soil +15 %; the code multiplies `supplyenemysoilfactor = 1.15` into the resupply **time**, making local foraging 13 % slower. | M | [manual-vs-code §18.2](mechanics-findings/manual-vs-code.md) | patch | **confirmed (headless)** |
| GTCW-004 | **Naval rate of fire is a constant.** `Mathf.Max(1f, distance / num4)` pins the blend factor to exactly 1, because every gun counted already passed `range >= distance`. Every ship in every engagement fires at the long-range rate; `navalbattlerofmindistance = 1.00` is dead and closing the range buys no rate of fire. The clamp wanted `Mathf.Min`. | C | [manual-vs-code §38](mechanics-findings/manual-vs-code.md) | patch | **fixed**, engine-tested; awaiting a headless or in-game confirm |
| GTCW-005 | **`occupiedby` is a latch.** Full occupation sets it and nothing ever clears it, so units in a state you once fully held are permanently exempt from the enemy-soil `fightingspirit` and supply penalties, and projects 93/94 keep paying — after every town is lost back. | C | [summary-game-state](code-findings/summary-game-state.md) § Traps | patch + repair-on-load | recorded |
| GTCW-006 | **Three turret ships get no turret bonus.** The rate-of-fire bonus tests `withinturret == 1`, while the ship panel counts `withinturret > 0`, so the three ships whose guns are split across two turrets lose the ×1.4 and the interface still calls their guns turret-mounted. | C | [manual-vs-code §39](mechanics-findings/manual-vs-code.md) | patch — **not** the data route, which would mean editing a field of `shiptypes.dat` that data mods author | **fixed**, engine-tested; awaiting a headless or in-game confirm |
| GTCW-007 | **`powratio`'s denominator assigns instead of accumulating** POW camp capacity, so only the last camp counts. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-008 | **`GetCurrentTaxValuePa(-1)` omits `taxrevenuemodifier[corporate]`**, which the per-type branch includes — so the all-types total disagrees with the sum of its parts. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-009 | **`transportcapacitypeak` is updated three times per pass** by a triple-nested loop whose outer index is unused, cutting its trailing window to a third of the intended length. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-010 | **The save loader's −100 M treasury floor is dead** — the value is written twice and the second read is unclamped. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-011 | **`battlecasualtiesavg` averages a cumulative counter**, so the "average" climbs monotonically and means nothing. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-012 | **`weaponindustrylast`'s warm-up gate uses `GameVars.frame`**, which rewinds during a load. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-013 | **`GetActivatedPolicies(researchedonly: true)` counts policies still in research**, ignoring the flags four call sites bother to pass. **Not a behavioural defect: every caller wants exactly that count.** `UpdatePolicyResearch` and the research tooltip divide research speed by the number of policies in research, and `CheckAIPolicyChange` keeps at least one in research; the ignored `includegrandpolicies` never matters because grand policies are researched to 1.0 at campaign start. Only the parameter name is misleading, as [23](code-findings/23-game-state.md) §7.6 item 17 already says. A patch that made it count completed policies would *create* a defect in all four callers. | C | [23](code-findings/23-game-state.md) §7.6, [summary-game-state](code-findings/summary-game-state.md) § Defects | **none** | wontfix |
| GTCW-014 | **`Regiment.UpdateRaids` writes `tradewarfare[2]` with the wrong sign.** All three factors are strictly positive against a negative-cost convention, so raiding any enemy market node (`IIP`, trade node or not) *reduces* the victim's displayed raid losses, corrupts the recomputed total in slot 0 and therefore the commerce-war slider. Ledger only — nothing in the simulation reads the array. | C | [33](code-findings/33-trade-warfare.md) §3 | patch | **confirmed (headless)**; commerce-war panel to check by eye |
| GTCW-015 | **`Regiment.UpdateRaids` never mirrors into the node's own `lossestradewarfare`**, which `IIP` and `CBuilding` both do, so a raided node's map tooltip omits regiment raids entirely. | C | [33](code-findings/33-trade-warfare.md) §3 | patch | recorded |
| GTCW-016 | **`nation[].name` is mutated at runtime** by `statesetname`, so everything that matches a state by string silently stops matching — `SearchNationByName` and policy 30's five-state volunteer bonus among them. Escapes being live only because the territorial names belong to state-sets that precede every shipped campaign start. | C | [summary-game-state](code-findings/summary-game-state.md) § Traps | **none** — the rename is intended and no consumer is reachable; see IMPLEMENTATION.md §6 | wontfix |
| GTCW-017 | **The 1862 midterms penalty has a one-day window.** Objective 9's chapter list is `{1}`; its trigger falls at about 21:24 on 4 Nov 1862 on the campaign clock and chapter 2 at about 21:45 on 5 Nov, so a config label describing −0.01 Union morale *per week* is worth about −0.15 points in total (corrected from "two days, −0.3"). Whether that is a deliberate token or a chapter-boundary accident is unresolved — **so this one ships off** even if fixed. | C | [manual-vs-code §32.1](mechanics-findings/manual-vs-code.md) | patch | recorded |
| GTCW-022 | **Bounties I charges nothing.** Policy 45/145 sets `bountieslevel = 0` and 46/146 sets 1, matching the "Bounties 1-2" arrays indexed from 0, but both consumers guard `bountieslevel > 0` — the recruit bounty in `Economy.GetUnitCost` and the fighting-spirit cap in `TransformStateSupportToFightingSpirit`. Bounties I therefore never pays `bountiescost[0]` ($300 per recruit) or takes `bountiesfightingspirit[0]` (−0.05); it is a free volunteer bonus. | C | [13](code-findings/13-morale-and-cohesion.md) §5 | patch | recorded |
| GTCW-060 | **The tariff factor leaks onto domestic connections.** `IIP.UpdateEconomy` initialises `num7 = 1f` once, before the loop over `connectediips` (`:890`), and sets it to `1 + taxcaps[tarifs] × tax[tarifs]` only when the neighbour's owner differs (`:895`), with no `else`. From the first foreign neighbour on, every later connection, same-alliance ones included, multiplies the neighbour's relative price by the node's own tariff factor in the attractivity term, so a border node under-imports from its own hinterland. Which connections are hit depends on the scene-authored `ConnectedIIPs` order and moves with the front, since a connection turns foreign when a neighbour changes hands. No tariff is charged on the domestic flow; only the volume is wrong | C | [18](code-findings/18-economy.md) §4, "Trade flow" | patch | recorded |
| GTCW-061 | **Raid casualties are squared.** Both blocks of `Autocalc.UpdateRaiding` (`:2356`, `:2383`) compute `num3 *= Random.Range(num3 × v, num3 × (1 + v))`, so a round's casualties are `num3² × U(0.5, 1.5)` instead of `num3 × U(0.5, 1.5)`. The config calls `v` = 0.5 a "+/-" variation, and `UpdateSkirmishing` (`:2529`, `:2563`) writes the same variation as `Random.Range(1 − v, 1 + v)`. Squared, the guards' small term is crushed (about 0.001 men a round in the harness's 001/E raid) and the raider's return term inflated (16–21); both sides' morale hits and the guards' supply loss are computed from the squared term; and since a round's casualties grow with the square of the time it covers, the casualty rate depends on the length of the update step | C | [14](code-findings/14-supply-and-logistics.md) §9 | patch | recorded |

### Defaults for the rows above

Applying "certainty decides whether we fix it, reach decides whether it is on",
**six of these twenty ship on**:

| Default | Rows | Why |
| --- | --- | --- |
| **on** | GTCW-001 | Nobody balances around a campaign that ends on a fixed date, and both tooltips already promise the single charge |
| | GTCW-002, GTCW-011, GTCW-012, GTCW-014, GTCW-015 | All five only feed a readout — a poisoned display tracker, an average of a cumulative counter, a trend line that resets for 1000 frames after every load, a ledger with no simulation reader, a missing tooltip mirror. Each one's full reader set was checked, not assumed |
| **off** | GTCW-003, GTCW-004, GTCW-005, GTCW-006, GTCW-007, GTCW-008, GTCW-009, GTCW-010, GTCW-017, GTCW-022, GTCW-060, GTCW-061 | Each moves a combat, supply or economy quantity that `Config/` exposes and data mods tune against. Certain or not, the player opts in |
| **not fixed** | GTCW-016 | **No reachable consumer.** The rename is intended, `SearchNationByName` has no callers, and `Policies.cs:726`'s seven state names are all permanent ones. See IMPLEMENTATION.md §6 |
| | GTCW-013 | **No behavioural defect** — the method returns what all four callers need. The plan for it in IMPLEMENTATION.md §5 is withdrawn: its "fix" would invert research speed and the AI's policy loop. Its risk note also names `Policies.cs:379`, which calls the default form and is untouched by `researchedonly` |

Two of these changed once the targets were read in the decompile, which is the
argument for reading them before promising anything. **GTCW-012** moved to
default-on: its only readers are a panel and the save file. **GTCW-008** is no
longer pending — the no-argument form feeds `Economy`'s `surplus` and
`Alliance.GetBalance()`, so it is real money and stays off.

## Perks — class C, M and D

**Source for every row: [34 — Perks](code-findings/34-perks.md)**, a fresh audit
of all 48 perks against the manual's Appendix C, traced through every
`HasPerk` / `GetPerkLevel` / `HasPerkParent` / `IncreasePerkExp` call site. Its
verdict: 17 perks work as written, 11 work differently, and **20 have a
headline effect that is dead, inverted or lands on the wrong unit** (§8).

This family gets its own section for three reasons. It is the largest body of
manual deviation found so far; the perks are *documented* in an appendix, so
"what it should do" is rarely in doubt; and almost every row is a misdirected
read rather than a wrong number, which is the kind of defect a patch repairs
cleanly.

**The intention for the family is plain: a perk should do what its tooltip and
the manual say it does.** Where the code and the config label agree against the
manual, the config label wins on magnitude — it is the number the designers
authored and data mods tune. Where the code contradicts *both*, it is a defect.

> **`perksasdocumented` — one switch for the family.** Every row here moves a
> combat or campaign quantity, so by the register's own rule each one ships
> `off`. That would make the family unreachable in practice, so the mod also
> binds a single `perksasdocumented` toggle that turns on every implemented perk
> fix at once. It is still opt-in, still one decision, and any id can still be
> forced on or off individually.

| Id | Defect | Class | Perk(s) | Source § | Fix | Status |
| --- | --- | --- | --- | --- | --- | --- |
| GTCW-023 | **Three perks' melee bonus is display-only.** All three multiply `meleestrength`, which only the unit panel reads; melee casualties never use it. Levels I and II of Spirited charge therefore do nothing in combat at all | C | Spirited charge, Cold steel, Close support | §3.5, §3.10, §3.15 | patch — **design call**, see IMPLEMENTATION.md §6b | recorded |
| GTCW-024 | **Deadly volley's casualty resistance reduces the drill reduction instead of the hit, and only from level III.** `moraleeffects[9] -= moraleeffects[9] * training * 0.7 * f` applies the 0.5 to the *reduction*, so at full drill the hit goes from −0.18 to **−0.39** per hour — the perk makes high-casualty morale worse — and at no drill it changes nothing. The config label is "reduction of morale hit", and the manual gives it at every level | C | Deadly volley | §3.6 | patch | **confirmed (headless)** |
| GTCW-025 | **Iron discipline II+ freezes the friendly-rout penalty instead of ignoring it.** A friendly rout within 60 adds −0.075 to `friendlyroutednear`, which normally decays at 0.3/h and is gone in fifteen minutes. From level II the perk zeroes the *decay* factor, so each rout costs −0.075 an hour for the rest of the battle, down to −1 | C | Iron discipline | §3.8 | patch | **confirmed (headless)** |
| GTCW-026 | **Iron discipline's cohesion rate is symmetric.** ×1.25 / 1.5 / 2 is applied to the rate at which cohesion approaches its target, and that target *falls* under fire and on the move, so the perk speeds the collapse by exactly the factor it speeds the recovery | C | Iron discipline | §3.8 | patch | recorded |
| GTCW-027 | **Vanguard applies its level II effect at level I and never applies its level I effect.** A bare `HasPerk(12)` zeroes the outflanked and flanking-fire terms at every level, which is the manual's level II; the detached penalty the manual removes at level I (`moraleeffects[15]`, "outside the influence of its commander") has no perk test anywhere | C | Vanguard | §3.13 | patch | **confirmed (headless)** |
| GTCW-028 | **Mounted rifles is weaker at every level, and always better than nothing.** Both mounted penalties become `(1 − base) × A[level] + base` using `perk_mountedriflesmalusfactor`, whose config label calls it the *malus* factor (0.75 / 0.5 / 0.2 remaining). The code reads it as the share of the penalty *recovered*, so mounted accuracy runs 0.90 / 0.80 / 0.68 and mounted reload 1.19 / 1.13 / 1.06 — backwards at every level | C | Mounted rifles | §3.11 | patch | **confirmed (headless)** |
| GTCW-029 | **Sharpshooters' accuracy bonus is gated `unittyp == 0`.** A detached skirmisher company carries the perk, its level and its experience, and loses the bonus; the level III range bonus has no such gate | C | Sharpshooters | §3.2 | patch | recorded |
| GTCW-030 | **Three reload bonuses are granted without the condition the manual attaches to them.** Expert skirmishers divides the reload time on *all* of the unit's fire, not only in skirmish order; Close support on every reload, not only canister; Sapper gunners with no cover test at all, unlike Ace of Spade and Lightning brigade, which check one | M | Expert skirmishers, Close support, Sapper gunners | §3.4, §3.15, §3.19 | patch | recorded |
| GTCW-031 | **Grand battery's morale effect is stronger than its label.** For each incoming artillery source the target counts the source and adds its distance, then divides by the mean. Grand battery adds `malus − 1` to the **count without adding distance**, so the mean distance shrinks by the same factor: a lone holder firing from 20 yd or more grows the term **×2.25 at II and ×4 at III**, not the ×1.5 / ×2 the label and the manual state | C | Grand battery | §3.17 | patch | recorded |
| GTCW-032 | **Engineering's build-speed multiplier leaks across the work party.** The factor is assigned inside the per-unit loop but initialised outside it, so once a holder has been processed every later non-holder on the same obstacle line — or at the same pontoon landing zone — builds at the holder's ×2 / 3 / 4 rate | C | Engineering | §3.1 | patch | recorded |
| GTCW-033 | **Six perk-experience rules do not match their own labels.** Sharpshooters' range gate `perk_sharpshooters_rangefrom` is loaded and never read; Ace of Spade levels with no cover test while the two perks that copy it check one; Cold steel and Spirited charge test the **victim's** unit type, so they only level against cavalry and against line infantry respectively; Expert scouts levels only when the revealer is a detached scout company; Mounted rifles and Stormers test unit types 1 and 3, which excludes cavalry scouts and includes foot skirmishers | C | six | §3.2, §3.5, §3.7, §3.9, §3.10, §3.11, §3.12 | patch | recorded |
| GTCW-034 | **Auto-resolve reads the old perk order: Sharpshooters and Zouave are swapped.** Three sites use the numbering of the legacy `157_Zouaves → ChoosePerk(1)` buttons — the casualty bonus on `HasPerk(2)`, the fatigue-morale exemption on `!HasPerk(1)` and the fatigue factor on `HasPerk(1)`. In auto-resolve, Sharpshooters brigades march like Zouaves and Zouave brigades shoot like sharpshooters | C | Sharpshooters, Zouave | §6 | patch | recorded |
| GTCW-035 | **Foot Cavalry's attrition reduction reads a brigade's battle perk 6.** `UpdateCampaignUnitStats` runs on the brigades and returns early above unit type 13, so `HasPerk(6)` there is Ace of Spade, not Foot Cavalry, and `HasPerkParent` is always false for a brigade. The formation's Foot Cavalry never reaches the forced-march sickness multiplier; infantry brigades holding Ace of Spade get it instead | C | Foot Cavalry | §4.7, §7 | patch | **confirmed (headless)** for the arithmetic; whether `doublequick` is ever set for a brigade is still open |
| GTCW-036 | **Ambulance Corps' mortality reduction reads a brigade's battle perk 8.** Same site, same cause: `HasPerk(8)` there is Expert scouts, so cavalry brigades holding Expert scouts get the 0.9 / 0.75 / 0.5 mortality cut and the Ambulance Corps formation's wounded do not | C | Ambulance Corps | §4.9, §7 | patch | **confirmed (headless)** |
| GTCW-037 | **Foot Cavalry's speed bonus has no forced-march test and no unit-type test.** The ×1.1 / 1.2 / 1.3 applies to every campaign move rather than to forced marches, and because the test is a bare `HasPerk(6)` a **fleet** holding Sea Raiders (fleet perk 6) gets it too | C | Foot Cavalry, Sea Raiders | §4.7, §7 | patch | recorded |
| GTCW-038 | **Partisan Brigades is inverted: it costs the raider and does nothing to the enemy.** `UpdateRaiding` has two blocks — the first damages the raided enemy and reads no perks, the second inflicts return casualties and a morale hit on one of the **raider's own** brigades. Both of Partisan Brigades' multipliers sit in the second: own casualties ×1.25 / 1.5 / 2 and own morale hit ×1.10 / 1.25 / 1.50. The config label for the second is "Raiding morale hit on **enemy** partisan brigades" | C | Partisan Brigades | §4.13 | patch | **confirmed (headless)**; corrected 2026-11-05 to apply the factors after the squared casualty term (GTCW-061), see IMPLEMENTATION.md |
| GTCW-039 | **Bushwhackers' skirmishing multiplier sits in the same own-casualties block.** ×1.25 / 1.20 / 1.10 on the raider's own losses. The declining array matches the manual, which is only because the manual documents a declining number; applied where it belongs it is a declining *bonus* | C | Bushwhackers | §4.14 | patch | **confirmed (headless)**; corrected 2026-11-05 to apply the factors after the squared casualty term (GTCW-061), see IMPLEMENTATION.md |
| GTCW-040 | **Bushwhackers' raiding-support multiplier amplifies the raider's own backlash.** `Regiment.UpdateRaids` scales both state-support writes of a raid by 1.10 / 1.25 / 1.50; in the shipped data those are **−0.00025 to the raider's own** support and **+0.0001 to the owner's**. The config label is "support drop in state due to raiding" | C | Bushwhackers | §4.14 | patch | recorded |
| GTCW-041 | **Embedded Reporters skips a holder under another formation and gives every formation under a holder the level I factor.** `CheckForGroupPerk` starts its walk at the unit's *parent* and tests the unit itself only at the top of the hierarchy, so a holder under a formation that does not hold the perk gets nothing (a top-level holder gets its factor); and the factor is indexed by `unit.GetPerkLevel(14)` — the asking unit's own level, which is 0 when it does not hold the perk — so every Corps below a holder gets ×1.25 whatever level the holder has reached. It scales defame as well as fame | C | Embedded Reporters | §4.15 | patch | **confirmed (headless)**. Fix rule: the nearest holder, counting the unit itself; the first version priced a holder under another holder at the parent's level and was corrected 2026-11-04 (IMPLEMENTATION.md) |
| GTCW-042 | **The siege-assault multipliers of four perks are overwritten a few lines later.** `FightUnit` multiplies `num5` for `type == 2`, then, still for `type == 2`, assigns `num5 = GetFortWeaponStrength(unit0)`. Sappers and Miners, Land Torpedoes (defend), River Expedition (assault) and Limelights (siege at night) all lose their multiplier entirely | C | Sappers and Miners, Land Torpedoes, River Expedition, Limelights | §4.11, §4.12, §4.16, §4.18 | patch | recorded |
| GTCW-043 | **The embarked-strength override meant for River Expedition tests perk 10.** `fightingstrengthembarkedriverexp` is labelled *"…has perk 'river expedition'"* (perk 17) and is selected by `HasPerk(10)`, which is Sappers and Miners. River Expedition loses it; Sappers and Miners gets 0.50 instead of 0.25 embarked strength on water terrain | C | River Expedition, Sappers and Miners | §4.11 | patch | recorded |
| GTCW-044 | **The skirmishing-efficiency perks never reach a real skirmish.** `Autocalc.UpdateSkirmishing` (type 6) reads **no perks at all** — it carries two locals, `num6` and `num7`, initialised to 1 and never assigned, in exactly the places a casualty and a morale multiplier belong. The three perks that promise skirmishing efficiency only ever touch the siege path, where GTCW-042 then discards it | C | Partisan Brigades, Bushwhackers, Land Torpedoes | §4.11–§4.14 | patch | recorded |
| GTCW-045 | **Limelights works only in the one case the manual does not list.** The ×1.25 / 1.5 / 2 night factor is applied in `FightUnit` for types 0, 1 and 2; type 1 (raid) and type 6 (skirmish) never call it and type 2's value is overwritten, leaving auto-resolved **field battles** at night — while the manual promises siege and skirmishing | C | Limelights (army) | §4.16 | patch | recorded |
| GTCW-046 | **Engineers & Mechanics buys an early completion event, not a shorter build.** The perk divides the construction timer on placement, so the building "finishes" at Condition 0.8 / 0.67 / 0.5 and a fort gets its garrison early — but in the same update cycle `UpdateRaidSmoke` sees Condition below 1 and re-arms the timer to `1 − Condition`, and the rest is built at the normal rate. **Total time to a full-condition building is unchanged.** | C | Engineers & Mechanics | §4.8 | patch | recorded — **open question**: confirm the re-arm by watching one fort |
| GTCW-047 | **Pontoon Train's "supply efficiency" runs the whole supply clock fast.** ×1.1 / 1.2 / 1.3 multiplies the *elapsed time* of the supply step, so consumption, depot draw-down, resupply **and the upkeep it pays** all scale together. The unit is not more efficient; it spends more per day | C | Pontoon Train | §4.10 | patch | recorded |
| GTCW-048 | **Pontoon Train's creek bonus is pathfinding cost, not speed.** ×0.75 / 0.5 / 0.25 multiplies the NavMesh area cost of `Small Rivers`, so the route planner detours less around creeks; nothing in `RegimentMove` reads area cost, so the unit does not move faster in one | M | Pontoon Train | §4.10 | patch | recorded |
| GTCW-049 | **Siege-strength perks are halved by a +1 baseline and then averaged over the side.** `UpdateSiegeStrength` computes `1 + Σ(factor − 1) / topunits.Count` and then **adds 1**, so the no-perk baseline is 2.0 and a lone holder's Siege Train yields +12.5 / +25 / +50 % instead of the stated +25 / +50 / +100 %. Balloon Corps' siege bonus runs through the same halving, and its array gives 1.05 / 1.10 / 1.15 at every level rather than only at III | C | Siege Train, Balloon Corps | §4.1, §4.3 | patch | recorded |
| GTCW-050 | **Flying Column's "maximum supplies" is a consumption divisor.** The stock cap is untouched; consumption is divided by 1.5 / 2.0 / 2.5. The column does last as long as the manual promises when cut off, and also draws less from its depots — a different mechanism with the documented result, and a worse game if "fixed" | M | Flying Column | §4.6 | **none** | wontfix |
| GTCW-051 | **Field Telegraph's information delay lands on its own fog marks and saturates.** The ×1.0 / 0.75 / 0.5 is applied in `SpottedEnemy.UpdateMarks` to the chance that *the player* gets a "last seen here" mark for the holder — counter-intelligence, and only ever for AI formations, since player units are never in fog. The base term saturates at its clamp within about eight game hours whatever multiplies it | C | Field Telegraph | §4.2 | patch — **blocked on reach**, see IMPLEMENTATION.md §6b | recorded |
| GTCW-052 | **The Bureau's information-delay operator is inverted.** `SpottedEnemy` *divides* the holder's own mark chance by 1.0 / 0.75 / 0.5, once per enemy unit inside its command range — the opposite of Field Telegraph's operator on the same value, which makes an AI formation holding the Bureau **easier** for the player to track | C | Bureau of Military Information | §4.4 | patch | recorded |
| GTCW-053 | **Sea Raiders' import-price increase has no reader but a tooltip.** The 1.10 / 1.25 / 1.50 is stored in `IIP.priceadjustment` and read only by the map tooltip ("Sea raiders affect import prices by …"). No price code reads it. If several Sea Raiders blockade one node, the last in `unitsinrange` sets the factor rather than the highest | C | Sea Raiders | §5.7 | patch — **implements a missing effect**, see IMPLEMENTATION.md §6b | recorded |
| GTCW-054 | **Balloon Barge's fort-bombardment array is dead at levels I and II.** The shipped values are 1.00 / 1.00 / 1.05 against a manual that promises +5 / +10 / +15 %: nothing at the first two levels and a third of the stated bonus at the third. The first **class D** row — a value that contradicts its own documentation rather than code that misreads it | D | Balloon Barge | §5.4 | override — post-parse, never a file edit | recorded |
| GTCW-055 | **Blockade Busters' own-port discount is applied inside the per-unit loop.** `blocked *= effectoonblockadewithinradius[...]` discounts only the blockade summed from units processed *before* the holder; blockaders later in `unitsinrange` are untouched, so the perk's worth depends on list order | C | Blockade Busters | §5.9 | patch | recorded |
| GTCW-056 | **Blockade Busters levels on the wrong fleet's orders.** The effect tests whether the *target* is blockading; the experience test is the **holder's own** `fleetorders == 1`. Attacking blockaders from port — the one thing the manual says levels it — earns nothing | C | Blockade Busters | §5.9 | patch | recorded |
| GTCW-057 | **Unguarded `HasPerk` reads cross the three perk namespaces.** One id means a battle perk on a brigade, an army perk on a formation and a fleet perk on a fleet, and nothing in `HasPerk` knows which. Five sites have no type guard and hand an effect to a perk that should not have it: `FogOfWar` gives Bureau formations and Balloon Barge fleets Expert skirmishers' concealment, and Ambulance Corps formations and Blockade Busters fleets Expert scouts' concealment *and* line of sight; `CheckAbilityToRun` lets a battle-scene HQ carrying Balloon Corps or Land Torpedoes run; `Regiment.UpdateBlockades` lets Foot Cavalry formations blockade. (The two sites where a collision *strips* an effect are GTCW-035 and GTCW-036; the embarked one is GTCW-043) | C | eight | §7 | patch | recorded |
| GTCW-058 | **The campaign AI never picks the last perk, and fills a slot the player cannot reach.** `ChoosePerk` is driven by `Random.Range(0, count − 1)` on the int overload, whose upper bound is exclusive: AI armies never get River Expedition (17) and AI fleets never get Port Defender (10). There is no branch filter, and nothing stops it filling the **fourth** perk slot, which `PanelHandler` never shows the player | C | AI selection | §2.3 | patch | recorded |
| GTCW-059 | **`IIP.UpdateBlockades` compounds the orgrunning penalty once per unit in range.** `blocked *= GamePrefs.orgrunningtradewar` sits inside the per-unit loop alongside GTCW-055, so a harbor with *n* units in range applies the factor *n* times. Found while reading that method for GTCW-055; not a perk defect | C | — | §5.9, in passing | patch | recorded |

### Defaults for the perk rows

| Default | Rows | Why |
| --- | --- | --- |
| **off, with one family switch** | GTCW-023 … GTCW-059 | Every one moves a combat or campaign quantity that `Config/` exposes and data mods tune against. `perksasdocumented` turns on the implemented set in one decision rather than thirty-seven |
| **not fixed** | GTCW-050 | Class M, but the documented *outcome* is already delivered by a different mechanism. "Fixing" it would make the perk worse |

**Nine are implemented** — GTCW-024, 025, 027, 028, 035, 036, 038, 039 and 041 —
and none is `confirmed`. The techniques, the deferred rows and the three that
need a design decision rather than a patch are in
[`mods/GTCW.BugFixes/IMPLEMENTATION.md`](mods/GTCW.BugFixes/IMPLEMENTATION.md) §6b.
## Class U — display defects, blocked on UI injection

The bulk of the known defects are here, in
[`ui-vs-simulation`](mechanics-findings/ui-vs-simulation.md) — roughly fifty of
them, from the permanently empty support-loss list to a morale breakdown whose
own total omits a 3-to-5× multiplier. **They are deliberately not itemised
yet**: the ModApi has no UI injection, so every row would be `blocked`, and the
sweep is worth doing when there is somewhere for the fixes to go.

The one standing rule for this class: **fix the label, never the number.**
Moving the simulation to match a wrong readout is how a display defect becomes
a balance change.

One exception already has an id, because its cause is a code defect rather than
a presentation one: GTCW-002, which is *why* the support-loss list is empty.

## Classes S, B and X — recorded, not ours

| Id | Item | Class | Source | Why not |
| --- | --- | --- | --- | --- |
| GTCW-018 | Three Acts still promise the **Transportation subsidy**, a category removed from the game | X | [manual-vs-code §28, §20](mechanics-findings/manual-vs-code.md) | Restoring a cut category is content, not repair |
| GTCW-019 | `NavyRankChangeTo{B,C}DatePerAlliance` carry the real 1862/1864 Navy reform dates and have **no consumers at all** | X | [summary-game-state](code-findings/summary-game-state.md) § Defects | Authored data for an unimplemented feature |
| GTCW-020 | **Intervention forces have unlimited recruits** — `GetRecruitAmounts` returns a flat 3000 and `HasEnoughRecruits` returns `true` unconditionally for alliances above 1 | B | [summary-game-state](code-findings/summary-game-state.md) § Traps | Reads as a deliberate shortcut for non-belligerents. **Classification unconfirmed** — revisit if European intervention is ever a playable force |
| GTCW-021 | `AddTradeWarfare` opens with two orphaned statements where a magnitude guard used to be (`Mathf.Abs(value); _ = 1000000f;`) | C | [33](code-findings/33-trade-warfare.md) §6 | **No behavioural effect.** Recorded so nobody spends a patch on it — though it is the reason GTCW-014 goes unnoticed |

---

## Counts

| Class | Rows | Of which actionable |
| --- | --- | --- |
| C | 53 | 50 — GTCW-021 and GTCW-013 have no behavioural effect, GTCW-016 no reachable consumer |
| M | 4 | 3 — GTCW-050 delivers the documented outcome already |
| U | ~50, unitemised | 0 — blocked on UI injection |
| D | 1 | 1 — GTCW-054, as a post-parse override |
| S / B / X | 3 | 0 by definition |

**Of the 54 actionable rows, 15 are implemented** — the six of wave 1 and the
nine perk fixes — and **none is `confirmed`.** Thirty-seven of the rows come
from the perk audit, which is why the perk family has its own section and its
own switch.

Next allocation: **GTCW-062**.
