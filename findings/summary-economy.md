# Summary — Economy

**Covers:** [18-economy](18-economy.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Three layers. **`IIP` nodes** are a spatial network of local markets — each
with its own stock, prices, transport capacity and trade ledger — and they are
what the previous three subsystems kept pointing at. **`Resource`** gives goods
a production recipe expressed as a *boolean expression* over inputs, per-consumer
demand coefficients, and twelve monthly harvest modifiers. **Macro** turns
deficit and debt into a credit-rating notch, which sets the interest rate,
which compounds the debt. Pricing is a logistic curve on scarcity, and large
transactions are correctly priced on their own average rather than the starting
stock.

## `IIP` finally decoded

Nine node types — Small Town, **Port**, Bridge, Ferry, Mountain pass, Indian
settlement, Trading route, invisible, Railroad line — and **every one is a
market**: `stock[]`, `price[]`, `producedpcspa[]`, `demandedpcspa[]`, a
`historicaltrades` ledger with counterparties, `condition`,
`transportbottlenecks`, railroad connections.

Counterparties can be other IIPs, `CBuilding`s, `Town`s or `Regiment`s — so
factories, towns, nodes **and armies** all trade in the same ledger. That is
why a depot with `closestiip == null` supplies nothing: no market to buy from.

Demand is population-driven and interpolated between `TradePopulation1850` and
`TradePopulation1870`. `DemandFocus*` lets a scenario impose **time-windowed,
side-specific demand spikes** on named goods — the main economy scripting hook.

## Pricing

```
scarcity = clamp(1 − stock / minpricereached, 0, 1)
rel      = scarcity / (scarcity + exp(scurvepricingC1 − scurvepricingC2 × scarcity))
price    = max(0.01, rel) × maxprice
```

| | |
| --- | --- |
| **`minpricereached` is a *quantity*, not a price** | It is the stock level at which price bottoms out — the abundance threshold. It also serves as the capacity denominator in depot maths and the stock scale everywhere else. The name has misled these notes before. |
| **Price is a logistic curve on scarcity**, two prefs | Shallow at both extremes, steep in the middle: **moderate shortages move prices more than severe ones.** |
| **Transactions are priced on their own average** | Buying a lot genuinely costs more per unit. Proper slippage modelling, and unusual care for a game economy. |

> **Performance note.** `GetLocalPrice` walks the transaction in **steps of
> ±1**, repricing each step. It is called per unit from `GetUnitCost` inside
> the campaign data tick, so a large army buying supplies drives a lot of
> iterations. Measure this before blaming a mod for a campaign-tick stall.

## Recipes are boolean, and substitution is real

`Config/resources.dat` gives each good four (input, amount) pairs joined by
three connectors — **0 = OR, 1 = AND**. The parser enumerates the satisfying
combinations into alternative recipes, then
`CheapestPreGoods(position, …)` picks the cheapest satisfiable one **at local
prices**.

> **A factory has no fixed bill of materials.** It picks the cheapest valid
> recipe at its nearest market, which is the mechanism behind the scarcity
> feedback loop seen from the supply side.

Two nice data conventions: records are **self-indexing** (the id selects the
array slot, so file order is free), and **`maxprice < 0` means "derive from the
recipe"** — `Σ(input.maxprice × amount)`. So a mod adding a good mid-chain can
leave `maxprice` at `-1` and get a consistent price for free.

`harvestingmodifier[12]` is **one modifier per month**, with `isharvested`
inferred from the data rather than flagged, and fertility is spatial via
`AssignFertilityMaps`.

## Macro: a real debt spiral

```
burden = −surplus × 1.25 + max(1, debt × 4)      (÷ sqrt(aggressiveness) for the AI)
notch  = lookup against ratingsurplustodebt[] × a per-year scale
notch += alliance.ratingmodifier + floor(GetProjectLevel(97) × project_creditrating)

interest → (base(economyCycle) + (rating/100)^exp × lin) / (1 + bankFunding)
```

> **Fiscal policy has teeth**, and `ratingmodifier` is exactly what the policy
> switch adjusts — the Southern Pacific Railroad arm in
> [summary-effects](summary-effects.md) ends with
> `ratingmodifier += southernpacificrailroadratingmodifier`. So
> **policy → rating → interest → treasury** is a closed feedback loop, with
> project 97 and bank funding as the levers against it.

Interest **drifts stochastically** toward its target rather than jumping.

**The boom/bust cycle is an authored curve** — `List<EconomyCycle>` of
`(date, factor)` pairs interpolated against the campaign date. Data, not
simulation, and therefore a high-value low-risk mod target *if* its source file
can be found (it hasn't been yet).

## Credibility decoded

`GetEconomyFactors(alliance)` returns the seven numbers the strategy panel
shows, and the last one closes an open question from
[summary-politics](summary-politics.md):

> **`credibility = (ratingnotches.Length − currentrating) × 5`** — an inverted
> credit-rating notch.

The composite that feeds the strategic balance divides each term by a
**hardcoded magic constant** (`/6e6`, `/7.5e5`, `/7.5e5`, `/1.5e6`), so the
relative weight of economic factors in the strategic assessment **cannot be
tuned without patching** — and the result then flows into the `GetBalance`
sliders, which are themselves read from UI widgets.

## The worst modding trap found so far

Transport-capacity policies key off **Unity scene object names**:

```csharp
if (((gameObject.name == "Trade_SantaFe") | (gameObject.name == "Trade_OregonTrail"))
    && Policy.GetStatusByPolicyID(72))  num *= GamePrefs.gowesttransportcap;
```

Policies 72, 79 (Union) and 179, 172 (Confederate) are wired to
`"Trade_SantaFe"`, `"Trade_OregonTrail"`, `"Trade_Butterfield"`,
`"Trade_Arizona"`, `"Trade_Pacific"`.

> Worse than the state-name strings in politics, which at least keyed off
> config data. **Renaming a trade node in the scene silently disables the
> policy, and a modder authoring a new trade route cannot attach these effects
> without reusing the exact names.**
>
> Policy 30 shows the better pattern the others should have used — it tests
> membership of a `northernroutes` list instead.

A leftover `_ = gameObject.name == "Port_Cleveland";` sits in the same
method — dead debug code.

## Where the economy reaches

| Target | Mechanism |
| --- | --- |
| Supply | Depot throughput × `max(0.1, 1 − price) ^ pref`; army purchases move prices |
| National morale | Reason code 34, "Private Wealth" → the loss condition |
| Treasury | `GetUnitCost` army upkeep; impressment pays a fraction |
| Weapons | `ConsumeWeapons`, `GetAdditionalWeaponCost` |
| Strategic balance | `GetEconomyFactors` → the strategy sliders |

Inputs: policies (rating, transport capacity, tariffs), projects (97 credit
rating, 99 infrastructure, 113 farm mechanization, 118 training manuals),
blockades, and raids via `AddShortages`.

## Still open

1. **Where `EconomyCycle` is loaded from.** Authored curve, high mod value,
   parse site not found.
2. **`UpdateEconomyAllianceData` / `UpdateEconomyEstimates`** — the
   production/demand aggregation arithmetic. Structure is mapped, the maths is
   not.
3. **`UpdateFilterMaps`** (~270 lines, run `workforcemapupdates` times per
   frame) — the spatial resource and workforce maps, and the most expensive
   economy code. All ten of them are `FilterMap` components, the shared 2-D
   field type described in [21-ai-battle](21-ai-battle.md) §8; the arithmetic
   that fills them is still untraced.
4. **Taxation** — `excisetax`, `tariffs`, `RewriteTaxValues` exist; the player
   controls and their effects were not followed.
5. **Blockade and trade warfare** — the naval side.
6. **`Town` vs `IIP` vs `CBuilding`** all hold stock and trade; the division of
   responsibility is unclear.
