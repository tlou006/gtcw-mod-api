# 21 — The battle AI

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `AIBattle` (5,458 lines), `BattleUnits`,
`Regiment`, `GamePrefs`, `FilterMap`; `Config/aistrategies.dat`,
`Config/battleprefs.txt`, `Config/debug.txt`
**Summary:** [summary-ai-battle](summary-ai-battle.md)

The campaign AI is [20-ai-campaign](20-ai-campaign.md). The two share the
`FilterMap` primitive, the `GamePrefs` idioms and nothing else — no types, no
state, no call path.

---

## 1. Three controllers, two lanes

### 1.1 The three instances

`BattleUnits.aibattle` is `GameObject.Find("GameController").GetComponents<AIBattle>()`
(`BattleUnits.cs` ~1368) — **three `AIBattle` components sit on the
`GameController` prefab**. They are assigned in `BattleUnits` ~7141–7162:

| Index | `SetSide` call | `isplayeraiorfeud` | Role |
| --- | --- | --- | --- |
| `aibattle[0]` | the side that is *not* the player's | 0 | the enemy AI — full macro + micro |
| `aibattle[1]` | `GetSideFromAlliance(GameVars.playeralliance)` | 1 | the **player's** auto-pilot — micro only |
| `aibattle[2]` | `SetSide(0, isfeud: true)` | 2 | the feud controller ([19](19-commanders-and-perks.md) §7) |

`SetSide` derives `isplayeraiorfeud`: `1` when `sideofai` equals the player's
side and `!debug_allarmiesai`, else `0`; `isfeud` forces `2`. So with
`debug_allarmiesai` set, `aibattle[1]` becomes a second *full* AI.

`aibattle[2]` is always told side `0` regardless of who is feuding; what makes
it the feud controller is `isplayeraiorfeud == 2`, not its side.

**Only objectives and entry points are shared** — `AddObjectiveToPool` is
called for `aibattle[0]` and `aibattle[1]`, never for `aibattle[2]`.

### 1.2 The micro lane — `Update()`, per frame

`AIBattle.Update()` (~652) returns early when `bunits == null`,
`!sideinitialized`, `debug_noaimovements`, (`debug_noenemyaimovements` and
this is the enemy instance), or the tutorial is inactive-but-not-engaged.

Then, **before the pause check**, every instance runs
`UpdateBlockedCrossings()` and `BlockedMovingOrder.UpdateOrders()` (§7).
After the pause check:

- if `isplayeraiorfeud == 0` or `ai_vs_ai`: `CheckObjectiveChains`,
  `AssignUnitsToObjectiveChains`, `SeparateUnitsToGroups`,
  `CheckReassignmentOfUnlinkedUnitsToReserve`, the hotspot map (§8), and the
  debug overlay;
- **unconditionally**: `UpdateMicroAIAll()`.

That last line is why `aibattle[1]` matters: the player's own formations get
micro AI (§10).

### 1.3 The macro lane — `UpdateAITasks()`, game-time gated

`BattleUnits.CalculateSideStatsAndUpdateAITasks` (~6553) early-returns unless
`currenttimefromstart - lastsidestatupdate >= GamePrefs.sidestatupdatecycle`
(**0.015 game hours**). When it passes it recomputes both sides'
`sideinformation` and then, if `GameVars.frame > 50`, calls
`UpdateAITasks()` on **all three** instances.

`UpdateAITasks` (~2789) is itself wrapped in `(isplayeraiorfeud == 0) | ai_vs_ai`,
so for `aibattle[1]` and `aibattle[2]` it is a no-op in normal play. For the
enemy AI it runs, in order:

```
AssignReserves → UpdateMovingTargets → LinkReservesToLineGroup →
CalculateFlankData → CheckIfFlanksAreAnchored → CheckGlobalAIStrategy →
CheckFlankMoves → CheckEndOfActions → AdjustGroupAIStance →
AdjustGroupFormations → CheckForFeudGroupActions
```

### 1.4 The cadence asymmetry

`GameVars.SetGameSpeed` sets `uniStormSystem.dayLength =
GamePrefs.battledaylengthinmins / gamespeed`, and
`battledaylengthinmins = 120` for "a day of 6–18h" — **1 game hour = 10 real
minutes at 1× battle speed**. Battle speeds are
`GameVars.gamespeedbattles = {1, 2, 5, 20}` (campaign: `{1, 5, 20, 50}`).

- The **macro lane is game-time gated**: every 0.015 game hours ≈ 9 real
  seconds at 1×, 0.45 s at 20×. Its *game-time* cadence is speed-invariant.
- The **micro lane is frame gated** (§2). Its *game-time* cadence degrades
  linearly with game speed.

So fast-forwarding a battle starves the micro AI and leaves the macro AI
untouched. This is the battle-layer counterpart of the campaign AI's
`sqrt(gamespeed)` throughput law ([20](20-ai-campaign.md) §1), but with the
opposite shape: there the whole AI slows down, here only the unit-level half
does.

---

## 2. The micro AI is a 30-slot task wheel

`UpdateMicroAIAll()` (~1978):

```csharp
List<Regiment> list = allgroupsassigned;
if (microaiupdatecycle < list.Count) UpdateMicroAI(list[microaiupdatecycle]);
microaitaskupdatecycle++;
if (microaitaskupdatecycle >= microaitasks) {          // microaitasks = 30
    microaitaskupdatecycle = 0;
    microaiupdatecycle++;
    if (microaiupdatecycle >= list.Count) microaiupdatecycle = 0;
}
fastupdate++;  if (fastupdate >= list.Count) fastupdate = 0;
StopMicroUnitsCloseToEnemyAndRotateTowards(list[fastupdate]);
```

Two lanes:

- **slow lane** — formation `microaiupdatecycle` holds the slot for **30
  consecutive frames** and runs exactly one task per frame. A given formation
  therefore completes one full pass every `30 × N` frames, where N is the
  number of assigned formations. At 60 fps with 12 formations that is ~6 real
  seconds, i.e. ~36 game seconds at 1× but ~12 game minutes at 20×.
- **fast lane** — one formation per frame gets
  `StopMicroUnitsCloseToEnemyAndRotateTowards`, so reactive halt-and-face runs
  30× more often than everything else.

`UpdateMicroAI` (~2515) dispatches on `microaitaskupdatecycle`. Two calls are
**unconditional every frame** — and both are **empty method bodies**:
`CheckReliefOfObjectve` (~3814) and `CheckReliefOfObjectveDueToLowMorale`
(~3818). (Spelling is the game's.)

### 2.1 The slot table

| Slot | Task | Notes |
| --- | --- | --- |
| 0 | `RepositionAIGroupMicroTowardsEnemy` | gated on `ai_stance > 0 && != 2`, `lastaiorder != 10`, `macroai != 3` |
| 1 | `UnlimberArtilleryAIMicro` | `ai_stance >= 0` |
| 2 | `CheckAIBombardment` | |
| 3 | — | `_ = aigroup.ai_stance; _ = 0;` — removed behaviour |
| 4 | `MicroAICheckForCharges(aigroup, 0)` | **`restrictunittypes = 0` → infantry only** |
| 5 | `MicroAICheckForRetreats` | only when `macroai == 3 && ai_stance == 1` |
| 6 | combat behaviour | `ai_stance == 0 → 1`, any of 1–4 → `2` |
| 7 | `AdjustCavalryBehavior` | `ai_stance >= 0` |
| 8 | `CheckLineFallbacks` | `ai_stance >= 0` |
| 9 | `CheckForEntryPointsCloseBy` | |
| 10 | `CheckForLieDown` | |
| 11 | `CheckSkirmishing` | |
| 12 | `CheckForLeftGuns` | |
| 13 | `CheckForPontoonConstruction(0f, aigroup)` | |
| 14 | `CheckForRallying` | |
| 15 | `CheckShorteningDistanceOfAri` | |
| 16 | `CheckPerkSelection` | static |
| 17 | `CheckExpandingFrontline` | `ai_stance >= 0` |
| 18 | — | never referenced |
| 19 | `MarchToSoundOfGuns` | |
| 20 | `CheckArtyFallback` | |
| 21 | `AssaultMove` | |
| 22 | `UpdateDefensivePositions` | **unreachable in the base game** — inverted `PerformAIActionDLCWL` guard, see §12.4 |
| 23 | `CheckUseOfReserves` | |
| 24 | `CheckOutOfFireRange` | |
| 25 | `CheckCounterBatteryFire` | |
| 26 | `CheckForSeparatedEnemy` | |
| 27 | `SetMovementSpeed` | |
| 28 | `UpdateDLCPlayerOrders` | DLC only (§11) |
| 29 | `UpdateSingleOperations` | |

Two further discarded reads sit between slots 22 and 23
(`_ = microaitaskupdatecycle; _ = 17;` and `… _ = 22;`) — leftovers of
conditions whose bodies were deleted.

---

## 3. The macro AI

### 3.1 `macroai` — the fourth stance enumeration

`CheckGlobalAIStrategy` (~3209) maintains `AIBattle.macroai`. The shipped
`battleprefs.txt` comment is explicit: **`0 = assault, 1 = attack, 2 = defend,
3 = retreat`** — the same ordering as `Regiment.stance` and
`grp[].stance` ([11](11-combat-resolution.md)), and the **reverse** of
`ai_stance` (§4). `-1` means "not yet decided"; most macro work gates on
`macroai >= 0`.

### 3.2 The decision variable

`BattleUnits.CalculateSideStatsAndUpdateAITasks` (~6845):

```
forcebalance[s] = totalforceweighted[s] / max(1, totalforceweighted[other])
                × (1 + averagemorale[s]      − averagemorale[other])
                × (1 + averageexperience[s]  − averageexperience[other])
```

`troopbalance` is the same quotient without the morale/experience terms, and
is not read by the macro AI.

### 3.3 Battle type

`BattleUnits.GetBattleType(alliance, usereadinesswhendraw = false)` (~7942)
returns **2** when our objective-point share ≥ `objectiveratiodefend`
(**0.65**), **1** when the enemy's share is, else **0**. With
`usereadinesswhendraw` it falls back to the readiness ratio against the same
threshold (and its mirror `1 − 0.65`), which is how a draw is broken.

### 3.4 The ladder

In order, with `init = commander initiative` and
`T(x) = x / (1 + init × influenceofcommanderinitiativeonattackdefendtrigger)`:

1. **Collapse.** With points on the board, no retreat timer and
   `eodcycle == 0`: if `VictoryBarBelowTrigger() == sideofai`, call
   `SetEndOfBattle(8)` (player side) or `(12)` and start the retreat timer.
   Note what the **default** form of that method actually tests
   (`BattleUnits` ~5320): it computes `GetVictoryTrigger(i, 0)` and then
   **discards it**, returning a side only when its
   `GetVictoryPointsRelation` has fallen to **≤ 0.01** — effective
   annihilation. `triggerforretreat` and
   `influenceofcommanderinitiativeonretreattrigger` are not consulted on this
   path. The separate cease-fire offer uses
   `allowceasefirebelowtrigger` (**0.40**) at `BattleUnits` ~5521.
   `triggerforretreat` *does* reach the victory bar itself — `BattleUnits`
   ~5215 subtracts `GetVictoryTrigger(0)` and `GetVictoryTrigger(1)` (both at
   `typeofevent = 0`) from each side's point share.
   **`triggerforretreatatnight` (0.275) has no effect at all:** its only
   consumer is `VictoryBarBelowTrigger(1)` (~5487), which computes it and
   discards it like type 0, so the night retreat at ~5491 is decided by
   `forcebalance < GetScreeningTrigger()` or the ≤ 0.01 collapse, gated on
   reinforcements and points.
2. **Voluntary retreat on points.** Outside standalone battles, if
   `VictoryBarBelowTrigger(2) == sideofai`, we are behind on points and our
   loss ratio > **0.01**, set `macroai = 3` and stamp `startoffallback`.
   Type 2 is the only form that uses the trigger:
   `max(0.01, triggerforretreat − influenceofcommanderinitiativeonretreattrigger × initiative)`
   = `max(0.01, 0.10 − 0.15 × init)`, which **clamps to 0.01 for any commander
   above 0.6 initiative** — so a bold general never retreats voluntarily on
   points, only on annihilation. `casualtiesrateforsurrender` 0.65 and
   `maxmoraleforsurrender` 0.40 gate the related unit-level surrender path.
3. **Retreat timer.** Once in `macroai == 3`, after
   `timetostartairetreattimer` (**1** game hour) the battle is ended as above.
   While `macroai == 3` nothing further is decided.
4. **Overrides.** `GameVars.aistrategy >= 0` wins outright; then
   `bunits.sideinformation[sideofai].macroai >= 0` wins. The latter is **read
   from the battle data file** (`BattleUnits` ~9948) and round-trips through
   the save (`ImportExportUnitData` ~7235/7365), so a scenario author can pin
   a side's macro stance permanently. `GameVars.aistrategy` is initialised to
   `-1` and **never assigned anywhere in the assembly** — a ready-made
   one-line hook.
5. **Pending change commit.** If `newmacroaitoset != macroai` and
   `timertosetnewmacroai` has elapsed, adopt it.
6. **Force-balance thresholds**, applied in sequence so later ones win:
   `battleType == 2 && forcebalance < T(forcebalancetriggerdefend)` → 2;
   `forcebalance >= T(forcebalancetriggerattack) || battleType == 0` → 1;
   `forcebalance >= T(forcebalancetriggerassaultflanking)` → 0; then
   `battleType == 2 && result != 0` → 2 again.
7. **Draw handling.** Outside standalone battles, when
   `GetBattleType(..., usereadinesswhendraw: true) == 1` and the candidate is
   `-1` or `2`: with no reinforcements inside 24 h, losses > 5 % and
   `forcebalance < GetScreeningTrigger()` → `macroai = 3` immediately;
   otherwise → 1.
8. **Assault promotion.** With `state >= 5`, a mean `flankmovesfactor` in
   `(-1, 0]` and `forcebalance >= T(forcebalancetriggerassaultnoflanking)`, a
   pending `1` is upgraded to `0`.

`GetScreeningTrigger()` is simply `forcebalancetriggerattack × 0.5`.

Commit timing: `timertosetnewmacroai = now + standardtimetochangemacroai ×
(1 − init × influencecommanderinitiativeonshorterstancechanges)` — **1.5 game
hours, scaled down to 0.45 h for a 1.0-initiative general**. From `macroai ==
-1` the change is immediate.

### 3.5 Shipped thresholds

| Pref | Value | Meaning |
| --- | --- | --- |
| `forcebalancetriggerdefend` | 0.80 | below → defend |
| `forcebalancetriggerattack` | 0.80 | at/above → attack |
| `forcebalancetriggerassaultnoflanking` | 3.0 | at/above → assault without flanking |
| `forcebalancetriggerassaultflanking` | 4.0 | at/above → assault with flanking |
| `influenceofcommanderinitiativeonattackdefendtrigger` | 0.7 | divisor term |
| `standardtimetochangemacroai` | 1.5 h | |
| `influencecommanderinitiativeonshorterstancechanges` | 0.7 | |
| `objectiveratiodefend` | 0.65 | battle-type split |
| `allowceasefirebelowtrigger` | 0.40 | cease-fire offer, not the AI retreat path |
| `triggerforretreat` | 0.10 | only via `VictoryBarBelowTrigger(2)` |
| `influenceofcommanderinitiativeonretreattrigger` | 0.15 | subtracted, so init > 0.6 clamps the trigger to 0.01 |
| `timetostartairetreattimer` | 1 h | |

A 1.0-initiative general divides every trigger by 1.7: defend at 0.47 instead
of 0.80, assault at 1.76 instead of 3.0. **Initiative is the dominant
personality dial in battle**, exactly as in the campaign layer.

### 3.6 Retreat spillover

When `aibattle[0].macroai == 3`, `BattleUnits` (~6853) walks every `grp`
of that alliance that the AI has **not** taken into `allgroupsassigned` and
calls `SetRetreatThroughEntryPoint(9999f)` on it and its subordinates —
so formations outside the AI engine still leave the field.

---

## 4. `ai_stance` — the group stance, and why it is not `macroai`

`GameVars.groupstancename = { "none", "screen", "defend", "attack", "assault" }`
(`GameVars.cs` ~6241). This is a **five-value enumeration in the opposite
order** from `macroai`/`stance`:

| | 0 | 1 | 2 | 3 | 4 |
| --- | --- | --- | --- | --- | --- |
| `macroai`, `Regiment.stance`, `grp[].stance` | assault | attack | defend | retreat | — |
| `Regiment.ai_stance`, `ai_stanceordered`, `ai_feudstance` | none | screen | defend | attack | assault |

`unitstatustext[20..23]` = `"Screen", "Defend", "Attack", "Assault"` confirms
the second list is what the UI shows. **Anything that treats a "stance" as a
single number must know which of the two it holds.** See
[16-enumerations](16-enumerations.md).

### 4.1 `AdjustGroupAIStance`

Runs in the macro lane, gated on `macroai >= 0 && state >= 5`. Per formation
in `unitsused`, with `s = GetGroupStrength(includemorale, includeexp,
usestrengthofenemysurrounding)` (§6.4):

```
candidate = 3                                                   // attack
if s > strengthtriggerforassault_micro[macroai]
   && (macroai == 0 || macroai == 1) && oobunitsymbolid == 0     → 4   // assault
if 0 < s < strengthtriggerfordefend_micro[macroai]               → 2   // defend
if macroai == 2                                                  → 2
if (macroai == 0||1) && (candidate != 2 || battleType == 1)
   && 0 < s < strengthtriggerforscreening_micro[macroai]         → 1   // screen
if macroai == 1 && forcebalance < GetScreeningTrigger()
   && candidate != 2                                             → 1
if macroai == 3                                                  → 1
```

Then `bunits.ChangeStance(go, candidate)`, and `ai_stance`/`ai_stanceordered`
are written directly.

Notes:
- `oobunitsymbolid == 0` (§6.1) means **only infantry-dominant formations ever
  assault**; a cavalry or artillery formation caps at attack.
- a macro **retreat** sets the group stance to **screen**, not retreat — there
  is no group stance 3-as-retreat.
- the arrays are indexed by `macroai`, and their stance-3 (retreat) slots ship
  as `0` for assault/defend and `99999` for the reserve/flank triggers, i.e.
  deliberately inert.

| Pref | assault | attack | defend | retreat |
| --- | --- | --- | --- | --- |
| `strengthtriggerforassault_micro` | 1.1 | 1.5 | 3 | 0 |
| `strengthtriggerfordefend_micro` | 0.5 | 0.8 | 1.5 | 0 |
| `strengthtriggerforscreening_micro` | 0.6 | 0.8 | 2 | 0 |
| `strengthtriggerforassigningreserves` | 1.7 | 1.4 | 0.9 | 99999 |
| `strengthtriggerfororderflanking` | 1 | 1.3 | 2.2 | 99999 |
| `strengthtriggerforcancellingflanking` | 0.6 | 0.9 | 1.5 | 99999 |

`minimummoraleforaistances` is indexed **`ai_stance - 1`**, so its four slots
are screen / defend / attack / assault = **0.45 / 0.40 / 0.60 / 0.75**.
`BattleUnits` ~7579 hardcodes index `1` (defend, 0.40).

### 4.2 `ChangeStance` hands the unit to `aibattle[1]`

`BattleUnits.ChangeStance` (~12231) does more than set a field:

```csharp
component.ai_stanceordered = newstance;
component.permanentlydetached = (newstance > 0);
if (component.alliance == GameVars.playeralliance && !debug_allarmiesai && (!ai_vs_ai | manuallyadded))
    aibattle[1].AddUnitToPool(component, -1, false, overwriteaigroups);
…
AddToOrderQueue(selectedunit, true, newstance + 3, …);   // order codes 3..7
```

**Giving one of your own formations any stance other than "none" enrolls it in
the player-side AI**, and setting it back to "none" (for a DLC-commanded unit)
removes it again. Stance changes travel through the order queue as types
`newstance + 3`; combat-behaviour changes as `newbehavior + 30`
(`ChangeCombatBehavior`).

### 4.3 Formations

`AdjustGroupFormations` (~2807), macro lane, `state >= 5 && macroai >= 0`,
only for formations whose ordered stance has been adopted, with no subordinate
moving or engaged:

| `ai_stance` | AI's requested formation id |
| --- | --- |
| 0 none | unchanged |
| 1 screen | 0 |
| 2 defend | 0 (and then blocked — see below) |
| 3 attack | 1 if ≥ 4 infantry subordinates, else 0 |
| 4 assault | 3 if Division, 2 if Corps-or-above, 1 if cavalry-dominant |

The debug label array in the method is
`{ "Line", "Double Line", "Successive Lines", "Attack Column", "March Column" }`,
but `BattleUnits.SetFormation` (~12258) **remaps the id per unit class** before
storing it, so those labels describe the request, not the result:

| requested | → `unittyp <= 13` | → `unittyp >= 14` |
| --- | --- | --- |
| 0 | 2 | 3 |
| 1 | 4 | 1 |
| 2 | — | 1 |
| 3 | — | 2 |
| 4 | 3 | 4 |
| 5 | 0 | 0 |
| 6 | 1 | — |

For formations, requests 1 and 2 both land on internal formation 1, so the
AI's "Double Line" and "Successive Lines" are the same thing. The final guard
excludes `ai_stance == 2`, so the defend row never fires; its body is also
`num = (groupcountperunittype[0] < 4) ? 0 : 0` — **a ternary with identical
arms**. The AI never requests 4 (March Column).

---

## 5. Objectives

### 5.1 Objective chains

An `ObjectiveChain` is the AI's unit of planning. It holds an ordered
`objectives` list with a parallel `objectivetimetomove`, a `usedstrategyid`,
and four unit pools:

| Pool | Contents |
| --- | --- |
| `reservegroups` | unassigned formations |
| `linegroup_centerunit` + `linegroup_leftunits` / `rightunits` | the line of battle, wings ordered outward from the centre |
| `screeninggroups` (+ centre/left/right) | cavalry |
| `artillerygroups` (+ centre/left/right) | artillery |

plus `anchoredflank[2]`, `flankpositions[2]`, `flankrotations[2]`,
`flankstrength[2]`, `flankunit[2]`, `flankunitsingle[2]`, `innerunit[2]` and
`flankmovesfactor`.

`AddUnitToPool(unit, usedstrategyid = -1, …)` (~4401) adds to
`unitsavailable`, registers every attached `unittyp >= 14` in
`allgroupsassigned`, then — **for `isplayeraiorfeud > 0` outside `ai_vs_ai`,
sets `state = 5` and returns `false` without creating any chain**. That is why
the player-side instance has no macro plan at all: no chain, `macroai` stays
`-1`, and `UpdateMicroAIAll` is the only thing it does.

For the enemy AI it looks for an existing chain with the same
`usedstrategyid`; dynamic chains (`-1`) are merged when the new unit is within
`distancetootheraigroupsforcombining` (**200**) of the chain's centre unit and
`CheckObjectiveChainUnitsForSameParent` agrees. Scripted chains
(`usedstrategyid >= 0`) always merge. Otherwise a new chain is created.

> The merge distance `num2` is declared **outside** the per-chain loop and is
> only recomputed when the chain has a unit to measure from, so an empty
> dynamic chain is tested against the *previous* chain's distance. Suspected
> defect; low impact, since empty dynamic chains are short-lived.

### 5.2 `state` — the boot ladder

| `state` | set in | meaning |
| --- | --- | --- |
| 1 | `CalculateNextObjectives` | at least one objective chosen |
| 2 | `AddUnitToPool` (end) | units in pools |
| 3 | `AssignUnitsToObjectiveChains` | units bound to chains |
| 4 | `SeparateUnitsToGroups` | sorted into line / cavalry / artillery |
| 5 | `CalculateFlankData`, and `AddUnitToPool` for the player instance | flanks known — macro work unlocked |

### 5.3 Dynamic objective selection

`CalculateNextObjectives(chainId, run = 0, usecheckformultipleobjectives = false)`
(~4216) is **greedy nearest-objective with no value weighting at all** — the
opposite of the campaign AI's economic value map
([20](20-ai-campaign.md) §4).

- Not defending (`macroai != 2`): for each reserve formation in condition 1,
  take the nearest `Objectives` that is unowned or enemy-owned. On the first
  pass (`run == 0`) with `debug_useflankingmaneuvers`, candidates whose path is
  blocked (`BattlefieldSetup.IsPathBlockedByObstacles`) are skipped; if that
  leaves nothing, it recurses once with `run = 1` and drops the check.
- Defending: take the nearest **owned** objective that has an enemy within a
  **hardcoded 250** while the group itself has no enemy within 250 — i.e. the
  nearest threatened-but-not-yet-contacted objective.
- Falls back to the line centre unit if no reserve qualifies.

`GetGroupCondition` returns 0 when `ai_stance > 0`, `groupmorale <
minimummoraleforaistances[ai_stance - 1]` and an enemy is within
`distancetoenemytocancelinterruption`, or when the object is inactive; else 1.

> `usecheckformultipleobjectives` is **never passed `true`** — the only two
> call sites use the default. The duplicate guard
> `IsObjectiveAlreadySetByAnyGroup` is still *evaluated* (C# `|` does not
> short-circuit) and its result is then discarded, so **two chains can and do
> converge on the same objective**.

### 5.4 Issuing the move

`UpdateMovingTargets` (~3699), macro lane, `state >= 4` and `macroai` neither
`-1` nor `3`. Per chain with a centre unit and at least one objective, it
refuses to issue while anything is in flight (`regimentpaths > 0` for a single
unit, `groupsubordinatesmovingnotfar > 0.3` for a formation, no attached units,
`pathinterrupted`, a `BlockedMovingOrder` already running), while an order of
type 0 or 1 is active, while non-artillery subordinates are engaged, or during
an assault move. `IsGroupStillAbleToFight(onlyfieldedunits: true)` must hold.

Then the target point:

- `ai_stance == 1` (screen) with `aiusefartherobjectivesforscreenstance`
  (a `static` field, `true`): a point **70 units short** of the objective, on
  the bearing from the group;
- otherwise the objective itself;
- then `CheckForSimilarPositions` if `useextensioncheck_objectives` (`true`),
  which nudges the point away from other AI groups' targets.

Dispatch splits on distance:

- **> 150 units** → `new BlockedMovingOrder(unit, blockedcrossings, 150f, dest)`
  (§7);
- **≤ 150** → `bunits.SetWaypoint(…, useorderdelay: true, …)`.

`groupsubordinatesmoving` and `…movingnotfar` are then **forced to 1** so the
same chain is not re-ordered on the next tick.

### 5.5 Reaching and timing

`CheckIfObjectivesTaken(chainId)` (~4151) only ever inspects index 0
(`Mathf.Min(count, 1)`). An objective is "done" when:

- a **dynamic** chain owns it; or
- a **scripted** chain owns it *and* the centre unit is within
  `objectivedistanceaichain` (**100**); or
- a scripted chain has any subordinate within 100 of it
  (`CheckIfAnySubordinateCloseToPosition`); or
- it has been deactivated and a scripted chain is within 100.

…**and** `objectivetimetomove[0] < now`. A `null` objective entry is popped on
the timer alone — that is how an authored "wait" slot works (§9).

When a scripted chain runs out of objectives it logs
`"objective chain N switched to dynamic"` and sets `usedstrategyid = -1`.
**A historical script is a prologue, not a whole plan**: once its objectives
fall, the same formations continue under the greedy dynamic engine.

---

## 6. Order of battle, flanks and reserves

### 6.1 `oobunitsymbolid`

From `Regiment.GetUnitSymbolData()` (~19253): 0 infantry, 1 cavalry,
2 artillery, 3 horse artillery, 4 skirmishers/scouts. For a formation
(`unittyp >= 14`) it is derived from `GetMaxUnitType()` — the dominant
subordinate type — so only 0/1/2 occur, and `groupunittype` is set alongside.

### 6.2 Sorting

`SeparateUnitsToGroups` (~4029), `state == 3` only: cavalry formations
(`oobunitsymbolid == 1`) move to `screeninggroups` **if the chain still has
infantry**; artillery (2) always moves to `artillerygroups`. Everything else
stays in `reservegroups`. Horse artillery (3) has no arm, which is harmless
because formations never report 3.

### 6.3 Flank data

`CalculateFlankData` (~3361), `state >= 4`, then sets `state = 5`. Per chain,
for each side: the outermost wing unit (or the centre if the wing is empty)
supplies `flankrotations[k]`, a `flankpositions[k]` offset half a group width
behind the line, and `flankstrength[k] = GetGroupStrength(includeflanksthreated,
usestrengthofenemysurrounding)`. `flankunitsingle[k]` is the single regiment at
that column end. Whichever flank's outermost regiment has the **more distant**
nearest enemy gets its *opposite* flank's strength inflated by
`flankstrengthfactorcloserenemy` (**1.1**) — the bias that pulls reserves
toward the flank the enemy is closer to. `innerunit[k]` is the next unit in
from the end.

> `num`, `num2` and `num3` are declared **outside** the per-chain loop and
> never reset, so the "which flank has the more distant enemy" result for
> chain *i* is contaminated by chains 0…*i*−1, and a chain whose flank
> regiments see no enemy silently inherits the previous chain's answer.
> Single-chain battles are unaffected.

### 6.4 `GetGroupStrength`

(~2953) The micro AI's universal ratio. Returns **0** if
`combatstatussubordinates <= 0` — i.e. a formation not in contact has no
strength opinion at all, which is why most triggers are written `> 0f &&`.

```
own = max(groupowninrange, groupstrengthaigroup)
enemy = usestrengthofenemysurrounding
      ? GetClosestEnemyOfGroup(group).groupowninrange
      : group.groupenemiesinrange
r = own / max(1, enemy)
if includeflanksthreated && flanksthreated > 0:  r /= 1 + flanksthreated / flankinginfluenceonaidecisions
if includemorale:  r *= lerp1(groupmorale, w_m) / max(0.01, lerp1(enemyMoraleInRange, w_m))
if includeexp:     r *= lerp1(groupexperience, w_e) / max(0.01, lerp1(enemyExpInRange, w_e))
```

where `lerp1(x, w) = (1 − w) + x·w` — the lerp-from-one idiom catalogued in
[11](11-combat-resolution.md) §"three tuning idioms".
`moraleinfluenceonaidecisions = expinfluenceonaidecisions = 0.25`,
`flankinginfluenceonaidecisions = 10`.

### 6.5 Reserve commitment

`AssignReserves` (~3823), `state >= 4`:

1. If the chain has no centre unit but has an objective, `GetMostSuitableUnit`
   picks one from the reserves (retrying with `ignorecombatstatus: true`) and
   it becomes `linegroup_centerunit`.
2. With a centre unit, for each flank *k*, commit a reserve when
   `flankstrength[k] < strengthtriggerforassigningreserves[macroai]`, the flank
   is not anchored, `flankstrength[k] > 0` and it is the weaker of the two —
   **or** `reservegroups.Count > 1 && Random.Range(0f, 1f) < 0.5f`. That second
   clause means that with two or more reserves on hand, **commitment is a coin
   flip that ignores the strength test entirely**. The method then `return`s,
   so **only one chain gets a reserve per macro tick** and the artillery and
   screening steps below are skipped for later chains that tick.
3. Artillery: drain `artillerygroups` into the artillery wings, capped at 20
   iterations.
4. `state >= 5`: push screening (cavalry) out to each unanchored flank,
   anchored on the wing's innermost unit.

> `int num4 = Random.Range(1, 2);` in the artillery loop. `Random.Range(int,
> int)` is **max-exclusive**, so this always returns 1: **AI artillery is
> always assigned to the left wing**. This is a distinct bug from the
> `Count - 1` family catalogued in [20](20-ai-campaign.md) §11 — here the
> bounds are simply one apart.

`CheckEndOfActions` (~2867) sends a wing unit back to the reserves when its
`GetGroupStrength` ≥ **1000** and ≤ 25 % of its subordinates are moving. Since
`GetGroupStrength` divides by `max(1, enemy)`, a value of 1000 means
"thousands of men in range and essentially no enemy" — coherent, but a ratio
compared against an absolute, and fragile if the strength scale ever changes.

> The same method assigns `regiment`/`list` from the left wing and then
> **unconditionally overwrites both from the right wing**, so whenever both
> wings are populated **the left wing's outermost unit is never evaluated**.

`CheckReassignmentOfUnlinkedUnitsToReserve` (~806, micro lane) returns a wing
unit to the reserves when its path is interrupted or it has no `unitlinkedto`,
nothing is moving and no enemy is in range.

### 6.6 Anchoring and flank moves

`CheckIfFlanksAreAnchored` (~3455) uses
`distancetocheckforanchoredflanksriver` (**80**) and
`distancetocheckforanchoredflankhills` (**50**);
`heightdifferencetoenemyunitanchored` (**5**) decides whether an enemy flank
counts as anchored on high ground. `CheckFlankMoves` (~3146) is gated on
`state >= 5 && macroai >= 0 && macroai != 3`, uses
`strengthtriggerfororderflanking[macroai]` to order a flanking move and
`strengthtriggerforcancellingflanking[macroai]` to abort one, moves at most
`maxunitsmovingforflankmaneuver` (**0.25**) of the force at once, and turns out
`rangeofflankingmove` (**0.4**) × group width before turning in.

---

## 7. Movement: carving the navmesh, and skipping the order delay

### 7.1 `blockedcrossings`

`UpdateBlockedCrossings` (~4688) runs **every frame on every instance**, one
unit of `BattleUnits.completeunitlist` per frame, double-buffered into
`blockedcrossingstemp` and published on wrap. For each enemy non-artillery
fighting unit it records:

- every `BattlefieldSetup.rivercrossingpositions` entry within
  `firerange × 1.5` that the unit is facing within ±45°;
- a point one `firerange` directly in front of the unit when it is stationary,
  in cover (`covervalue > 0.15`, `coverobject != 3`) and not routed.

So the list is "covered crossings plus entrenched kill zones".

### 7.2 `BlockedMovingOrder`

Constructing one (~360) spawns a throwaway `GameObject` with a **carving
`NavMeshObstacle`** (box, `range³`) at every blocked position farther than
`range` (150) from the destination, and records `GameVars.frame`.
`UpdateOrders()` — called every frame, before the pause check, by all three
instances — waits **two frames** for the navmesh to rebake, then issues

```csharp
bunits.SetWaypoint(order.unit, order.destination, newpath: true,
                   doublequick: false, -1f, modifylastwaypoint: false,
                   useorderdelay: false, …);
```

and destroys the obstacles.

Two consequences:

- the AI routes around covered crossings and kill zones by **temporarily
  mutating global navmesh state** — any mod that reads pathing during those
  two frames sees a different map;
- **long AI moves (> 150 units) are issued with `useorderdelay: false`**,
  while the short path at ≤ 150 uses `true`. `AssaultMove` and
  `MicroAICheckForRetreats`' fallback also bypass the delay. This is an
  unlisted battle-layer counterpart to the campaign's
  `debug_aicheat_noorderdelayinfrontofenemy`
  ([20](20-ai-campaign.md) §9.2) — but it is not a switchable cheat, it is
  hardcoded into the call sites.

### 7.3 `CheckForSimilarPositions` / `CheckAIGroupInRange`

(~5374, ~5447) Spread targets so two AI groups do not stack on the same point;
applied to objective moves (`useextensioncheck_objectives`) and assault moves
(`useextensioncheck_assaultmove`), both `static` fields shipped `true`.

---

## 8. `FilterMap` — the engine's 2-D field primitive

`AIBattle.Start` does `hotspots = gameObject.AddComponent<FilterMap>();
hotspots.Initialize(20);` — a **20×20** grid, the same resolution as the
campaign heatmaps.

`FilterMap` (349 lines) is used far beyond the AI:

| Owner | Maps |
| --- | --- |
| `AIBattle` | `hotspots` |
| `AICampaign` | `aiareas` |
| `BattlefieldSetup` | `supporttexture`, `precipitation` |
| `Economy` | `fertilitymapfilter[]` (one per resource), `availableworkforce`, `slavery`, `availablecapital`, `transportbottlenecks`, `marketcapacity`, `hospitals`, `tradeandsupply`, `supply` |

That closes the "`UpdateFilterMaps`" open question in
[18-economy](18-economy.md) — the economy's maps are instances of this one
type.

Mechanics worth knowing before hooking any of them:

- a cell's value lives in the **alpha byte of a `Color32`**, so it is
  quantised to 0–255 and saturates;
- `Paint(pos, value, …)` writes `min(existing + 255 × value × falloff, 255)`
  into **`tempmap`**, with a radial falloff over `range` rings;
- **`Clear()` is a buffer swap, not a wipe**: `visiblemap = tempmap`, then
  `tempmap` is reset. Painting accumulates privately and `Clear()` publishes
  it. The name is actively misleading;
- `AnalyzeClusters()` flood-fills `visiblemap` into `Cluster`s with
  `centerposition`, `highestvalue` and `totalclustervalue` (divided by 255).

`Paint` also contains a stray `_ = 2801;`.

### 8.1 The hotspot pipeline

`UpdateAIHotspotUnits` (~4733) rebuilds the enemy-unit list every 100 frames,
or on first use: all enemy `unittyp <= 13` that are active, or inactive
reinforcements arriving within **12 hours**, and not routed.
`UpdateAIHotspotList` (~4764) paints **5 units per frame** with

```
value = strength × morale × (1 + covervalue) / 100000
```

at the unit's position, or at its reinforcement entry point if it is not yet
on the map — so **the hotspot map includes enemy formations that have not
arrived**. On wrap it publishes and sets `hotspotstate = 1`;
`AnalyzeHotspotMap` clusters it and resets to 0. `CheckForSeparatedEnemy`
(~4807) is the only consumer.

---

## 9. `Config/aistrategies.dat`

### 9.1 Format

`AIBattle.StrategiesImportFileName = "Config/aistrategies.dat"`, parsed by the
static `StrategyImport()` (~4631), called from `Start()` — **once per
`AIBattle` instance, so three times per battle**.

```
<record count>
repeat count times:
  <record index>            # parsed, used as the array slot
  <scenario id>             # parsed into LoadedObjectiveChains.scenarioid
  repeat 20 times:
    <objective name>        # a scene GameObject name, or blank
    <timed movement>        # float, absolute battle clock in hours
```

**42 lines per record, plus the count line.** Verified:
`1 + 176 × 42 = 7,393` = the shipped line count. Record 0:

```
SudleySprings @ 0 → MatthewsHill @ 0 → HenryHill @ 13
```

A blank name is kept in step with its timer (the loader appends `""` whenever
the name list falls behind), and in `CreateObjectiveChain` a blank name with a
timer > 0 becomes a **`null` objective** — an authored pause before the next
objective (§5.5).

### 9.2 `timedmovement` is an absolute battle hour

`UpdateMovingTargets` tests `objectivetimetomove[0] <= uniStormSystem.Hour +
battlepasseddays × 24`, so the number is "not before this hour of the battle
clock". Record 0's `HenryHill @ 13` is First Bull Run's afternoon fight.

### 9.3 Objective names are scene object names

`GetObjectiveRefFromName` (~4608) searches the first-level children of
`Battlemap/Map/Objectives` and then `Battlemap/Map/EntryPoints` by **exact
name**, returning `null` on a miss, and `CreateObjectiveChain` only keeps the
result `if (objectiveRefFromName is Objectives)`.

Two consequences for mod authors:

- **an `EntryPoint` name in `aistrategies.dat` is looked up successfully and
  then silently dropped** — only `Objectives` can be chained;
- **a misspelled name is silently skipped.** The shipped file already has a
  candidate: record 38 uses `DixvilleCrossroad` while record 39 uses
  `DixvilleCrossroads`. At most one of those resolves.

This is the same name-coupling class as the economy's trade-node objects
([18](18-economy.md)) and the campaign AI's expeditionary-unit display names
([20](20-ai-campaign.md) §10).

### 9.4 `scenarioid` is dead data

`LoadedObjectiveChains.scenarioid` is assigned in the constructor and **read
nowhere in the assembly**. The chain a formation uses is selected by
`grp[].groupai`, which indexes `loadedobjectivechains` **by record index**, not
by scenario. The scenario id is documentation — useful documentation, since it
identifies the battlefield, but the engine ignores it.

The shipped distribution over the 15 scenario ids:

| sid | records | sid | records | sid | records |
| --- | --- | --- | --- | --- | --- |
| 0 | 23 | 9 | 5 | 19 | 9 |
| 1 | 6 | 11 | 10 | 20 | 12 |
| 3 | 11 | 13 | 13 | 24 | 13 |
| 4 | 7 | 14 | 14 | 27 | 9 |
| 5 | 22 | 15 | 10 | | |
| 7 | 12 | | | | |

Records are **not** grouped strictly by scenario (0–5 are sid 0, 6–10 sid 1,
then 11–14 sid 0 again). Identifiable sets: sid 0 First Bull Run
(SudleySprings, MatthewsHill, HenryHill, StoneBridge, NewMarket), 1 Shiloh
(ShilohChurch, PittsburgLanding, SunkenRoad), 5 Gettysburg (McPhersonRidge,
SeminaryRidge, CulpsHill, CemeteryRidge), 13 Chickamauga (ReedsBridge,
JaysMill, Viniard, Horseshoe), 19 Perryville. Many records are empty chains —
a formation with that `groupai` gets a chain with no scripted objectives and
behaves dynamically from the start.

### 9.5 A loader defect: the index line must equal the ordinal

```csharp
int num = int.Parse(streamReader.ReadLine());
loadedobjectivechains[num] = new LoadedObjectiveChains(int.Parse(…));
for (int j = 0; j < 20; j++) { … loadedobjectivechains[i].objectivenames.Add(s); … }
```

The record is **constructed at `num`** and **filled at `i`**. The shipped file
has `num == i` throughout, so it works. A mod-authored file with records out
of order, or with a duplicated or out-of-range index, writes into a `null` slot
and throws a `NullReferenceException` inside `Start()` — which happens before
any AI initialisation, so the battle scene fails to come up. **Any tool that
edits this file must renumber the index lines to match position.**

### 9.6 Selecting a strategy, and switching it off

`grp[].groupai` is read from the battle data file
(`BattleUnits.LoadBattleData`, ~10028), per formation, along
`startformation`, `startposition`, `stance`, `objectiveposition`,
`orderactivationtime`, `trigger`, `readiness` and `entrenchmentlevel`.
Formations with `groupai == -1` inherit from a parent (~7198).

Two switches discard the authored chains entirely:

- `GameVars.usedynamicstrategies` → the loader forces `groupai = -1`
  (~10041). It is set from the historical-battle setup menu
  (`PanelHandler` ~5844: `GetAIMode() == 0` → historical, else dynamic) and is
  saved and restored with the game (`ImportExportUnitData` ~1667/1919).
- `GameVars.debug_alwaysusedynamicstrategy` → `CreateObjectiveChain` forces
  `usedstrategyid = -1`, from `Config/debug.txt`.

So the "AI mode" option in the battle setup screen is exactly "use
`aistrategies.dat` or not".

---

## 10. What the player-side AI actually does

`aibattle[1]` has no objective chain and no `macroai`. What it runs is the
30-slot wheel over the player's stance-bearing formations — but four
behaviours additionally consult `GamePrefs.playeraistanceactive[5]`, which
despite the name is indexed **by behaviour, not by stance**:

| Index | Behaviour | `Config/debug.txt` | Read in code? |
| --- | --- | --- | --- |
| 0 | `CheckExpandingFrontline` | `true` | yes (~1415, ~1482) |
| 1 | `CheckDefensiveFacing` | `true` | **no** |
| 2 | `MarchToSoundOfGuns` | `false` | yes (~913) |
| 3 | `CheckForLeftGuns` | `true` | yes (~2422) |
| 4 | `AssaultMove` | `true` | yes (~851) |

Index 1 is parsed and never consulted — one more shipped-but-unread pref,
matching the `cohesionbyfire` values in
[13](13-morale-and-cohesion.md). In this case it is moot twice over, because
`CheckDefensiveFacing` is itself unreachable in the base game (§12.4).

Each guard is of the form `alliance == playeralliance && !playeraistanceactive[k]
&& !ai_vs_ai`, so the flags **only restrain the player's own units**; the enemy
AI always runs all five.

Practically: assigning one of your divisions a stance hands it to the
player-side AI, which will then re-dress its line, face it defensively, pick
up abandoned guns, choose perks, deploy and recall skirmishers, unlimber and
withdraw its guns, charge with its infantry, rally its worst unit, lie down
under fire, fall back when morale cracks, and — in assault stance — launch
assault moves at the nearest enemy. It will **not** march to the sound of the
guns, because that one ships `false`, and it will not re-face defensively,
because that whole subsystem is gated off (§12.4).

This is the battle-layer twin of the campaign AI running the player's treasury
under `automanagefinances` ([20](20-ai-campaign.md) §8).

---

## 11. The feud controller

`aibattle[2]`, `isplayeraiorfeud == 2`. Units arrive through
`Regiment.cs:7357`:
`bunits.aibattle[2].AddUnitToPool(this, -1, forcecreatingnewchain: false, overwriteaigroups: true)`.

`UpdateMicroAI` opens with:

```csharp
int ai_stance = aigroup.ai_stance;
if (isplayeraiorfeud == 2 && aigroup.ai_feudstance == aigroup.ai_stance) {
    ai_stance = GetDifferentStance(aigroup.ai_stance);
    aigroup.ai_feudstance = ai_stance;
}
```

`GetDifferentStance` (~2477) rerolls `Random.Range(0, 4)` until it differs from
the old stance. The `int` overload is **max-exclusive**, so it returns 0–3 and
**never 4** — a feuding formation is never given the assault stance. The
trailing `if (num > 4) num = 4;` clamp is unreachable.

Note that the local `ai_stance` computed above is **not used by any of the 30
tasks**, which all read `aigroup.ai_stance` directly; the side effect on
`aigroup.ai_feudstance` is what matters. Most micro guards are written
`(aigroup.ai_feudstance == -1) | (isplayeraiorfeud == 2)`, i.e. "not feuding,
or I *am* the feud controller" — that is how control passes between
`aibattle[0]` and `aibattle[2]` without either removing the unit from the
other's pools.

`CheckForFeudGroupActions` runs in the **macro** lane (so for `aibattle[0]`
and, under `ai_vs_ai`, `aibattle[1]`):

```
p = initiative² × probfeudgroupmovement
  × (political ? chanceoffeudspoliticalcommanders : 1)
  × (!westpoint && !political ? chanceoffeudsvolunteercommanders : 1)
```

and on a hit the formation is sent at the nearest enemy within
`neededdistancefeudgroupmovement` (**250**) with `useorderdelay: true`.
The feud prefs are `feudupdatecycle` 1 h, `feudtriggerlevel` 0.3,
`feudprobability` 1, `timeoffeudaction` 0.15 h, `durationoffeuds` 5,
`probfeudgroupmovement` 1.0 — matching [19](19-commanders-and-perks.md) §7,
where the trigger itself is documented.

---

## 12. The micro behaviours

One line each; all run from the slot table in §2.1 and nearly all iterate
`aigroup.allattachedunits`.

| Method | What it does |
| --- | --- |
| `RepositionAIGroupMicroTowardsEnemy` | the main advance. Target distance `distanceofmaxformicroaiplacement[ai_stance]` (none 1, screen 0.9, defend 1, attack 0.85, assault 0.75 — a fraction of max weapon range), trigger distance `triggerdistancetoactivatemicroaimovement` 275 when the path was interrupted else `…noninterrupted` 75. Per stance: screen → movement mode 4, cover on, fighting units only; defend → mode 0; attack → mode 0, all types; assault → mode 0, cover skipped |
| `StopMicroUnitsCloseToEnemyAndRotateTowards` | fast lane. Halt and face within `distancetoturntowardsenemy` 40 when the angle error exceeds `angletriggerforturningtowardsenemy` 25° |
| `UnlimberArtilleryAIMicro` | limbered guns with an enemy inside `firerange` unlimber and rotate to face. **No null guard on `allattachedunits[i]`** |
| `CheckArtyFallback` | guns with an enemy inside `artilleryfallbackdistances[1]` (40) and no friendly unit inside `[0]` (40) limber, fall back `artilleryfallbackenemyclosedist` on the retreat bearing, movement mode 4 |
| `CheckShorteningDistanceOfAri` | close the range when the guns cannot reach |
| `CheckCounterBatteryFire` | retarget onto enemy batteries |
| `CheckAIBombardment` | preparatory fire; `aibombardduration` 0.25 game hours when assaulting |
| `CheckOutOfFireRange` | pull units that cannot reach anything |
| `MicroAICheckForCharges` | in assault stance only, sets movement mode 3 on **infantry only** (`restrictunittypes = 0` at the single call site, so the default 13 never applies); cancels mode 3 when the stance leaves assault. The actual go/no-go is `Regiment.UpdateChargeTarget` (§12.1) |
| `MicroAICheckForRetreats` | `macroai == 3` and screen stance: break melee (mode 4), fall back `standardretreatdistance` on `unitrange.retreatangle` (mode 2) when the enemy is inside fire range, else head for an entry point |
| `CheckLineFallbacks` | fall back `standardfallbackdistance` (10) when `morale < moraletriggerforfallbackifenemyclose[ai_stance]` (none 0, screen 0.6, defend 0.55, attack 0.6, assault 0.5) or when outflanked without cover |
| `CheckIfOutflanked` / `GroupIsOutflanked` | **never called** — the only read of `debug_usedefensivefallback` is inside `CheckIfOutflanked`, so the "Use AI defensive fallbacks if outflanked" option does nothing (§12.4) |
| `UpdateDefensivePositions` / `DefensivePosition` | pairs own units to the enemies facing them (`unitrange.closestenemyunitfarreg`) and shuffles the line to cover gaps — **unreachable in the base game** (§12.4) |
| `CheckDefensiveFacing` / `GetGroupPosFacingData` / `MostEnemiesAreOutsideAngle` | rotate a defending formation toward the enemy mass. `aidefensiveslices` 20 angle samples over `aidefensivemaxrange` 250, `repositioninggroupsdefensivemaxangle` 20°, `maxcovervalueforgrouprotation` 0.05, `additionalanglewhenincover` +65° (`additionalanglewhenincovermicro` +35° for a single brigade), distance band `repositioninggroupdefensivedistancemin` 150 to `repositioninggroupdefensivedistance` 275 — **unreachable in the base game** (§12.4) |
| `CheckExpandingFrontline` | traffic control: a unit whose destination lands within a group width of a friendly unit's destination sidesteps by half the two widths, left or right by its offset in the group. Requires `pathinterrupted`, no enemy within 1.5 × fire range |
| `AdjustCavalryBehavior` | dismount inside `dismountdistanceforcavalryifenemyclose` 50, remount beyond `movingdistanceforcavalrytomount` 75; skipped in screen and defend stance and below `moraletriggerforfallbackifenemyclose[ai_stance] × 1.2` |
| `CheckSkirmishing` | detach skirmishers from **front-row** (`GetRow() == 0`) infantry with morale ≥ `aiminmoraletodeployskirmishers` 0.5, out of cover, no enemy in fire range, and ≥ `aitimetolastcombattoredeployskirmishers` 2 h since the last deployment — only in screen or defend stance or when the path is interrupted. Scouts detach from cavalry in screen stance only. Reattach on assault stance, melee, `macroai == 3`, morale < `aimoraletriggertoreattachskirmishers` 0.3, or an enemy within `aimindistancereattachskirmishers` 40 |
| `CheckForLieDown` | lie down under fire |
| `CheckForEntryPointsCloseBy` | occupy an entry point within `distancemicroaimovetowardsentrypoint` 150 |
| `CheckForLeftGuns` | crew abandoned guns within `aidistanceforgrabbingguns` 50 |
| `CheckForPontoonConstruction` | build pontoons; intraday search radius `aimaxdistanceforconstructingpontoons` 100. `AICheckInitialPontoonConstruction` handles the deployment phase and is called from `BattleUnits` ~9244 for `aibattle[0]` and, under `ai_vs_ai`, `aibattle[1]` |
| `CheckForRallying` | `SetRallyCommand(aiminmoraleforrallying = 0.5)` on each formation echelon from `unittyp 14` upward, **returning after the first success** — one rally order per full pass, i.e. one per `30 × N` frames |
| `CheckPerkSelection` | **picks a perk uniformly at random** from `PerkRow.PerksShownInfantrySingle` / `…CavalrySingle` / `…ArtillerySingle` for any fighting unit with `ActivePerks() <= 0`. No evaluation whatsoever. (Uses the battle perk namespace of [19](19-commanders-and-perks.md) §1.1, and `Random.Range(0, array.Length)` correctly) |
| `MarchToSoundOfGuns` | move an idle formation toward fighting. Gated on `playeraistanceactive[2]`, which ships **`false`** |
| `AssaultMove` | §12.2 |
| `CheckUseOfReserves` | feed the reserve pool into a pressed line |
| `CheckForSeparatedEnemy` | uses the hotspot clusters to spot an isolated enemy group and sets `aiassaultmoveisolatedtarget` |
| `SetMovementSpeed` | double-quick / normal |
| `UpdateSingleOperations` | units outside any AI group (`aiassignedref == null`), registered via `SingleOperationsAddUnit`; gets the fast-lane halt-and-face with `shortendistance: true`. Contains two discarded reads of the trigger-distance prefs, so the distance it computes is thrown away |
| `UpdateDLCPlayerOrders` / `CheckCurrentOrderUpdate` | §11 below |

### 12.1 The charge decision lives in `Regiment`

`Regiment.UpdateChargeTarget` (~12407) is where a charge is actually resolved.
With `movementmode == 3` and no target yet, against `unitrange.enemyinrangereg[0]`:

```
W(u) = strength × (1 + w[0]) × (1 + Experience()·w[1]) × (1 + morale·w[2]) × (1 + (1 − fatigue)·w[3])
r = W(own) / max(1, W(enemy));   if mounted && weapon >= 0:  r *= weapon.meleemounted
```

with `weightingfactorsformicroaicharge` = **{1, 0.7, 1.5, 0.6}**. Note
`(1 + w[0])` multiplies **both** numerator and denominator, so the shipped
strength weighting **cancels out of the ratio entirely** — only experience,
morale and fatigue weightings have any effect.

Eligibility: infantry outside screen/defend stance; cavalry outside
screen/defend stance but **only against cavalry, artillery, skirmishers,
scouts or routed units**; never against a formation, a unit already in melee,
or (for infantry) a routed enemy. Distance must be inside `maxchargeradius`.
If `r > microaitriggerforcharge` (**3**) the unit sets `chargetarget` and
movement mode **6**; infantry with a loaded volley first stops and fires
(`waitforfirstvolleytobefinished`, `distancefactorfiringlastvolleybeforecharge`)
before converting. Otherwise the charge is cancelled and `lastaichargetime` is
stamped.

`nomicroaichargeifunitisoutflanked` ships `true`.

### 12.2 `AssaultMove`

(~829) `macroai == 0` only, dynamic chains only (a scripted chain returns), the
group not linked to another, an enemy known at range, nothing moving or
engaged, and no other `unitsused` group already moving to within **100** of the
same point. Then `aiassaultmove = true` and
`SetWaypoint(…, newpath: false, useorderdelay: false, …)`.

### 12.3 Two cooldowns that never gate

```csharp
// AdjustGroupAIStance, ~1395
… && unitsused[i].lastaistancechangetime < GameVars.currenttimefromstart + GamePrefs.updatetimeforresettingofaistance
// MicroAICheckForCharges, ~1937
… && allattachedunits[i].lastaichargetime < GameVars.currenttimefromstart + GamePrefs.timetorenewaichargecheck
```

Both timestamps are written as `last… = GameVars.currenttimefromstart`, and
both prefs are positive (`updatetimeforresettingofaistance` **1** game hour,
`timetorenewaichargecheck` **0.5**). `last ≤ now < now + positive`, so **both
conditions are always true** and neither cooldown ever blocks anything. The
offset belongs on the left: `last + pref < now`.

Consequence: a formation's group stance can flip on consecutive macro ticks
(0.015 game hours apart) if `GetGroupStrength` straddles a threshold, and a
unit whose charge was just cancelled for weakness can be re-ordered to charge
on its next pass through slot 4. The config comments describe the intended
behaviour that does not happen.

### 12.4 The whole defensive-facing subsystem is switched off

`PerformAIActionDLCWL(unit, groupforstancecheck = null)` (~2112) is the
per-unit "may the AI touch this?" gate:

```csharp
if (!DLC_WL.dlc_scenarioactive) return true;                       // base game
if (groupforstancecheck != null && groupforstancecheck.ai_stance >= 1
    && unit.groupaiobject == groupforstancecheck.groupaiobject) return true;
if (unit.dlcw_isundercommander) return false;
return true;
```

**In the base game it returns `true` unconditionally.** There are 28 call
sites in `AIBattle`, and **26 of them use it as a positive requirement**
(`… && PerformAIActionDLCWL(x)` or `… || !PerformAIActionDLCWL(x)` in an
early-return chain). Two do the opposite:

```csharp
// UpdateDefensivePositions, ~1673
if (aigroup.unittyp > 13 && … && (ai_stance == 1 || ai_stance == 2)
    && !PerformAIActionDLCWL(aigroup, aigroup)) { … }

// CheckDefensiveFacing, ~1732
if (PerformAIActionDLCWL(aigroup, aigroup) || …) return;
```

So task-wheel slot 22 and everything under it — `GetGroupPosFacingData`,
`CheckDefensiveFacing`, `MostEnemiesAreOutsideAngle`, and the whole
`DefensivePosition` nested class with its `OwnUnits` inner class (~230 lines
together) — **never executes outside the DLC career scenario**, and inside it
only for units under the chosen commander, which is the inverse of what the
guard does everywhere else. The AI's shipped prefs for it
(`aidefensiveslices`, `aidefensivemaxrange`,
`repositioninggroupsdefensivemaxangle`, `additionalanglewhenincover`,
`additionalanglewhenincovermicro`, `repositioninggroupdefensivedistance`,
`repositioninggroupdefensivedistancemin`, `maxcovervalueforgrouprotation`) are
loaded and, for the most part, reach nothing.

Separately, **`CheckIfOutflanked` has no callers at all**, and its only helper
`GroupIsOutflanked` has no other caller. Since `CheckIfOutflanked` holds the
**only read of `GameVars.debug_usedefensivefallback`** in the assembly, the
`Config/debug.txt` entry *"Use AI defensive fallbacks if outflanked"* —
shipped `true` — **does nothing**.

Practical consequence: an AI formation in screen or defend stance does not
re-face toward a shifting enemy and does not close gaps in its line. What it
*does* do is `CheckLineFallbacks` (slot 8), `StopMicroUnitsCloseToEnemyAndRotateTowards`
(the fast lane) and `RepositionAIGroupMicroTowardsEnemy` (slot 0, but excluded
for `ai_stance == 2`). **Restoring the two guards to the polarity used by the
other 26 call sites is a two-line transpiler and the single largest behavioural
change available in this file.** It should be treated as experimental: the
subsystem has, on this reading, never run in a shipped build.

---

## 13. The DLC order channel

`CheckCurrentOrderUpdate(unit, type, position, destinationname, rotation,
width, depth, calledfromcampaign = false)` (~4964, 410 lines — the largest
method in the file) opens with

```csharp
if (!DLC_WL.dlc_scenarioactive || dlc_chosencommander out of range
    || GameVars.commander[dlc_chosencommander].currentcommand == null) return;
```

so **the whole thing is dead weight in the base game**. It is the career-mode
channel: when the player commands one formation, the AI's decisions for its
parent are translated into displayed orders via
`CareerInformationPanel.ShowNewOrder(DLC_WL.givenorder)`. It is called from
`UpdateMovingTargets` (types 0 and 12), `AssaultMove` (4),
`CheckGlobalAIStrategy` (12) and 20-odd other sites, and recursively for the
parent unit (type 11).

Seventeen order types (0–16) each map to a `Tooltip.GetTooltipText(id, 0)`
headline with ids in 1080–1203. Those ids exist in **none** of the shipped
tooltip files (`tooltips_battle.txt` tops out at 512,
`tooltips_campaign.txt` at 1833 but without these entries,
`tooltips_unique.txt` at 3101 with 61 entries) — they live in
`Config/tooltips_dlcwl.txt`, which `DLC_WL.cs:3442` probes for and which this
install does not have. **The order names cannot be recovered from a base-game
install.**

`PerformAIActionDLCWL(unit, groupforstancecheck)` (~2112) is the per-unit
"may the AI touch this?" gate used throughout the micro AI, and it is the
mechanism by which the DLC removes the player's own formation from AI control.
It returns `true` unconditionally in the base game, which is why two inverted
call sites switch a whole subsystem off (§12.4).

Incidental: `Tooltip.RegisterNewTooltip(string headline, string content)` and
its `(int id, …)` overload are **public static and append to the live
`tooltipentry` list**, returning the new id. That is a ready-made registration
point for mod-supplied tooltip text — relevant to the Phase 3 question of
whether new policies and projects can be described without shipping assets
([09](09-policy-and-project-effects.md)).

---

## 14. Config reference

### 14.1 `Config/battleprefs.txt`

The AI block runs from "AI Preferences & Feud" (around line 632) to around line
884. It resolves by **strict comment/value alternation** against the parse
order in `GamePrefs.cs` ~6320–6560. The shipped values quoted throughout this
file come from there; the larger tables are in §4.1 and §12. Prefs not already
quoted:

| Pref | Value |
| --- | --- |
| `maxunitlevelforallocationtoai` | 15 (Corps) |
| `reinforcementdelaystrengthfactor` | 0.7 (`strength × 1/delay^0.7`) |
| `maxdeviationpathupdatestowardsenemy` | 5 |
| `factormaxnonariunitsstoppedforapproaching` | 0.35 |
| `distancetomovegrouptowardsenemyifcontact` | 80 |
| `distancetoswitchtonewtarget` | 25 |
| `microaidistancemovementbeforeturning` | 1 |
| `aibattleminimumfightingratio` / `aibattleminimumunits` | 0.55 / 3 (OR'd) |
| `maximumdistancetoobjectiveroadmovement` | 250 |
| `repositioninggroupsdefensivesubmoving` / `…subfighting` | 0.15 / 0.15 |
| `reinforcementmaxtimeforairetreatdecisions` | 24 h |
| `casualtiesrateforsurrender` / `maxmoraleforsurrender` | 0.65 / 0.40 |
| `minnationalmoralesurrender` / `minavgarmymoralesurrender` | 0.4 / 0.5 |
| `probsurrenderlowmoralelin` / `…exp` | 0.01 / 1.5 |
| `maxdistancecavalryattack` (cavalry attack on weak enemies) | 90 |
| `maxenemymoraleforcavalrychargenonarty` | 0.45 |

`distancetootheraigroupsforcombining` ships **200** while its own config
comment reads *"changed 04.06.21 to -1 (deactivated), before:400"* — the
comment describes neither the shipped value nor a plausible history. Treat the
comment as stale.

`durationoffeuds` ships **5** while its comment works an example with 10 (noted
already in [19](19-commanders-and-perks.md) §7).

### 14.2 Fame triggers — closing a [19](19-commanders-and-perks.md) question

The same file resolves the battle-result index that findings/19 left open:

```
The factor to multiply the fame influence for each unit level
(starting with brigade, division, corps, army)
famefactorleveladjustment = { 0.005, 0.0065, 0.009, 0.013 }

Trigger for getting famous, referenced to battle outcome
[minor victory, major victory, minor defeat, major defeat]
fametrigger    = { 0.025,  0.02,   0.03,   0.04 }
defamedtrigger = { -0.0018, -0.0027, -0.0016, -0.0015 }
```

So **`fametrigger[]` and `defamedtrigger[]` are indexed by
[minor victory, major victory, minor defeat, major defeat]**, and the
corresponding victory-condition thresholds in the same file are vp ratio
≥ 0.7 (minor victory), ≥ 0.8 (major victory), ≤ 0.3 (minor defeat),
≤ 0.2 (major defeat), with `critical losses` above 0.4.

Reading the arrays: it is *easier* to become famous in a minor victory
(0.025) than a major one (0.02), and *harder* to avoid being defamed in a
major victory (−0.0027) than in a major defeat (−0.0015) — the thresholds are
scaled to the number of candidates, not to the glory.

### 14.3 Battle difficulty

`PanelHandler` ~5853, on starting a historical battle:

```csharp
GameVars.usedbonusbattles = componentInParent2.GetBonus();
GameVars.casualtiesmodifier = GameVars.usedbonusbattles * GamePrefs.casualtiesmodifierbonus;
```

with `minstartbonusbattle` 0 and `maxstartbonusbattle` 0.5 — the same shape as
the campaign's two sliders ([20](20-ai-campaign.md) §9.1). `usedbonusbattles`
is then consumed in `BattleUnits.LoadBattleData` (~9950):
`sideinformation[j].militaryexp` and `.fightingspirit` for the **non-player**
side are multiplied by `1 + usedbonusbattles`, after which
`usedbonusbattles = 0f`. In `ai_vs_ai` both sides are non-player, so only the
first consumes it.

### 14.4 `Config/debug.txt`

| Entry | Field | Ships |
| --- | --- | --- |
| Show AI information for battles | `debug_showaibattleinfos` | false |
| AI for players army | `debug_allarmiesai` | false |
| Turn off all AI actions | `debug_noaimovements` | false |
| Turn off enemy AI actions | `debug_noenemyaimovements` | false |
| Use flanking maneuvers if enemy is entrenched/behind rivers | `debug_useflankingmaneuvers` | true |
| Use AI defensive fallbacks if outflanked | `debug_usedefensivefallback` | true |
| Always use dynamic strategy | `debug_alwaysusedynamicstrategy` | false |
| Player stance units to use: × 5 | `playeraistanceactive[]` | see §10 |

`debug_showaibattleinfos` is the single most valuable switch for research: it
instantiates `DebugAIObject` labels at world positions announcing every stance
change (with the strength factor), formation change, objective assignment,
reserve commitment and flank-strength pair, and draws the two
`flankpositionobj` markers per chain. It is the battle equivalent of
`debug_showcampaignaimapvalues`.

---

## 15. Defects and quirks

Ordered by how likely a mod is to hit them.

1. **The entire defensive-facing subsystem is gated off** — two inverted
   `PerformAIActionDLCWL` guards out of 28 call sites, in a method that
   returns `true` unconditionally in the base game. ~230 lines of behaviour
   that has, on this reading, never run in a shipped build (§12.4).
2. **`CheckIfOutflanked` has no callers**, and it holds the only read of
   `debug_usedefensivefallback`, so the `debug.txt` option *"Use AI defensive
   fallbacks if outflanked"* does nothing (§12.4).
3. **Both AI cooldowns are inert** (§12.3) — stance and charge re-checks have
   no hysteresis.
4. **AI artillery always goes to the left wing** — `Random.Range(1, 2)` in
   `AssignReserves` (§6.5).
5. **Reserve commitment is a 50 % coin flip** whenever more than one reserve
   exists, bypassing `strengthtriggerforassigningreserves` (§6.5).
6. **`AssignReserves` returns after one chain's flank assignment**, so in a
   multi-chain battle the later chains' artillery and screening steps are
   skipped on that tick (§6.5).
7. **`CheckEndOfActions` never evaluates the left wing** when both wings are
   populated (§6.5).
8. **Flank "more distant enemy" state leaks between chains** —
   `num`/`num2`/`num3` are not reset per chain in `CalculateFlankData` (§6.3).
9. **Chains can duplicate objectives** — `usecheckformultipleobjectives` is
   never true (§5.3).
10. **A feuding formation never assaults** — `Random.Range(0, 4)` in
    `GetDifferentStance` (§11).
11. **The strength weighting in the charge formula cancels out** (§12.1).
12. **`playeraistanceactive[1]` is parsed and never read** (§10).
13. **`triggerforretreatatnight` has no effect** — `VictoryBarBelowTrigger`
    only compares against the trigger for `typeofevent == 2`, and computes
    and discards it for 0 and 1. The night-retreat decision falls back to
    `forcebalance < GetScreeningTrigger()` (§3.4).
14. **`scenarioid` in `aistrategies.dat` is parsed and never read** (§9.4).
15. **An `EntryPoint` name in `aistrategies.dat` resolves and is then
    dropped**; a misspelled name is silently skipped, and the shipped file
    contains `DixvilleCrossroad` / `DixvilleCrossroads` (§9.3).
16. **`StrategyImport` constructs at the index line and fills at the ordinal**,
    so an out-of-order mod file throws during `Start()` (§9.5).
17. **Two unconditional empty calls per micro frame** —
    `CheckReliefOfObjectve` and `CheckReliefOfObjectveDueToLowMorale` (§2).
18. **Slot 18 of the task wheel is unused**, and slot 3 holds a pair of
    discarded reads (§2.1).
19. **`AdjustGroupFormations`' defend arm is doubly dead** — a ternary with
    identical arms, inside a branch the final guard excludes (§4.3).
20. **Five dead methods**, ~235 lines: `CheckExpandingFrontline_xxx` (100),
    `CheckIfOutflanked` (49), `CheckAIBombardment_xxx` (53),
    `GetSubstituteFromSecondLine` (20), `GroupIsOutflanked` (13, reachable
    only from `CheckIfOutflanked`).
21. **Unguarded `allattachedunits[i]` dereferences** in
    `UnlimberArtilleryAIMicro`, `MicroAICheckForRetreats` and the second and
    third loops of `CheckSkirmishing`, where 27 other sites in the same file
    do guard. `allattachedunits` is rebuilt by
    `Regiment.UpdateAllAttachedUnits` as
    `GetComponentsInChildren<Regiment>(includeInactive: true)`, so it holds no
    C# nulls — but a destroyed subordinate between rebuilds is Unity
    "fake null" and will throw. Latent, not certain.
22. **`CheckEndOfActions` compares a ratio against 1000** (§6.5).
23. `RemoveUnitFromPool` removes `linegroup_rightunits` twice (harmless).
24. `new List<Regiment>();` discarded allocations in `AssignReserves` and
    `CheckForRallying`; `_ = 2801;` in `FilterMap.Paint`; the dead local
    initialisation at the head of `AdjustGroupAIStance`.

### 15.1 What is *not* a bug

- **`FilterMap.Clear()` is a buffer swap**, not a wipe, so the
  paint-then-clear-then-analyze sequence in `Update()` is correct (§8). The
  name is the only problem.
- `GetUnitSymbolData().oobunitsymbolid == 3` (horse artillery) is unhandled in
  `SeparateUnitsToGroups`, but formations can never report 3 (§6.1).

---

## 16. Hook seams

Highest leverage first.

| Seam | Why |
| --- | --- |
| `GameVars.aistrategy` | a `static int`, default `-1`, **never written by the game**. Setting it to 0–3 pins every AI side's macro stance. One field assignment, no patching |
| `UpdateDefensivePositions` / `CheckDefensiveFacing` guards | a two-line transpiler flipping the two inverted `PerformAIActionDLCWL` tests turns ~230 lines of written-but-dormant defensive behaviour back on. The largest behavioural change in the file, and **experimental by definition** — it has apparently never run in a shipped build (§12.4) |
| `AIBattle.CheckGlobalAIStrategy` | postfix on the whole macro decision; read `bunits.sideinformation[sideofai].forcebalance` and overwrite `macroai`/`newmacroaitoset` |
| `AIBattle.GetGroupStrength` | postfix. **Every** micro and reserve decision funnels through this one ratio; it is the single best place to change the AI's aggression without touching any behaviour |
| `AIBattle.AdjustGroupAIStance` | postfix to impose your own stance policy; the method already writes `ai_stance` directly |
| `AIBattle.CalculateNextObjectives` | postfix. Replace greedy-nearest with a weighted choice — the obvious parallel to the campaign AI's value map, and the biggest single improvement available |
| `AIBattle.CheckPerkSelection` | `public static`, random today. A prefix returning `false` plus your own choice is a two-line mod |
| `AIBattle.StrategyImport` | `private static`, called from `Start()`. A prefix can load chains from anywhere, or build them procedurally, with no file at all |
| `AIBattle.CreateObjectiveChain` / `AddObjectiveToPool` | add or rewrite chains at runtime; `AddObjectiveToPool` is `public` |
| `GamePrefs.playeraistanceactive` | five booleans deciding what the player's auto-pilot may do. Wiring index 1 to its config entry is a one-line fix |
| `BattleUnits.ChangeStance` | the choke point where a player formation is enrolled in `aibattle[1]` |
| `AIBattle.CheckCurrentOrderUpdate` | `public static`; the DLC order channel. A prefix could surface AI intent in the base game |
| `Tooltip.RegisterNewTooltip` | `public static`, appends to the live table — mod-supplied tooltip text |
| `FilterMap.Paint` / `AnalyzeClusters` | shared by the AI, the economy and the battlefield maps. Patch with care; see §8 |

A "fix the AI's known bugs" mod is unusually cheap: items 3, 4, 5, 9, 10 and
13 in §15 are each a transpiler on one line or a prefix that recomputes one
value, and none of them changes any data format. Items 1 and 2 are equally
small but much larger in effect, and belong behind their own toggle.

---

## 17. Open questions

1. `RollUpEnemyObjectivesInZone`, `CheckFinalPositions` and
   `UpdateMicroMovementInOffensive` are campaign-layer
   ([20](20-ai-campaign.md) §15); the battle layer's equivalents
   `CheckUseOfReserves`, `CheckFlankMoves` and `LinkReservesToLineGroup` were
   read only for their gating, not line by line. Their geometry is undone.
2. `GetMostSuitableUnit` (~3676) — the reserve-selection scoring. Read for
   signature only.
3. `CheckReserveOrder` / `UpdateReserveOrder` / `CheckRemovalOfOrders` /
   `AssignReserveToOperationalGroup` — the link/offset bookkeeping that keeps
   wings dressed. `unitlinkedto` and `lastaiorder` (code 10 appears in the
   slot-0 guard) are not decoded.
4. `combatbehavior` is still only partly decoded: the AI uses 1 and 2, 3
   triggers a charge, 4 appears in a cavalry guard, 9 shows as "Bombarding".
   `ChangeCombatBehavior` special-cases 7. The full set and
   `Config/unitprefs.txt` array lengths would settle it
   ([16](16-enumerations.md)).
5. `eodcycle` gates `CheckGlobalAIStrategy` and `CheckCurrentOrderUpdate`; its
   state machine is still open from [15](15-attrition-and-condition.md).
6. `VictoryBarBelowTrigger(int)` — the argument selects a trigger; only 0
   (default) and 2 are used by the AI. The others are unexplored.
7. `Objectives.owner` / `GetDistanceOfClosestUnitOfAlliance` and the
   objective-capture timing (`timetotakeobjectives1000` 1.0 h per 1000 men,
   `timetoleaveobjectives` 0.2, `pointratioobjectives` 0.125) belong to a
   victory-conditions write-up that does not exist yet.
8. `PerkRow.PerksShownInfantrySingle` / `…CavalrySingle` / `…ArtillerySingle`
   are `static int[]` populated from prefab fields — the same asset-dump
   dependency flagged in [19](19-commanders-and-perks.md).
9. `BattlefieldSetup.IsPathBlockedByObstacles` and `rivercrossingpositions` —
   how crossings are enumerated, and what `distancetoenemyforinterruption`
   measures.
10. Whether the two-frame `NavMeshObstacle` carve in `BlockedMovingOrder`
    actually completes in two frames on a loaded machine, and what happens to
    the order if it does not. Needs a runtime check.
11. `Config/tooltips_dlcwl.txt` would name the 17 DLC order types; it is not in
    this install.
12. `aiwaitforreinforcements` is a `public static bool` shipped **`false`** and
    **read nowhere** — another dead switch, or a leftover.
