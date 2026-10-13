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

## `historicalbranch` — complete

From the matching West Point bonus checks in `UpdateMorale`
([13](13-morale-and-cohesion.md) §2):

| # | Branch | Matching `unittyp` |
| --- | --- | --- |
| 0 | Infantry | 0, 3 |
| 1 | Cavalry | 1, 4 |
| 2 | Artillery | 2 |

Used by `increaseofmoralecapforcorrectbranch`,
`increaseofrofwhencorrectbranch` and `increaseofaccuracywhencorrectbranch` —
all three additionally require `commander.westpoint` and
`commander.status == 0`.

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

---

## Political reason codes

Two parallel accounting arrays, both with **slot 0 as a computed total**, both
label tables hardcoded in the assembly. Full tables in
[17-politics-and-support](17-politics-and-support.md).

| Array | Size | Labels |
| --- | --- | --- |
| `Nation.supportinfluence[10, 50]` | 10 alliances × 50 slots, 42 named | `GameVars.supportdescr[42]` — 23 of the live codes are policies |
| `Alliance.nationalmoraleinfluence[50]` | 50 slots, 35 named | `GameVars.Alliance.nationalmoraledescr[35]` — slots 13 and 15–19 are empty |

Known support reason codes referenced elsewhere in these findings: **32** =
medical treatment of wounded, **34** = Project: Propaganda, **6** = raids,
**1** = battle casualties, **38** = commander. National morale **33** = state
support, **34** = private wealth.

---

## Partially decoded

These have confirmed members but no complete set. Listed so later passes know
what is already known.

| Enumeration | Known values |
| --- | --- |
| `formation` | 1 = skirmish; 2 = (used in `AssignChargeTarget` offsets); 4 = **anti-cavalry square** — the only formation that suppresses cavalry melee shock. Indexes `meleeformationmodifier[]` and `meleeformationmodifiercav[]` |
| `movementmode` | 3 = charge order; 5 = **sets cohesion to 0 outright**; 6 = charge target assigned |
| `combatbehavior` | 0–2 use `GetFireRange(calcusedfirerange: true)`; 7 = normal artillery fire; 8 = canister-hold (switches to 7 inside `canisterrange`), also "counter-battery" for shell probability; 9 = bombardment |
| `currentterrain` | 3, 4, 5, 8 = embarked (`fightingstrengthembarked`). Indexes `terraininfluenceoncohesion[]` and `terrainfatigue[]` |
| `eodcycle` | 0 = normal play; 2 → 3 → 4 observed in the end-of-day chain. Suspends firing, melee, cohesion and fatigue accrual |
| `cavalryorders` | 3 = guard duty (`desertioninfluenceguard`) |
| `commander.status` | 0 = active; `>= 2` = lost (campaign morale slot 9). WIA/KIA/POW filters exist in `CommandersList` |
| `IIP.IIPType` | `IIP.id_harbor` confirmed |
| `CBuilding.BuildingType` | `id_supplydepot = 22` confirmed |
| `weather.GetClimateData(pos, n)` | 100 = temperature; 105 = bad-weather measure (÷100) |
| `AdjustSupport` reason codes | **Complete** — 42 named codes, see above and [17-politics-and-support](17-politics-and-support.md) |

---

## Perk ids are **not** a single enumeration

Documented in [12-melee](12-melee.md) §6 and worth repeating here because it is
the one trap in this file that a reader might not expect.

**The same numeric perk id means different things depending on unit type and
context.** Confirmed collisions:

| Perk | Meanings found |
| --- | --- |
| 2 | Zouaves (fatigue factor); sharpshooter bonus in `Autocalc.FightUnit`; canister selection check |
| 4 | Spirited charge (melee); night combat efficiency (naval); skilled cartographers (intelligence, movement speed) |
| 7 | Iron discipline (cohesion recovery); floors the melee resolve; zeroes the friendly-rout morale penalty |
| 8 | Ambulance corps (wounded mortality) |
| 9 | Cold steel (melee, enemy morale malus); pontoon train (supply speed, movement cost); battery runner (naval vs. forts) |
| 10 | Sappers and miners (siege assault); river expedition embarked strength |
| 11 | Stormers (accuracy); land torpedoes (skirmish) |
| 14 | Close support (melee strength — display only; weapon strength; reload speed) |
| 15 | Skilled laying / counter-battery accuracy; night combat efficiency |
| 16 | Grand battery bombardment accuracy |
| 17 | River expedition (siege assault efficiency) |
| 18 | Sapper gunners (reload speed) |

> **`HasPerk(n)` is not a global registry.** Any ModApi perk surface must key
> on *(unit type, perk id)*. A proper perk catalogue — mapping each id to its
> meaning per unit type — is a prerequisite for documenting commanders, and is
> the largest single gap left in Phase 2.

---

## Open questions

- Complete `formation`, `movementmode`, `combatbehavior` and `currentterrain`.
  All four index `GamePrefs` arrays, so **the array lengths in
  `unitprefs.txt` bound each set** — reading the parser's loop counts would
  give the sizes immediately, which is the cheapest way to finish them.
- `overridesymbol` (echelon, compared against `supplytoplevelbf`).
- The perk catalogue per unit type — **the largest gap in this file.**
- Which policy maps to which support / national-morale reason code. The label
  tables name the policies, so this is mechanical; it belongs in the planned
  policy catalogue.
- `unitstatustext[20..23]` (Screen / Defend / Attack / Assault) look like a
  stance set distinct from the auto-resolve `usedstance` values. Worth
  confirming they are separate.

### Resolved since first writing

- ~~`AdjustSupport` reason codes — only 32 and 34 are known.~~ **Complete** —
  all 42 are hardcoded in `GameVars.supportdescr`.
- ~~`encamped` / `entrenched` / `winterquarters` derivation.~~ **Resolved** —
  they are campaign unit statuses 12, 8 and 14 in `unitstatustextcamp[28]`.
