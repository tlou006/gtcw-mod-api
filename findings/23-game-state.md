# Game state: `GameVars.alliance[]` and `GameVars.nation[]`

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `decompiled/Assembly-CSharp/GameVars.cs`,
`ImportExportUnitData.cs`, `BattleUnits.cs`, `Economy.cs`, `Policies.cs`;
`game/Campaigns/001/A/nations.dat`, `game/Config/campaignprefs.txt`
**Summary:** [summary-game-state](summary-game-state.md)

This is the write-up of the campaign's *persistent, mutable* state: the two
arrays that almost every other finding in this folder ends up pointing at.
`findings/17` describes what state support *means*; this file describes where
it lives, who is allowed to write it, when it is recomputed, and what a hook
must not touch.

---

## 1. Where the state lives

`GameVars` is a `MonoBehaviour` on the `GameController` object, but every field
on it is **`static`**. There are 341 static fields at the top level plus
sixteen nested types. The consequence that matters:

> **Static state is not cleared by a scene change.** `GameController` is
> rebuilt on every transition (findings/03), but the statics it declares
> survive. That is why `BattleController.Start` and `CampaignController.Start`
> each assign `GameVars.currenttimefromstart = 0f` by hand.

So a hook that caches `GameVars.alliance` in a field will keep a reference to
the *previous* campaign's array after a return to the main menu and a reload.
Always re-read `GameVars.alliance` / `GameVars.nation` through the static.

### 1.1 The nested types

| Nested type | Lines | Documented in |
| --- | --- | --- |
| `KeyCodes` | 13–28 | — (input binding) |
| `MinMaxRanks` | 29–122 | findings/19 |
| `Commander` | 123–2577 | findings/19 |
| `ResourceUsage` | 2578–2722 | findings/18 |
| `ShipType` | 2723–3000 | — |
| `BuildingType` | 3001–3137 | findings/18 |
| `Technology` | 3138–3170 | — |
| **`Alliance`** | **3171–4463** | **this file** |
| **`Nation`** | **4464–4906** | **this file** |
| `WeaponGroup` | 4907–4971 | findings/11 |
| `Weapon` | 4972–5374 | findings/11 |
| `WeaponClass` | 5375–5379 | findings/11 |
| `AnimCycle` | 5380–5396 | — |
| `GroupFormationParam` | 5397–5430 | findings/16 |
| `FormationParam` | 5431–5443 | findings/16 |
| `WeatherType` | 5444–5453 | findings/20 |

`Alliance` is the larger of the two by a factor of three: 183 fields and 41
methods (plus a constructor and five more on its three nested helper types)
against `Nation`'s 38 fields and 19 methods.

---

## 2. The two arrays

```csharp
public static Alliance[] alliance;   // GameVars.cs ~6172
public static Nation[]   nation;     // GameVars.cs ~6174
```

Both are created — not merely populated — by **`GameVars.import_nations(string
foldername)`** (~7204), which reads one file, `<foldername>/nations.dat`. The
array lengths therefore come from the data, not from code:

| Shipped scenario set | `nation.Length` | `alliance.Length` |
| --- | --- | --- |
| `Campaigns/001/*` | 53 | 8 |
| most `Battlefields/*` | 52 | 8 |
| `Battlefields/005/A` | 54 | 8 |

**Never hardcode 53.** The alliance count is 8 in every shipped file, which is
why `GameVars.allianceid_*` (0 Union, 1 CSA, 2 GB, 3 France, 4 Spain, 5
non-war) can be treated as stable, but the state count is per-scenario. Code
that walks states must use `GameVars.nation.Length`; the game itself does this
consistently and also guards `stateOfField < GameVars.nation.Length` wherever a
map lookup feeds an index.

`Nation` is a US state and `Alliance` is a country — see findings/17 §1 for
why that naming is the single most confusing thing in the codebase. The full
53-entry state table is in findings/22 §11.1.

### 2.1 `import_nations` call sites

| Caller | Folder |
| --- | --- |
| `MainMenu` ~770 | the selected scenario, on highlight in the menu |
| `CampaignController` ~68 | the campaign being started or loaded |
| `BattleController` ~73 | the battlefield, or `Battlefields/DebugArmySetup` |
| `BattleUnits` ~1379/1388/1398 | battlefield / campaign / explicit folder |
| `Frontline2` ~909 | `FolderWhereNationsFileIsStored` |

Loading a saved campaign is **two stages**: `import_nations` on the scenario
folder builds the arrays from the authored file, then
`ImportExportUnitData.OverwriteNationsData` (`CampaignController` ~227,
`BattleUnits` ~1405) overwrites the mutable subset from the save folder. This
split has a direct modding consequence, in §3.3.

---

## 3. `nations.dat`: one filename, two formats

### 3.1 The authored format (read by `GameVars.import_nations`)

Count-header plus one field per line, the house style from findings/06.

**State record — 19 lines:**

| # | Field | Note |
| --- | --- | --- |
| 1 | `name` | matched by string elsewhere; see §3.4 |
| 2 | `alliance` | owning country |
| 3–4 | `support[0]`, `support[1]` | **divided by 100 on read** |
| 5 | `abbrev` | |
| 6–8 | `census[0..2]` | population at 1850 / 1860 / 1870 |
| 9 | `recruitable` | |
| 10–11 | `mobilizationrate[0..1]` | **divided by 100 on read** |
| 12 | `contractlength` | months |
| 13 | `slaveryallowed` | |
| 14–19 | `statesetname[0..5]` | per-state-set rename; usually blank |

**Alliance record — 42 lines:**

| # | Field | Note |
| --- | --- | --- |
| 1 | `name` | |
| 2 | `symbolcolor` | `"RRR-GGG-BBB"`, parsed at **fixed offsets** `(0,3) (4,3) (8,3)` |
| 3 | `militaryexperience` | raw fraction here; **×100 in the save format** |
| 4–6 | `transportcapacity[0..2]` | also copied to `transportcapacitytarget` |
| 7 | allied-to list | `"NN;NN;"`, 3 chars per entry, `Substring(m*3, 2)` |
| 8 | at-war list | same encoding |
| 9–10 | `name2`, `name3` | "Federal", "Yankee" |
| 11 | `shipdesignation` | "USS" |
| 12–13 | `standardcoatcolorindex`, `standardtrouserscolorindex` | |
| 14 | `uniformcolorvariation` | **one value used for all three channels** |
| 15–17 | `standardweapons[0..2]` | |
| 18 | `treasury` | |
| 19 | trade-agreement list | same 3-char encoding |
| 20 | `prewarname` | |
| 21–25 | `taxcaps[0..4]` | **÷100**; `tax[]` is set to `1f` here, not read |
| 26 | `debt` | **×1,000,000** |
| 27–34 | `subsidies[0..7]` | followed immediately by `Policies.UpdateSubsidyEffect(l)` |
| 35 | `nationalmorale` | ÷100 |
| 36–37 | `battleswon`, `casualtiessuffered` | |
| 38 | `commanderinchief` | commander id |
| 39 | researched policies | `"NNN;"`, 4 chars per entry, `Substring(n*4, 3)` → `Policy.ResearchPolicy` |
| 40 | intervention levels | 4 chars per entry, ÷100, → `AdjustInterventionLevel(index, …)` |
| 41 | `volunteersadj` | |
| 42 | `contractlength` | version-gated: `(version >= 0.7403) \| !folder.Contains("Save")` |

Verified against `game/Campaigns/001/A/nations.dat`:
`1 + 53 × 19 + 1 + 8 × 42 = 1345` lines, and the alliance count line ("8")
is at line 1009 exactly as predicted.

The `aipersonalities.dat` block that `import_nations` reads immediately
afterwards is documented in findings/20.

### 3.2 Shipped alliance values

| id | name | prewarname | treasury | debt | `taxcaps` | `subsidies` | c-in-c |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | Union | Northern States | 2,000,000 | 64 M | .30 .05 0 0 .20 | .6 .2 .3 .1 .1 .15 **0 .1** | 980 |
| 1 | Confederacy | Southern States | 2,000,000 | 10 M | .30 .05 0 0 .20 | .4 .4 .1 .5 .1 .15 **.2 .05** | 28 |
| 2 | Great Britain | Great Britain | 35,000,000 | 0 | all 0 | all 0 | −1 |
| 3 | France | France | 27,000,000 | 0 | all 0 | all 0 | −1 |
| 4 | Spain | Spain | 15,000,000 | 0 | all 0 | all 0 | −1 |
| 5 | Mexico | Mexico | 10,000,000 | 0 | all 0 | all 0 | −1 |
| 6 | Sea Trade | Trade | 100,000,000 | 0 | all 0 | all 0 | −1 |
| 7 | Neutral States | Neutral States | 100,000,000 | 0 | all 0 | all 0 | −1 |

Three things fall out of that table.

1. **`atwarwith` is empty for everyone at scenario start.** The war is
   declared by Militia Act I (findings/22 §5), not by the data file.
2. **Both belligerents start with a trade agreement with each other** — the
   Union lists 01…07, the Confederacy 00 and 02…07.
3. **Both ship nonzero funding in subsidy slots whose category is "N/A".**
   Slot 6 and slot 7 have `basicsubsidiescost = 0` and no `GetSubsidyFactor`
   consumer, so they cost nothing and do nothing — but they *do* count toward
   `GetAdminCost`'s per-subsidy-type surcharge (§7.3). Corroborates
   findings/22's abandoned-ladder story from the other side of the data.

Foreign powers' zero `taxcaps` mean they collect no tax at all (§7.2) and
their huge flat treasuries are the whole model of their finances.

### 3.3 The save format (written by `SaveNationsData`, read by `OverwriteNationsData`)

Same filename, completely different layout: **self-describing**, every array
preceded by its own length, no labels, no names. It omits `name`, `abbrev`,
`census`, `statesetname`, `averageposition`, and on the alliance side `name`,
`name2`, `name3`, `prewarname`, `symbolcolor`, `shipdesignation`, the two
uniform colour indices, `uniformcolorvariation` and `standardweapons`.

Verified against the shipped `game/Campaigns/001/A/Save/nations.dat`
(a developer leftover — there is also a `Save27_12_2024_14_55_29`):
state records are **1099 lines each**, and
`1 + 53 × 1099 = 58248` puts the alliance count line ("8") at 58249. ✔

Of those 1099 lines, **1000 are `supportinfluence` / `supportinfluencelast`**
(a `float[10,50]` pair, interleaved). Three array lengths are data-driven and
confirm other findings: `recruitedunitspertype.Length = 19`
(`GamePrefs.unittypes`, findings/16), `productionfactor.Length = 38`
(`resources.dat`, findings/18), and `weaponstock.Length = 87` (`weapons.txt`).

> **The modding rule that follows from the two-stage load:** a field that
> appears *only* in the authored format can be edited in the scenario's
> `nations.dat` and the change will take effect on an **existing save**,
> because nothing in the save overwrites it. A field that appears in the save
> format cannot — the save wins. Names, abbreviations, census figures, colours
> and standard weapons are in the first group; everything mechanical is in the
> second.

Two asymmetries between the two formats:

- `militaryexperience` is a **raw fraction** in the authored file (Union
  `.05`) and a **percentage** in the save (`33.04` → ÷100 on read). Same
  field, same filename, two units.
- `OverwriteNationsData` writes into `GameVars.nation[i]` for
  `i < num` where `num` is the save's own count, with no check against
  `GameVars.nation.Length`. A save with more states than its scenario throws
  `IndexOutOfRangeException` inside `Start()`.

### 3.4 A legacy 40-line variant still ships

Three shipped `nations.dat` files use an older alliance record of **40 lines**,
missing the last two fields (`volunteersadj`, `contractlength`):

- `game/Battlefields/014/B/nations.dat`
- `game/Battlefields/019/B/nations.dat`
- `game/Battlefields/DebugArmySetup/nations.dat`

`import_nations` has a version gate for `contractlength` but **none for
`volunteersadj`**, and the gate it does have (`!folder.Contains("Save")`) is
true for every battlefield folder — so both fields are read unconditionally.
Loading one of these files consumes two lines past the end of each record;
the first overrun hits the *next* alliance's name, and
`float.Parse("Confederacy")` throws `FormatException` inside `Start()`.

The two battlefield sublevels are unreachable: scenario discovery requires a
`ScenarioDescr.txt` (`MainMenu` ~1048), 014/B has none and 019/B has only a
`ScenarioDe_backup.txt`. `Battlefields/DebugArmySetup` is reachable — it is
what `GamePrefs.debug_loaddebugarmysetup` selects (`BattleController` ~61,
`BattleUnits` ~1373) — so **that debug switch crashes on load in this build**.

The same files also name alliance 7 *"Non War States"* rather than *"Neutral
States"*, matching the vestigial `GameVars.allianceid_nonwar = 5`.

---

## 4. `Nation` field map

38 fields. "Authored" = present in the scenario `nations.dat`; "Save" = present
in the save format; "Derived" = recomputed from other state.

### 4.1 Identity and authored data

| Field | Authored | Save | Notes |
| --- | --- | --- | --- |
| `name` | ✔ | — | **mutated at runtime** by `statesetname`, see below |
| `abbrev` | ✔ | — | |
| `census[3]` | ✔ | — | 1850 / 1860 / 1870, against the private `datearray` |
| `statesetname[6]` | ✔ | — | |
| `averageposition` | — | — | set by the campaign map; read by `AICampaign` for zone geometry |
| `alliance` | ✔ | ✔ | |
| `recruitable` | ✔ | ✔ | |
| `slaveryallowed` | ✔ | ✔ | `SetSlaveryAllowed` |
| `contractlength` | ✔ | ✔ | months |
| `mobilizationrate[2]` | ✔ | ✔ | draft ceiling per alliance |
| `changedalliance` | — | ✔ | set when Militia Act I reassigns the state |
| `townsinstate` | — | — | rebuilt by `Nation.CreateTownList()` |

> **The rename trap.** `BattlefieldSetup` ~1237 does
> `GameVars.nation[i].name = GameVars.nation[i].statesetname[num];` whenever
> the active state-set changes. Everything that matches a state **by name**
> then silently stops matching: `Nation.SearchNationByName`, policy 30's
> volunteer bonus (§5.2), and policy 30's crop list in `Policies`
> (findings/22 §10). `statesetname` is blank in all shipped records, so this
> is latent rather than live — but a scenario that fills it will break those
> name matches.

### 4.2 Support and influence

| Field | Save | Written by | Notes |
| --- | --- | --- | --- |
| `support[2]` | ✔ | `AdjustSupport` only | per alliance, clamped [0,1] — findings/17 |
| `supportinfluence[10,50]` | ✔ | `AdjustSupport` | slot `[a,0]` is a recomputed total; 42 reason codes |
| `supportinfluencelast[10,50]` | ✔ | `AdjustSupport`, then decayed | **the NaN defect, findings/17** |
| `supportthroughappointedcommanders[2]` | ✔ | political-commander pass, findings/19 | reason code 2 |
| `supportthroughappointedcommanderstemp[2]` | — | same | accumulator |
| `printingworks[2]`, `printingworks_temp[2]` | ✔ | economy | `GetInfluenceOfPrintingWorksOnDraftSupport` |
| `militaryschools[2]`, `militaryschools_temp[2]` | ✔ | economy | `GetInfluenceOfExperienceLossThroughMilitarySchools` |

The decay is in `UpdateAllianceData`:
`supportinfluencelast[a, j] /= Mathf.Pow(1f + num2, 30f)` for every state,
once per alliance pass. It is the only consumer of that array besides the
display path, which is the one that never produces a string.

### 4.3 Population and the recruiting pools

| Field | Save | Notes |
| --- | --- | --- |
| `population` | ✔ | interpolated from `census`, then drifted by `eogrowthrates` |
| `lastpopulationupdate` | ✔ | floating campaign date of the last `UpdatePopulation` |
| `eogrowthrates` | ✔ | **derived, not authored** — see §5.1 |
| `averageavailableworkforce` | ✔ | mean `availableworkforcelevel` over `townsinstate` |
| `volunteers[2]`, `drafts[2]` | ✔ | **remaining pool** = ceiling − already recruited |
| `volunteersrecruited[2]`, `draftsrecruited[2]` | ✔ | cumulative, only `AdjustRecruitingPool` writes them |
| `recruitedunitspertype[19]` | ✔ | by `unittyp` |

### 4.4 Statistics and occupation

| Field | Save | Notes |
| --- | --- | --- |
| `menfielded` | ✔ | **player's units only** — see §5.3 |
| `casualties`, `deserters` | ✔ | cumulative, written by `Autocalc` ~3967 and `BattleUnits` ~5688 from `losses[2]` |
| `alliancetroops[2]` | ✔ | echelon- *and* visibility-filtered presence flag, §6.2 |
| `occupiedby` | ✔ | **a latch**, see §5.4 |
| `lastborderalliance` | — | change-detector only; triggers a border mesh recolour in `BattlefieldSetup` ~1252 |
| `productionfactor[38]` | ✔ | per-goods multiplier, `SetProductionFactor` |

---

## 5. The `Nation` derived pipeline

Everything here runs from `GameVars.UpdateNationsData()` and
`GameVars.UpdateWorkforceFluctuation()`, both called from the campaign
heartbeat in `BattleUnits` ~1540 (§6.1).

### 5.1 Population

```
ResetStartPopulation(year)  →  population = Interpolate(datearray, year, census, useexp: true)
UpdatePopulation()          →  population += censusDelta(now, last) + population * eogrowthrates * dt
eogrowthrates               =  (1 / max(0.1, sqrt(averageavailableworkforce)) − 1.2) * 0.04
```

`datearray` is the private `{1850, 1860, 1870}` and the interpolation is
exponential. The default `ResetStartPopulation()` argument is **1850**, so a
freshly constructed `Nation` starts at the 1850 census; `GameVars.ResetPopulationValues()`
re-seeds every state at the actual start date, and it runs **at `GameVars.frame == 13`
and only when `SceneManagement.currentoperation == 0`** (`CampaignController`
~294) — i.e. for a new campaign, not a loaded one, which is correct because a
loaded save restores `population` directly.

`eogrowthrates` has an equilibrium at `averageavailableworkforce = 1/1.2² ≈
0.694`: above that the state loses population, below it gains. The static
`UpdateWorkforceFluctuation()` sums `averageavailableworkforce` across all
states and then **only tests the sum against zero** — the aggregate is
computed and discarded, so despite the name there is no redistribution
between states.

### 5.2 Recruiting pools — the volunteer pool is a power law, not linear

`Nation.UpdateRecruitData()` recomputes both pools for both alliances.

**Drafts** are a hard gate, as findings/17 has it:

```
ceiling = population * recruitratedraftsfullmobilization(0.04) * mobilizationrate[i]
        * alliance[i].draftspolicyadjrunning * dlcMultiplier
drafts[i] = (support[i] >= statesupportrecruiting(0.3)
             && (GetAmountOfTownsOccupied(i) > 0 || townsinstate.Count == 0))
            ? floor(ceiling) − draftsrecruited[i]
            : 0
```

**Volunteers** are not linear. findings/17 said they scale linearly with
support; that is only the inner term.

```
mods = recruitmentprojectfactor * volunteerspolicyadj * volunteerspolicyadjrunning
     * volunteersadj * (1 + GetProjectLevel(113) * project_farmmechanizationrecruitment)
raw  = population * recruitratevolunteersfullsupport(0.016) * effSupport * mods * dlcMultiplier
pool = 3 * raw^0.9                         // adjvolpoolshighnumberslin = 3, …exp = 0.1
volunteers[i] = floor(pool) − volunteersrecruited[i]
```

The config calls it a *"reduction of volunteers pools for higher recruiting
numbers"*, and it is — but only above the crossover. `3 × raw^0.9 = raw` at
`raw = 3¹⁰ = 59,049`, so **below a raw pool of ~59 k the correction multiplies
the pool up, and only above it does it bite.**

`effSupport` is `support[i]`, zeroed when below `statesupportrecruiting`, with
two overrides:

- `if (Policy.CurrentChapter == 0 && alliance != i) effSupport = 0;` —
  **before the war a state recruits only for its own side.**
- Policy 33 (USCT): `×(1 + usctmobilizationrateadj)`, and if any town in the
  state is Union-owned **and** `slaveryallowed`, the support threshold is
  bypassed entirely.
- Policy 30 (Northern Routes), Union only: `×northernroutesvolunteersmodifier`
  for `name` in `{Illinois, Ohio, Indiana, Iowa, Kansas}` — **matched by
  string**, so §4.1's rename trap applies. This is the five-state list
  findings/22 §10 pairs against the seven-state crop list in `Policies`.

Both pools can go negative internally; `GetRecruitAmounts` clamps with
`Mathf.Max(0, …)`. Two shortcuts for foreign powers:
`GetRecruitAmounts` returns a flat **3000** when `alliance >= volunteers.Length`,
and `HasEnoughRecruits` returns `true` unconditionally for `alliance > 1` —
**intervention forces have unlimited recruits.**

### 5.3 `menfielded` is the player's number, not the state's

`UpdateUnitStatistics(stateid)` zeroes `menfielded` and then sums `strength`
over units with `playeralliance == componentsInChildren[i].alliance`,
`unittyp <= 13`, `state == stateid`, under an active root. So the recruiting
list's "men fielded" column is *the player's* men raised in that state. Its
only caller is `RecruitingList` ~114, and it does a full
`GameObject.Find("Battlemap/Units").GetComponentsInChildren<Regiment>(true)`
**per state** — 53 scene walks each time the panel refreshes.

### 5.4 `occupiedby` is a latch that never clears

```csharp
int num = -1;
for (…) if (townsinstate[k].Owner != alliance && num == -1) num = townsinstate[k].Owner;
if (num < 0) continue;
bool flag = true;
for (…) if (townsinstate[l].Owner != num) flag = false;
if (flag) occupiedby = num;
```

Full occupation sets `occupiedby`; nothing ever sets it back to −1. When the
owner retakes towns, `num` goes back to −1 and the loop `continue`s, leaving
the stale value. Consumers:

- `Regiment.IsOnEnemySoil(usestateoccupation: true)` (~27160) returns
  **false** for a state whose `occupiedby == alliance` — so the
  `fightingspirit` penalty (findings/13) and `supplyenemysoilfactor`
  (findings/22 §7) stop applying there.
- Project 93 Occupation Administration and project 94 Suppress Population
  (`Projects` ~433/~444) keep firing their support effects (reason codes 36
  and 37) on states the alliance no longer holds.

So **once you have fully occupied a state, you keep the occupation bonuses in
it permanently, even after losing every town back.** A state with zero towns
is never marked occupied at all.

---

## 6. `Alliance`: the master campaign tick

### 6.1 Two clocks

The campaign heartbeat is in `BattleUnits` ~1532, gated on
`GamePrefs.updatetimeforcampaigndata = 0.00002` campaign years (**≈ 10.5 game
minutes**). Each beat runs, in this order:

```
UpdateAllianceData()            // one alliance, round-robin
GameVars.UpdateNationsData()    // all states
Economy.UpdateMacroEconomy()
Economy.UpdateEconomyEstimates(dt)
GameVars.UpdateWorkforceFluctuation()
UpdateCommanderInChief()
bfs.UpdatePaperMap1Legend()
Policy.CheckForChapterUpdate()
```

`UpdateAllianceData(bool forceupdate = false)` processes the single alliance
`alliancerunthrough` and increments it only on a pass that actually ran. Its
own gate is per-alliance:
`currenttimefromstart < lastalliancedataupdate + GamePrefs.updaterhythmalliancedata / 365f`,
with `updaterhythmalliancedata = 0.50` **days**.

The two gates compose into a burst: once a pass completes, the next seven
heartbeats clear all seven remaining alliances, then the whole thing idles
until 12 game hours after the first one. So the operational statement is:

> **Every alliance's derived data, policy effects, policy research and
> economic AI refresh once per 12 game hours**, and the elapsed term `num2`
> handed to every rate integration in the pass is ≈ `0.5/365` campaign years.

That pins findings/22's "replayed every tick" to a number. When the round
wraps, `alliancerunthrough` resets to 0 and the frontline object lists are
rebuilt.

### 6.2 What the pass does

Reset-then-accumulate over `alliance[a].unitlist`:

| Field | Scope | Note |
| --- | --- | --- |
| `averageexperience` | `unittyp <= 13` | `Regiment.Experience()` |
| `averagecommanderleadership`, `averagecommanderfame` | `unittyp <= 13` | via `GameVars.commander[unit.commander]` |
| `averagearmymorale` | `unittyp <= 13` | followed by a **discarded `float.IsNaN(...)` call** |
| `troopsfielded` / `troopsinrecruitment` | split on `transfertime <= 0` | |
| `totaldisabled` | `losses[0] + sick` | |
| `recruitscontractexpiries` | `GetContractExpiries()` | |
| `weaponunitsequipped[87]` | **reallocated every pass** | men for non-artillery, `statsguns` for `unittyp == 2` |
| `artilleryunits` / `otherunits` | `unittyp == 2` or not | |
| `averageweaponstrengthguns` / `…rifles` | same split | `AICampaign.GetWeaponStrengthFactor` |
| `armyupkeeppa`, `recruitmentcostpa` | `unittyp > 13 && != 17` | plus the weapon-cost trailing value |
| `navyupkeeppa`, `totaltonnage` | `unittyp == 17` | plus pool vessels, alliances 0–1 only |

`nation[k].alliancetroops[a]` is cleared for **every** state at the start of
alliance `a`'s pass and then set only for formations with `unittyp > 13 &&
unittyp != 17` whose `GetUnitVisibilityStateOnCampaignMap(...) > 0` — so it is
an echelon-filtered *and* fog-filtered presence flag, not "any unit present".
`CampaignObjective` ~783/~798 reads it for Louisville (state 14) and St. Louis
(state 22).

Then, still inside the pass:

```
Town.CheckAllianceCapital(a)
UpdateSubsidyRecruitment(dt)
Policies.UpdateSubsidyEffect(a)
Policies.UpdatePolicyEffects(a, dt)        ← the thirteen-field reset + replay, findings/22 §6
  … subsidy funding accrual, §7.3
constructioncostspa.Update / weaponcostspa.Update
WeaponList.CheckWeaponOrders(a)
  … loyalstates count
Policies.CheckAIPolicyChange(a)            ← findings/22 §13
AICampaign.UpdateProjects(a)
Policies.UpdatePolicyResearch(a, dt)
AICampaign.UpdateFinancialAI(a)
AICampaign.UpdateInterventions(a)
AICampaign.UpdateRailroadConstruction(a, dt)
AICampaign.UpdateTransportCapacity(a)
Weapon.UpdateStandardization(a, w) for all 87 weapons
AICampaign.CheckPurchaseWeapons(a)
  … tradewarfarelast / nationalmoralelast trailing averages
  … national morale ↔ state support coupling, §8
AICampaign.CheckWeaponUpgrades(a)
AICampaign.UpdateCompanyFoundations(a, dt)
militaryexperience += GetMilitaryExpImprovementThroughMilitarySchools() * dt   // capped at 1
  … the whole DLC_WL block, if dlc_scenarioactive && dlc_active
```

`loyalstates` counts states where
`support[a] / max(1e-5, sum(support)) > GamePrefs.statesupportownstate (0.65)`.

`powratio = totalPrisoners / max(1, capacity)`, and **`capacity` is assigned,
not accumulated**, inside the camp loop:
`num8 = GamePrefs.powcampcapacity[powcamps[n].level];`. With several camps the
denominator is only the last one's capacity. When there are no camps at all
`powratio` is forced to `10f`.

---

## 7. Money

### 7.1 Treasury

`AdjustTreasury(float adjustmentby)` is the only intended writer, and it holds
an **AI handicap that belongs with findings/20's list**:

```csharp
if (adjustmentby < 0f && this != alliance[playeralliance])
    adjustmentby += (-adjustmentby) * (usedcampaignbonusrunning * GamePrefs.influenceaibonusonfinances);
```

`influenceaibonusonfinances = 0.3` (*"the influences of AI bonus on all
spending of AI"*), so at the maximum bonus of 0.5 **every non-player alliance's
expenses are 15 % cheaper**. It applies to Britain, France and the rest too,
not just the enemy.

There is **no floor** in `AdjustTreasury` — only a `DebugOwnLog` when a single
adjustment exceeds ±100 M. The one floor that exists is in the save loader and
is dead: `OverwriteNationsData` reads `treasury`, clamps it at −100,000,000
(~1216), and then **reads the same value again** at ~1237 without the clamp,
because `SaveNationsData` writes `treasury` twice (lines 804 and 831).
`fleetcounter` and `subsidiesfactor` are likewise written and read twice.

### 7.2 Taxation — the caps are entirely policy-driven

`tax[5]` is the slider (0–1, **initialised to `1f` by `import_nations`**) and
`taxcaps[5]` is the ceiling it scales. Five types, `GameVars.tax_*`:
0 tariffs, 1 excise, 2 income, 3 corporate, 4 land sales.

```
revenue_pa(i) = base(i) * tax[i] * taxcaps[i] * GamePrefs.taxrevenuemodifier[i]
```

| i | base | `taxrevenuemodifier` | shipped cap | raised to … by |
| --- | --- | --- | --- | --- |
| 0 tariffs | `volumeimports` | 8 | 0.30 | **0.50** by Tariff Act (24/124); **0.15** by Free Trade (141) |
| 1 excise | `tradevolume` | 5 | 0.05 | — |
| 2 income | `privateincome × totalpopulation × baseprivateincome(250)` | 1 | **0** | 0.05 / 0.15 / 0.15 by Revenue Act I–III (26–28 / 126–128) |
| 3 corporate | `corporateprofits` | 2.5 | **0** | 0.00 / 0.00 / **0.10** — only Revenue Act III |
| 4 land sales | `companyfoundationsandupgrades × absolutevaluelandsales(2.5 M)` | 0.7 | 0.20 | **0.50** by Feed Europe I–II (31/32) |

So **income tax and corporate tax are unusable until the Revenue Acts**, and
corporate tax until Revenue Act III specifically.

Two notes:

- **The aggregate branch omits one modifier.** `GetCurrentTaxValuePa(-1)` sums
  all five terms but the corporate term is written
  `corporateprofits * tax * taxcaps` with **no
  `taxrevenuemodifier[tax_corporate]`**, while `GetCurrentTaxValuePa(3)`
  includes it. The summed balance therefore understates corporate revenue by
  2.5× once the cap is nonzero. Harmless in the shipped data, a trap for a mod
  that raises the cap earlier.
- **Tariff Act and Free Trade are deliberately commutative.** Case 124 checks
  `GetStatusByPolicyID(141)` and multiplies by `freetradetariffs`; case 141
  checks 124 and multiplies by `tariffactsubsidies`. Either order yields
  `0.5 × 0.15 = 0.075`. That is the right pattern to copy for any hook that
  writes a shared field under the replay contract — but note the *product* is
  worse than either policy alone, since Free Trade's 0.15 is below the base
  0.30 to begin with.
- The Revenue Act rungs are absolute assignments, not increments, and all
  three are replayed every pass. The final cap is whichever rung sits last in
  `activatedpolicies`, which in normal play is the highest. A mod that adds a
  fourth rung must keep that ordering property.

### 7.3 The subsidy trio

Three arrays, easy to confuse:

| Array | Length | Meaning | Written by |
| --- | --- | --- | --- |
| `subsidies[8]` | 8 | the player's slider position per category | `Finances` ~251/277, `AICampaign` ~6009–6020, both loaders |
| `subsidiesfactor[8]` | 8 | the ceiling unlocked by the policy ladders | `AdjustSubsidyFactor` only |
| `subsidyfunding[8]` | 8 | money actually accrued into each category | `AdjustSubsidyFunding`, `subsidies.dat` |

```csharp
public float GetSubsidyFactor(int id) => subsidies[id] * subsidiesfactor[id];

public float GetSubsidyCost(int id) =>
    -(Mathf.Min(1f, subsidies[id]) * GamePrefs.basicsubsidiescost[id] * subsidiesfactor[id]);
```

> **The effect is uncapped; the cost is capped at 1.** `AdjustSubsidyFactor`
> clamps `subsidies[id]` at **10**, while `GetSubsidyCost` clamps the cost
> contribution at `min(1, subsidies[id])`. Any mod that drives a slider above
> 1.0 gets up to 10× the effect for 1× the price.

`AdjustSubsidyFactor(id, tovalue, skipadjustmentoldvalue = true)` optionally
rescales `subsidies[id]` by `old/new` to preserve the product — but only
`if (!skipadjustmentoldvalue && id >= 1)`, and the default skips it entirely.
**Slot 0, politics, is excluded even when compensation is requested**, which is
what makes raising the Government Funding cap immediately raise the policy slot
count rather than merely the headroom:

```csharp
// Policies.cs ~375
maxpolicies = GamePrefs.minimumpolicies(7)
            + Mathf.RoundToInt((GamePrefs.maximumpolicies(12) - 7) * GetSubsidyFactor(subsidy_politics));
```

The config labels `maximumpolicies` *"maximum usable policies (when politics
subsidies are set to full)"* = 12, but `GetSubsidyFactor` can reach 2 at the
top of the ladder, giving **17** — there is no clamp.

Funding accrues once per pass, with a second AI handicap:

```csharp
float num13 = (a != playeralliance) ? 1f + usedcampaignbonusrunning * GamePrefs.influenceaibonusonsubsidies : 1f;
for (i …) AdjustSubsidyFunding(i, dt * (-GetSubsidyCost(i)) * num13);
```

`influenceaibonusonsubsidies = 1`, so at bonus 0.5 **non-player alliances
accrue subsidy funding 50 % faster**. `AdjustSubsidyFunding` clamps at 0 and
never at a ceiling.

`basicsubsidiescost = {10 M, 50 M, 50 M, 50 M, 50 M, 50 M, 0, 0}` — the last
two are the "n/a" categories, which is why the nonzero shipped values in slots
6 and 7 (§3.2) cost nothing. They are not free, though:

```csharp
// GetAdminCost
foreach tax[i]      > 0 → num++
foreach subsidies[j] > 0 → num2++
cost = totalpopulation * Interpolate([0,1], averagesupport, [basicadmincostspercapitasupport0, …100])
     * (1 + num * increaseadmincostspertaxtype + num2 * increaseadmincostspersubsidytype)
```

so **both belligerents pay an admin surcharge at game start for dead subsidy
categories** — the Union for one, the Confederacy for two.

### 7.4 Two different `GetBalance()`

`Alliance.GetBalance()` is the real figure:

```
GetCurrentTaxValuePa() + GetAdminCost() + GetSubsidyCost()
  + interestcostpa + armyupkeeppa + navyupkeeppa + recruitmentcostpa + supplydepotpurchasespa
```

Do not confuse it with **`CampaignStrategy.GetBalance(bool)`** from
findings/17 §6, which reads ten `Slider.value` fields under `UI/Strategy` and
reports the *war* balance, not money. Two unrelated methods, same name.

`IsRatingOkForRecruitment / …Construction / …WeaponPurchases` all test
`currentrating / ratingnotches.Length > 1 − minimumratingfor…`; remember from
findings/18 that **a lower `currentrating` index is a better rating**.

---

## 8. National morale ↔ state support are coupled both ways

Inside each alliance pass, for `a <= 1`:

| Direction | Rate pref | Value | Code |
| --- | --- | --- | --- |
| private income → national morale | `convergencenatmoraletowardsprivatewealth` | 10.0 | `AdjustNationalMorale(34, Δincome × dt × rate)`, suppressed on the first pass |
| `averagesupport` → national morale | `convergencenatmoraletowardsstatesupport` | 0.5 | `AdjustNationalMorale(33, (averagesupport − nationalmorale) × dt × rate)` |
| national morale → each own state's support | `convergencestatesupporttowardsnatmorale` | 0.25 | `AdjustSupport(a, (nationalmorale − support[a]) × dt × rate, 3)` |
| towns lost → support | `statesupportlosttowns` | −1.00 p.a. | weighted by the fraction of towns in foreign hands, reason **4** |

That identifies the **writers** of support reason codes **3** ("National
morale") and **4** ("State occupation") and of national morale codes **33**
("State Support") and **34** ("Private Wealth") — the labels were already in
findings/17's tables, but not the sites. Note code 4's writer is the
*fraction of towns in foreign hands*, not full occupation, so the label is
looser than the mechanic; `Nation.occupiedby` (§5.4) is the unrelated full-
occupation flag. findings/22 §8.1 has the policy block, support 9–31.

`averagesupport` itself is **population-weighted across towns**, computed in
`Economy.cs` ~2068 with the guard `Owner <= 1 && state >= 0` — so only towns
held by a belligerent contribute. It is the same `averagesupport` that drives
the policy research rate (findings/22 §4) and `GetAdminCost`.

`AdjustNationalMorale(int type, float value)` mirrors `Nation.AdjustSupport`
exactly: add, clamp to [0,1], **recompute the delta after clamping**,
accumulate into `nationalmoraleinfluence[type]`, then rebuild slot 0 as the
sum of 1…49. It does **not** write `nationalmoralelast`, so national morale has
no counterpart to findings/17's `supportinfluencelast` NaN defect —
`nationalmoralelast` is purely the trailing average from the alliance pass.

`AddTradeWarfare(int type, float value)` is the same shape for
`tradewarfare[10]` — slot 0 recomputed as the total of 1…9, no clamping — and
it opens with two orphaned statements, `Mathf.Abs(value);` and `_ = 1000000f;`,
where a magnitude guard used to be. That closes findings/18's open question
about it: there is no guard and no clamp.

---

## 9. Three idioms for recomputed state

Anyone hooking this layer needs to recognise which of these a field belongs
to, because **two of the three make a direct write futile.**

### 9.1 Double-buffered sweep: `<field>` ← `<field>temp`

A cursor walks one entity per frame, accumulating into the `temp` field; when
the cursor wraps to 0 the published field is overwritten and `temp` zeroed.
`Economy.cs` ~2092:

```csharp
privateincome        = privateincometemp        / max(1, totalpopulationtemp);
averageworkforcelevel = averageworkforceleveltemp / max(1, totalpopulationtemp);
averagesupport       = averagesupporttemp       / max(1, totalpopulationtemp);
totalpopulation      = totalpopulationtemp;
… all four temps = 0
```

Pairs on `Alliance`: `privateincome`, `totalpopulation`,
`averageworkforcelevel`, `averagesupport`, `corporateprofits`,
`corporaterevenues`, `volumeimports`, `tradevolume`,
`companyfoundationsandupgrades`, `supplydepotpurchasespa`,
`transportcapacityused`; and on `Nation`,
`supportthroughappointedcommanders`, `printingworks`, `militaryschools`.

> Writing the published field is undone at the next wrap; writing the `temp`
> field is zeroed at the next wrap. To change one of these you must change the
> **source** (a `Town`, a `CBuilding`, `Nation.support`) or postfix the publish
> step itself.

Note `privateincome` is a **per-capita** figure despite the name, and
`averageworkforcelevel` carries a `× unemploymentmultiplier (0.3)` that the
config itself labels *"for stats purposes"*.

### 9.2 Reset-then-accumulate within one pass

`UpdateAllianceData` zeroes ~18 fields at the top and refills them from
`unitlist` before returning (§6.2). A write between passes survives at most 12
game hours. Same story for `Policies.UpdatePolicyEffects`'s thirteen fields
(findings/22 §6) and for `Nation.UpdateRecruitData`'s pools.

### 9.3 Trailing average: `Tools.AddToAverage(current, sample, 0.25, dt)`

`attrition`, `returnedtoduty`, `recruitsreinforced`, `averagereenlistmentratio`,
`battlecasualtiesavg`, `volunteersgrowth`, `draftsgrowth`,
`avgcorporaterevenues`, `weaponindustrylast`, `tradewarfarelast[]`,
`nationalmoralelast[]`, `transportcapacitypeak[]`. These *do* accept a write —
it simply decays.

Three problems in this group:

- **`battlecasualtiesavg` averages a cumulative counter.** Its sample is
  `casualtiessuffered`, which only ever grows, so the "average" is a lagging
  copy of the running total rather than a rate.
- **`weaponindustrylast`'s warm-up gate is `GameVars.frame < 1000`**, and the
  frame counter rewinds (findings/10), so a scene transition can put it back
  into warm-up and snap the trailing value to the instantaneous one.
- **`transportcapacitypeak` is updated three times per pass.** The loop is
  `for (num10 < transportcapacity.Length)` wrapping `for (num11 < 3)`, with
  `num10` unused in the body. The nominal window is
  `calculationdurationaveragetransportutilization = 0.025` campaign years
  (~9.1 days); the effective window is a third of that.

---

## 10. The top-level statics

341 of them. Rough shape: 65 `debug_*` switches (findings/20 §14, findings/21
§15), a few dozen player display/options toggles that belong to the options file
rather than the simulation, 55 `string[]` label tables (findings/16), a block of
`int` name constants (`subsidy_*`, `tax_*`, `allianceid_*`, `key_*`,
`DrawingOrder_*`), the seven `automanage*` flags plus `autoplacebuildings` that findings/20
shows the campaign AI shares with the player, and the actual world state:

```csharp
public static Alliance[] alliance;          public static Nation[] nation;
public static List<Commander> commander;    public static Weapon[] weapon;        // 87
public static List<ShipType> shiptypes;     public static List<BuildingType> buildingtypes;
public static List<Technology> technologies; public static List<WeaponGroup> weapongroup;
public static int playeralliance = 0;       public static int year, startyear, currentmonth;
public static float currenttimefromstart;   public static float currenthourofday;
public static float gamespeed = 1f;         public static int frame;
public static List<int> historicalnewspaperstriggered;
public static List<Policy> usachosenpoliciescampaign, csachosenpoliciescampaign;
public static float usedcampaignagressiveness, usedcampaignbonus, usedcampaignbonusrunning,
                    usedbonusbattles, casualtiesmodifier;
public static bool ai_vs_ai, tutorialactive, interventions, usefow, gamepaused, …
```

### 10.1 `currenttimefromstart` has two meanings, now pinned

findings/19 flagged this; the two writers are:

| Scene | Expression | Unit |
| --- | --- | --- |
| battle | `uniStormSystem.Hour + battlepasseddays * 24f` (`BattleUnits` ~8186, ~14842) | **hours** since midnight of battle day 0 |
| campaign | `max(max(BU.year, uniStorm.yearCounter) − 1860, 0) + (month−1)/12 + (day−1)/365.25 + Hour/8766 − debugmonthadjustment` (`CampaignController` ~490) | **years** since 1860 |

So in the campaign it is `GetCampaignFloatingDate() − 1860 −
debugmonthadjustment`, and `GameVars.currenttimefromstart = 0f` is asserted at
the top of both controllers. Any hook that compares it against a pref must know
which scene it is in; the prefs follow suit (`updaterhythmalliancedata` is in
days and divided by 365, the battle AI's cooldowns are in hours — findings/21
§13).

### 10.2 Two dead authored tables

`GameVars.NavyRankChangeToBDatePerAlliance` = `float[10]` with slot 0 =
`1862.6f` and slots 1–9 = `9999f`; `…ToCDatePerAlliance` the same with slot 0 =
`1864.1f`.

The two real US Navy rank reforms, authored for alliance 0 and sentinelled off
for everyone else — and **neither array has a single consumer anywhere in the
assembly.** Compare findings/19's `ranks.txt` rows 0–9, also dead. The intent
is visible: Confederate naval ranks would never have changed.

`GetAllianceFromColor(Color)` compares `symbolcolor.r/g/b` with `==` on floats
parsed from three-digit decimal substrings, so it works only for colours that
came through `import_nations` unmodified.

---

## 11. Mod seams

Ranked by how safe they are.

1. **`Nation.AdjustSupport` / `Alliance.AdjustNationalMorale` /
   `Alliance.AddTradeWarfare`.** Single chokepoints, already written in the
   clamp-then-recompute-delta style, every caller funnels through them. The
   `supportinfluencelast` fix from findings/17 is a postfix here.
2. **`Alliance.AdjustTreasury`.** The only intended treasury writer; a prefix
   can neutralise the AI finance handicap with one line.
3. **`Alliance.GetSubsidyFactor` / `GetSubsidyCost` / `GetCurrentTaxValuePa` /
   `GetAdminCost` / `GetBalance`.** Pure functions of state — safe to postfix,
   and the natural place to implement findings/22's ten dead pref arrays
   without touching the policy switch.
4. **`BattleUnits.UpdateAllianceData`.** A postfix here runs once per alliance
   per 12 game hours with `num2` already computed as the right `dt`. This is
   the correct place to write anything the pass itself resets (§9.2) and the
   only place a derived aggregate can be overridden durably. It is an instance
   method on `BattleUnits`, so the patch target is the instance.
5. **`Nation.UpdateRecruitData` / `UpdatePopulation`.** Postfix to adjust pools
   or growth; both are recomputed from scratch each call, so a prefix that
   mutates inputs works and a write to the outputs persists until the next
   call (≈ every heartbeat).
6. **`GameVars.import_nations`.** A postfix sees the freshly built arrays
   before any save overwrite — the right hook for adding states or alliances,
   subject to §2's length warning and to `Alliance`'s constructor dependencies
   (below).
7. **Avoid**: direct writes to any field in §9.1 or §9.2, and to
   `nation[].name` (the state-set pass may rewrite it).

### 11.1 Construction order hazards

`new Alliance()` reads three things that must already exist:

```csharp
weaponstock          = new float[weapon.Length];       // GameVars.weapon — 87
weaponorders         = new WeaponOrder[weapon.Length];
standardizationyear[k] = weapon[k].standardizationyear;
interventionpointsprojects = new float[alliance.Length];  // the static array, mid-construction
interventionpointspolicy   = new float[maxpoliciesingame]; // 200
bestcommanderidperlevel    = new int[commanderranks];      // 10
```

`import_nations` assigns `alliance = new Alliance[num2]` *before* the loop, so
`alliance.Length` is already correct when each element's constructor runs — but
constructing an `Alliance` anywhere else, before `GameVars.weapon` is loaded or
outside that window, throws. And `interventionpointspolicy` fixes the hard
ceiling on policy ids at **200** (`maxpoliciesingame`); the highest shipped id
is 179.

---

## 12. Defects and oddities

1. **`occupiedby` never clears** (§5.4) — permanent occupation bonuses and a
   permanent enemy-soil exemption in any state you once fully held.
2. **`transportcapacitypeak` updated 3× per pass** (§9.3) — a triple-nested
   loop with an unused outer index; the trailing window is a third of its
   configured length.
3. **`powratio`'s denominator uses only the last POW camp's capacity** (§6.2) —
   assignment where accumulation was meant.
4. **The save loader's −100 M treasury floor is dead** (§7.1) — the value is
   written twice and the second, unclamped read wins.
5. **`GetCurrentTaxValuePa(-1)` omits `taxrevenuemodifier[tax_corporate]`**
   (§7.2) — the summed balance and the per-type breakdown disagree 2.5× once
   corporate tax is unlocked.
6. **Subsidy effect clamps at 10, subsidy cost clamps at 1** (§7.3).
7. **`maxpolicies` can reach 17** against a config comment that says 12
   (§7.3).
8. **Both belligerents ship funding in dead subsidy slots** (§3.2) and pay
   `GetAdminCost`'s per-category surcharge for them (§7.3).
9. **`battlecasualtiesavg` averages a cumulative counter** (§9.3).
10. **`weaponindustrylast`'s warm-up gate uses the rewinding frame counter**
    (§9.3).
11. **`float.IsNaN(averagearmymorale);` is a discarded call** in
    `UpdateAllianceData` — the same shape as findings/17's always-false
    `== float.NaN` test, here not even assigned.
12. **`AddTradeWarfare` opens with two orphaned statements** (§8).
13. **`UpdateWorkforceFluctuation`'s cross-state sum is discarded** (§5.1) —
    computed, tested against zero, never used; the "fluctuation" never
    redistributes.
14. **`_ = BattleUnits.towns[j].CityName == "Leavenworth";`** — a discarded
    comparison inside `Nation.CreateTownList`, the same leftover shape as
    findings/18's `_ = gameObject.name == "Port_Cleveland";`.
15. **`NavyRankChangeTo{B,C}DatePerAlliance` have no consumers** (§10.2).
16. **Three shipped `nations.dat` files are in the legacy 40-line format**
    (§3.4), and one of them is what `debug_loaddebugarmysetup` loads — so that
    debug switch throws `FormatException` on load.
17. **`GetActivatedPolicies(researchedonly: true)` counts policies *in*
    research**, i.e. `researchprogress < 1f`. The name is inverted, and in that
    branch `includeactsinresearch` and `includegrandpolicies` are ignored
    although four call sites pass them. Behaviour is correct everywhere it is
    used (including findings/22's "the AI holds one policy at a time"); only
    the signature lies.
18. **`OverwriteNationsData` trusts the save's state count** (§3.3).
19. **`import_nations` reads `militaryexperience` as a fraction while the save
    format uses percent** (§3.3).

---

## 13. Open questions

- `Alliance.aodemand[38]` / `aodemandtmp[38]`: the pass smooths them as
  `(tmp/dt/5 + old*4)/5` per goods, but who writes `aodemandtmp`, and what
  consumes `aodemand`? Likely the army-order demand feeding findings/18's
  markets.
- `bestiipplaces[]` / `bestiipplacesprob[]`: saved by name + world position +
  probability; produced by which AI pass?
- `Alliance.orgrunning`, `impressment`, `lettersmarque`, `legalblockade`,
  `bountieslevel`, `maxslaveryfactor` — all policy-set booleans/levels with
  consumers not yet traced. `maxslaveryfactor` was already open in findings/22.
- `Alliance.ratingmodifier` is an `int` accumulated from
  `feedeuropecreditratingunion[...]` floats; confirm the truncation (also open
  in findings/22).
- `Nation.averageposition` — written where? `AICampaign` reads it for every
  zone/state distance calculation, so its freshness matters for AI quality.
- `Nation.lastborderalliance` only recolours a border mesh; is the border
  geometry per state-set (`Set<N>` folders) authored or generated?
- `GameVars.currentmonth` vs `uniStormSystem.monthCounter` — two month
  sources; which is authoritative for `harvestingmodifier[12]` (findings/18)?
- The `Save/` and `Save27_12_2024_14_55_29/` folders shipped under
  `Campaigns/001/A/` are developer leftovers; is `Saving.latestfolder`
  ever pointed at them, and do they load cleanly on this build?
- `Alliance.shipnametypelist` round-trips `nameidused`/`runthrough` but not the
  name lists themselves — those come from the authored side. Confirm that a
  renamed ship pool survives a save.
