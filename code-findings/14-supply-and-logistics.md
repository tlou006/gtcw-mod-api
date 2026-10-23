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

### The shipped numbers, and what they weigh

`unitprefs.txt`, under *Supply & End of Day*. The first label is unusually
generous: it **decodes the echelon ladder** in passing.

| Pref | Value | Config label |
| --- | --- | --- |
| `supplytoplevelbf` | **15** | *"Top unit for starting supply chains on battlefield (according to unit type: 13=Brigade, 14=Division, 15=Corps, 16=Army), using overridden state"* |
| `supplyfactormalusnotopunit` | **0.5** | applied when no top unit of that echelon exists |
| `supplydistancemodificatorbf` | **0.1** | the exponent on distance |
| `supplydistanceweightingbf` | **0.15** | how much the distance term counts |
| `supplyadminweightingbf` | **0.25** | commander administration's share |

> **Battlefield supply chains are rooted at corps.** A division links to its
> corps, a brigade to its division, and the corps to the map edge.

**Distance barely matters.** With an exponent of 0.1 and a weighting of 0.15
the factor is nearly flat:

| Distance | `distanceFactor` |
| --- | --- |
| 1 | 1.000 |
| 100 | 0.945 |
| 500 | 0.931 |
| 2 000 | **0.920** |

What costs a unit is **depth and its commander**, because the links multiply:

| | `supplystate` |
| --- | --- |
| Corps, 800 from its entry point | 0.925 |
| Division under it, 200 away | 0.868 |
| Brigade under that, 50 away | 0.825 |
| …× admin 0.5 (`1 − 0.25 × 0.5`) | **0.722** |
| …× admin 0.0 | **0.619** |
| Detached, no corps above it | a further ×0.5 |

### `overridesymbol` is an echelon override

Closing §11's first open question. `CalculateSupplyLine` opens with

```csharp
int num = overridesymbol;
if (num == 0 && unittyp > 0) num = unittyp;
```

so `overridesymbol` is the **echelon this unit is treated as**, falling back to
its `unittyp` when unset. Against `supplytoplevelbf = 15` that gives three
branches: `num < 15` links to the parent, `num == 15` links to an entry point
and sets `istopsupplyunit`, and `num > 15` (army, army group) becomes a top
supply unit **only** if the `flag` loop finds an attached unit below corps
level — in which case it also collects the ×0.5 malus.

Two slips in the same method, both inert but worth knowing before anyone
patches it:

- `if (overridesymbol > GamePrefs.supplytoplevelbf) supplystate = -1f;` is the
  first statement, and `supplystate = 0f` eight lines later overwrites it
  unconditionally. A dead store.
- Inside the `flag` loop, `int num2 = …overridesymbol;` is followed by
  `if (num == 0 && unittyp > 0) num = unittyp;` — which normalises the **outer**
  echelon rather than `num2`. The guard cannot fire there (`num > 15` already),
  so the line does nothing, but it means an attached unit whose
  `overridesymbol` is 0 is compared as 0 and always sets `flag`.

### 2a. The corps wagon train, and the overnight handout

Three methods in `BattleUnits` do all the work, and together they are the whole
battlefield supply economy.

**`CalculateOriginalSupplyStates()`** (called once, from
`BattleController` ~284) seeds each **top supply unit** with
`groupsupplyreserve[k] × scenarioFraction × relevantStrength`, then calls
`ResupplyAllUnits(1f)` so everyone starts full.

| `groupsupplyreserve` | Days carried | Config label |
| --- | --- | --- |
| 0 — small-arms ammunition | **1.0** | *"Group supply reserve in days -> ammunition infantry"* |
| 1 — artillery ammunition | **1.0** | *"…ammunition artillery"* |
| 2 — provisions | **2.5** | *"…provision"* |
| 3 — forage | **2.5** | *"…forage"* |

scaled by the scenario file's per-group `ammoinf / ammoart / provisions /
forage` fractions, so a scenario can start a corps short.

**`EatProvisionsAndForage(consumeratio = 1f)`** runs once a night, at the
`eodcycle` 0 → 1 transition ([10](10-simulation-tick.md) §8), and subtracts
**one full man-day** of provisions per brigade — plus one of forage for
`unittyp` 1, 2 **and 4**.

> **Two and a half nights of food, and a battlefield has no other source.** A
> third night puts the army at the 5 % cliff (§5). That, rather than an
> explicit limit, is what ends long battles.

**`ResupplyAllUnits()`** runs at the `eodcycle` 1 → 2 transition and is where
`supplystate` finally does something. Per top supply unit, in two passes:

```
request[k]  = Σ over its brigades of max(strength − stock[k], 0) × brigade.supplystate
ration[k]   = min(1, poolStock[k] / max(1, request[k]))
each brigade: stock[k] += max(strength − stock[k], 0) × supplystate × ration[k]
              pool[k]  -= the same amount
```

skipping any brigade that is `isrouted` or has `surrenderstate > 0`.

> **A cut supply line means no resupply at all, not slower resupply.** The
> handout is scaled by each brigade's own `supplystate`, which interdiction
> sets to 0 — so a brigade with an enemy on its line receives nothing however
> full the corps' wagons are. **Routed brigades are skipped outright** and
> start the next day on whatever they had left.
>
> `ration[k]` is a straight pro-rata when the corps cannot cover every
> request, so a short corps spreads the shortfall evenly across its brigades
> rather than prioritising any of them.

**And the chain is only recomputed at nightfall.** `CalculateSupplyLine()` is
called from `Regiment.Update`'s battle branch under
`if ((bunits.eodcycle > 0) | supplyiconactive)` — so outside the end-of-day
cycle it runs **only while the player has the supply overlay switched on**.

> **Battlefield interdiction is judged at dusk.** A line cut during the
> afternoon and cleared before nightfall costs nothing; a line cut at dusk
> costs that brigade its entire night's resupply. The supply figures shown
> mid-battle are stale unless the overlay is on.

### Which unit types draw what, four ways

The three battlefield methods and the campaign consumption path do not agree on
which `unittyp` values spend from which slot:

| | Ammo slot 0 | Forage slot 3 |
| --- | --- | --- |
| Supply-icon tooltip (§1) | — | 1, 2, **4** |
| Battle allocation (`CalculateOriginalSupplyStates`) | 0, 1, **3, 4** | 1, 2, **4** |
| Battle fill ratio (`groupsupplystate`) | 0, 1 | 1, 2 |
| Battle overnight meal (`EatProvisionsAndForage`) | — | 1, 2, **4** |
| Campaign consumption (§3a) | 0, 1 | 1, 2 |

> So **allocation and consumption include the light types while the fill ratio
> excludes them.** The effect is small and one-directional: a corps with
> skirmishers and scouts is issued ammunition and forage for them that its own
> percentage does not count, so the reading sits at 100 % until a few per cent
> has been spent. Worth knowing before trusting the figure to the point.

`groupsupplystate[k] = stock[k] / relevantStrength / groupsupplyreserve[k]`,
clamped to `[0, 1]` — **closing §11's seventh open question**: the formation
figure is *days carried as a fraction of the full reserve*, not a ratio against
consumption, and it is computed only for top supply units.

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

### 3b. Replacements come down the same pipe

`Regiment.UpdateIIPSupplyLink()` — the method §3a's campaign resupply lives in
— finishes by **filling the formation's brigades back up to establishment**,
from their home states' recruiting pools. Nothing else in the assembly does;
there is no separate replacement system, and no player order triggers it. It
runs on the slow frame-paced cycle
([10-simulation-tick](10-simulation-tick.md) §10) with `num6` as the elapsed
campaign time since the method last ran for this unit.

Per attached brigade:

```
targetSize  = GetMaxUnitSize(alliance, unittyp)            // or unittypesize[]/basicgarrisonsize[]
            × horseartillerysizefactor if horse artillery

time        = Interpolate([0, 2000], refpoint,
                          [refillspeeddistance0, refillspeeddistance2000])
time       /= draftActSpeed × dlc × Interpolate([0,1], readiness,
                                 [reinforcementspeedadjustmentreadiness, 1])
time       /= GetWeatherAdjustment()[4]

men         = targetSize × elapsed / time × supplyTerm
            × prioritysteps[reinforcementpriority]
            × reformrecruitingfactor if reforming
```

then clamped so `men + strength + losses[0] + sick ≤ targetSize`, and drawn
from the home state's pools — volunteers (`recruitingtype` 0) first, drafts
second, each capped at `GetRecruitAmounts`. Each draw calls
`Economy.GetUnitCost(…, payforunit: true, …)` for the men delivered and
`AdjustRecruitingPool`, and a drafted draw also calls
`BattleUnits.AdjustStateSupportDrafts` ([17](17-politics-and-support.md) §5).

| Term | Pref | Value |
| --- | --- | --- |
| Refill time, at the supply node | `refillspeeddistance0` | **1.5** campaign years for a whole unit |
| Refill time, at distance 2,000 | `refillspeeddistance2000` | **4.0** campaign years |
| Readiness floor | `reinforcementspeedadjustmentreadiness` | 0.25 at readiness 0, lerped to 1 |
| Winter / rain | `reinforcementmultiplierwinter` | 0.25 at full snow or full precipitation |
| Priority steps | `prioritysteps` | **{0, 0.5, 1.0}** — low, medium, high |
| Reforming | `reformrecruitingfactor` | ×10, for `timetoreform` = 0.08333 yr (one month) |
| Draft Act I / II | `draftactreplacementspeed` | ×1.5 / ×2.0 |

So a 2,000-man brigade at the best case takes `2000 / 1.5 / 52` ≈ **26 men a
week**, and the table in
[the-manpower-tap](../mechanics-findings/the-manpower-tap.md) §5 works the rest
of the combinations out.

Six findings fall out of the detail.

**1. `prioritysteps[0]` is zero.** The low reinforcement priority does not slow
replacements, it stops them: the whole per-brigade figure is multiplied by 0.
`CampaignMainPanel` ~3676 cycles the setting 0 → 1 → 2 → 0 over three buttons
labelled low/medium/high, so this is reachable in one click and has no other
symptom.

**2. `refpoint` is the distance to the chosen supply node, not to the home
state.** It is initialised to **2000** at the top of the method and overwritten
only when the IIP scoring loop (§3a) picks a node, so a brigade with no node in
range refills at `refillspeeddistance2000` — the worst rate. The config labels
both prefs *"…if distance to home state is 0 / 2000"*, which is not what the
code measures.

**3. The winter penalty ignores winter quarters, twice over.**
`GetWeatherAdjustment()` computes five values and the winter-quarters branch
resets only the first four — snow, precipitation, movement and supply —
leaving index **4**, the reinforcement multiplier, at its weathered value. The
config label is *"Lower reinforcements multiplier during winter months if unit
is not encamped [<1]"*, and encampment is never consulted. Meanwhile
`Regiment.GetReinforcementspeedWinter()` — a public method with **no callers
anywhere in the assembly** — implements the label's rule and disagrees with the
pref as well:

```csharp
// winter = monthCounter <= winterquartersmonthuntil(3) || >= winterquartersmonthfrom(11)
if (!inWinterQuarters && winter) return 0.01f;                             // 1 % of normal
if (inWinterQuarters  && winter) return Mathf.Max(0.01f, 0.25f);           // 25 %
return 1f;
```

So the published rule exists in two implementations: a live one that applies
0.25 to everyone in snow or rain, and a dead one that would apply **0.01** to
anyone outside winter quarters between November and March.

**4. Reforming is the only way to go fast.** `SendToReform()` sets
`reformtime = now + timetoreform` and transfers the unit toward
`nation[state].averageposition`; while `reformtime` is in the future the refill
term uses `supplystate = 1` regardless of actual supply **and** multiplies by
`reformrecruitingfactor = 10`. Effective refill time is 0.15 campaign years
against a one-month window, so a brigade that leaves empty comes back at about
**55 % of establishment**. The flag is cleared by the same loop the moment it
expires.

**5. The Draft Acts do not stack.** `num38` is **assigned** from
`draftactreplacementspeed[0]` by Act I and then from `[1]` by Act II, so the
second Act replaces the first's ×1.5 with ×2.0 rather than compounding to ×3.
The two tests are per alliance against policies 39/40 (Union) and 139/140
(Confederate), and this is the only consumer of the pref.

**6. Replacement equipment is drawn from the connected depots first.**
`GetUnitCost` is handed the same `supplydepotsfirst` list the supply pass built,
so a brigade filling up consumes uniforms, rifles, horses and gun pieces out of
depot stock before anything is bought at market
([18-economy](18-economy.md) for the price path). Replacements therefore
compete with daily supply for the same depot stock, and the purchase enters the
alliance's army-order demand for each of those goods via
`ResourceUsage.AddAODemand` ([23](23-game-state.md) §13).

Two readouts come out of the same loop: `latestreinforcements` is a trailing
average of **men per week** actually delivered, and `latestrecruitmentcostpa`
is the formation's replacement spend per annum. `reinforcementsneeded` is the
raw shortfall across the formation, reset at the top of each run.

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

### 5a. The whole overnight batch, with its shipped curves

`BattleUnits.CheckDesertionsAndMorale()` (`BattleUnits` ~8114) runs **once per
night**, from the `eodcycle` 0 → 1 transition
([10](10-simulation-tick.md) §8). It applies three things in order, and none of
them is scaled by elapsed time — each is a flat per-night hit.

**1. Casualty shock.**

```
r    = Tools.SumUp(losses) / (strength + Tools.SumUp(losses))
hit  = −( r × (1 − Experience()) × expinfluenceofhighcasualtiesinfluenceonmorale
        + r × (1 − GetTraining()) × drillinfluenceofhighcasualtiesinfluenceonmorale )
       ^ casualtiesinfluenceonmoraleexp
       × casualtiesinfluenceonmoralelvg
```

with both influence prefs at **1**, the exponent at **5** and the leverage at
**3**:

| Day's losses | Green, undrilled | Both at half | Elite **and** fully drilled |
| --- | --- | --- | --- |
| 10 % | −0.001 | 0.000 | **0** |
| 20 % | −0.031 | −0.001 | **0** |
| 30 % | **−0.233** | −0.007 | **0** |
| 40 % | **−0.983** | −0.031 | **0** |

> **Full drill plus full experience removes the overnight casualty shock
> entirely**, at any casualty level, because both terms carry `(1 − stat)`. For
> green troops it is negligible below a fifth of the unit lost and the whole
> morale bar above two fifths. Note `losses` here is the **full** six-slot sum,
> prisoners included.

**2. The supply hit.**

```
morale += −( clamp(1 − GetAvgSupplyState(), 0, 1) ) ^ supplyinfluenceonmoraleexp
           × supplyinfluenceonmoralelvg
```

with the exponent at **7** and the leverage at **0.7**:

| Average supply | Morale lost that night |
| --- | --- |
| 70 % | −0.000 |
| 50 % | −0.005 |
| 40 % | −0.020 |
| 30 % | **−0.058** |
| 20 % | **−0.147** |
| 10 % | **−0.335** |
| 0 % | **−0.700** |

> **Supply only hits morale once per day**, not per frame, and it uses the
> **average** supply state here while `supplystate` itself is the **minimum**.
> So the morale penalty is softer than the headline supply figure suggests —
> and `GetAvgSupplyState()` averages only the slots a unit actually uses
> (ammunition plus provisions, ÷2; plus forage ÷3 for `unittyp` 1, 2 and 4).
>
> The seventh power means **half-empty wagons cost nothing and a tenth-full
> army collapses overnight**. There is no useful middle.

**3. Desertion — and it is nothing like the campaign rate.**

```
num7 = max(min(1 − morale − 0.2 × fame − 0.2 × admin, 1), 0)
       ^ moraleinfluenceondesertionsexp (2)
       × moraleinfluenceondesertionslvg (0.2)
       × strength
     → SufferCasualties(num7, …, overridelosstype: 2)
```

Compare the campaign formula
([15](15-attrition-and-condition.md) §2c), which is
`0.5 × drive² × 0.2 × strength × supportFactor × elapsed`: the overnight pass
drops the **0.5 base rate**, drops **`supportFactor`**, and above all drops
**`× elapsed`**. It is a flat `drive² × 0.2 × strength` men, once.

| Morale (average general) | `drive` | Overnight, 3,000-man brigade | Campaign equivalent for one night |
| --- | --- | --- | --- |
| 0.8 | 0.0 | 0 | 0 |
| 0.5 | 0.3 | **54 men** | 0.03 men |
| 0.2 | 0.6 | **216 men** | 0.11 men |

> **A night in the field during a battle costs roughly two thousand times the
> desertion of a night on the campaign map at identical morale.** Whether the
> missing `elapsed` is intended — a battle night *is* a discrete event, so a
> flat hit is defensible — or whether the two formulas were meant to match is
> not decidable from the code. Either way the magnitude is what makes
> multi-day battles dissolve units rather than merely bleed them, and the lever
> is morale, squared.

See [15-attrition-and-condition](15-attrition-and-condition.md) for what
condition then does, and [13-morale-and-cohesion](13-morale-and-cohesion.md)
for campaign morale slots 3–7 (out of food, out of ammo, low condition men /
horses / guns).

---

## 6. `eodcycle` — resolved

`BattleUnits.eodcycle` is an `int` phase counter that gates a surprising amount
of behaviour: it suspends firing ([11](11-combat-resolution.md) §3b), melee
([12-melee](12-melee.md) §1), cohesion updates and fatigue accumulation,
continuous objective points and autosave, and it drives the overnight
attrition, desertion and morale batch (§5a).

**The full seven-state machine is now transcribed in
[10-simulation-tick](10-simulation-tick.md) §8**, including the ordering this
section needed: state 0 → 1 at `hourCounter >= dayendhours[month−1]` fires
`EatProvisionsAndForage()` and `CheckDesertionsAndMorale()`; state 1 → 2 fires
`LevelAmmo()`, `ResupplyAllUnits()` and `AdjustConditionOfArmies()`, and sets
game speed to 1000 to fast-forward the night; state 3 → 4 pauses the game at
dawn. So **the supply batch of §2a and §5a all happens inside states 1 and 2**,
on the positions and supply states the army held at nightfall.

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

1. ~~**`overridesymbol`**~~ — **resolved** in §2: an echelon override falling
   back to `unittyp` when 0, compared against `supplytoplevelbf = 15` (Corps),
   whose own config label names the ladder 13 Brigade / 14 Division / 15 Corps
   / 16 Army.
2. **`IIP`** — "important infrastructure point"? `IIP.id_harbor` is confirmed;
   the full type set and how IIPs relate to towns and depots is not. It is the
   junction between the economy and logistics and deserves its own pass with
   the economy subsystem.
3. ~~**The depot/local split.**~~ **Resolved** in §3a: both draws, the node
   selection rule, the shipped rates in days, and the forced-march cut-off.
4. ~~**`eodcycle`'s full state machine.**~~ **Resolved** in
   [10-simulation-tick](10-simulation-tick.md) §8; §6 records what it means
   here.
5. **`Economy.resid_*` constants and `resids_regularsupply[]`** — needed to
   finish the resource↔supply mapping, and they belong with the economy.
6. **Fleet supply** (`unittyp == 17`) takes an entirely separate harbour-based
   path that was not followed.
7. ~~**`groupsupplystate[]` aggregation**~~ — **resolved** in §2a:
   `stock[k] / relevantStrength / groupsupplyreserve[k]`, clamped `[0, 1]`, on
   top supply units only, so it reads *days carried as a fraction of the full
   reserve* rather than a ratio against consumption.
8. **What "2,000" is in miles.** `supplyspeeddistance2000` is keyed to a raw
   campaign-map distance, and the map's unit-to-mile scale is still unknown —
   `FilterMap.GetFieldRealWorldFieldSize()` returns
   `terrain.terrainData.size.x × mapfactor`, both **scene values**, so this
   needs an asset dump rather than more reading. Until it is pinned, §3a's
   distance figures are relative, not absolute. The battle map has a
   calibration the campaign map lacks: `tooltips_battle.txt` quotes "roughly 30
   yards" and "roughly 150 yards" against weapon ranges.
9. **Why forced march cuts supply entirely** rather than capping it at the
   `maxsupplyratioretreat` = 0.25 the config ships. The early return is
   unambiguous; whether the cut-off or the unread pref is the intended
   behaviour is not recoverable from the code.
10. **`fillratiotarget`** is a per-depot field driving the buy/sell sign in
    `UpdateEconomy`. Whether the player sets it, and its default, were not
    traced.
11. **Whether a brigade acting as its own supply root can resupply at all.** A
    detached brigade becomes `istopsupplyunit` (§2), and `ResupplyAllUnits`'s
    distribution loop skips `componentsInChildren[m] == topUnit` — so the
    brigade holds a pool it is never issued from. Whether `groupstrengthperunittyp`
    gives a lone brigade a pool in the first place was not followed through;
    the test is a detached brigade in a multi-day battle, watching whether its
    ammunition recovers overnight.
