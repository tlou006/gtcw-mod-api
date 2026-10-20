# Summary — the battle AI

**Build:** see [00-build-provenance](00-build-provenance.md)
**Covers:** [21-ai-battle](21-ai-battle.md)

The campaign AI has its own summary, [summary-ai-campaign](summary-ai-campaign.md).
The two layers share the `FilterMap` primitive and nothing else.

---

## The shape of it

`AIBattle` is **three components on the `GameController` prefab**, not one:
`aibattle[0]` drives the enemy, `aibattle[1]` drives **the player's own
stance-bearing formations**, and `aibattle[2]` is the feud controller. What
separates them is `isplayeraiorfeud` (0 / 1 / 2), and the whole macro half of
the class is wrapped in `(isplayeraiorfeud == 0) | ai_vs_ai`.

Each instance runs two lanes with completely different clocks:

- a **macro lane** (`UpdateAITasks`) gated on **0.015 game hours** of elapsed
  game time — plan the line of battle, choose the army's stance, commit
  reserves, order flank moves;
- a **micro lane** (`Update`) gated on **frames** — a 30-slot task wheel where
  each formation holds the wheel for 30 consecutive frames and runs one task
  per frame.

Because `SetGameSpeed` sets `dayLength = battledaylengthinmins / gamespeed`
(120 min for 12 game hours, so **1 game hour = 10 real minutes at 1×**), the
macro lane's game-time cadence is speed-invariant and the micro lane's is not.
**Fast-forwarding a battle starves the unit-level AI and leaves the
army-level AI untouched.** At 20× a formation re-checks charges, fallbacks and
skirmishers about once every twelve game minutes instead of every thirty-six
game seconds. It is the battle-layer counterpart of the campaign's
`sqrt(gamespeed)` law, but with the opposite shape — there the whole AI slows,
here only half of it does.

---

## Four things worth knowing before anything else

**There are two stance enumerations, in opposite orders.**
`macroai` / `Regiment.stance` / `grp[].stance` run `0 assault, 1 attack,
2 defend, 3 retreat`. `Regiment.ai_stance` / `ai_stanceordered` /
`ai_feudstance` run `0 none, 1 screen, 2 defend, 3 attack, 4 assault` —
`GameVars.groupstancename` names them and `unitstatustext[20..23]` is what the
UI shows. Any API that passes "a stance" as an `int` must say which. Worse,
`GamePrefs.minimummoraleforaistances` is indexed `ai_stance - 1`, giving a
*third* ordering (screen, defend, attack, assault) with no "none" slot.

**Giving your own division a stance enrolls it in the AI.**
`BattleUnits.ChangeStance` sets `permanentlydetached` and calls
`aibattle[1].AddUnitToPool`. From then on the player-side AI will re-dress its
line, face it defensively, pick up abandoned guns, choose its perks at random,
deploy and recall skirmishers, unlimber and withdraw its guns, charge with its
infantry, rally its worst unit, lie down under fire and fall back when morale
cracks — and in assault stance launch assault moves at the nearest enemy. It
will **not** march to the sound of the guns, because that one behaviour's flag
ships `false`. The five flags live in `GamePrefs.playeraistanceactive` and are
indexed **by behaviour, not by stance**; index 1 (`CheckDefensiveFacing`) is
parsed from `debug.txt` and never read — moot anyway, since that behaviour is
gated off for everyone (below). This is the direct twin of the campaign AI
running the player's treasury under `automanagefinances`.

**`aistrategies.dat` is a prologue, not a plan.** 176 records of up to 20
objectives each, 42 lines per record (`1 + 176 × 42 = 7,393` verified), keyed
to scene GameObject names under `Battlemap/Map/Objectives`
("SudleySprings", "HenryHill", "McPhersonRidge"). The paired float is an
**absolute battle-clock hour** — First Bull Run's record 0 reads Sudley
Springs @ 0 → Matthews Hill @ 0 → **Henry Hill @ 13:00**. When a scripted
chain runs out of objectives it logs *"switched to dynamic"* and reverts to
the generic engine. And the generic engine is **greedy nearest-objective with
no value weighting at all** — the opposite of the campaign AI's economic value
map, and the single largest improvement available to a mod.

**`FilterMap` is the engine's shared 2-D field type.** `AIBattle` uses one
20×20 instance for its enemy hotspot map, `AICampaign` for its zone map,
`BattlefieldSetup` for support and precipitation, and **`Economy` for ten
maps** — fertility per resource, workforce, slavery, capital, transport
bottlenecks, market capacity, hospitals, trade & supply, supply. That closes
the `UpdateFilterMaps` question in [18-economy](18-economy.md). Three traps:
a cell is the **alpha byte of a `Color32`** so values quantise to 0–255 and
saturate; `Paint` writes into a private `tempmap`; and **`Clear()` is a buffer
swap that publishes `tempmap`, not a wipe**. The name is the only thing wrong
with it.

---

## How the AI decides what to do

One number drives the army: `forcebalance = ownWeightedForce / enemyWeighted
× (1 + Δmorale) × (1 + Δexperience)`. Every threshold it is compared against is
divided by `1 + initiative × 0.7`, so **initiative is the dominant personality
dial in battle** exactly as it is in the campaign. A 1.0-initiative general
defends below 0.47 instead of 0.80 and assaults above 1.76 instead of 3.0.

`GetBattleType` splits the battle on objective-point share against
`objectiveratiodefend` (**0.65**): we hold most → defend, the enemy does →
attack, neither → contested, with readiness as the tiebreak.

Below that, every micro and reserve decision funnels through one method,
`GetGroupStrength` — a strength ratio divided by `1 + flanksThreatened/10` and
multiplied by morale and experience ratios at weight 0.25, using the
lerp-from-one idiom already catalogued in
[11-combat-resolution](11-combat-resolution.md). It returns **0** when the
formation is not in contact, which is why so many triggers read `> 0f &&`.
**It is the best single hook in the file:** one postfix there moves the AI's
aggression everywhere at once without touching a behaviour.

The order of battle is a **line group** (centre plus wings ordered outward), a
**screening group** (cavalry on the flanks), an **artillery group** and a
**reserve pool**, all hanging off an `ObjectiveChain`. Sorting is by
`oobunitsymbolid`, the formation's dominant subordinate type.

---

## Two unlisted AI advantages

The campaign layer's ten named cheats ([20](20-ai-campaign.md) §9.2) have no
battle-layer equivalent list — but two advantages are hardcoded into call
sites rather than exposed as switches:

**AI long moves skip the order delay.** Any AI objective move longer than 150
units goes through `BlockedMovingOrder`, which issues
`SetWaypoint(…, useorderdelay: false, …)`. `AssaultMove` and the retreat
fallback do the same. Short moves (≤ 150) pay the delay like the player.

**The AI temporarily carves the navmesh to route around danger.**
`UpdateBlockedCrossings` maintains a list of river crossings covered by an
enemy unit facing them within 1.5 × fire range, plus a kill-zone point in
front of every stationary entrenched enemy. `BlockedMovingOrder` spawns
carving `NavMeshObstacle` boxes at each of those, waits **two frames** for the
bake, issues the move, and destroys them. Anything that reads pathing during
those two frames sees a different map. This runs on all three instances every
frame, before the pause check.

---

## Defects a mod will notice

**A whole subsystem is gated off backwards.** `PerformAIActionDLCWL` is the
per-unit "may the AI touch this?" test, and in the base game it returns `true`
unconditionally. Twenty-six of its twenty-eight call sites use it as a positive
requirement. The two that do not are `UpdateDefensivePositions`
(`… && !PerformAIActionDLCWL(…)`) and `CheckDefensiveFacing`
(`if (PerformAIActionDLCWL(…) || …) return;`). So task-wheel slot 22 and
everything under it — the angle-sampling defensive re-facing, the gap-filling
`DefensivePosition` pass, ~230 lines with eight dedicated prefs — **never
executes outside the DLC career scenario**. AI formations in screen or defend
stance do not re-face toward a shifting enemy and do not close gaps in their
line. Separately, **`CheckIfOutflanked` has no callers at all**, and since it
holds the only read of `debug_usedefensivefallback`, the `debug.txt` option
*"Use AI defensive fallbacks if outflanked"* — shipped `true` — does nothing.

Flipping those two guards is a two-line transpiler and by far the largest
behavioural change available in the file. It should also be the most carefully
tested, because on this reading the code has never run in a shipped build.

**Both AI cooldowns are inert.** `lastaistancechangetime` and
`lastaichargetime` are stamped with `currenttimefromstart` and then tested as
`last < currenttimefromstart + pref`, which is always true for a positive
pref. The offset belongs on the left. So a formation's group stance can flip
on consecutive macro ticks when `GetGroupStrength` straddles a threshold, and
a unit whose charge was just cancelled for weakness can be re-ordered to
charge on its next pass. The config comments describe behaviour that does not
happen (`updatetimeforresettingofaistance` 1 h, `timetorenewaichargecheck`
0.5 h).

**AI artillery is always assigned to the left wing.** `int num4 =
Random.Range(1, 2);` — the `int` overload is max-exclusive, so it always
returns 1. A near relative of the 39 `Random.Range(0, list.Count - 1)` sites
in the campaign layer, and the same root cause: Unity's two `Random.Range`
overloads differ on whether `max` is inclusive.

**Reserve commitment is a coin flip.** The flank-reinforcement test is
`weakFlank || (reserves > 1 && Random.Range(0f, 1f) < 0.5f)`, so with two or
more reserves on hand `strengthtriggerforassigningreserves` is bypassed half
the time. `AssignReserves` then `return`s, so in a multi-chain battle only one
chain gets a reserve per macro tick and the later chains' artillery and
screening steps are skipped that tick.

**`CheckEndOfActions` never evaluates the left wing** when both wings are
populated — the right-wing assignment overwrites the left unconditionally.
And in `CalculateFlankData` the "which flank has the more distant enemy"
accumulators are declared outside the per-chain loop and never reset, so a
multi-chain battle contaminates each chain's answer with the previous ones'.

**Objective chains can duplicate objectives.** The duplicate guard is behind a
parameter that is never passed `true`; because C# `|` does not short-circuit,
the check still *runs* and its answer is then discarded.

**A feuding formation never assaults.** `GetDifferentStance` rerolls
`Random.Range(0, 4)`, which cannot produce 4, and the trailing
`if (num > 4) num = 4;` clamp is unreachable.

**The strength weighting in the charge formula cancels out.** `(1 + w[0])`
multiplies both sides of the ratio, so the shipped
`weightingfactorsformicroaicharge[0] = 1` has no effect; only the experience,
morale and fatigue weightings matter.

**The AI picks perks uniformly at random.** `CheckPerkSelection` draws from
`PerkRow.PerksShown*Single` the moment a unit has no active perk, with no
evaluation whatsoever. It is `public static`, so this is a two-line mod.

**`aistrategies.dat` has three mod hazards.** `StrategyImport` constructs the
record at the *index line* and fills it at the *loop ordinal*, so a file whose
records are out of order writes into a `null` slot and throws inside `Start()`
— before any AI initialisation, so the battle scene simply fails to load. Any
editing tool must renumber. An `EntryPoint` name resolves successfully and is
then silently dropped, because only `Objectives` are chained. And a misspelled
name is silently skipped — the shipped file already contains both
`DixvilleCrossroad` and `DixvilleCrossroads`, so at most one of those resolves.
The second line of every record, `scenarioid`, is parsed and **read nowhere**;
chain selection is by record index, via each formation's `groupai` field in the
battle data file.

Also dead: ~235 lines in five uncalled methods
(`CheckExpandingFrontline_xxx`, `CheckIfOutflanked`, `CheckAIBombardment_xxx`,
`GetSubstituteFromSecondLine`, `GroupIsOutflanked`); two **empty** methods called unconditionally
every micro frame (`CheckReliefOfObjectve`,
`CheckReliefOfObjectveDueToLowMorale`); task-wheel slot 18; a ternary with
identical arms inside a branch its own guard excludes; `aiwaitforreinforcements`,
a `public static bool` read nowhere; and `playeraistanceactive[1]`.

---

## Two questions closed elsewhere in the repo

**`fametrigger[]` / `defamedtrigger[]`** — `battleprefs.txt` names the index
set that [19-commanders-and-perks](19-commanders-and-perks.md) left open:
**[minor victory, major victory, minor defeat, major defeat]**, with
`fametrigger = {0.025, 0.02, 0.03, 0.04}` and
`defamedtrigger = {−0.0018, −0.0027, −0.0016, −0.0015}`, alongside
`famefactorleveladjustment` = {brigade 0.005, division 0.0065, corps 0.009,
army 0.013}. Reading them: it is *easier* to become famous in a minor victory
than a major one, and *harder* to escape being defamed in a major victory than
in a major defeat — the thresholds scale to the number of candidates, not the
glory. The matching victory thresholds are vp ratio ≥ 0.7 / ≥ 0.8 / ≤ 0.3 /
≤ 0.2.

**`movementmode`, `formation` and `oobunitsymbolid`** are now fully resolved
and recorded in [16-enumerations](16-enumerations.md). The notable one:
mounted units have `SetMovementMode` silently remap advance 1 → 3 and fall
back 2 → 4, and the status label table does not follow, so **a mounted unit
advancing reads "Marching" and one falling back reads "Retreating"**.

---

## Where to hook

`GameVars.aistrategy` is a `static int`, defaults to `-1`, and is **never
written anywhere in the assembly** — assigning it 0–3 pins every AI side's
macro stance with no patching at all. Beyond that: `GetGroupStrength` for
aggression, `CalculateNextObjectives` to give objective choice any value
function, `CheckGlobalAIStrategy` to replace the macro ladder,
`StrategyImport` (a prefix can build chains procedurally with no file),
`CheckPerkSelection`, and `GamePrefs.playeraistanceactive` to decide what your
own auto-pilot may do.

A "fix the battle AI's known bugs" mod is unusually cheap: the inert
cooldowns, the left-wing artillery, the coin-flip reserves, the duplicate
objectives and the feud stance are each one line, and none of them touches a
data format. The two inverted defensive guards are just as small and far
larger in effect, so they belong behind their own toggle.

`Tooltip.RegisterNewTooltip` is `public static` and appends to the live
tooltip table, which is a useful find for the Phase 3 question of describing
mod-supplied policies and projects.

And one piece of free leverage, mirroring the campaign layer:
`debug_showaibattleinfos` in `Config/debug.txt` makes the AI narrate itself in
world space — every stance change with its strength factor, every formation
change, objective assignment, reserve commitment and flank-strength pair, plus
two flank markers per chain. For research it is worth more than reading the
source.
