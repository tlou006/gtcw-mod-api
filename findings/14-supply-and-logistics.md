# 14 — Supply and logistics

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment.CalculateSupplyLine`,
`Regiment.UpdateIIPSupplyLink`, `CBuilding.GetSupplyEfficiency`,
`BattleUnits`, `Economy`, `LogisticsPanel`, `GamePrefs`; values quoted from
the shipped `Config/unitprefs.txt`
**Summary:** [summary-logistics](summary-logistics.md)

Third Phase 2 subsystem. Like combat, supply turns out to be **two separate
models** — a battlefield one and a campaign one — sharing the same four-element
stock array.

---

## 1. The four supply types

`supplyinstock`, `supplychange`, `supplyfromdepots`, `supplylocal` and
`groupsupplystate` are all `float[4]`. Decoded from the supply-icon tooltip,
which labels each one and names its divisor:

| Index | Supply type | Divided by (for the fill ratio) |
| --- | --- | --- |
| 0 | **Infantry / small-arms ammunition** | strength of unit types 0, 1, 3, 4 |
| 1 | **Artillery ammunition** | strength of unit type 2 |
| 2 | **Provisions** (men) | total group strength |
| 3 | **Forage** (horses) | shown only for unit types 1, 2, 4 |

Stock is initialised to `strength` in every slot, so the units are
**man-days**: `supplyinstock[i] / strength` is the fill fraction the UI shows
as a percentage.

> **Forage applies only to horse-using types** — Cavalry (1), Artillery (2) and
> Scouts (4). See [16-enumerations](16-enumerations.md) for the full
> `unittyp` table, which this subsystem finally pinned down.

`SetAmmoVariable(int id, float adjustby = -1f)` is the bridge to combat:

```
ammo[id] = clamp(supplyinstock[id] / strength, 0, 1)
```

so the `ammo[]` array that [11-combat-resolution](11-combat-resolution.md)
drains is a **normalised view of stock**. For `id == 1` (artillery) all three
ammunition types are adjusted together, since artillery stock is one pool
split across solid shot / shell / canister.

---

## 2. Battlefield supply: a multiplicative chain to an entry point

`Regiment.CalculateSupplyLine()`. All its prefs carry a `bf` suffix.

```
distanceFactor(d) = 1 − (1 − 1 / max(1, d ^ supplydistancemodificatorbf))
                        × supplydistanceweightingbf
```

**Subordinate units** (echelon below `GamePrefs.supplytoplevelbf`) link to
their parent:

```
parent = BattleUnits.GrabNextParentUnit(this)
if any enemy unit intersects the line parent → self:
    supplylineinterrupted = true;   supplystate stays 0
else
    supplystate = distanceFactor(dist to parent) × parent.supplystate
```

**Top supply units** (echelon == `supplytoplevelbf`, or orphaned) link to the
map edge instead:

```
entryPoint = bunits.SearchForClosestEntryPoint(pos, alliance, checkforenemyunits: true, ignoreneutral: true)
supplystate = entryPoint == null ? 0 : distanceFactor(dist to entryPoint)
istopsupplyunit = true
```

Then two global adjustments:

```
if orphaned:  supplystate *= supplyfactormalusnotopunit
supplystate -= supplyadminweightingbf × supplystate × (1 − commander.GetCommanderAdministration())
```

Four things worth drawing out:

- **It is a multiplicative chain up the command hierarchy.** Each link
  multiplies a distance-decay factor by its parent's state, so penalties
  **compound with hierarchy depth**, not just with distance. A brigade four
  echelons down and far from the edge is multiply penalised.
- **Supply interdiction is geometric and real.** The line from unit to parent
  is traced with `bfs.CheckForUnits(..., usewidecolliders: true)`, and *any*
  enemy unit on it sets `supplylineinterrupted` and leaves `supplystate` at 0.
  Also gated on `UnitIsInsideRadius(1)` — the unit must be within its
  commander's radius to draw supply at all.
- **Commander administration is the mitigator**: at `admin == 1` there is no
  penalty, at `admin == 0` the unit loses `supplyadminweightingbf` of its
  supply state.

> **`EntryPoint` is the battlefield supply source.** This corrects
> [02-type-inventory](02-type-inventory.md), which called `EntryPoint` a
> "false friend" — it is not a program entry point, but it is much more than a
> reinforcement marker: it is where battlefield supply physically originates.
> Capturing or threatening entry points cuts an army's supply.

---

## 3. Campaign supply: Liebig's law of the minimum

`Regiment.UpdateIIPSupplyLink()`, ~560 lines. Two sources feed each stock type
— **depots** (`supplyfromdepots[]`) and **local foraging**
(`supplylocal[]`) — against a measured `consumption[]`.

All three are **running averages**, smoothed by
`Tools.AddToAverage(..., GamePrefs.supplystatsaverage, elapsed)`, so they are
rates rather than instantaneous values.

```
supplystate = min over i in 0..3 of:
                  consumption[i] <= 0.01 ? 1
                                         : (supplyfromdepots[i] + supplylocal[i]) / consumption[i]
clamp [0, 1]
```

> **Campaign supply state is the *worst* ratio across the four types.** An army
> swimming in ammunition is still "out of supply" if its forage is short. This
> is the single most important thing to know about campaign logistics, and it
> means the binding constraint is usually invisible unless you check all four.

`GetAvgSupplyState()` exists separately for the cases that want the mean rather
than the minimum.

Other mechanics in the same method:

- **Supply updates only for units visible on the campaign map**
  (`GetUnitVisibilityStateOnCampaignMap(...) != 0` returns early), and fleets
  (`unittyp == 17`) take a separate harbour path entirely.
- **Node condition acts as distance.** Choosing the nearest economic node uses
  `distance / max(0.01, iip.condition)` — a damaged harbour or depot is
  effectively further away.
- **Perk 9 (pontoon train) inflates the elapsed-time delta**
  (`supplyspeedpontoontrain[level]`, for self *and* parent), which speeds
  resupply.
- **Supply costs money and moves markets.** Each unit's draw is priced through
  `Economy.GetUnitCost(...)`, charged to the treasury, and registered at the
  node via `closestiipforsupply.AddTradeData(...)` — so **an army buying
  supplies raises local prices.**
- **Impressment** (an alliance policy flag) pays only
  `GamePrefs.impressmentcostsupplies` of the bill and refunds the rest to the
  treasury — i.e. seizing supplies rather than buying them.
- `speedofsupply` is a separate smoothed metric,
  `min(1, supplyspeeddistance0 / distance)`.
- A `Messages.msg_outofsupply` notification fires below
  `GamePrefs.lowsupplymessagetrigger`.

### A permutation trap

The economy resource order and the supply-type order are **not the same**. The
method explicitly remaps:

```
array8[0] = array[2];  array8[1] = array[3];
array8[2] = array[1];  array8[3] = array[0];
```

and applies per-type conversion factors `supplytogoodsratioadjrifleammo` and
`supplytogoodsratioadjrifleartilleryammo` at specific indices.

> Anything mapping between `Economy.resources[]` ids and supply slots must go
> through `resids_regularsupply[]` rather than assuming index equality. This is
> exactly the sort of thing a ModApi should hide.

---

## 4. Depots are an economy building

`CBuilding.id_supplydepot = 22`. A depot holds `stock[]` of **economy
resources** flagged `Economy.resources[i].issupplydepotgood` — supply is not a
separate currency, it is the economy's own goods.

`CBuilding.GetSupplyEfficiency(float strengthofunit)`:

```
if (closestiip == null) return 0;                  // an unlinked depot supplies nothing

fill      = mean over supply goods of stock[i] / (minpricereached[i]
                                                  × maximumsupplydepotgoods[level]
                                                  × supplydepotaccumulationfactor[i])
priceMean = mean over supply goods of Economy.GetLocalPrice(i, …, relative: true, closestiip)
priceTerm = max(0.1, 1 − priceMean) ^ buysupplygoodspriceratio

throughput = 1 / maximumtimetobuildupsupplystock
           × minpricereached[provisions] × maximumsupplydepotgoods[level]
           × supplydepotaccumulationfactor[provisions]
           × priceTerm × Condition × supplydepotspeedperlevel[level]

demand     = 1 / timesupplyconsume × supplytogoodsratio

return clamp(throughput / demand / max(1, strengthofunit), 0, 1)
```

> **Expensive goods literally throttle resupply.** `priceTerm` falls as the
> local relative price rises, so a shortage that drives prices up reduces the
> rate at which depots can push supply forward — a scarcity feedback loop
> running from the economy straight into army effectiveness.

Depot capacity scales with `level`, throughput with
`supplydepotspeedperlevel[level]`, and both are degraded by the building's
`Condition`. Initial stock is seeded by `SetInitialDepotStock()` at
`initialsupplydepotstockfactor` of capacity (called at campaign boot frame 41,
see [10-simulation-tick](10-simulation-tick.md)).

`BattleUnits.GetClosestDepotsAndTowns(position, alliance, number, avoidenemy)`
is the spatial lookup, and is also what `AICampaign` uses to pick raid targets
— **the AI raids your logistics by querying the same function your units use
to find it.**

---

## 5. How shortage actually bites

Three distinct paths, at three different cadences and thresholds. This is the
part most likely to surprise a modder.

| Path | Trigger | Cadence | Effect |
| --- | --- | --- | --- |
| `UpdateCondition` | stock / (strength + sick) **≤ 0.05** | continuous | `conditionmen` / `conditionhorses` degrade |
| `BattleUnits.AdjustConditionOfArmies` | stock **≤ 0** | end of day | further condition loss |
| `BattleUnits` overnight pass | any shortfall | end of day | morale hit via `GetAvgSupplyState()` |

### The 5% cliff

```
if (supplyinstock[2] / max(1, strength + sick) <= 0.05f)
    conditionmen += conditionoutofsupply[0] × elapsed × trainingBuffer;
if (supplyinstock[3] / max(1, strength + sick) <= 0.05f)
    conditionhorses += conditionoutofsupply[1] × elapsed × trainingBuffer;
```

> **Supply shortage is a cliff, not a gradient.** At 20% provisions a unit is
> completely fine; at 5% it begins to fall apart. There is no graduated penalty
> between. The `0.05` is a **hardcoded literal**, not a pref — one of the few
> combat-relevant magic numbers found so far.

### The overnight morale hit

In the end-of-day batch (the same pass that computes
`desertionslastnight`):

```
morale += −( clamp(1 − GetAvgSupplyState(), 0, 1) ) ^ supplyinfluenceonmoraleexp
           × supplyinfluenceonmoralelvg
```

> **Supply only hits morale once per day**, not per frame, and it uses the
> **average** supply state here while `supplystate` itself is the **minimum**.
> So the morale penalty is softer than the headline supply figure suggests.

See [15-attrition-and-condition](15-attrition-and-condition.md) for what
condition then does, and [13-morale-and-cohesion](13-morale-and-cohesion.md)
for campaign morale slots 3–7 (out of food, out of ammo, low condition men /
horses / guns).

---

## 6. `eodcycle` — the end-of-day state machine

`BattleUnits.eodcycle` is an `int` phase counter that gates a surprising amount
of behaviour. Confirmed: it suspends firing
([11](11-combat-resolution.md) §3b), melee ([12-melee](12-melee.md) §1),
cohesion updates and fatigue accumulation, and it drives the overnight
attrition, desertion and morale batch.

Transitions observed are driven by UniStorm's clock against the month-indexed
daylight prefs:

```
if ((uniStormSystem.Hour < lastunistormhrspoint) | (dayCounter != lastunistormday)) && eodcycle == 2
    → eodcycle = 3
if (eodcycle == 3 && hourCounter < GamePrefs.dayendhours[month − 1] && …)
    → eodcycle = 4
```

Values 0 through at least 4 exist; 0 means "normal play". The full state
machine is not traced.

---

## 7. `GamePrefs` keys

**Battlefield supply:** `supplytoplevelbf`, `supplydistancemodificatorbf`,
`supplydistanceweightingbf`, `supplyfactormalusnotopunit`,
`supplyadminweightingbf`.

**Campaign supply:** `supplytogoodsratio`, `supplytogoodsratioadjrifleammo`,
`supplytogoodsratioadjrifleartilleryammo`, `supplygoodsfactor`,
`timesupplyconsume`, `supplystatsaverage`, `supplyspeeddistance0`,
`consumptionerosion`, `armyupkeepmodifier`, `lowsupplymessagetrigger`,
`impressmentcostsupplies`, `supplyspeedpontoontrain[]`.

**Depots:** `maximumsupplydepotgoods[]`, `supplydepotspeedperlevel[]`,
`maximumtimetobuildupsupplystock`, `initialsupplydepotstockfactor`,
`buysupplygoodspriceratio`, and per-resource `supplydepotaccumulationfactor`,
`minpricereached`, `issupplydepotgood`.

**Shortage:** `conditionoutofsupply[]`,
`influenceonconditionifprovisionsnegative`,
`influenceonconditionifforagenegative`, `supplyinfluenceonmoraleexp`,
`supplyinfluenceonmoralelvg`.

---

## 8. Open questions

1. **`overridesymbol`** is the echelon value compared against
   `supplytoplevelbf`, and defaults to `unittyp` when 0. Its own derivation and
   value set are untraced — it decides where the supply chain is rooted.
2. **`IIP`** — "important infrastructure point"? `IIP.id_harbor` is confirmed;
   the full type set and how IIPs relate to towns and depots is not. It is the
   junction between the economy and logistics and deserves its own pass with
   the economy subsystem.
3. **The depot/local split.** `supplyfromdepots` vs `supplylocal` are
   accumulated in the uninspected middle of `UpdateIIPSupplyLink` (~lines
   5050–5330). *How much* an army can forage locally, and what limits it, is
   the main gap left in this subsystem.
4. **`eodcycle`'s full state machine** (§6).
5. **`Economy.resid_*` constants and `resids_regularsupply[]`** — needed to
   finish the resource↔supply mapping, and they belong with the economy.
6. **Fleet supply** (`unittyp == 17`) takes an entirely separate harbour-based
   path that was not followed.
7. **`groupsupplystate[]` aggregation** — how a formation's supply is rolled up
   from its subordinates.
