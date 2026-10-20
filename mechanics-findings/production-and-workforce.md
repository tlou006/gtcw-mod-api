# Production and workforce

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Translates:** [18](../code-findings/18-economy.md),
[22](../code-findings/22-policy-and-project-catalogue.md),
[23](../code-findings/23-game-state.md),
[21](../code-findings/21-ai-battle.md) §8
**Companion to:** [the-war-economy](the-war-economy.md), which covers prices,
trade, taxes and debt. This file is the supply side: where goods come from and
what stops them.

[the-war-economy](the-war-economy.md) §3 left one thing unanswered, and it was
the biggest hole in it: **output is multiplied by the workforce available where
the building stands**, and nothing explained where that number comes from or
how a player affects it. This file closes that.

The short version:

> Your production base is limited by a **32×32 grid of local labour** that towns
> fill and buildings and **recruitment** drain. Every soldier you have ever
> raised permanently removes five people from his home state's labour pool —
> casualties included, because the dead are never returned. Slave labour is
> exempt from the whole mechanism and costs a third as much. New industry
> appears on its own, but only where a good's price is already above 72.5 % of
> maximum, only while a credit rating holds, and only for building types the
> game is willing to place — **mines and ports are not among them.**

---

## 1. The workforce map

There is one field, 32×32 cells over the whole campaign map, rebuilt from
scratch on a rolling cycle. Three things paint into it:

| Painter | Contribution |
| --- | --- |
| A **town** | `+ √(representing population) × 0.002` |
| A **small town** | `+ √50,000 × 0.002` = **+0.447** (a fixed figure) |
| A **building** | `− √(workers for its level) × 0.002 × 5` = `− √workers × 0.01` |

Every value is **square-rooted before painting**, which compresses the whole
field: doubling a town's population raises its contribution by only 41 %.

Worked out, the scale is sobering:

| Painter | Paints |
| --- | --- |
| Town of 200,000 | +0.894 |
| Town of 100,000 | +0.632 |
| Small town | +0.447 |
| Town of 25,000 | +0.316 |
| Level-0 plantation (150 workers) | −0.122 |
| Level-4 factory (1,620) | −0.402 |
| Level-4 farm (3,240) | −0.569 |
| Level-4 mine (8,100) | −0.900 |
| **Level-4 plantation (12,150)** | **−1.102** |

> **A single fully upgraded plantation outweighs a city of 100,000.** Large
> upgraded buildings, not population, are what empties a region's labour pool.

### How local is "local"

Painting goes into **one cell**, and the published map then gets **one Gaussian
blur pass**. So a town or a building affects its own cell strongly and its
immediate neighbours weakly, and nothing further. The in-game economy alert
saying to avoid "creating new or upgrading existing companies nearby" means
*nearby* literally.

Three mechanical details worth knowing, from the shared field type
([21](../code-findings/21-ai-battle.md) §8):

- A cell's value is stored in **one byte**, so it is quantised to 1/255 and
  **saturates at 1.0**. Piling more towns into an already-full cell does
  nothing.
- Paints into the same cell **add**, and are clamped at paint time to a floor of
  **0.15** and a ceiling of 1.0.
- **That floor is applied before the blur, not after.** An isolated building
  surrounded by empty cells is clamped to 0.15 in its own cell and then
  averaged against its empty neighbours, so the *published* value can fall
  below 0.15. The 0.15 is a paint-time clamp, not a guaranteed minimum.

---

## 2. What a building actually gets

```
relativeworkforce = min(1, mapValue × 2.0 × (1 − slaveRatio) + 1.0 × slaveRatio)
```

and output is multiplied by it directly. Two consequences:

**The scale saturates at a map value of 0.5.** Anything above that is wasted on
production — a building in a cell reading 0.5 and one reading 1.0 produce
identically.

**Free labour never goes below 30 %** where the paint-time floor survives the
blur, because `0.15 × 2 = 0.30`. A building in the emptiest part of the map
still runs at roughly a third.

The game warns you below 50 %, which is a map value of 0.25.

| Map value | Free-labour building runs at |
| --- | --- |
| 0.50 or more | 100 % |
| 0.40 | 80 % |
| 0.30 | 60 % |
| 0.25 | 50 % ← the alert threshold |
| 0.15 | 30 % |

### Upgrading a building triples its labour bill

Workforce per level is a **×3 geometric ladder** for every building type:

| Building | L0 | L1 | L2 | L3 | L4 |
| --- | --- | --- | --- | --- | --- |
| Plantations | 150 | 450 | 1,350 | 4,050 | 12,150 |
| Mines | 100 | 300 | 900 | 2,700 | 8,100 |
| Farms | 40 | 120 | 360 | 1,080 | 3,240 |
| Iron Works, Port | 30 | 90 | 270 | 810 | 2,430 |
| Factories, Foundries, Mills, etc. | 20 | 60 | 180 | 540 | 1,620 |
| Bank, Academy, Telegraph, News Agency | 10 | 30 | 90 | 270 | 810 |

**Level 4 needs 81 times the labour of level 0.** The drain on the map only
grows as √workers, so it is 9× rather than 81× — but it is still the dominant
term, and it is why a cluster of high-level buildings strangles itself.

> **What to do with this:** spread upgrades across regions rather than stacking
> levels in one place. Two level-2 factories in separate towns produce more
> than one level-4 factory in a town that cannot staff it, because the level-4
> site's own drain pushes its workforce down the scale above.

---

## 3. Slave labour is exempt from the entire mechanism

Look again at the formula in §2. The slave fraction contributes `1.0 ×
slaveRatio` **unconditionally** — it does not consult the workforce map at all.
A building worked entirely by slaves runs at 100 % in the emptiest region on
the map.

```
slaveRatio = the building type's slavery demand
           × the owner's maxSlaveryFactor          (starts at 1.0)
           × (slavery legal in this state ? Confederate support here : 0)
```

The building type's demand, from the shipped data:

| Demand | Building types |
| --- | --- |
| 95 % | **Plantations** |
| 80 % | Saltworks |
| 75 % | **Farms**, **Mines** |
| 50 % | Brickworks, Mills |
| 40 % | Lumber Mills |
| 30 % | Port |
| 25 % | Supply Depot |
| 20 % | Foundries, Market |
| 15 % | **Factories** |
| 10 % | **Iron Works**, News Agency |
| 0 % | Bank, Hospital, Fort, Military Academy, Telegraph Station, Prison Camp |

Three things follow, and together they are the most consequential asymmetry in
the economy.

**1. Plantations are effectively immune to labour shortage, and farms and mines
nearly so.** At 95 % demand and full slavery factor in a high-support state, a
plantation reads about 0.96 regardless of the map. The same cell gives a
Factory (15 % demand) about 0.40.

**2. A slave-worked building still drains the map at full strength.** The
negative paint uses the building's *total* workers for its level, with no
slavery adjustment. So a level-4 plantation removes 1.10 from its cell —
crushing the free-labour buildings around it — while being almost unaffected
itself.

**3. Slaves cost a third as much.** Wage cost is **$1.00 per free worker per
year** against **$0.33 per slave**, charged against the building's profit.

> **What to do with this:** in a slavery state, site plantations, farms and
> mines anywhere, and keep factories and iron works in or next to large towns.
> Playing the Confederacy, the labour constraint barely applies to your raw
> materials and applies fully to your manufacturing — which is the historical
> shape of the problem, arrived at mechanically.

### The slavery factor is a slow, two-sided clock

`maxSlaveryFactor` starts at 1.0 and is moved only by policy:

- **Underground Railroad** subtracts **0.10 once** and then **0.05 per year**,
  continuously — and it applies to **both alliances**, not just the researcher.
- Slavery-restriction policies subtract a further **0.10** each.

There is **no floor on the value anywhere in the code.** Over a four-year war
the Underground Railroad alone takes it from 1.00 to about 0.70, which drops a
plantation's slave ratio from 0.95 to 0.67 — pushing it onto the workforce map
for a third of its labour and tripling the wage cost on that third.

**And the third term is political.** The slave ratio scales with **Confederate
political support in the building's state**, so as a state's Confederate
support falls, its plantations and mines lose their labour exemption. Capturing
territory does not immediately end the mechanism — residual Confederate support
keeps it running — but eroding support does.

---

## 4. Every soldier you ever raised costs five workers, permanently

A town's painted contribution uses `representingpopulation`, not raw
population:

```
representingpopulation = max(0, (state population − recruitedTotal × 5)
                             × this town's share of the state)
```

The multiplier is 5 by design — the config explains it as soldiers
representing the working part of the population. The town tooltip reports the
result honestly: *"Fielded soldiers reduce workforce by X %"*.

What the tooltip does not say is that **`recruitedTotal` is a running total that
casualties never reduce.**

- Raising a unit **adds** its strength to the state's recruited count.
- Disbanding a unit **returns only its surviving strength**.
- The **killed**, the **deserted**, and the **wounded of a disbanded unit** are
  never returned at all.

> **So casualties have a permanent economic cost on top of the men themselves.**
> Every soldier killed or deserted removes **five people** from his home state's
> labour pool for the rest of the campaign, which lowers every workforce value
> near every town in that state, which lowers production at every building
> there. A bloody year does not just cost you an army; it costs you the
> industry that would have replaced it.
>
> **What to do with this:** spread recruitment across states rather than
> drawing repeatedly from your best industrial ones, and treat a high-casualty
> offensive as an industrial decision as well as a military one. This is also
> the strongest economic argument for avoiding needless assaults that the game
> never makes explicitly.

---

## 5. Fertility is applied twice

Agricultural and extractive goods can carry a **fertility map** — a texture per
resource, loaded from the campaign's assets, giving a 0–1 multiplier by
location.

It is applied to output **squared**. The code caches the sampled value and
then multiplies by the cache *and* the sample:

```csharp
num2 = fertilityfactor[resourceid];                  // the cache
if (num2 == -1f) { num2 = GetFertility(resourceid);  // sample it
                   fertilityfactor[resourceid] = num2; }
if (num2 >= 0f) num *= fertilityfactor[resourceid] * num2;   // both
```

Since the map runs 0 to 1, squaring always reduces:

| Fertility shown | Effect on output |
| --- | --- |
| 1.0 | ×1.00 |
| 0.8 | ×0.64 |
| 0.6 | ×0.36 |
| 0.5 | ×0.25 |
| 0.3 | ×0.09 |

> **What to do with this:** good ground matters far more than the fertility
> overlay suggests. Half-decent land yields a quarter of the output, not half.
> Place farms and plantations on the best cells available rather than the
> nearest convenient ones — the difference between 0.8 and 1.0 fertility is a
> 36 % output loss, not 20 %.

Whether the double application is intended cannot be settled from code; the
arithmetic is certain. **Which goods have fertility maps at all needs an asset
dump** — they are Unity textures named by resource id, and a modder cannot add
one without touching the asset bundle.

---

## 6. The whole output formula

Putting §2 to §5 together with [the-war-economy](the-war-economy.md) §3:

```
output(good) = the building type's production factor for its level
             × (1 + effortShare^0.4)        the specialisation bonus
             × effortShare                   your allocation of the building
             × the good's abundance threshold × 30
             × relativeworkforce             §2 — the labour term
             × Condition                     damage
             × fertility²                    §5 — if the good has a map
             × the month's harvest modifier   if the good is seasonal
             × the good's own production multiplier
             × elapsed time
```

Everything a player controls sits in three of those terms: **where** the
building is (workforce, fertility), **what level** it is (production factor, at
the cost of tripled labour), and **what you point it at** (effort share, twice
over). Condition and the harvest calendar are the war and the seasons.

---

## 7. Where new industry comes from

Industry grows by itself, through a two-stage process. Stage one runs **one
node per frame** and builds a probability; stage two runs **twice per game day
per alliance** and commits.

### Stage one: the probability at each node

For each infrastructure node, the game finds the good with the **highest local
relative price**, provided it clears **72.5 %** of maximum — the deep end of
the price curve in [the-war-economy](the-war-economy.md) §1. If no good is that
expensive, nothing is proposed. Then:

```
probability = (localPrice − 0.725) / 0.275          how far past the gate
            × nationalValue(good)                   see below
            × availableWorkforce(here)              the §1 map
            × fertility(here, that good)             NOT squared here
            × (1 + 0.5 × availableCapital(here))    banks
            × 0.18                                   a flat damper
            × (1 + 0.35 × corporateTaxSlider)        see below
            × (1 + 0.25 × 0)                         the dead subsidy term
```

`nationalValue` is a per-alliance, per-good index,
`√( (averagePrice / maxPrice) × demand ÷ max(production, stock/5) )`, clamped
to 1 — so **a local shortage only attracts industry if the good is scarce
nationally too.** A single expensive town in an otherwise well-supplied country
will not get a factory.

Two findings in that formula:

> **The subsidy term is dead.** The variable it multiplies is assigned `0f` on
> both branches of the preceding `if`/`else`, so the config's "Influence of
> subsidies on company foundation probability" (1.25) can never apply. Subsidies
> still matter — see stage two — but not here.

> **The corporate tax slider is worth +35 % company foundations, and it reads
> the slider rather than the effective rate.** Because the slider starts at
> maximum and the *cap* is what policy gates, you get the full bonus from the
> first day of the campaign even though corporate tax collects nothing until
> Revenue Act III.
>
> **This corrects the advice in [the-war-economy](the-war-economy.md) §5.**
> Zeroing the **income** tax slider before the Revenue Acts is still free
> money. Zeroing the **corporate** tax slider is not: it saves 5 % of
> administration cost and costs you 35 % of your industrial growth. **Leave the
> corporate slider at maximum.**

The accumulated probability across all nodes is also the **land-sales tax
base** — multiplied by $2.5 M per unit. So your land-sales revenue is a live
readout of how fast your industry is expanding, and it dries up when prices are
comfortable.

### Stage two: what actually gets built

The per-alliance pass picks the best candidate and builds it, subject to gates
that matter a great deal:

**Government-funded types are gated on their subsidy being fully funded.** The
shortfall test is `1 − (funded / cost)³`, and construction only proceeds when
that reaches zero — i.e. **when the category's accumulated funding covers the
full cost.** At 50 % funded, 87 % of the cost still counts as missing.

And **every productive building is government-funded**, out of just two
categories:

| Category | Types and their cost |
| --- | --- |
| **Agriculture** | Farms $2.5 M, Plantations $3.0 M, Mills $1.63 M, Saltworks $1.5 M |
| **Industry** | Factories $4.5 M, Iron Works $3.75 M, Mines $3.25 M, Foundries $2.5 M, Lumber Mills $1.75 M, Brickworks $1.5 M, Port $7.5 M |
| *(unsubsidised)* | Bank, Market, Hospital, News Agency, Military Academy, Supply Depot, Prison Camp |

> **If the Agriculture and Industry subsidy sliders are not funded, your
> productive base cannot grow at all.** Those two sliders are not a bonus, they
> are the construction budget for the entire economy.

**Private, unsubsidised types are gated on your credit rating** instead.

**Mines and ports never grow by themselves.** The foundation routine only
considers building types flagged for automatic placement, and **Mines and Port
are not flagged** (nor are Bridges, Forts, Telegraph Stations or the military
camps). So your coal, iron ore, lead, niter, salt and copper output — and your
harbour transport capacity — will **never** expand on its own. Those must be
placed and upgraded by hand.

**Upgrades are preferred to new sites.** If an existing building of the chosen
type sits within 100 units, it is upgraded instead — which, per §2, triples its
labour bill. This is how a region quietly strangles itself.

**And it only runs for you if auto-manage is on.** Two checkboxes in the
building list, one per tab, gate the whole thing for the player's alliance:
the private tab's checkbox controls unsubsidised types, the government tab's
controls subsidised ones. Turn one off and that category simply stops growing —
there is no other organic growth path. *(The two internal flags are named the
opposite way round from their effect, which is a code wart rather than a
gameplay one: the UI wiring is consistent.)*

### Three cliffs at one credit rating

Three separate systems share the same test, `notch / 26 > 0.55`, which bites at
**CCC+ and worse** — the tooltips phrase it as "below B−":

- **private construction stops**
- **recruitment stops** (and new ship construction)
- **weapon orders stop**

And at the very bottom notch, bond issuance stops too
([the-war-economy](the-war-economy.md) §6).

> **What to do with this:** the credit rating is not a score, it is a set of
> switches. Falling to CCC+ does not make the war expensive, it makes the war
> *unfightable* — no recruits, no weapons, no new industry, all at once. That
> is the real reason to care about the 1861 budget.

---

## 8. The other side of the ledger: slack labour makes people poor

The same map feeds a second system, in the opposite direction. A town's
**available workforce level** is the map value doubled and clamped — the same
number a building reads — and it is treated as **unemployment**:

```
privateWealth = (1 − unmet consumption)                  floored at 0.25
              × interp(workforceLevel: 1.6 → 0.9)        ← high slack, low wealth
              × (1 + 0.25 × availableCapital)            banks again
              × the alliance's public wealth factor
```

and `incomeTax = privateWealth × representingPopulation × slider × cap × 1.5`.

So abundant labour is **good for your factories and bad for your citizens**.
The wealth multiplier runs from **1.6** where there is no slack to **0.9**
where there is complete slack. Private wealth in turn drives income tax
revenue, and national morale through reason code 34.

| | Production | Private wealth |
| --- | --- | --- |
| Map 0.50+ (full slack) | 100 % | ×0.9 |
| Map 0.25 | 50 % | ×1.25 |
| Map 0.15 | 30 % | ×1.6 |

> **Production wins this trade.** It spans 30–100 % (a 3.3× range) against the
> wealth factor's 0.9–1.6 (1.8×), so a region with surplus labour is the better
> outcome overall. But it is worth knowing that a fully employed region is
> reporting higher private wealth and paying more income tax, so the two halves
> of the economy panel will seem to disagree about whether a place is doing
> well. They are measuring different things.

**Available capital** is the one input that helps both halves at once. Banks
paint it — `0.50` at level 1 and `1.00` at level 2, over a radius of three
cells, raised further by **project 96 Subsidize Banks**. It multiplies private
wealth by up to **1.25**, multiplies company foundation probability by up to
**1.5**, and reduces building maintenance costs. It is the only term in the
whole production story with no downside.

---

## 9. What to actually do

1. **Keep the corporate tax slider at maximum** even before the Revenue Acts
   (§7). It buys 35 % more industrial growth for 5 % of administration cost.
   Zero the **income** slider only.
2. **Fund the Agriculture and Industry subsidy sliders.** They are the
   construction budget for every productive building in the game, and
   construction waits for full funding (§7).
3. **Build mines and ports by hand** (§7). Nothing else will.
4. **Spread upgrades across regions instead of stacking levels** (§2). Level 4
   needs 81× the labour of level 0.
5. **Keep factories and iron works near large towns; put plantations, farms and
   mines wherever you like** if you hold slavery states (§3).
6. **Spread recruitment across states**, and remember that casualties
   permanently cost five workers each in the dead man's home state (§4).
7. **Site farms on the best fertility you can find** — the effect is squared,
   so 0.8 land yields 64 %, not 80 % (§5).
8. **Never let the credit rating reach CCC+** (§7). Three systems switch off at
   once.
9. **Build banks.** Cheapest compounding effect in the economy (§8).
10. **Watch land-sales tax revenue** as your industrial-growth indicator (§7).

---

## 10. For modders

- The whole labour field is **one 32×32 byte map with one blur pass**. Raising
  `workforcemapresolution` is the single biggest change available to the
  production model, and it is a config number.
- **The fertility squaring** (§5) and **the two production-project bugs**
  ([the-war-economy](the-war-economy.md) §3) are one-token fixes each.
- **The dead subsidy term** in the foundation probability (§7) is a
  single assignment; restoring it would make the subsidy sliders matter twice,
  which may be more than intended.
- `maxSlaveryFactor` has **no clamp**, so a long campaign with several
  slavery policies can in principle drive it negative. Nothing in the shipped
  policy set gets there, but a mod adding a fourth reduction should add a floor.
- **The variety damper in stage two is order-dependent.** It divides a running
  factor by the recent-founding count of *each type it examines in turn*,
  rather than per type, so low building-type ids are systematically favoured.
- `CheckCompanyFoundations_x` (69 lines) has **no callers** — an orphaned
  earlier version of the sweep.
- Fertility maps are `Resources.Load` textures named by resource id, so new
  ones cannot be added without rebuilding the asset bundle.

---

## 11. Open questions

1. **Which goods have fertility maps.** Needs an asset dump; the code loads
   `Campaigns/<folder>/FertilityMaps/<resourceId>` and silently skips missing
   ones. This joins the asset-dump backlog in
   [code-findings/README](../code-findings/README.md).
2. **What one map cell is worth in miles.** The map is 32×32 over the terrain,
   so cell size is `terrain size ÷ 32`, but the campaign terrain's dimensions
   were not read. `FilterMap.GetFieldRealWorldFieldSize()` exists and would
   answer it directly.
3. **How much the fertility squaring actually costs in a real campaign.**
   *(Untested.)* It depends entirely on the distribution of values in those
   textures, which is question 1.
4. **Population growth.** `eogrowthrates` is set from
   `1/√(average available workforce) − 1.2`, scaled by a fluctuation speed of
   0.04, so **population grows where labour is scarce and shrinks where it is
   abundant** — a stabilising feedback on the whole system. The exact
   integration into `UpdatePopulation` was not followed.
5. **`GetMarketInfluencesNearby` and the market capacity map** remain untraced
   ([18](../code-findings/18-economy.md) §8), as does whether Market buildings
   affect production or only transport.
