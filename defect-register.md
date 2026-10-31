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
> and building in `mods/GTCW.BugFixes/Fixes/`. None is `confirmed`: each still
> owes the in-game repro and confirm its spec names.

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

---

## Class C and M — the fix mod's work

Ordered by what a fix is worth, not by id.

| Id | Defect | Class | Source | Fix | Status |
| --- | --- | --- | --- | --- | --- |
| GTCW-001 | **The 1864 election charges its penalty twice.** Objectives 26 and 27 each apply −0.20 Union national morale on the first pass after 10 Nov 1864; in the branch where Lincoln loses, both fire in the same pass. −40 instead of the −20 both tooltips promise, which puts morale under `breakmoraletrigger` and ends the campaign within one tick. | C | [manual-vs-code §32.2](mechanics-findings/manual-vs-code.md), [24](code-findings/24-campaign-objectives.md) §3.2 | patch | **fixed**, awaiting confirm |
| GTCW-002 | **The support-loss breakdown is destroyed by a NaN.** `Mathf.Pow(negative, 0.5f)` makes every support *loss* write NaN into `supportinfluencelast`, and the guard is `== float.NaN`, which is always false in C#. The NaN then propagates through the decay and is never cleared, so a reason code that has ever lost support is poisoned for the rest of the campaign. | C | [17](code-findings/17-politics-and-support.md) §(the NaN defect) | patch | **fixed**, awaiting confirm |
| GTCW-003 | **The Confiscation Act makes enemy-soil foraging worse.** Manual, appendix and config comment all say supply efficiency on enemy soil +15 %; the code multiplies `supplyenemysoilfactor = 1.15` into the resupply **time**, making local foraging 13 % slower. | M | [manual-vs-code §18.2](mechanics-findings/manual-vs-code.md) | patch | **fixed**, awaiting confirm |
| GTCW-004 | **Naval rate of fire is a constant.** `Mathf.Max(1f, distance / num4)` pins the blend factor to exactly 1, because every gun counted already passed `range >= distance`. Every ship in every engagement fires at the long-range rate; `navalbattlerofmindistance = 1.00` is dead and closing the range buys no rate of fire. The clamp wanted `Mathf.Min`. | C | [manual-vs-code §38](mechanics-findings/manual-vs-code.md) | patch | **fixed**, awaiting confirm |
| GTCW-005 | **`occupiedby` is a latch.** Full occupation sets it and nothing ever clears it, so units in a state you once fully held are permanently exempt from the enemy-soil `fightingspirit` and supply penalties, and projects 93/94 keep paying — after every town is lost back. | C | [summary-game-state](code-findings/summary-game-state.md) § Traps | patch + repair-on-load | recorded |
| GTCW-006 | **Three turret ships get no turret bonus.** The rate-of-fire bonus tests `withinturret == 1`, while the ship panel counts `withinturret > 0`, so the three ships whose guns are split across two turrets lose the ×1.4 and the interface still calls their guns turret-mounted. | C | [manual-vs-code §39](mechanics-findings/manual-vs-code.md) | patch — **not** the data route, which would mean editing a field of `shiptypes.dat` that data mods author | **fixed**, awaiting confirm |
| GTCW-007 | **`powratio`'s denominator assigns instead of accumulating** POW camp capacity, so only the last camp counts. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-008 | **`GetCurrentTaxValuePa(-1)` omits `taxrevenuemodifier[corporate]`**, which the per-type branch includes — so the all-types total disagrees with the sum of its parts. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-009 | **`transportcapacitypeak` is updated three times per pass** by a triple-nested loop whose outer index is unused, cutting its trailing window to a third of the intended length. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-010 | **The save loader's −100 M treasury floor is dead** — the value is written twice and the second read is unclamped. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-011 | **`battlecasualtiesavg` averages a cumulative counter**, so the "average" climbs monotonically and means nothing. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-012 | **`weaponindustrylast`'s warm-up gate uses `GameVars.frame`**, which rewinds during a load. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-013 | **`GetActivatedPolicies(researchedonly: true)` counts policies still in research**, ignoring the flags four call sites bother to pass. | C | [summary-game-state](code-findings/summary-game-state.md) § Defects | patch | recorded |
| GTCW-014 | **`Regiment.UpdateRaids` writes `tradewarfare[2]` with the wrong sign.** All three factors are strictly positive against a negative-cost convention, so raiding an enemy trade node *reduces* the victim's displayed raid losses, corrupts the recomputed total in slot 0 and therefore the commerce-war slider. Ledger only — nothing in the simulation reads the array. | C | [33](code-findings/33-trade-warfare.md) §3 | patch | **fixed**, awaiting confirm |
| GTCW-015 | **`Regiment.UpdateRaids` never mirrors into the node's own `lossestradewarfare`**, which `IIP` and `CBuilding` both do, so a raided trade node's map tooltip omits regiment raids entirely. | C | [33](code-findings/33-trade-warfare.md) §3 | patch | recorded |
| GTCW-016 | **`nation[].name` is mutated at runtime** by `statesetname`, so everything that matches a state by string silently stops matching — `SearchNationByName` and policy 30's five-state volunteer bonus among them. Escapes being live only because the territorial names belong to state-sets that precede every shipped campaign start. | C | [summary-game-state](code-findings/summary-game-state.md) § Traps | **none** — the rename is intended and no consumer is reachable; see IMPLEMENTATION.md §6 | wontfix |
| GTCW-017 | **The 1862 midterms penalty has a two-day window.** Objective 9's chapter list is `{1}` and chapter 2 begins 6 Nov 1862, so a config label describing −0.01 Union morale *per week* is worth −0.3 points in total. Whether that is a deliberate token or a chapter-boundary accident is unresolved — **so this one ships off** even if fixed. | C | [manual-vs-code §32.1](mechanics-findings/manual-vs-code.md) | patch | recorded |

### Defaults for the rows above

Applying "certainty decides whether we fix it, reach decides whether it is on",
**six of these seventeen ship on**:

| Default | Rows | Why |
| --- | --- | --- |
| **on** | GTCW-001 | Nobody balances around a campaign that ends on a fixed date, and both tooltips already promise the single charge |
| | GTCW-002, GTCW-011, GTCW-012, GTCW-014, GTCW-015 | All five only feed a readout — a poisoned display tracker, an average of a cumulative counter, a trend line that resets for 1000 frames after every load, a ledger with no simulation reader, a missing tooltip mirror. Each one's full reader set was checked, not assumed |
| **off** | GTCW-003, GTCW-004, GTCW-005, GTCW-006, GTCW-007, GTCW-008, GTCW-009, GTCW-010, GTCW-013, GTCW-017 | Each moves a combat, supply or economy quantity that `Config/` exposes and data mods tune against. Certain or not, the player opts in |
| **not fixed** | GTCW-016 | **No reachable consumer.** The rename is intended, `SearchNationByName` has no callers, and `Policies.cs:726`'s seven state names are all permanent ones. See IMPLEMENTATION.md §6 |

Two of these changed once the targets were read in the decompile, which is the
argument for reading them before promising anything. **GTCW-012** moved to
default-on: its only readers are a panel and the save file. **GTCW-008** is no
longer pending — the no-argument form feeds `Economy`'s `surplus` and
`Alliance.GetBalance()`, so it is real money and stays off.

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
| C | 17 | 15 — GTCW-021 has no behavioural effect, GTCW-016 no reachable consumer |
| M | 1 | 1 |
| U | ~50, unitemised | 0 — blocked on UI injection |
| D | 0 so far | — |
| S / B / X | 3 | 0 by definition |

Next allocation: **GTCW-022**.
