# 16 — Enumerations reference

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Config/unitprefs.txt` (declared names), plus code guards across
`Regiment`, `Autocalc`, `SceneManagement`, `BattleUnits`, `GamePrefs`
**Summary:** referenced by all Phase 2 summaries

A cross-cutting reference. These integer sets appear in dozens of guards and
index dozens of `GamePrefs` arrays; every subsystem so far has had to decode
part of one. Collected here so later work can look them up instead of
re-deriving them.

**The ModApi will need all of these as public enums**, because a mod author
writing `if (unit.Type == 2)` is writing a bug.

---

## `unittyp` — complete

Declared in `Config/unitprefs.txt` under *Unit Data*, which states **19 unit
types** and names each one. This is data-driven — the names can be changed —
but **the indices are hardcoded in guards throughout the assembly**, so they
cannot be reordered.

| # | Name | Notes |
| --- | --- | --- |
| 0 | Infantry | Infantry branch (`historicalbranch` 0) |
| 1 | Cavalry | Cavalry branch (1); mounted; draws forage |
| 2 | Artillery | Artillery branch (2); draws forage; the only type that selects non-zero `firepower[]` |
| 3 | Skirmishers | Infantry branch (0) |
| 4 | Scouts | Cavalry branch (1); draws forage |
| 5 | Courier | Excluded from morale, cohesion, fatigue and supply |
| 6–9 | *Not used* | Declared as "Not used" in the config |
| 10 | Battery | ↓ echelons |
| 11 | Battalion | |
| 12 | Regiment | |
| 13 | Brigade | |
| 14 | Division | |
| 15 | Corps | Checked by `GetPerkLevelParent` |
| 16 | Army | |
| 17 | **Fleet** | Separate harbour-based supply; own UI panel; `fleetref` |
| 18 | Army Group | |

### Why the common guards look the way they do

| Guard | Means |
| --- | --- |
| `unittyp <= 13` | A fighting unit or a low echelon — the *only* things that fight, tire, lose cohesion or hold condition |
| `unittyp > 13` | Division and above, plus Fleet — formations. Early-returns from `UpdateMorale`, `UpdateCondition`, `CalculateCohesion`, `CalculateMeleeCasualties` |
| `unittyp == 5` | Courier — excluded alongside `> 13` from most per-unit simulation |
| `unittyp == 2` | Artillery — the artillery branch of every combat formula |
| `unittyp == 1 \|\| == 4` | Cavalry and Scouts — the cavalry branch |
| `unittyp == 1 \|\| == 2 \|\| == 4` | Horse-using types — forage, and horse condition in the fatigue multiplier |
| `unittyp == 0 \|\| == 3` | Infantry and Skirmishers — the infantry branch |
| `unittyp == 17` | Fleet — diverted to naval paths everywhere |
| `istopsupplyunit && unittyp > 13` | A "Supply Base": a formation HQ holding the pool |

> **The `6–9` gap is real and declared.** Four reserved slots the developers
> left unused. A mod adding a unit type has free indices there, but
> `Config/unitprefs.txt` ships a fixed count of 19 and every array is sized
> from it, so the slots must be filled in place rather than appended.

---

## `historicalbranch` — **two different enumerations**

The field name is shared by two classes with different value sets. Labels in
`GameVars.branchtext[5]`, with prose in `branchtextsub[5]`.

| Class | Values |
| --- | --- |
| `Regiment.historicalbranch` | 0 infantry (`unittyp` 0, 3), 1 cavalry (1, 4), 2 artillery (2) |
| `GameVars.Commander.historicalbranch` | 0 infantry, 1 cavalry, 2 artillery, **3 engineer, 4 navy** |

`historicalbranch == 4` is the navy discriminator throughout: excluded from the
commander-in-chief election and from army rank tables, and capped at the Fleet
rank ceiling.

Branch *matching* drives `increaseofmoralecapforcorrectbranch`,
`increaseofrofwhencorrectbranch` and `increaseofaccuracywhencorrectbranch` —
but the guard differs between the two combat models:

| Path | Guard |
| --- | --- |
| `Regiment` (manual battle, campaign) | `status == 0 && branch matches && commander.westpoint` |
| `Autocalc` | `status == 0 && branch matches` — **no `westpoint` requirement** |

> **Engineer (branch 3) has no mechanical effect anywhere in the base game**,
> despite `branchtextsub[3]` promising siege and obstacle bonuses. 61 of the
> 1,906 shipped commanders carry it. See
> [19](19-commanders-and-perks.md) §11.

---

## `SceneManagement.currentoperation` — complete

Every assignment site accounted for. See
[03-scene-wiring](03-scene-wiring.md) for the detail.

| # | Meaning |
| --- | --- |
| 0 | Idle / main menu |
| 1 | Standalone battle, fresh |
| 2 | Campaign running (steady state) |
| 3 | Campaign battle, fresh |
| 4 | Campaign → battle transition in progress |
| 5 | Returning from a campaign battle |
| 6 | Campaign loaded from save |
| 7 | Standalone battle from save |
| 8 | Campaign battle from save |

**3 and 8 force the global `Config/battleprefs.txt`**, so campaign battles
never honour a battlefield-local override
([05-config-paths-and-overrides](05-config-paths-and-overrides.md)).

---

## `SceneManagement.bo_*` batch operations — complete

16 named ids, dispatched by an `if`-chain with no default
([10-simulation-tick](10-simulation-tick.md) §6).

| # | Name | # | Name |
| --- | --- | --- | --- |
| 0 | `bo_showmainmenu` | 8 | `bo_loadcampaignbattle` |
| 1 | `bo_hidemainmenu` | 9 | `bo_savecampaigndata` |
| 2 | `bo_resetloadingscreen` | 10 | `bo_savehistoricbattle` |
| 3 | `bo_loadhistoricbattle` | 11 | `bo_loadhistoricbattlefromsave` |
| 4 | `bo_rebuildscene` | 12 | `bo_savecampaignbattle` |
| 5 | `bo_closebattlescenario` | 13 | `bo_loadcampaignbattlefromsave` |
| 6 | `bo_loadcampaignscenario` | 14 | `bo_showblackscreen` |
| 7 | `bo_exitcampaignscenario` | 15 | `bo_hideblackscreen` |

---

## `losses[6]` — partial

From consumption sites ([11-combat-resolution](11-combat-resolution.md) §6).

| # | Meaning | Confirmed by |
| --- | --- | --- |
| 0 | **Wounded** (recoverable; counts toward establishment strength) | `groupwounded`, `strength + losses[0] + sick`, `priorwounded` |
| 1 | **Killed** | `inflcasualtiesonstatesupport × losses[1]` |
| 2 | **Deserters / missing** | `nation[state].deserters += losses[2]`; desertion writes here |
| 3 | **Prisoners** | excluded from own-casualty sums; routed melee losses use `overridelosstype = 3` |
| 4, 5 | unknown | no consumption site found |

---

## `usedstance` (auto-resolve) — complete

From the force-balance thresholds in `Autocalc.CalculateStance`
([11](11-combat-resolution.md) §2).

| # | Stance | Trigger |
| --- | --- | --- |
| 0 | Assault / flanking | `forcebalancetriggerassaultflanking` (highest balance) |
| 1 | Attack | `forcebalancetriggerattack` |
| 2 | Defend | `forcebalancetriggerdefend` (lowest) |
| 3 | Retreat | `triggerforretreat`, via `0.5 + enemyPoints − ownPoints` |

All thresholds are divided by `1 + commander.initiative × influenceofcommanderinitiativeon*trigger`.

## `fightingdistance` — complete

A 0–2 **closeness band** derived from the stance matchup, **not** a measured
distance. **2 = close, 0 = distant.** Indexes artillery `firepower[]` and
`randomcasualtiesfactordistance[]`. Matrix in
[11-combat-resolution](11-combat-resolution.md) §2.

## Ammunition index — complete

`firepower[3]`, `ammo[3]`, `standardroundsperunittyp[unittyp, n]`.

| # | Ammunition | Selection |
| --- | --- | --- |
| 0 | Solid shot | Fallback; enables `bouncing`. The **only** index non-artillery uses |
| 1 | Explosive shell | `prob_explosives_*` probability; triggers explosion effects |
| 2 | Canister | Within `canisterrange × firerange`; fires three projectiles at ±15° |

---

## `supplyinstock[4]` — complete

| # | Supply type | Applies to |
| --- | --- | --- |
| 0 | Infantry / small-arms ammunition | unit types 0, 1, 3, 4 |
| 1 | Artillery ammunition | unit type 2 |
| 2 | Provisions | all |
| 3 | Forage | unit types 1, 2, 4 |

Measured in man-days: `supplyinstock[i] / strength` is the fill fraction.
See [14-supply-and-logistics](14-supply-and-logistics.md).

---

## Label tables hardcoded in `GameVars`

A cluster of `public static string[]` arrays in `GameVars` enumerate states and
conditions with their display text. **They are in the assembly, not in config**
— so a mod cannot extend them without patching, and the indices are the
enumeration.

### `unitstatustextcamp[28]` — campaign unit status

| # | | # | | # | |
| --- | --- | --- | --- | --- | --- |
| 0 | Awaiting Orders | 10 | Besieged | 20 | Sailing |
| 1 | Marching | 11 | Siege Assault | 21 | Bombarding Fort |
| 2 | Forced Marching | 12 | **Encamped** | 22 | Engaging |
| 3 | Transported | 13 | Breaking Camp | 23 | Intercepting |
| 4 | Skirmishing | 14 | **Winter Quarters** | 24 | Moving to port |
| 5 | Retreating | 15 | Scouting | 25 | Resupplying |
| 6 | Routed | 16 | Raiding | 26 | Repairing |
| 7 | **Entrenching** | 17 | Guarding | 27 | Raiding *(duplicate)* |
| 8 | **Entrenched** | 18 | In Battle | | |
| 9 | Siege Combat | 19 | Blockading | | |

> **This answers the `encamped` / `entrenched` / `winterquarters` question left
> open by [15-attrition-and-condition](15-attrition-and-condition.md) §6**:
> they are campaign unit *statuses* (12, 8, 14), the same states that select
> the drill rate and the wounded mortality/recovery pair. Status 17 "Guarding"
> is the `cavalryorders == 3` guard duty.
>
> Note index 27 repeats "Raiding" — probably naval raiding versus land raiding
> at 16.

### `unitstatustext[25]` — battle unit status

| # | | # | | # | |
| --- | --- | --- | --- | --- | --- |
| 0 | Idle | 9 | Retreating | 18 | Reattaching |
| 1 | Layn down *(sic)* | 10 | Routed | 19 | Double Quick |
| 2 | Marching | 11 | Constructing | 20 | Screen |
| 3 | Running | 12 | In building | 21 | Defend |
| 4 | Charging | 13 | Forming up | 22 | Attack |
| 5 | Firing | 14 | Bombarding | 23 | Assault |
| 6 | In melee | 15 | Reloading | 24 | Engaged |
| 7 | Advancing | 16 | Rallying | | |
| 8 | Falling back | 17 | Evading | | |

Indices 20–23 (Screen / Defend / Attack / Assault) read as **stances**, which
aligns with the auto-resolve stance set above but is not the same array.

### Ten-band condition descriptors

Each maps a `[0, 1]` stat to text by `(int)(value × 10)`:

| Array | Bands |
| --- | --- |
| `moralecondition[10]` | Broken, Panicked, Unstable, Nervous, Stable, Stable, Confident, Confident, Determined, Eager |
| `cohesioncondition[10]` | Broken, Fragmented, Fragmented, Disrupted, Disrupted, Failing, Good, Good, Intact, Intact |
| `fatiguecondition[10]` | Exhausted, Very Tired, Very Tired, Tired, Tired, Tiring, Winded, OK, Rested, Well Rested |
| `trainingleveltext[10]` | Untrained, Poor, Poor, Regular, Regular, Good, Good, Very Good, Very Good, Excellent |
| `meleecondition[10]` | Worthless, Bad, Bad, Poor, Poor, Good, Good, Strong, Strong, Excellent |

> `cohesioncondition` reads **"Failing" at band 5, between "Disrupted" (4) and
> "Good" (6)** — out of order relative to its neighbours. Likely a copy slip in
> the table; it means a unit at 50–60% cohesion is described worse than one at
> 40–50%.

### Others

| Array | Entries |
| --- | --- |
| `conditiontext[4]` | Prose unit-condition descriptions ("whipped and in dire need of rest & supplies" → "very good for any kind of orders") |
| `conditiontextcamp[6]` | Abysmal, Alarming, Deteriorated, Poor, Sufficient, Good |
| `conditiontextfleets[5]` | Barely Afloat, Alarming, Deteriorated, Good, Intact |
| `intelligencetext[6]` | Blind, Minimal, Poor, Good, Very good, Excellent |
| `readinesstext[4]` | Deficient, Low, Mediocre, Excellent |
| `weaponaccuracytext[5]` | Poor, Mediocre, Good, Very Good, Excellent |

Also `GameVars.standardunittypegarrisons = 11` (Battalion), i.e. the default
`unittyp` for garrison units.

### Commander ranks

`GameVars.commanderranks = 10`, and the two rank label tables are the
enumeration:

| # | `ranktext[10]` | `navyranktext[10]` |
| --- | --- | --- |
| 0 | Volunteer | Master |
| 1 | Lieutenant | Lieutenant |
| 2 | Captain | Commander |
| 3 | Major | Captain |
| 4 | Lieutenant Colonel | Flag Officer |
| 5 | Colonel | *(empty)* |
| 6 | Brigadier General | *(empty)* |
| 7 | Major General | *(empty)* |
| 8 | Lieutenant General | *(empty)* |
| 9 | General | *(empty)* |

Which table is used is decided by `Commander.historicalbranch == 4`. The navy
stops at 4, matching the Fleet row of `Config/ranks.txt`. `branchtext[5]` =
infantry, cavalry, artillery, engineer, navy. Rank windows per `unittyp` are in
[19](19-commanders-and-perks.md) §3.1.

---

## Dual-unit and dual-meaning globals

Not enumerations, but the same class of trap and the same consequence for a
ModApi: a single name whose meaning depends on where you read it.

| Name | Meanings |
| --- | --- |
| `GameVars.currenttimefromstart` | **Hours since battle start** in a battle scene (`uniStormSystem.Hour + battlepasseddays × 24`); **years since 1860** in the campaign (`CampaignController`). `Commander.GetSeniority()` assumes the campaign meaning |
| `BattleUnits.GetUnitVisibilityStateOnCampaignMap` | **Not a fog-of-war check**, despite the name. An echelon/rendering class: 0 = not drawn (a subordinate), 1 = movable formation, 2 = garrison division or Army-above-Corps, 3 = fleet. The campaign AI filters on `!= 0`, which is why it sees everything — see [20](20-ai-campaign.md) §3 |
| `GameVars.Weapon` units | Metric fields for the battle layer, imperial for campaign/naval/UI, nothing keeping them in sync — see [11](11-combat-resolution.md) |
| `cohesion` | Code: 1 = perfect order. Config labels: "cohesion" means *disorder* — see [13](13-morale-and-cohesion.md) |
| `minpricereached` | A **quantity**, not a price — see [18](18-economy.md) |
| `historicalbranch` | Three values on `Regiment`, five on `Commander` (above) |
| `stance` | Two enumerations in opposite orders: `stance`/`macroai` is 0 assault…3 retreat, `ai_stance` is 0 none…4 assault — see the `ai_stance` section below |
| `formation` | Two id spaces, translated per unit class by `BattleUnits.SetFormation`; the AI's label array names the *requested* id — see below |
| `FilterMap.Clear()` | **A double-buffer swap that publishes `tempmap`, not a wipe.** Shared by the campaign zone map, the battle hotspot map, the battlefield support/precipitation textures and ten `Economy` maps — see [21](21-ai-battle.md) §8 |

---

## Political reason codes

Two parallel accounting arrays, both with **slot 0 as a computed total**, both
label tables hardcoded in the assembly. Full tables in
[17-politics-and-support](17-politics-and-support.md).

| Array | Size | Labels |
| --- | --- | --- |
| `Nation.supportinfluence[10, 50]` | 10 alliances × 50 slots, 42 named | `GameVars.supportdescr[42]` — 23 of the live codes are policies |
| `Alliance.nationalmoraleinfluence[50]` | 50 slots, 35 named | `GameVars.Alliance.nationalmoraledescr[35]` — slots 13 and 15–19 are empty |

Known support reason codes referenced elsewhere in these findings: **1** =
battle casualties, **2** = commander appointments (political commanders — see
[19](19-commanders-and-perks.md) §9), **6** = raids, **32** = medical treatment
of wounded, **33** = condition of enemy POW camps, **34** = Project:
Propaganda, **38** = "Commander" (**DLC-only**). National morale **33** = state
support, **34** = private wealth.

> `supportinfluence` is dimensioned `[10, 50]` but the campaign file declares
> **8** alliances — Union, Confederacy, Great Britain, France, Spain, Mexico,
> Sea Trade, Neutral States. The first dimension has two spare slots.

---

## Partially decoded

These have confirmed members but no complete set. Listed so later passes know
what is already known.

| Enumeration | Known values |
| --- | --- |
| `formation` | See the dedicated section below — the **requested** ids are complete, the stored ids are not |
| `movementmode` | **Complete** — see the dedicated section below |
| `combatbehavior` | 0–2 use `GetFireRange(calcusedfirerange: true)`; 7 = normal artillery fire; 8 = canister-hold (switches to 7 inside `canisterrange`), also "counter-battery" for shell probability; 9 = bombardment |
| `currentterrain` | 3, 4, 5, 8 = embarked (`fightingstrengthembarked`). Indexes `terraininfluenceoncohesion[]` and `terrainfatigue[]` |
| `eodcycle` | 0 = normal play; 2 → 3 → 4 observed in the end-of-day chain. Suspends firing, melee, cohesion and fatigue accrual |
| `cavalryorders` | 3 = guard duty (`desertioninfluenceguard`) |
| `commander.status` | **Complete** — 0 active, 2 POW, 3 WIA, 4 KIA, 5 DLC-only; **1 is never assigned anywhere**. `> 0` is the universal "out of duty" test (campaign morale slot 9). See [19](19-commanders-and-perks.md) §5.1 |
| `Regiment.campaignstance` | **Complete** — **0 = offensive, 1 = defensive**. Set by `AICampaign.CheckStanceSetting` via `SetCampaignStance`. Distinct from the battle `stance` (0–3) above |
| `AICampaign.RaidForce.currentstate` | **Complete** — 0 idle, 1 raising, 2 searching for an objective, 3 moving, 4 on objective (burns depots), 5 aborting, 6 returning. See [20](20-ai-campaign.md) §11.1 |
| `AICampaign.SeaInvasionForce.currentstate` | Partial — 1 raising, 2 moving to the embarkation spot, 3 embarked with escort |
| `AICampaign.currentfactionupdatejob` | **0–35, with 17 unused** — the campaign AI's job scheduler; full table in [20](20-ai-campaign.md) §2 |
| `IIP.IIPType` | `IIP.id_harbor` confirmed |
| `CBuilding.BuildingType` | `id_supplydepot = 22` confirmed |
| `weather.GetClimateData(pos, n)` | 100 = temperature; 105 = bad-weather measure (÷100) |
| `AdjustSupport` reason codes | **Complete** — 42 named codes, see above and [17-politics-and-support](17-politics-and-support.md) |

---

## Perk ids — **three namespaces**, resolved

Earlier notes here and in [12-melee](12-melee.md) §6 recorded a set of
apparently irreconcilable collisions, where the same numeric perk id meant
different things in different places. **They were cross-namespace
collisions.** There are three separate perk enumerations, each with its own
tooltip file, and the full catalogue is in
[19-commanders-and-perks](19-commanders-and-perks.md) §1.

| Namespace | File | Count | Holder |
| --- | --- | --- | --- |
| Battle | `Config/PerkBattleTooltips.txt` | 19 | `unittyp <= 13` — fighting units and low echelons, **1 slot** |
| Army campaign | `Config/PerkTooltipsArmyCampaign.txt` | 18 | `unittyp > 13`, not 17 — formations, **4 slots** |
| Fleets | `Config/PerkFleetsTooltips.txt` | 11 | `unittyp == 17` — fleets, **4 slots** |

The slot count is the discriminator, set in one line of
`BattleUnits.create_regiment`:

```csharp
int num = ((component.unittyp <= 13) ? 1 : 4);
```

So the old collision table resolves cleanly: perk 9 is *Cold steel* on a
brigade, *Pontoon train* on a division, and *Battery runner* on a fleet.

Battle perks are further partitioned by branch, **hardcoded in `PerkRow`**:
infantry 0–7, cavalry 8–13, artillery 14–18.

> **`HasPerk(n)` is still not a global registry** — it searches one untyped
> list. But the namespace is derivable from `unittyp` alone, so a ModApi perk
> surface should key on *(namespace, id)* with the namespace computed from the
> unit type, not on *(unit type, id)* as this file previously suggested.

`GameVars.maxperklevels = 3`, and levels are 0-based, so `perklevel` is 0–2
("I", "II", "III").
---

## `movementmode` — complete

`Regiment.SetMovementMode(int newmode = 0)` and
`Regiment.UpdateUnitStatus()` together pin the whole set. The bugle calls in
`SetMovementMode` name four of them outright, and `UpdateUnitStatus` maps each
to an `unitstatustext[]` entry.

| Mode | Meaning | Bugle | `unitstatus` shown |
| --- | --- | --- | --- |
| 0 | normal movement | "charge off" on leaving 6 | 2 Marching / 3 Running / 19 Double Quick |
| 1 | **advance** (on foot) | advance | 7 Advancing |
| 2 | **fall back** (on foot) | fall back | 8 Falling back |
| 3 | advance (mounted) — remap of 1 | — | 2 Marching |
| 4 | **retreat / fall back** (mounted) — remap of 2 | retreat order / order complete | 9 Retreating |
| 5 | rout movement | — | 10 Routed |
| 6 | **charge** (target assigned) | charge / cavalry charge / cavalry attack routed | 4 Charging |

The remap is the trap:

```csharp
if (unittyp <= 13 && mounted > 0) {
    if (newmode == 2) newmode = 4;
    if (newmode == 1) newmode = 3;
}
```

So **a mounted unit ordered to advance ends up in mode 3 and its status reads
"Marching", and one ordered to fall back ends up in mode 4 and reads
"Retreating"** — the label table was never extended to follow the remap. Any
UI or API that reports "what is this unit doing" has to undo this.

Mode 5 is the one that **sets cohesion to 0 outright**
([13-morale-and-cohesion](13-morale-and-cohesion.md)); mode 3 is what the
micro AI sets to *offer* a charge, and `Regiment.UpdateChargeTarget` promotes
it to 6 once the strength ratio clears `microaitriggerforcharge`
([21](21-ai-battle.md) §12.1).

---

## `formation` — two id spaces

There are **two** formation numberings and `BattleUnits.SetFormation`
translates between them, differently per unit class. The battle AI's own debug
label array is

```
{ "Line", "Double Line", "Successive Lines", "Attack Column", "March Column" }
```

which describes the **requested** id, not the stored one. The remap
(`BattleUnits.SetFormation`, ~12258, then `CheckFormationRestrictions`):

| Requested | → stored, `unittyp <= 13` | → stored, `unittyp >= 14` |
| --- | --- | --- |
| 0 | 2 | 3 |
| 1 | 4 | 1 |
| 2 | — | 1 |
| 3 | — | 2 |
| 4 | 3 | 4 |
| 5 | 0 | 0 |
| 6 | 1 | — |

Consequences: for formations, requests **1 and 2 both store 1**, so the AI's
"Double Line" and "Successive Lines" are the same thing; and the stored values
are what index `meleeformationmodifier[]` / `meleeformationmodifiercav[]`,
where 1 = skirmish and **4 = anti-cavalry square** (the only formation that
suppresses cavalry melee shock) were already known from
[12-melee](12-melee.md).

The battle AI requests 0–3 only; it never asks for March Column. Which it
requests per group stance is in [21](21-ai-battle.md) §4.3.

---

## `oobunitsymbolid` — complete

`Regiment.GetUnitSymbolData()` (~19253). The order-of-battle symbol class,
and the key the battle AI sorts its formations by.

| Value | Meaning |
| --- | --- |
| 0 | Infantry |
| 1 | Cavalry |
| 2 | Artillery |
| 3 | Horse artillery (`unittyp == 2 && ishorseartillery`) |
| 4 | Skirmishers / Scouts (`unittyp` 3 or 4) |

For a formation (`unittyp >= 14`) it is derived from `GetMaxUnitType()` — the
dominant subordinate type — so only 0, 1 and 2 ever occur, and
`Regiment.groupunittype` is written alongside it. That is why
`AIBattle.SeparateUnitsToGroups` handles only 1 and 2, and why only
infantry-dominant formations are allowed to assault.

---

## `ai_stance` — the group stance, **distinct from `stance`**

`GameVars.groupstancename = { "none", "screen", "defend", "attack", "assault" }`,
and `unitstatustext[20..23]` carries the same four names for display. This is
the player- and AI-facing formation stance, written by
`BattleUnits.ChangeStance` into `Regiment.ai_stance` / `ai_stanceordered`, with
`ai_feudstance` as the feud controller's shadow copy.

**It is a different enumeration from `stance` / `macroai`, in the opposite
order.** Side by side:

| | 0 | 1 | 2 | 3 | 4 |
| --- | --- | --- | --- | --- | --- |
| `Regiment.stance`, `grp[].stance`, `AIBattle.macroai` | assault | attack | defend | retreat | — |
| `Regiment.ai_stance`, `ai_stanceordered`, `ai_feudstance` | none | screen | defend | attack | assault |
| `GamePrefs.minimummoraleforaistances` index (`ai_stance - 1`) | screen 0.45 | defend 0.40 | attack 0.60 | assault 0.75 | — |

Only `defend` happens to agree, at 2. Everything that passes "a stance" as an
`int` must state which space it is in. See [21](21-ai-battle.md) §4.

Order-queue encoding, from `ChangeStance` and `ChangeCombatBehavior`:
a stance change is queued as `ai_stance + 3` (codes **3–7**) and a combat
behaviour change as `combatbehavior + 30`.


---

## Open questions

- Complete `combatbehavior` and `currentterrain`. Both index `GamePrefs`
  arrays, so **the array lengths in `unitprefs.txt` bound each set** — reading
  the parser's loop counts would give the sizes immediately, which is the
  cheapest way to finish them. `formation` and `movementmode` are now done
  (see above); what is still missing for `formation` is the meaning of each
  **stored** id for `unittyp <= 13`.
- `overridesymbol` (echelon, compared against `supplytoplevelbf`).
- ~~The perk catalogue per unit type~~ — **done**, see
  [19-commanders-and-perks](19-commanders-and-perks.md) §1. What remains is the
  prefab-authored `PerksShownGroup` / `PerksShownFleet` availability lists,
  which are scene data and need an asset dump.
- Which policy maps to which support / national-morale reason code. The label
  tables name the policies, so this is mechanical; it belongs in the planned
  policy catalogue.

### Resolved since first writing

- ~~`AdjustSupport` reason codes — only 32 and 34 are known.~~ **Complete** —
  all 42 are hardcoded in `GameVars.supportdescr`.
- ~~`encamped` / `entrenched` / `winterquarters` derivation.~~ **Resolved** —
  they are campaign unit statuses 12, 8 and 14 in `unitstatustextcamp[28]`.
- ~~The battle-result enumeration that `fametrigger[]`, `defamedtrigger[]` and
  `famefactorleveladjustment[]` are indexed by.~~ **Resolved** from the
  `battleprefs.txt` comments — `fametrigger[]` and `defamedtrigger[]` are
  **[minor victory, major victory, minor defeat, major defeat]**;
  `famefactorleveladjustment[]` is **[brigade, division, corps, army]**. See
  [21-ai-battle](21-ai-battle.md) §14.2.
- ~~`unitstatustext[20..23]` (Screen / Defend / Attack / Assault) look like a
  stance set distinct from the auto-resolve `usedstance` values.~~
  **Confirmed separate** — they are `ai_stance` 1–4, a five-value enumeration
  in the reverse order from `stance`. See the `ai_stance` section above.
- ~~Complete `formation` and `movementmode`.~~ **Resolved** — sections above.
