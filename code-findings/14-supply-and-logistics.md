# 14 — Supply, logistics and readiness

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment.CalculateSupplyLine`,
`Regiment.UpdateIIPSupplyLink`, `Regiment.UpdateRaids`,
`Regiment.CheckReadinessActions`, `CBuilding.UpdateEconomy`,
`IIP.GetTransportCapacity`, `CampaignArmyPanel.GetReadinessStep`,
`BattleUnits`, `Economy`, `LogisticsPanel`, `GamePrefs`; values quoted from
the shipped `Config/unitprefs.txt` and `Config/campaignprefs.txt`
**Summary:** [summary-logistics](summary-logistics.md)

Third Phase 2 subsystem. Like combat, supply turns out to be **two separate
models** — a battlefield one and a campaign one — sharing the same four-element
stock array. A later pass against the official manual added the third supply
source (raiding, §9), the transport-capacity model (§8) and **readiness**
(§10), which no file in this folder had traced.

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

> **Forage is shown for the horse-using types** — Cavalry (1), Artillery (2)
> and Scouts (4). See [16-enumerations](16-enumerations.md) for the full
> `unittyp` table, which this subsystem finally pinned down. **The campaign
> consumption path charges it only to 1 and 2** (§3a), so a scout detachment is
> shown a forage row it never spends from.

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

### 3a. The depot/local split, resolved

`UpdateIIPSupplyLink` lines ~5060–5370. Consumption runs first, then an
eligibility gate, then the two sources in a fixed order: **depots first, local
foraging second, against whatever deficit the depots did not cover.**

**Consumption**, per attached brigade, using establishment strength
`e = strength + losses[0] + sick`:

```
provisions:  stock[2] -= e × elapsed / timesupplyconsume × perk5 × fortFactor × orgReform × dlc
forage:      stock[3] -= same, only if unittyp == 1 or 2
rifle ammo:  stock[0] -= e × elapsed × consumptionerosion / timesupplyconsume × …, only unittyp 0 or 1
arty ammo:   stock[1] -= same, only unittyp == 2
```

| Pref | Value | Meaning |
| --- | --- | --- |
| `timesupplyconsume` | 0.03 campaign years | **11.0 days** to eat a full provisions or forage load |
| `consumptionerosion` | 0.25 | ammunition burns at a quarter of that rate — **43.8 days** for a full load |
| `reducedconsumptionforts` | — | garrisons consume less |
| `supplyconsumptionfactortransport` | — | applied while `shownmodel != null`, i.e. embarked on rail or ship |
| `project_organisationreformlowerconsumption` | — | Organization Reform project |
| `suppliescarriedflyingcolumn[]` | — | perk 5, as `1 / value`; applies from the unit **or its parent** |

> **Forage is charged to `unittyp` 1 and 2 only — not 4.** [§1](#1-the-four-supply-types)
> records scouts (4) as a horse-using type because the *tooltip* shows them a
> forage row, but the campaign consumption path skips them. Likewise
> ammunition is charged only to 0, 1 and 2, so **skirmishers and scouts draw
> provisions and nothing else.**

**The eligibility gate**, immediately after consumption:

```csharp
bool flag = true;
if (inbattle && garrisonreference != null)                      flag = false;
if ((running | doublequick) && !inbattle && regimentpaths > 0)   flag = false;
if (!flag) { supplystate = 0f; latestreinforcements = 0f; return; }
```

> **A unit on forced march consumes supply and receives none.** `doublequick`
> is the campaign forced march (§10a), `running` its faster variant. The
> consumption above has already been applied, and the early return skips both
> sources, zeroes `supplystate` and stops reinforcements. The config ships a
> pref for the intended version of this — *"Max supply ratio of units
> retreating or on forced march [0-1]"*, `maxsupplyratioretreat` = 0.25 — and
> it is **parsed and never read** ([07](07-config-data-model.md)). The shipped
> behaviour is a full cut-off rather than a 25 % cap.

**Choosing a node.** Over every IIP in range:

```
score = iip.GetTransportCapacity() / max(1, distance) / max(0.01, iip.GetAveragePrices(supplyGoods))
      × tradefactorenemyiips        if at war with the owner
      = 0                           if the owner is neither yours nor an ally
      = 0                           if IIPType == id_invisible and the unit is not a garrison
```

The highest score wins, and its distance becomes `refpoint`.

> **The army does not attach to the nearest node, it attaches to the best one.**
> Capacity, distance and price trade off linearly, so with a railroad worth
> **×11** on capacity (§8) a railhead outranks a plain road junction up to
> eleven times further away. Cheap goods pull an army the same way.

**The two draws.** With `R = e × supplytogoodsratio` (the full requirement in
goods units) and `D` the current deficit:

```
depot:  min(R × elapsed / supplyspeeddepots, D) × ammoFactor × weather[3]
        → AdjustStockOfSupplyDepots(), which further multiplies by the
          depot's Condition and supplydepotspeedperlevel[level]
          and is capped by what the depot actually holds

local:  min(R × elapsed / num15 × priceTerm × supplyefficiencyfactor, D − depotDraw)
        × ammoFactor
        → Economy.Consume() at the chosen node, and AddTradeData() so the
          draw registers as local demand
```

where

```
num15     = Interpolate(refpoint over [0, 2000],
                        [supplyspeeddistance0, supplyspeeddistance2000])
            × supplyenemysoilfactor      (if standing in a state at war with you)
            / max(0.01, iip.condition)
            / tradefactorenemyiips       (if at war with the node's owner)

priceTerm = 1 − max(relativeLocalPrice − pricelevelsupplygoodssupplyreduction, 0)
                / (1 − pricelevelsupplygoodssupplyreduction)
                × (1 − supplyratioifpricesontop)
```

| Pref | Value |
| --- | --- |
| `supplyspeeddepots` | 0.0025 yr |
| `supplyspeeddistance0` | 0.02 yr |
| `supplyspeeddistance2000` | 0.15 yr |
| `supplyefficiencyfactor` | 2.25 |
| `supplyspeedammo` | 0.3 |
| `pricelevelsupplygoodssupplyreduction` | 0.7 |
| `supplyratioifpricesontop` | 0.25 |
| `tradefactorenemyiips` | 0.1 |
| `supplydepotspeedperlevel[]` | 0.5 / 0.75 / 1.0 |

Reading those as days to refill a unit from empty, at perfect price and
condition:

| Source | Provisions / forage | Ammunition |
| --- | --- | --- |
| Depot, level 3 | **0.9 days** | 3.0 days |
| Depot, level 1 | 1.8 days | 6.1 days |
| Foraging, adjacent node | 3.2 days | 10.8 days |
| Foraging, node at distance 2,000 | 24.3 days | 81 days |
| Foraging from an enemy-owned node | ×10 slower | ×10 slower |

> **Three things follow.** A level-3 depot in range is worth roughly **four
> adjacent economic nodes**, and twenty-seven distant ones — which is the
> quantitative content of the manual's claim that depot supply arrives "much
> faster, without restrictions of the IIP transport capacity", and it is
> literally true: no capacity term appears in the depot draw at all.
> **Ammunition is always the slow one**, 3.3× behind food, so a unit that has
> just fought is short of exactly the thing it takes longest to replace. And
> **node condition divides the local rate**, so a freshly captured railhead at
> condition 0.3 supplies at a third of its eventual rate — the mechanism
> behind "it takes some time to get the captured infrastructure working".

> **Weather throttles depots but not foraging.** `GetWeatherAdjustment()[3]`
> multiplies the depot draw and does not appear in the local draw. Bad weather
> therefore shifts an army's supply mix *towards* living off the land.

> **Transport capacity never enters either rate.** It decides which node the
> army attaches to, and it governs how fast the economy can restock that node
> — so its effect on the army arrives indirectly, through the node's prices
> and through `priceTerm`. The manual's "this rate of supply is dependent on
> the transport capacity of the IIPs" is true as an outcome and false as a
> mechanism.

> **The Confiscation Act has its sign inverted.** `supplyenemysoilfactor` is
> 1 by default and set to `confiscationsupplyflow` = **1.15** by the
> Confiscation Act (`Policies.cs:677`). It multiplies `num15`, which is the
> *time* to resupply — so the act makes foraging on enemy soil **13 % slower**.
> The config label reads *"Confiscation, supply flow improvement if unit is on
> enemy soil, factor [>1]"* and the manual promises *"Supply efficiency of
> armies operating in enemy states + 15 %"*. All three agree on the intent and
> the code does the opposite.

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

> **What this formula suggests**, and what was first recorded here, is that
> expensive goods throttle resupply: `priceTerm` falls as the local relative
> price rises. The shape is right and the conclusion survives — but not through
> this function.

> **Correction, and it matters.** `GetSupplyEfficiency` has **exactly one
> consumer in the assembly**: `Economy.ShowFlowConnectionLine`
> (`Economy.cs:1860`), which uses it as the weight of the depot-to-army line in
> the **supply-line overlay**. It is a drawing value, not a mechanism. The
> depot's actual delivery rate is the `supplyspeeddepots` draw of §3a, and its
> actual restocking is `UpdateEconomy` below. The conclusion the formula
> suggested — that prices throttle resupply — is still right, but it arrives
> through two different places: `priceTerm` in the depot's **purchases**, and
> `priceTerm` in the army's **local foraging** (§3a).

### How a depot actually restocks

`CBuilding.UpdateEconomy`, the `id_supplydepot` branch (`CBuilding.cs:1900`),
per supply good `k`:

```
fullStock = minpricereached[k] × maximumsupplydepotgoods[level]
                               × supplydepotaccumulationfactor[k]
fill      = stock[k] / max(1, fullStock)
sign      = +1 if fill < fillratiotarget, −1 if above        (manual mode)
            −300 if burndowncounter > 0                      (the depot is burning)
priceTerm = max(0.1, 1 − relativeLocalPrice) ^ buysupplygoodspriceratio

delta     = max(0.001, elapsed / maximumtimetobuildupsupplystock)
            × fullStock × priceTerm × sign × closestiip.condition
```

clamped so stock stays in `[0, fullStock]`, then paid for through
`Economy.Consume(...)` with the Logistics Reform discount
`1 + level × project_logisticsreform`.

| Pref | Value | Effect |
| --- | --- | --- |
| `maximumsupplydepotgoods[]` | 2.0 / 4.0 / 6.0 | capacity multiplier on each good's `minpricereached`; **×3 from level 1 to 3** |
| `supplydepotspeedperlevel[]` | 0.5 / 0.75 / 1.0 | both the delivery rate to armies and the depot's own stock movement; **×2 from level 1 to 3** |
| `maximumtimetobuildupsupplystock` | 0.075 yr | 27.4 days to fill from empty at a comfortable price |
| `minimumsupplydepotstocknosupply` | 0.1 | a depot with no units nearby for a while is drawn down to 10 % of capacity |
| `project_logisticsreform` | −0.05 | **5 % off supply costs per level** of project 100 — a cost effect, not a speed one |

> **Depot level is worth more than it looks.** Capacity ×3 and rate ×2
> compound: a level-3 depot both holds three times the stock and pushes it out
> twice as fast, so it refills from the economy three times faster in absolute
> terms and supplies an army twice as fast. Project **115, Supply Reform**, is
> what unlocks levels 2 and 3 (`CBuilding.cs:3070` blocks the upgrade while
> `!projects.Contains(115)`).
>
> **A burning depot empties 300× faster than it fills.** `burndowncounter > 0`
> sets the sign term to −300, so a successful raid on a depot destroys its
> contents almost immediately rather than degrading it.

Initial stock is seeded by `SetInitialDepotStock()` at
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


**Campaign supply, added in §3a:** `supplyspeeddistance2000`,
`supplyspeeddepots`, `supplyspeedammo`, `supplyefficiencyfactor`,
`pricelevelsupplygoodssupplyreduction`, `supplyratioifpricesontop`,
`tradefactorenemyiips`, `reducedconsumptionforts`,
`supplyconsumptionfactortransport`, `project_organisationreformlowerconsumption`,
`suppliescarriedflyingcolumn[]`, `confiscationsupplyflow`,
`minimumsupplydepotstocknosupply`, `project_logisticsreform`,
`project_infrastructure`, `railroadtransportcapacityfactor`.

**Raiding (§9):** `raidingefficiencyinf`, `raidingefficiencycav`,
`guardingefficiencyinf`, `guardingefficiencycav`, `cunninginfluenceonraiding`,
`cunninginfluenceonguarding`, `raidvolume`, `raiddamage`,
`minimummoraleraiding`, `minimummoraleguarding`,
`reinforcementdelaycavalryorders`.

**Readiness (§10):** `readinessrecoveryperyear`,
`readinessrecoveryunitsizelin`, `readinessrecoveryunitsizeexp`,
`influenceadminonreadiness`, `readinessadjustmenttraining`,
`influenceofintelligenceonreadiness`,
`readinessrecoveryfriendlyterrainmodifier`,
`regroupingrangefasterreadinessrecovery`, `influencesupplyonreadiness`,
`influenceofmoraleonreadiness`, `influenceofnonarrivedunits`,
`influenceofwinterquartersonreadiness`, `readinessconsumptionmovement[]`,
`readinessadjustmentinitiative`, `influencemilexponreadinessconsumption`,
`influencewinteronreadinessconsumption`, `readinessconsumptionfactorifembarked`,
`readinessconsumptionskirmishing`, `readinessconsumptionsieges`,
`readinessconsumptioncasualties`, `readinessreductioncommanderchange`,
`readinessminormajordefeat[]`, `influencereadinessoncommanderrange`,
`influencereadinessoncombatrange`, `readinessorderdelay`,
`movementspeedmultiplierreadiness[]`, `reinforcementspeedadjustmentreadiness`,
`withdrawal_percunitsreadiness0`, `withdrawal_percunitsreadiness1`.

---

## 8. Transport capacity, node by node

`IIP.GetTransportCapacity(usecondition, simulateallconnections, shift)`
(`IIP.cs:1160`) is the number the economy's trade flow divides work by, and the
number §3a's node selection ranks on. It starts from the node's serialized
`TransportCapacityFactor` and applies the chain below. [18](18-economy.md) §4
has the same formula from the trade side, including the five policies that key
off **Unity scene object names**; what follows adds the shipped values.

```
capacity = TransportCapacityFactor
         × (1 + railroadtransportcapacityfactor)   if any connected railroad is complete
         × policy multipliers                      Go West!, Union Pacific, Northern Routes,
                                                   Southern Pacific, Slavery to the West
         × (1 + GetProjectLevel(99) × project_infrastructure)
         × (1 + marketcapacity.GetValueOnPos(position))
         × alliance.transportcapacityports          if harbour
         × (1 + (BuildingLevel + shift) ^ transportcapacityportlevel)   if harbour
         × condition                                if usecondition
         × GetTransportCapacityAdjustmentInfra()
```

| Pref | Value | Effect |
| --- | --- | --- |
| `railroadtransportcapacityfactor` | **10.0** | a completed railroad gives `1 + 10` = **×11** |
| `project_infrastructure` | 0.05 | **+5 % per level** of project 99, Infrastructure Reform |
| `marketinfluenceoninfastructure[]` | up to 0.5 | a market's painted field adds up to **+50 %** nearby |
| `unionpacificrailroadtransportcapacity` | 1.25 | +25 %, Union, railroad nodes |
| `southernpacificrailroadtransportcapacity` | 1.25 | +25 %, CSA |
| `gowesttransportcap`, `northernroutestcap`, `slaverytothewesttransportcap` | — | per-policy, on named trail and route nodes |
| `transportcapmodifierlocomotiveszero` | 0.9 | land nodes lose 10 % when locomotive stock is exhausted |
| `transportcapmodifiertransportshipszero` | — | the same for sea and river nodes |

> **The railroad multiplier is ×11, not ×10.** `num2` starts at `1f` and the
> pref is *added* to it, so the "Railroad transport capacity leverage factor"
> of 10 lands as eleven times a plain road. The official manual says *"where a
> normal road has transport capacity of one, a railroad will increase this by a
> factor of ten"* — a reasonable reading of the config, one notch off the code.
>
> **Capacity is multiplied by `condition`** when `usecondition` is true, which
> it is for every call in the supply and trade paths. A raided node at
> condition 0.4 carries 40 % of its goods **and** falls down §3a's node
> ranking, so raiding hurts twice.
>
> **Nothing about transport capacity reaches a unit's supply rate directly**
> (§3a). It decides which node an army attaches to, and it governs how fast the
> economy restocks that node; the army feels it through the node's prices.

### 8a. The Transportation subsidy no longer exists

`campaignprefs.txt` ships three effect constants for it — *"Upkeep cost factor
of transports for transportation subsidies at full level"* = 0.5,
*"Increased IIP transport capacity for transportation subsidies at full level"*
= 1.5 and *"Increased railroad construction speed for transportation subsidies
at full level"* = 2.0 — and **all three are parsed and never read.**

The reason is visible in `GameVars`: the subsidy id constants include
`subsidy_transportx = 4` and `subsidy_tradewarx = 5` with an `x` suffix, and
those ids are **reused** by `subsidy_military = 4` and `subsidy_diplomacy = 5`.
The cost array's own config label names the eight slots
*"(politics, economy, agriculture, industry, military, diplomacy, n/a, n/a)"*
with the last two costed at 0. Transport, Trade War and Civil Order were
removed; their effect constants were left behind.

> **Railroads are funded from the Industry subsidy.**
> `GameVars.subsidyidrailroads = 3` is `subsidy_industry`
> (`RailroadList.cs:293`, `BattleUnits.cs:302`), which is exactly what the
> manual says — Industry funds *"construction of new industries and
> railroads"*. The manual's six categories match the code; it is only the
> leftover prefs that mislead. See
> [18](18-economy.md) for the full list of ten dead subsidy-effect constants.

---

## 9. Raiding is a third supply source

`Regiment.UpdateRaids()` (`Regiment.cs:4574`) is gated on
`cavalryorders == 2` — the campaign **Raid** order. Despite the field name it
is set on any campaign formation, and the efficiency it uses sums **infantry
and cavalry** brigades (artillery contributes nothing):

```
groupraidingefficiency = sum over attached unittyp 0 and 1 brigades of
    strength × raidingefficiency<inf|cav> × (1 + cunninginfluenceonraiding × cunning)
groupguardefficiency   = same with the guarding prefs
```

Then, per enemy-owned IIP within range:

```
net = mean over those IIPs of (own raiding efficiency - sum of enemy guard efficiency at it)
net = max(0.1, net)

for each such IIP within buglerange:
    Economy.AdjustStockIIP(iip, GamePrefs.raidvolume, elapsed × net)   // take the goods
    alliance[victim].AddTradeWarfare(2, cost × elapsed × net)          // bill it as trade warfare

Economy.TransformResourceUsageToSupply(plunder, this, elapsed)         // into own stock
for each enemy building within buglerange: Damage(net × raiddamage)
```

| Pref | Value |
| --- | --- |
| `raidingefficiencyinf` / `raidingefficiencycav` | 1.0 / **2.0** |
| `guardingefficiencyinf` / `guardingefficiencycav` | 0.7 / **1.7** |
| `cunninginfluenceonraiding` / `...guarding` | 0.5 / 0.4 |
| `raidvolume` per man per year | provisions 0.002, forage 0.001, rifle ammo 0.0005, artillery ammo 0.0001 |
| `raidvolume`, marked `[not active]` in the config | horses 0.001, rifles 0.0005 |
| `raiddamage` | 0.075 per unit of efficiency per year |
| minimum morale to raid / to guard | 0.45 / 0.40 |
| casualties on raiders / on guards | 50 / 0.3 per year, scaled by efficiency |

> **Guarding subtracts directly from raiding, and the result is floored at
> 0.1.** So guards can reduce a raid to a tenth of its effect but never stop
> it, and because cavalry counts 2.0 raiding against 1.7 guarding, **cavalry is
> the better tool on both sides of that exchange** — just slightly better at
> raiding than at stopping raids.
>
> **The plunder is real supply**, converted into the raider's own stock by
> `TransformResourceUsageToSupply`. That is what the manual means by raiders
> living off the land, and it is the only path by which a unit gains supply
> when the two sources of §3a are unavailable.
>
> **Raiding and guarding both cost 50× in reinforcement delay.**
> `reinforcementdelaycavalryorders` = 50 is added to the delay of any brigade
> reinforcing a formation that holds a raid or guard order
> (`Regiment.cs:26742`). The manual does not mention it.

---

## 10. Readiness — the operational tempo stat

Not previously traced anywhere in this folder. Readiness is a `[0, 1]` float on
each campaign formation, updated in the same per-unit pass as training and
morale (`Regiment.cs:6144-6250`).

### 10a. The four bands are hardcoded quartiles

`CampaignArmyPanel.GetReadinessStep(reg)` returns 0 for `readiness <= 0.25`,
1 up to 0.5, 2 up to 0.75 and 3 above — **literals, not prefs** — labelled by
`GameVars.readinesstext = { "Deficient", "Low", "Mediocre", "Excellent" }`.
Every restriction in the game is expressed against that step, and
`Regiment.CheckReadinessActions()` enforces the ladder:

| Step | Readiness | What `CheckReadinessActions` does |
| --- | --- | --- |
| <= 2 | 75 % or below | `doublequick = false` — **forced march is cancelled** |
| <= 1 | 50 % or below | `SetFleetOrders(0)`, `cavalryorders = 0` — **raid, guard, patrol and fleet orders are cancelled** |
| <= 0 | 25 % or below | `campaignstance` forced to 1 — **offensive stance is impossible** |

Two more gates live elsewhere:

- **Movement onto enemy soil** is refused at step 1 or below
  (`BattleUnits.cs:12744`), *unless* the target is within regrouping range — an
  exemption the manual does not mention. Occupied states count as friendly.
  For fleets the gate is step 0 and only when no friendly harbour is within 5
  of the target.
- **Deploying a garrison** needs step 2 or better, i.e. **above 50 %**
  (`BattleUnits.DeployGarrison`).

### 10b. Consumption

Only while moving, interpolated on the support of the state being crossed:

| Source | Pref | Value |
| --- | --- | --- |
| Moving, at state support 1 / 0 | `readinessconsumptionmovement[]` | **−8.5 / −25.0** per year |
| Commander initiative, at 100 % | `readinessadjustmentinitiative` | ×0.35 |
| National military experience, at 100 % | `influencemilexponreadinessconsumption` | ×0.50 |
| Winter, snow only | `influencewinteronreadinessconsumption` | ×4.0 |
| Embarked on rail or ship | `readinessconsumptionfactorifembarked` | **×0.75** |
| Skirmishing | `readinessconsumptionskirmishing` | −7.5 per year |
| In a siege | — | −5 per year |
| After a battle | — | −2.5 absolute per 100 % casualties, ×1.2 on a defeat |
| Commander changed recently | — | −0.10 absolute |
| Fleets: moving / in battle / bombarding | — | −25 / −200 / −30 per year |

> **State support is the dominant term.** Marching through a hostile state
> burns readiness at **−25 per year against −8.5 in a loyal one** — about 15
> days from full to empty against 43. An aggressive commander (×0.35) and a
> war-experienced nation (×0.50) together cut that by 83 %.
>
> **Rail and sea transport cost *less* readiness, not more.** The embarked
> multiplier is 0.75 applied to a per-year rate, so an hour on a train costs
> three quarters of an hour marching — and covers far more ground. The manual
> states the opposite.

### 10c. Recovery

Base `readinessrecoveryperyear` = **6.8** per year, multiplied by:

| Factor | Pref | Value |
| --- | --- | --- |
| Unit size | `readinessrecoveryunitsizelin / N ^ readinessrecoveryunitsizeexp` | 3.00 / N^0.43, N = attached units |
| Commander administration, at 100 % | `influenceadminonreadiness` | ×1.33 |
| Training, at 100 % | `readinessadjustmenttraining` | ×1.5 |
| Intelligence gathering | `influenceofintelligenceonreadiness` | x(1 + 0.005 × value) |
| On friendly terrain | `readinessrecoveryfriendlyterrainmodifier` | ×1.10 |
| Within regrouping range, not on friendly terrain | `regroupingrangefasterreadinessrecovery` | ×1.10 |
| Supply state 0 | `influencesupplyonreadiness` | ×0.5 |
| Morale 0 | `influenceofmoraleonreadiness` | ×0.4 |
| A brigade still arriving | `influenceofnonarrivedunits` | ×0.33 |
| In winter quarters | `influenceofwinterquartersonreadiness` | ×0.75 |

> **Size is the biggest lever.** A 4-brigade corps recovers at
> `3 / 4^0.43 = 1.65x` base and a 12-brigade army at `1.03x`, so **splitting a
> large army into corps to rest it is worth about 60 % more recovery per day.**
> Full stack: 6.8 × 1.65 × 1.33 × 1.5 is about 22 per year, or **16 days from
> empty to full**; a large army under a poor commander and short of supply can
> take months.
>
> **Winter quarters slow readiness recovery by a quarter.** They help condition
> and training; they do not help readiness.

Readiness then feeds back out: command range ×(1 − 0.33) at zero, combat range
×(1 − 0.25), order delay ×(1 + 10 × shortfall), movement speed ×0.3 and
reinforcement speed ×0.25. It also decides withdrawal losses — **100 % of units
take skirmish casualties when withdrawing at readiness 0, against 20 % at
readiness 1.**

### 10d. Four readiness prefs the config marks dead itself

`campaignprefs.txt` ships `readinesstargetentrenched` = 0.1,
`readinesstargetencamped` = 0.4, `readinessforcedmarch` = 0.4 and an
intelligence-based recovery factor of 0.008, each with **`[deactivated]`** in
its own label — and all four are indeed parsed and never read. Unusually for
this codebase, the config is being straight with the modder.

> **The two intelligence prefs are one letter apart and only one is live.**
> `intelligenceinfluenceonreadiness` (0.008, *"Readiness improvement factor
> based on intelligence value [deactivated]"*) is the dead one;
> `influenceofintelligenceonreadiness` (0.005, *"Influence of intelligence on
> readiness recovery, multiplier per intelligence value"*) is the live one in
> §10c. A modder who edits the first and sees nothing has edited the wrong
> field, not the wrong file.

---

## 11. Open questions

1. **`overridesymbol`** is the echelon value compared against
   `supplytoplevelbf`, and defaults to `unittyp` when 0. Its own derivation and
   value set are untraced — it decides where the supply chain is rooted.
2. **`IIP`** — "important infrastructure point"? `IIP.id_harbor` is confirmed;
   the full type set and how IIPs relate to towns and depots is not. It is the
   junction between the economy and logistics and deserves its own pass with
   the economy subsystem.
3. ~~**The depot/local split.**~~ **Resolved** in §3a: both draws, the node
   selection rule, the shipped rates in days, and the forced-march cut-off.
4. **`eodcycle`'s full state machine** (§6).
5. **`Economy.resid_*` constants and `resids_regularsupply[]`** — needed to
   finish the resource↔supply mapping, and they belong with the economy.
6. **Fleet supply** (`unittyp == 17`) takes an entirely separate harbour-based
   path that was not followed.
7. **`groupsupplystate[]` aggregation** — how a formation's supply is rolled up
   from its subordinates.
8. **What "2,000" is in miles.** `supplyspeeddistance2000` is keyed to a raw
   campaign-map distance, and the map's unit-to-mile scale is still unknown
   (`FilterMap.GetFieldRealWorldFieldSize()`, [18](18-economy.md) §11). Until
   that is pinned, §3a's distance figures are relative, not absolute.
9. **Why forced march cuts supply entirely** rather than capping it at the
   `maxsupplyratioretreat` = 0.25 the config ships. The early return is
   unambiguous; whether the cut-off or the unread pref is the intended
   behaviour is not recoverable from the code.
10. **`fillratiotarget`** is a per-depot field driving the buy/sell sign in
    `UpdateEconomy`. Whether the player sets it, and its default, were not
    traced.
