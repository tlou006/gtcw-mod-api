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

1. **Where `EconomyCycle` is loaded from.** It is an authored curve and
   therefore a high-value, low-risk mod target, but the parse site was not
   found — likely `campaignprefs.txt` or a scenario file.
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
3. **`UpdateFilterMaps`** (~270 lines) builds the spatial resource/workforce
   maps and is run `GamePrefs.workforcemapupdates` times per frame
   ([10](10-simulation-tick.md) §4) — the most expensive economy code, and the
   arithmetic is still untraced. The *container* is now known: all ten maps
   (`fertilitymapfilter[]` per resource, `availableworkforce`, `slavery`,
   `availablecapital`, `transportbottlenecks`, `marketcapacity`, `hospitals`,
   `tradeandsupply`, `supply`) are `FilterMap` components, the engine-wide 2-D
   field type documented in [21-ai-battle](21-ai-battle.md) §8 — including the
   byte quantisation and the fact that `Clear()` is a buffer swap, not a wipe.
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
5. **Blockade and trade warfare.** `UpdateBlockades`, `lossestradewarfare`,
   `GetTradeRatios`. The naval side of the economy. `AddTradeWarfare` itself is
   resolved — a 10-slot accumulator with slot 0 as the recomputed total, no
   clamping, and two orphaned statements where a guard used to be
   ([23](23-game-state.md) §8).
6. **`Town` vs `IIP` vs `CBuilding`.** All three appear as trade
   counterparties with their own stock; the division of responsibility is
   unclear.
7. **`GetMarketInfluencesNearby()` and `Economy.marketcapacity`** — a spatial
   market-density term, untraced.
8. **The composite weightings in `GetEconomyFactors`** are hardcoded; whether
   anything else reads those magic divisors is unchecked.
