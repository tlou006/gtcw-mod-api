# 18 — Economy

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Economy` (2,564 lines), `Economy.Resource`,
`IIP`, `CampaignStrategy`, `GameVars.Alliance`; schema from
`Config/resources.dat`
**Summary:** [summary-economy](summary-economy.md)

Fifth Phase 2 subsystem, and the one the previous three kept pointing at. Three
layers:

1. **`IIP` nodes** — a spatial network of local markets with their own stock,
   prices and transport capacity.
2. **`Resource`** — goods with production recipes, per-consumer demand and
   seasonal harvests.
3. **Macro** — credit rating, interest rates and a boom/bust cycle.

---

## 1. `IIP` is a local market, and this is what it stands for

`IIP.typename[9]` finally resolves the acronym's referents — these are **map
infrastructure nodes**, each of which is also a market:

| # | Type | Notes |
| --- | --- | --- |
| 0 | Small Town | `id_smalltowns` |
| 1 | **Port** | `id_harbor`; `isriverport` / `isseaport`; capacity scales with `BuildingLevel` |
| 2 | Bridge | `id_bridge` |
| 3 | Ferry | |
| 4 | Mountain pass | |
| 5 | Indian settlement | |
| 6 | Trading route | |
| 7 | invisible | `id_invisible` |
| 8 | Railroad line | `id_railroad` |

Each node carries a full market: `stock[]`, `price[]`, `relprice[]`,
`producedpcspa[]`, `demandedpcspa[]`, `historicaltrades` (a list of
`LastTrade` records with counterparties), `continuousprofit`,
`continuousprofitimports`, `lossestradewarfare`, `condition`,
`transportbottlenecks`, `connectedrailroads`, and a `state` link.

Demand is population-driven and **interpolated across the war**:
`TradePopulation1850` and `TradePopulation1870`, with `LinkStatePopulation`
tying a node to its state's population instead. `DemandFocusID[]` /
`DemandFocus[]` / `DemandFocusSide[]` / `DemandFocusTimeFrom[]` /
`DemandFocusTimeTo[]` let a scenario author impose **time-windowed,
side-specific demand spikes** on specific goods — the main scripting hook for
the economy.

Counterparties can be other IIPs, `CBuilding`s, `Town`s or `Regiment`s
(`GetListOfCBSellers`, `GetListOfTownSellers`, `GetListOfRegSellers`), so
armies, factories, towns and nodes all trade in the same ledger.

> This closes the `IIP` question left open by
> [14-supply-and-logistics](14-supply-and-logistics.md) and
> [17-politics-and-support](17-politics-and-support.md). A depot's
> `closestiip` being `null` means **no market to buy from**, which is why
> `GetSupplyEfficiency` returns 0 outright in that case.

---

## 2. Pricing: an S-curve on scarcity, priced at the average

`Economy.CalculatePrice(resourceid, currentstock, returnrelative, amount)`:

```
// 1. effective stock, averaged across the transaction rather than marginal
if (amount < 0)  // withdrawal: average down to the floor at 0
if (amount > 0)  // addition:   average up, capped at minpricereached

scarcity = clamp(1 − effectiveStock / minpricereached, 0, 1)       // 0 abundant, 1 empty

// 2. logistic transform
rel = scarcity / (scarcity + exp(scurvepricingC1 − scurvepricingC2 × scarcity))
rel = max(0.01, rel)

price = returnrelative ? rel : rel × resources[resid].maxprice
```

Three things worth drawing out:

- **`minpricereached` is a *quantity*, not a price** — it is the stock level at
  which price bottoms out, i.e. the abundance threshold. It doubles as the
  capacity denominator in depot maths
  ([14](14-supply-and-logistics.md) §4) and as the stock scale everywhere
  else. The name is misleading and has misled these notes before.
- **Price is a logistic (S-curve) function of scarcity**, tuned by exactly two
  prefs, `scurvepricingC1` and `scurvepricingC2`. Shallow at both extremes,
  steep in the middle — so moderate shortages move prices far more than
  severe ones.
- **Large transactions are priced on their own average**, not on the starting
  stock. Buying a lot genuinely costs more per unit. That is proper slippage
  modelling and unusual care for a game economy.

### `GetLocalPrice` walks the transaction in unit steps

```csharp
GetLocalPrice(resid, position, amount, alliance, relative, determinediip)
```

Resolves the nearest IIP (`BattleUnits.GetClosestIIP`) and then **loops in
steps of ±1**, repricing against the depleting stock each step and returning
the volume-weighted average.

> **Performance note:** the loop iterates once per unit of `amount`. It is
> called from `Economy.GetUnitCost` inside the campaign data tick
> ([10-simulation-tick](10-simulation-tick.md) §4), once per unit, so a large
> army buying supplies drives a lot of iterations. Worth knowing before a mod
> calls it in a hot path, and worth measuring before blaming a mod for a
> campaign-tick stall.

---

## 3. `Resource`: recipes, demand drivers, seasons

### The `Config/resources.dat` schema

A count header, then per record:

| Field | Encoding |
| --- | --- |
| resource id | int (records are **self-indexing** — the id selects the array slot, so order in the file is free) |
| `name` | string |
| **pre-goods block** | 4 × (input id, amount) with 3 interleaved connectors — see below |
| `bonusresource` | int |
| `multiplier` | float |
| `demandpercitizen` | float |
| `demandpersoldier` | float |
| `demandpercavalry` | float |
| `demandpergun` | float |
| `demandpertrain` | float |
| `demandpertransport` | float |
| `minpricereached` | float (a quantity) |
| `maxprice` | float — **negative means "derive from the recipe"** |
| `tradenodeproduction` | float |
| `issupplydepotgood` | `bool.Parse` |
| `productionmultiplier` | float |
| `supplydepotaccumulationfactor` | float |
| `harvestingmodifier[12]` | 12 floats, **one per month** |

`isharvested` is set when any monthly modifier is `>= 0`, so **seasonality is
inferred from the data rather than flagged.** Fertility is spatial too —
`Economy.AssignFertilityMaps(folder)` loads per-scenario maps.

### Recipes are a 4-term boolean expression

The pre-goods block is four (id, amount) pairs joined by three connectors,
where **0 = OR and 1 = AND**. `ImportResources` enumerates the satisfying
combinations into concrete `pregoods` recipes via `AddPreGood(n, ids, amounts)`,
then calls `RemoveIdenticalPreGoods()`.

So a good can have **alternative recipes** — "iron AND (coal OR charcoal)"
expands into two two-input recipes. `CheapestPreGoods(position, amount,
alliance, determinediip)` then picks the cheapest satisfiable one **at local
prices**, which is how substitution works.

> **Production chains respond to local prices.** A factory does not have a
> fixed bill of materials; it picks the cheapest valid recipe at its nearest
> market. That is the mechanism behind the scarcity feedback loop seen from the
> supply side.

### Derived prices

`maxprice < 0` triggers derivation: `maxprice = Σ(input.maxprice × amount)`
over the first recipe, propagating `-1` if an input is itself underived. Raw
goods therefore need explicit prices and manufactured goods can inherit them.

> **A mod adding a good to the middle of a chain can leave `maxprice` at `-1`
> and get a consistent price for free** — provided its inputs resolve first.

### Per-alliance bookkeeping

`Resource.ProductAllianceData[]` tracks, per alliance: `averageprice`,
`demandpa`, `productionpa`, `stockiniips`, `stockindepots`,
`totaltradevolume`, **`excisetax`**, **`tariffs`**, `relativestock`, and
`TrailingData[]` series for `importvalue` / `exportvalue`. Every field has a
`_tmp` twin, committed by `RewriteTemps(maxprice)` — a double-buffered
accumulate-then-publish pattern that appears throughout the campaign tick.

`Resource.RewriteTaxValues(cmanualtime)` recomputes excise and tariff values
and is called for every resource at the end of `UpdateMacroEconomy`.

### Production output per building — `CBuilding`

`GetProductionNumbers` (~1718) is the output line:

```
out(g) = buildingtypes[type].productionfactor[level]
       × (1 + productionefficiency[g])
       × resources[g].minpricereached
       × corporateproductionmultiplier          (30.0)
       × dt × production[g]                      (the player's effort share)
       × relativeworkforce × Condition
       × resources[g].productionmultiplier
       × harvestingmodifier[currentmonth]        if != -1f   (~1729)
```

Note the harvest sentinel: **`-1` means "no seasonal modifier", not zero**, so
Forage (−1 for Jan–May) produces at full rate in those months while Crops (0
for Jan–May) produce nothing. Easy to read backwards.

`UpdateProductionEfficiency` (~2320) sets

```
productionefficiency[g] = production[g] ^ productionefficiencyexp     (0.4)
                        × productionefficiencylinear                  (1.0)
                        × num2 × num4 × num3
```

so output in `g` scales as `share × (1 + share^0.4)` — **sub-additive, so
concentrating a building on one good beats splitting it.** One good at 100 %
yields 1.00 against 0.88 for a 50/50 split and 0.79 across four.

`producedgoods` is parsed from `buildingtypes.dat` as **10 slots, keeping those
`>= 0`** (`GameVars` ~6818). The shipped lists: Farms `{9,13,16,21,37}`,
Plantations `{8,26,30,31}`, Iron Works `{0,1,29,35,28}`, Factories
(`id_manufactory = 12`) `{4,12,15,20,22,23,25,32,34}`, Mines
`{5,7,14,18,19,24,27}`, Foundries `{3,17}`, Port `{10,33}`; `id_factory = 5`
("Factory") produces **nothing**.

> **Two project bonuses are unreachable.** Both tests in
> `UpdateProductionEfficiency` compare the **loop counter** against a resource
> id instead of `producedgoods[i]`:
>
> ```csharp
> if (BuildingType == id_farm && i == Economy.resid_crops)        // 9
> if (BuildingType == id_plantation && i == Economy.resid_cotton) // 8
> ```
>
> A farm has 5 produced goods and a plantation 4, so `i` never reaches 9 or 8.
> `GetProjectLevel(114)` has **exactly one consumer in the assembly** — this
> one — so **project 114 Plantation Mechanization has no effect at all**.
> Project 113 keeps its `project_farmmechanizationrecruitment` half
> (`GameVars` ~4852); only the crops half is dead. Both are nevertheless
> advertised by `Projects.GetProjectEffect` (~757/758). In both cases the
> intended good is at index **0** of its building's list, so the one-token fix
> restores the designed behaviour.

> **Project 104 Subsidize Industry omits the manufactory.** Its guard is
> `(type == id_factory) | (type == id_ironworks) | (type == id_foundry)`, and
> `id_factory` produces nothing — so the +5 %/level reaches **Iron Works and
> Foundries only**, not the Factories that make Provisions and Uniforms. The
> other three sites that group industrial buildings (`CBuilding` ~502,
> `Policies` ~1270 and ~1668) all include `id_manufactory`. Project 105
> Subsidize Agriculture correctly covers both Farms and Plantations.

Wage costs are `wagecostworker` **1.0** and `wagecostslave` **0.33** per
year, with a per-building-type `slaverydemand` fraction.

> **Fertility is applied twice.** `GetProductionNumbers` ~1719 caches the
> sampled value into `fertilityfactor[resourceid]` and then multiplies by
> **both** the cache and the local copy:
> `num *= fertilityfactor[resourceid] * num2`. The maps are
> `Initialize(width, 0f, 1f, texture)`, so values are [0,1] and squaring always
> reduces — 0.8 fertility yields 0.64. Also, when `GetFertility` returns `-1`
> (no map, or `applied == false`) the cache is set to `-1` and so never warms,
> re-querying every call.

`GetFertility` reads `Economy.fertilitymapfilter[resid]`, built by
`AssignFertilityMaps(folder)` from `Resources.Load<Texture2D>("Campaigns/" +
folder + "/FertilityMaps/" + i)` — one per resource id, skipped silently when
absent. **Which ids exist is an asset-dump question**, and a mod cannot add one
without rebuilding the bundle.

### The workforce map

`GetRelativeWorkforce` (~1746) is the labour term in the output line:

```csharp
relativeworkforce = min(1f, availableworkforce.GetValueOnPos(pos)
                            * GamePrefs.workforceleverage * (1f - slaveRatio)
                        + 1f * slaveRatio);
```

> **Slave labour bypasses the map entirely** — the `1f * slaveRatio` term is
> unconditional, so a building at `slaveRatio = 0.95` reads about 0.96 wherever
> it stands. With `workforceleverage = 2.0` the free-labour term saturates at a
> map value of 0.5 and bottoms at `minworkforcemap × 2 = 0.30`.

`GetSlaveRatio` (~836):
`slaverydemand × alliance.maxslaveryfactor × (nation[state].slaveryallowed ?
nation[state].support[1] : 0)` — the third term is **Confederate state
support**, so the exemption decays with political support. `maxslaveryfactor`
starts at 1.0 and is moved only by `Policies` ~1177/~1183 (Underground
Railroad: `-0.10` once plus `-0.05`/yr, applied to **both** alliances) and
~1429 (`slaveryrestrictionfactor`). **There is no clamp on it anywhere.**

The map itself is painted in `UpdateFilterMaps` (~1546 onward), one town, one
small town and one building per call, with `workforcemapupdates = 10` calls per
frame:

| Painter | Value |
| --- | --- |
| `towns[i]` | `+ √representingpopulation × workforcefactortowns` (0.002) |
| `smalltowns[i]` | `+ √workforcesmalltowns × 0.002` = **+0.447** |
| `cbuildings[i]` | `− √workforceperlevel[level] × 0.002 × usedworkforcemodifier` (5) |
| `slavery` map | `+ √(workforce × slaveratio) × 0.002 × 10` |

`workforceperlevel` is a **×3 ladder** in `buildingtypes.dat` (Plantations
150→12,150; Mines 100→8,100; Farms 40→3,240; most others 20→1,620), so the
drain grows as `√81 = 9×` from level 0 to 4. **The building's drain uses its
total workers with no slavery adjustment**, so a slave-worked plantation
depresses its neighbours at full strength while being exempt itself.

`Paint` defaults to `range: 1` — a single cell — and the cycle ends with
`availableworkforce.Clear(); availableworkforce.Blur();`. Since `Clear()`
publishes and `Blur()` then operates on the published map, **the
`minimumfactor` floor is applied per paint and can be diluted below 0.15 by the
blur.** `Initialize` scales it correctly
(`minimumfactor = clamp(_min × 255, 0, 255)`), so there is no unit bug there.

> **Recruitment drains the map at 5× and permanently.** `Town.SetLevel` (~681)
> computes `representingpopulation = max(0, (nation[state].population −
> (Σvolunteersrecruited + Σdraftsrecruited) × workforcereductionrecruited) ×
> weightedcitysize)` with the pref at **5**. `AdjustRecruitingPool` subtracts
> only on disband, and only the **surviving strength** — `BattleUnits` ~12085
> passes `-strength` where `RecruitUnit` ~12131 added `+strength`. So the
> killed (`losses[1]`), the deserted (`losses[2]`) and a disbanded unit's
> wounded (`losses[0]`) are never returned. `Town.GetTooltipInfo` reports the
> resulting percentage to the player.

`Town.UpdateConsumption` (~752) reads the same map as
`availableworkforcelevel = min(1, value × workforceleverage)` and treats it as
**unemployment**: `privatewealth` is multiplied by
`interp(level, influenceprivatewealthworkforce0 = 1.6 →
influenceprivatewealthworkforce1 = 0.9)`, then by
`1 + availablecapital × (availablecapitalinfluenceonprivatewealth − 1)` (1.25)
and `alliance.publicwealthfactor`, clamped [0,1]. `incometax = privatewealth ×
representingpopulation × tax × taxcaps × taxrevenuemodifier`. So slack labour
helps production and hurts wealth, tax and national morale (reason 34).

`GameVars.UpdateWorkforceFluctuation` closes the loop:
`eogrowthrates = (1 / max(0.1, √averageavailableworkforce) − 1.2) ×
workforcefluctuationspeed` (0.04), so population grows where labour is scarce
and shrinks where it is abundant, with equilibrium at ≈ 0.69.

### Company foundations

Two live methods, and a third that is dead:

- **`AICampaign.UpdateCompanyFoundationList()`** — called **once per frame**
  from the AI update (~2246), sweeps **one IIP per frame** and accumulates
  `alliance.companyfoundationsandupgradestemp`, published on wrap.
- **`AICampaign.UpdateCompanyFoundations(alliance, dt)`** — called from the
  12-hour per-alliance pass (`BattleUnits` ~3907); picks and commits.
- **`CheckCompanyFoundations_x`** (~5862, 69 lines) — **no callers**.

The per-node probability (~5691 onward):

```
best good = highest relprice >= minimumpriceforcompanyfoundations (0.725)
num  = (relprice − 0.725)/0.275 × resources[g].productalliancedata[owner].value
num4 = num × availableworkforce(pos) × fertility(pos)        // not squared here
     × ((influenceonfoundationcapital − 1) × availablecapital(pos) + 1)  // 1.50
     × generalcompanyfoundingprob                                        // 0.18
…temp += num4 × ((influenceonfoundationsubsidies − 1) × num5 + 1)        // num5 ≡ 0f
             × ((influenceonfoundationcorporatetax − 1) × num6 + 1)      // 1.35
```

> **`num5` is assigned `0f` on both branches of the preceding `if`/`else`**, so
> `influenceonfoundationsubsidies` (1.25) is **dead**. And `num6` is
> `alliance.tax[tax_corporate]` — the **slider**, not the effective rate — so
> the full ×1.35 applies from turn one even though `taxcaps[tax_corporate]`
> ships at 0. Two discarded `float.IsNaN(...)` calls sit on this path (~5728
> and in `RewriteTemps`).

`productalliancedata[].value` is written only in `RewriteTemps`:
`√((averageprice / maxprice) × demandpa ÷ max(0.01, productionpa,
(stockiniips + stockindepots) / 5))`, clamped [0,1] — a national scarcity
index, so a purely local shortage does not attract industry.

`companyfoundationsandupgrades` is also the **land-sales tax base**
(`GetCurrentTaxValuePa`, `GameVars` ~4362/~4367, × `absolutevaluelandsales`).

`UpdateCompanyFoundations` (~5595) then:

- subsidised types (`subsidytype >= 0`) run only if
  **`automanageconstructionp`** (or non-player / `ai_vs_ai`), and each needs
  `GetMissingSubsidyFundingCost(...) >= 0f`, which is
  `−(1 − (funded/cost)^missingsubsidycostexp) × cost × leverage` with the
  exponent at **3** — i.e. **only when the category is fully funded**. (That
  method also contains a discarded `Mathf.Max(0f, num − num2);`.)
- unsubsidised types run only if **`automanageconstructiong`** *and*
  `IsRatingOkForConstruction()`.
- both require `buildingtypes[j].aiplacement`, **false for Mines, Port,
  Factory, Bridges, Fort, Telegraph and the camps** — so mines and ports never
  auto-found or auto-upgrade.
- an existing building of that type within 100 units is **upgraded instead**.
- the trigger is `Random.Range(0f, 1f) < prob × dt × 50f`.

Shipped `subsidytype` from `buildingtypes.dat`: **Agriculture (2)** = Farms
$2.5 M, Plantations $3.0 M, Mills $1.63 M, Saltworks $1.5 M; **Industry (3)** =
Factories $4.5 M, Iron Works $3.75 M, Mines $3.25 M, Foundries $2.5 M, Lumber
Mills $1.75 M, Brickworks $1.5 M, Port $7.5 M. Everything productive is
subsidised; Bank, Market, Hospital, News Agency, Military Academy, Supply Depot
and Prison Camp are not. `GameVars.subsidynames` =
`{Politics, Economy, Agriculture, Industry, Military, Diplomacy, N/A, N/A}`.

> **The two auto-manage flags are named inversely to their effect** —
> `automanageconstructionp` gates the *subsidised* loop and
> `automanageconstructiong` the *unsubsidised* one — but `BuildingList` ~602
> shows tab 0 lists `subsidytype < 0` and writes `…g`, tab 1 lists
> `subsidytype >= 0` and writes `…p`, so the UI wiring is consistent and only
> the field names mislead.

### Ten subsidy-effect constants are shipped and never read

`campaignprefs.txt` holds a consecutive block under *"Policies & Subsidies"*
describing what each subsidy slider *does*. **None of the ten is read
anywhere in the assembly** — each has exactly two references, its declaration
and its loader line:

| Pref | Config label | Value |
| --- | --- | --- |
| `improvedpricesdiplomacy` | Improved prices factor for full diplomacy subsidies | 1.50 |
| `interventionlevelincrease` | Improvement of intervention level per year of full diplomacy subsidies | 0.5 |
| `improvedproductivitycompanies` | Improved productivity of industries for full diplomacy subsidies | 1.5 |
| `improvedproductivityfarms` | Improved productivity of farms for full diplomacy subsidies | 1.5 |
| `upkeeptransportsfactor` | Upkeep cost factor of transports for transportation subsidies | 0.5 |
| `iiptransportcapacityfactor` | Increased IIP transport capacity for transportation subsidies | 1.5 |
| `railroadconstructionspeedfactor` | Increased railroad construction speed for transportation subsidies | 2.0 |
| `blockadingefficiencytradewar` | Increased blockading / blockade-running efficiency for trade war subsidies | 2.0 |
| `supportcivilorder` | Decrease factor of support events for full civil order subsidies | 0.5 |
| `intelligencefactorcivilorder` | — | — |

Two more in the same neighbourhood are also dead: `ratingmodifiers[3]` and
`agriculturalintervention[4]`, each filled by a loader loop and read nowhere.
The live ones are `basicsubsidiescost[8]`, `agriculturalsubsidies[5]`,
`additionalrecruitssubsidies` and `timetoreachrecruitstarget`.

> **Three of those categories no longer exist.** `GameVars` declares
> `subsidy_transportx = 4`, `subsidy_tradewarx = 5` and
> `subsidy_civilorderx = 7` with an `x` suffix, and ids 4 and 5 are **reused**
> by `subsidy_military = 4` and `subsidy_diplomacy = 5`. The cost array's own
> config label names the slots *"(politics, economy, agriculture, industry,
> military, diplomacy, n/a, n/a)"* and costs the last two at 0, and
> `GameVars.subsidynames` ends in `N/A, N/A`. Transport, Trade War and Civil
> Order were cut; their effect constants stayed in the file.
>
> **The remaining four are effects of categories that do exist** — the
> Diplomacy slider's price and intervention bonuses, and the industry and farm
> productivity bonuses — and they are dead too. What a subsidy slider now does
> is buy **funding** in its category (`AdjustSubsidyFunding`,
> `subsidyfunding[]`), which pays for projects and for the construction
> budgets of §3, plus one direct effect: politics raises `maxpolicies`
> (`Policies.cs:375`) and military feeds `additionalrecruitssubsidies`
> (`GameVars.cs:4436`). The direct-multiplier design these ten belong to is
> gone.
>
> **Railroads are funded from Industry.** `GameVars.subsidyidrailroads = 3` is
> `subsidy_industry` (`RailroadList.cs:293`, `BattleUnits.cs:302`), which is
> what the official manual says. The manual's six categories match the code
> exactly; it is only the leftover prefs that mislead a modder. See
> [14](14-supply-and-logistics.md) §8a.

> **The variety damper is order-dependent.** `num3` starts at 1 and is divided
> by `1 + CountListMatches(lastmonthscompanyfoundings, k)` **inside** the loop
> over candidate types, so the divisor accumulates across every type examined
> and low building-type ids are systematically favoured.

`IsRatingOkForConstruction`, `IsRatingOkForRecruitment` and
`IsRatingOkForWeaponPurchases` share one formula,
`currentrating / ratingnotches.Length > 1 − minimumrating…`, with all three
prefs at **0.45** — so **private construction, recruitment (and ship
construction) and weapon orders all stop together at notch 15 (CCC+)**. The
tooltips phrase it as "below B−", which matches. `ShipRecruitingList` ~494
renders it as *"Congress permits construction … while the debt rating … is
below"*, inverting the sense; `CampaignMainPanel` ~2804 has it right.

`availablecapital` is painted only by **banks**
(`bankinfluenceonavailablecapital` = {0.50, 1.00} by level, × `1 + level(96) ×
project_subsidizebanks`, `range: 3`), is **never blurred**, and feeds private
wealth, foundation probability and `GetMaintainanceCosts`
(`× interp(capital, 1 → reductioncostsfullcapital)`).

---

## 4. Transport capacity is the trade constraint

`IIP.GetTransportCapacity(usecondition, simulateallconnections, shift)`:

```
cap = TransportCapacityFactor
    × (1 + railroadtransportcapacityfactor)        if any connected railroad is complete
    × <policy multipliers>                          see below
    × (1 + GetProjectLevel(99) × project_infrastructure)
    × (1 + GetMarketInfluencesNearby())
    × transportcapacityports                        if harbour
    × (1 + (BuildingLevel + shift) ^ transportcapacityportlevel)   if harbour
    × condition                                     if usecondition
    × GetTransportCapacityAdjustmentInfra()
```

`transportbottlenecks` and `GetTcapFactor()` expose where the network is
binding; `transcapperalliance[,]` is a static per-alliance cache.

### Policies key off `GameObject` names

```csharp
if (((gameObject.name == "Trade_SantaFe") | (gameObject.name == "Trade_OregonTrail"))
    && Policy.GetStatusByPolicyID(72))        num *= GamePrefs.gowesttransportcap;
…
if (((gameObject.name == "Trade_Butterfield") | (gameObject.name == "Trade_Arizona")
   | (gameObject.name == "Trade_Pacific")) && Policy.GetStatusByPolicyID(172))
                                              num *= GamePrefs.slaverytothewesttransportcap;
```

Policies 72, 79, 30 (Union) and 179, 172 (Confederate) are wired to
**specific scene object names**.

> **This is worse than the state-name strings in
> [17-politics-and-support](17-politics-and-support.md) §5.** Those keyed off
> config data; these key off **Unity scene object names**. Renaming a trade
> node in the scene silently disables the policy, and a modder authoring a new
> trade route **cannot** attach these policy effects without reusing the exact
> names. There is also a `northernroutes` list that policy 30 checks by
> membership, which is the better pattern the others should have used.

A leftover `_ = gameObject.name == "Port_Cleveland";` — a discarded comparison
— sits in the same method, i.e. dead debug code.

---

## 5. Macro: debt → rating → interest

`Economy.UpdateMacroEconomy(manualctime)`, called from the campaign data tick.

```
economyFactor = GetCurrentEconomyFactor()
yearScale     = Interpolate([1860,1861,1862,1863,1864,1865,1870], now, ratingmodifieryear)

// per alliance: credit rating
burden = −surplus × 1.25 + max(1, debt × 4)
if (AI side) burden /= sqrt(max(1, usedcampaignagressiveness))        // AI handicap
notch  = highest n with burden/1e6 >= ratingsurplustodebt[n] × yearScale
notch += alliance.ratingmodifier                                      // policies move this
notch += floor(GetProjectLevel(97) × project_creditrating)
clamp to [0, ratingnotches.Length − 1]

// per alliance: interest rate
base   = max(0.01, interestratebase × (1 + (economyFactor − 1) × interestrateleverageeconomy))
rTerm  = (rating/100) ^ interestratelverageratingexp × interestrateleverageratinglinear
target = (base + rTerm) / (1 + GetFundingImprovementThroughBanks())
interest drifts toward target by a random step each update
```

> **Fiscal policy has teeth.** Deficit and debt set a credit-rating notch,
> which sets the interest rate, which compounds the debt. `ratingmodifier` is
> exactly what the policy switch adjusts — e.g. the Southern Pacific Railroad
> arm in [09-policy-and-project-effects](09-policy-and-project-effects.md)
> ends with `ratingmodifier += southernpacificrailroadratingmodifier`. So the
> policy → rating → interest → treasury chain is a real feedback loop, and
> project 97 (credit rating) and the bank-funding term are the levers against
> it.

Note the interest rate **drifts stochastically** toward its target rather than
jumping, via `Random.Range(target − i×1.01, target − i×0.99)`.

### The boom/bust cycle is an authored curve

`GetCurrentEconomyFactor()` interpolates a `List<EconomyCycle>` of
`(date, economyfactor)` pairs against the campaign date. So the economic cycle
is **data, not simulation** — a scripted curve, and therefore trivially
moddable once its source file is identified.

### Credibility decoded

`Economy.GetEconomyFactors(alliance)` returns seven values, matching the
`CampaignStrategy` display:

| # | Factor | Source |
| --- | --- | --- |
| 0 | Composite | weighted sum of 1–6 ÷ 10 |
| 1 | Weapon technology | `(avgGuns + avgRifles × 2) × 20` |
| 2 | Corporate profits | `alliance.corporateprofits` |
| 3 | Domestic trade | `alliance.tradevolume` |
| 4 | Foreign trade | `alliance.volumeimports` |
| 5 | Private income | `privateincome × totalpopulation × baseprivateincome` |
| 6 | **Credibility** | `(ratingnotches.Length − currentrating) × 5` |

> **`Alliance.credibility` is an inverted credit-rating notch** — that closes
> the open question from
> [17-politics-and-support](17-politics-and-support.md) §9.

The composite divides each term by a **hardcoded magic constant**
(`/6e6`, `/7.5e5`, `/7.5e5`, `/1.5e6`):

```csharp
num4 = (num + corporateprofits/6000000f + tradevolume/750000f
        + volumeimports/750000f + num2/1500000f + num3) / 10f;
```

> These weightings are **not in config**, so the relative importance of
> economic factors in the strategic assessment cannot be tuned without
> patching. And the result feeds the `GetBalance` sliders
> ([17](17-politics-and-support.md) §6), which are themselves read from UI
> widgets.

---

## 6. Where the economy reaches out

| Target | Mechanism |
| --- | --- |
| **Supply** | Depot throughput scales with `max(0.1, 1 − localPrice) ^ buysupplygoodspriceratio`; army purchases call `AddTradeData` and move prices ([14](14-supply-and-logistics.md)) |
| **National morale** | Reason code 34, "Private Wealth" ([17](17-politics-and-support.md) §3) |
| **Treasury** | `GetUnitCost` charges army upkeep; `impressment` pays only a fraction |
| **Weapons** | `ConsumeWeapons`, `GetAdditionalWeaponCost` — arms procurement draws on goods |
| **Strategic balance** | Via `GetEconomyFactors` into the strategy sliders |
| **Recruitment** | Indirectly, through state support and `project_farmmechanizationrecruitment` |

And the inputs: policies (rating modifiers, transport capacity, tariffs),
projects (97 credit rating, 99 infrastructure, 113 farm mechanization, 118
training manuals), blockades (`UpdateBlockades`, `lossestradewarfare`), and
raids (`AddShortages`, `ResetShortages`).

---

## 7. `GamePrefs` keys

**Pricing:** `scurvepricingC1`, `scurvepricingC2`.

**Macro:** `interestratebase`, `interestrateleverageeconomy`,
`interestratelverageratingexp` *(sic)*, `interestrateleverageratinglinear`,
`ratingsurplustodebt[]`, `ratingnotches[]`, `ratingmodifieryear[]`,
`baseprivateincome`, `project_creditrating`.

**Transport:** `railroadtransportcapacityfactor`, `transportcapacityports`,
`transportcapacityportlevel`, `project_infrastructure`, `gowesttransportcap`,
`unionpacificrailroadtransportcapacity`, `northernroutestcap`,
`southernpacificrailroadtransportcapacity`, `slaverytothewesttransportcap`.

**Supply linkage:** `buysupplygoodspriceratio`, `supplytogoodsratio`,
`maximumsupplydepotgoods[]`, `supplydepotspeedperlevel[]`,
`maximumtimetobuildupsupplystock`, `initialsupplydepotstockfactor`.

---

## 8. Open questions

1. ~~**Where `EconomyCycle` is loaded from.**~~ **Resolved:** the tail of each
   scenario's `battledata.dat`, read by `BattleUnits.import_battledata` (~10101)
   as a count followed by (date, factor) pairs, and written back by
   `ImportExportUnitData` ~542, so it is carried in saves. All six shipped
   sublevels hold the same seven points — `1850:1.15, 1853:0.90, 1856:0.91,
   1859:1.08, 1861:1.07, 1865:0.92, 1870:1.00` — interpolated linearly on
   `GetCampaignFloatingDate()`. `currenteconomyfactor` has exactly **two**
   consumers: the interest-rate base (§5) and `Town` civilian consumption
   (~730). `GetEconomyCycleText()` buckets it at 0.93 / 0.97 / 1.00 / 1.04 /
   1.08 into the six `GameVars.economycycletext` labels, so the label pins the
   factor to ±0.02. Translated in
   [the-war-economy](../mechanics-findings/the-war-economy.md) §7.
2. **`UpdateEconomyAllianceData` and `UpdateEconomyEstimates`** (~145 and ~35
   lines) hold the actual production/demand aggregation. The *structure* is
   mapped here; the aggregation arithmetic is not.
   What is now known is the publication step: the per-town sweep accumulates
   into `privateincometemp` / `totalpopulationtemp` / `averageworkforceleveltemp`
   / `averagesupporttemp` and publishes all four when the cursor wraps
   ([23](23-game-state.md) §9.1). `privateincome` is therefore a **per-capita**
   figure, `averagesupport` is population-weighted and counts only towns held
   by a belligerent, and `averageworkforcelevel` carries a
   `unemploymentmultiplier` the config itself labels "for stats purposes".
3. **`UpdateFilterMaps`** (~270 lines) builds the spatial maps and is run
   `GamePrefs.workforcemapupdates` = **10** times per frame
   ([10](10-simulation-tick.md) §4) — the most expensive economy code. The
   *container* is `FilterMap`, the engine-wide 2-D field type documented in
   [21-ai-battle](21-ai-battle.md) §8, including the byte quantisation and the
   fact that `Clear()` is a buffer swap, not a wipe. **The `availableworkforce`,
   `slavery` and `availablecapital` arithmetic is now traced above**
   ("The workforce map"); what remains untraced is `transportbottlenecks`,
   `marketcapacity`, `hospitals`, `tradeandsupply` and `supply`.
4. ~~**Taxation.**~~ **The revenue model is resolved** in
   [23-game-state](23-game-state.md) §7.2: `revenue_pa(i) = base(i) * tax[i] *
   taxcaps[i] * taxrevenuemodifier[i]` over five types, with
   `taxrevenuemodifier = {8, 5, 1, 2.5, 0.7}` and every cap set exclusively by
   policy — income and corporate tax ship at cap **0** and are unusable until
   the Revenue Acts. `GetCurrentTaxValuePa(-1)` omits
   `taxrevenuemodifier[corporate]` that the per-type branch includes. What is
   still untraced is the resource-level plumbing: `excisetax_tmp` /
   `tariffs_tmp` on `Economy.resources[].productalliancedata[]` and
   `Resource.RewriteTaxValues`.
5. **Blockade and trade warfare — now largely resolved.** `UpdateBlockades` is
   **private on `IIP`** (~621), not on `Economy`. It rebuilds `blocked` from
   scratch each pass over `unitsinrange`: a non-fleet with `cavalryorders == 2`
   blockades any non-trade node; a fleet with `fleetorders == 1` blockades
   `id_harbor`; a fleet with **perk 6** also blockades a trade node and sets
   `priceadjustment = priceincreasessearaiders[level]`. Each contributor adds
   `blockadeefficencyvalue × rangeFactor × (1 + level(106) ×
   project_blockadeefficiency)`, where `rangeFactor` is
   `blockadeefficiencyfactorcombatrange` inside `buglerange` and 1 outside, and
   **perk 5** multiplies. Then `blocked /= 1 + level(106) ×
   project_blockaderunnerefficiency` — **project 106 sits on both sides of the
   same contest** — an enemy `lettersmarque` imposes a floor on harbours, and
   the result is clamped to 1.

   **Consumption** is `IIP` ~927: `flow *= 1f - Mathf.Max(blocked,
   connectediips[j].blocked)`, so a connection is throttled by its **worse**
   end. ~993 books the shortfall: `(num10 - num9) * price * tariff rate` into
   `AddTradeWarfare(1, …)` and `lossestradewarfare`, gated on
   `blocked + connected.blocked > 0 || (condition < 1 && condition > 0)`.

   > **So `lossestradewarfare` is forgone *tariff revenue*, not lost trade
   > value**, it scales with the alliance's own tariff rate, and it conflates
   > blockade with node damage. A nation with zero tariffs reports zero
   > blockade losses however hard it is squeezed.

   **Two defects.** The **perk-8** reduction
   (`blocked *= effectoonblockadewithinradius[level]`) and the `orgrunning`
   modifier (`blocked *= orgrunningtradewar`) are both **inside** the loop over
   `unitsinrange`, so they compound once per unit in range and scale only the
   contributions accumulated so far — making the result depend on list order.
6. **`Town` vs `IIP` vs `CBuilding`.** All three appear as trade
   counterparties with their own stock; the division of responsibility is
   unclear.
7. **`GetMarketInfluencesNearby()` and `Economy.marketcapacity`** — a spatial
   market-density term, untraced.
8. **The composite weightings in `GetEconomyFactors`** are hardcoded; whether
   anything else reads those magic divisors is unchecked.
