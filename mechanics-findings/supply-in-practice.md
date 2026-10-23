# Supply in practice

What a depot is for, why an army can starve inside friendly territory, and
what running short actually costs — in days, in men and in morale points.

**Translates:** [`14-supply-and-logistics`](../code-findings/14-supply-and-logistics.md),
with the condition consequences from
[`15-attrition-and-condition`](../code-findings/15-attrition-and-condition.md).
**Summary:** [summary-logistics](../code-findings/summary-logistics.md).
Conventions, including the Derived / Observed / Untested tags, are in
[`README.md`](README.md).

---

## 1. There are two supply systems, and they share nothing but a number

| | Campaign map | Battlefield |
| --- | --- | --- |
| Where supply comes from | depots, economic nodes, raiding | **a map-edge entry point** |
| Rooted at | each formation, independently | **corps level** |
| How a shortage arrives | continuously, as stock drains | **once a night**, at the overnight resupply |
| What cuts it | distance, price, node condition, forced march | an enemy unit standing on the line |

Both write the same four-slot stock array, and both count it in **man-days**:

| Slot | What | Who actually spends it |
| --- | --- | --- |
| 0 | Small-arms ammunition | infantry and cavalry |
| 1 | Artillery ammunition | artillery |
| 2 | **Provisions** | everyone |
| 3 | **Forage** | cavalry and artillery |

> **Skirmishers and scouts draw provisions and nothing else** on the campaign
> map — the supply tooltip shows a scout detachment a forage row it never
> spends from, and shows skirmishers an ammunition row they never spend from
> either. On the battlefield they *are* issued both, and the fill percentage is
> computed as if they were not, so a corps with many detachments reads 100 %
> supply for slightly longer than it should
> ([14](../code-findings/14-supply-and-logistics.md) §1, §3a).

A full load lasts, with nothing coming in:

| | Time to empty |
| --- | --- |
| Provisions or forage | **11 days** |
| Ammunition | **44 days** |

So on the campaign map **food is the clock and ammunition is almost never the
problem** — unless you have just fought, which is the one thing that empties
the slow-filling slot (§3).

---

## 2. Your supply percentage is your worst stock, not your average

The campaign supply figure is the **minimum** of the four ratios of
(what is arriving) to (what is being consumed)
([14](../code-findings/14-supply-and-logistics.md) §3):

> **An army swimming in ammunition is "out of supply" if its forage is
> short.** The headline percentage tells you the binding constraint's value and
> not which constraint it is, so the number is only actionable once you open
> the breakdown and read all four.

There is a second figure, used in exactly one place and computed differently:
the **average** across the types the unit actually uses. That is the one the
overnight morale penalty reads (§5), which is why the morale hit is gentler
than the headline suggests.

---

## 3. Three sources, and only one of them is fast

Refilling a brigade from empty, at a good price and full node condition
([14](../code-findings/14-supply-and-logistics.md) §3a):

| Source | Provisions / forage | Ammunition |
| --- | --- | --- |
| **Level-3 supply depot in range** | **0.9 days** | 3.0 days |
| Level-1 supply depot | 1.8 days | 6.1 days |
| Foraging from an adjacent economic node | 3.2 days | 10.8 days |
| Foraging from a node 2,000 away | 24 days | 81 days |
| Foraging from an **enemy-owned** node | ×10 slower | ×10 slower |

Three things follow directly.

**A level-3 depot in range is worth about four adjacent economic nodes**, and
twenty-seven distant ones. Nothing else in the logistics model comes close.

**Ammunition is always 3.3× slower than food.** A brigade that has just fought
a battle is short of precisely the thing that takes longest to replace, so
consecutive engagements are limited by ammunition long before they are limited
by rations.

**A freshly captured railhead supplies at a fraction of its eventual rate**,
because node condition *divides* the foraging rate. At condition 0.3 it
delivers at a third of normal — the mechanism behind infrastructure taking
time to start working after you take it.

### The army attaches to the best node, not the nearest

Node choice scores `transport capacity ÷ distance ÷ average price`, so a
railhead — worth **×11** on capacity once its railroad is complete — outranks a
plain road junction up to eleven times further away, and cheap goods pull an
army the same way
([14](../code-findings/14-supply-and-logistics.md) §3a, §8).

> **Transport capacity never enters your supply rate directly.** It decides
> which node you attach to and how fast the economy restocks that node; you
> feel it through the node's prices. The manual's "this rate of supply is
> dependent on the transport capacity of the IIPs" is true as an outcome and
> false as a mechanism.

### Raiding is the third source

A formation under the **Raid** order converts plundered goods into its own
stock, which is the only way a unit gains supply when depots and nodes are both
out of reach. Cavalry is twice as efficient at raiding as infantry (2.0 against
1.0) and guards are slightly worse at stopping it than raiders are at doing it
(1.7 against 2.0), so **cavalry is the better tool on both sides of that
exchange**. Guarding subtracts from raiding and the result is floored at 0.1,
so guards can reduce a raid to a tenth but never stop it.

> **Raid and guard orders cost 50× in reinforcement delay** — a multiplier the
> manual does not mention. A raiding formation is effectively cut off from
> replacements while the order stands.

---

## 4. What a depot is actually for

A supply depot is an **economy building** holding real economy goods, not an
abstract supply pool. Three facts decide how to use it
([14](../code-findings/14-supply-and-logistics.md) §4):

- **Level matters twice over.** Capacity goes ×3 from level 1 to level 3 and
  throughput ×2, so a level-3 depot both holds three times the stock and pushes
  it out twice as fast. **Project 115, Supply Reform, is what unlocks levels 2
  and 3** — the upgrade is blocked outright without it.
- **An unlinked depot supplies nothing.** A depot with no connected economic
  node has no source to buy from.
- **A burning depot empties 300× faster than it fills.** A successful raid on a
  depot destroys its contents almost immediately rather than degrading them,
  and it takes **27 days** to fill one from empty at a comfortable price.

A depot also buys its stock through the market, at a price-sensitive rate, so
**an expensive local market throttles the depot as well as the army**. The
Logistics Reform project (100) takes 5 % off supply costs per level — a money
saving, not a speed one.

> **The AI raids your logistics using the same lookup your units use to find
> it.** `GetClosestDepotsAndTowns` serves both, so a depot placed where your
> army can reach it easily is a depot the enemy can find easily.

---

## 5. Why an army starves in friendly territory

Five separate reasons, none of which is "the enemy cut my supply line".

**1. Forced march cuts supply to zero.** Not to a quarter — to nothing. A unit
on forced march consumes its stock as normal and receives neither depot nor
local supply, and its reinforcements stop too. The config ships the intended
version of this rule — *"Max supply ratio of units retreating or on forced
march"* at 0.25 — and it is **parsed and never read**
([14](../code-findings/14-supply-and-logistics.md) §3a).

**2. Local prices throttle foraging.** The local draw is multiplied by a price
term that falls once relative prices pass 0.7, down to a floor of 25 %. An army
sitting on an expensive market forages at a quarter rate while the market is
nominally full of goods — and **its own buying is what raised the price**,
since each draw registers as local demand.

**3. Node condition divides the rate**, so a raided or newly captured node
supplies slowly even while it shows stock.

**4. Bad weather throttles depots but not foraging.** The weather factor
multiplies the depot draw only, so **bad weather shifts an army's supply mix
towards living off the land** — which is the slower source.

**5. The Confiscation Act makes foraging on enemy soil slower, not faster.**
The factor it sets multiplies the *time* to resupply, so the Act delivers a
**13 % slowdown** where its own label, and the manual, promise a 15 %
improvement
([14](../code-findings/14-supply-and-logistics.md) §3a).

> So the common failure is not interdiction. It is **a forced march, or an
> expensive market, or a wrecked railhead** — and the first of those is
> something the player does deliberately without being told the cost.

---

## 6. What a shortage costs

### Condition: a cliff at 5 %, not a slope

Condition degrades only once a stock falls to **5 % or below** of the unit's
establishment. At 20 % provisions a brigade is completely fine; at 5 % it starts
coming apart at −40 per year for men and −30 for horses, buffered by drill
([what kills your men](what-kills-your-men.md) §6).

> **There is no graduated penalty.** Nothing at all happens between 100 % and
> 6 %, and the 5 % threshold is a hardcoded literal rather than a config value.
> The practical consequence is that **supply percentages above 5 % are not a
> warning, they are a countdown** — and at 11 days to empty from full, the
> countdown is short.

### Morale: nothing above half, catastrophic below a fifth

Once a night, every brigade takes `−(1 − average supply)^7 × 0.7`. The seventh
power makes it one of the sharpest curves in the game
([14](../code-findings/14-supply-and-logistics.md) §5):

| Average supply | Morale lost that night |
| --- | --- |
| 70 % | 0.0 points |
| 50 % | 0.5 |
| 40 % | 2.0 |
| 30 % | **5.8** |
| 20 % | **15** |
| 10 % | **34** |
| 0 % | **70** |

> **Half-empty wagons cost nothing and a tenth-full army collapses
> overnight.** There is no useful middle. And because this reads the *average*
> of the types a unit uses while the headline figure is the *minimum*, a
> brigade showing 20 % supply is usually taking the 40 %-row penalty, not the
> 20 % one.

### The overnight batch is where battles really cost men

The same end-of-day pass applies two more hits, and both are flat per night
rather than scaled by time
([14](../code-findings/14-supply-and-logistics.md) §5,
[15](../code-findings/15-attrition-and-condition.md) §2c):

**Casualty shock.** `−(ratio × (1 − experience) + ratio × (1 − drill))^5 × 3`,
where ratio is the day's losses as a share of the unit. Fifth power again:

| Day's losses | Green, undrilled | Half-trained | Elite **and** fully drilled |
| --- | --- | --- | --- |
| 10 % | 0.1 points | 0.0 | **0** |
| 20 % | 3 | 0.1 | **0** |
| 30 % | **23** | 0.7 | **0** |
| 40 % | **98** | 3 | **0** |

> **Full drill plus full experience removes the overnight casualty shock
> entirely**, at any casualty level, because both terms multiply by
> `(1 − stat)`. For green troops it is nothing below a fifth of the unit lost
> and the whole morale bar above two fifths.

**Desertion, and this is the one nobody expects.** The overnight pass runs
`drive² × 0.2 × strength` men, **once, flat**, with none of the campaign
version's rate scaling. A 3,000-man brigade at 50 % morale under an average
general loses **54 men in a single night of a battle**; at 20 % morale it loses
**216**.

> The campaign formula for the same brigade and the same morale works out at
> about **0.03 men a night**. **A night spent in the field during a battle
> costs roughly two thousand times the desertion of a night on the campaign
> map at identical morale.** Multi-day battles are not just bloody, they
> dissolve units — and the lever is morale, squared.

---

## 7. Battlefield supply is a different game

On the battlefield there are no depots and no markets. Supply runs in a chain
from a **map-edge entry point** down the command hierarchy, and the chain is
rooted at **corps** level — the config names the ladder outright
(13 Brigade, 14 Division, 15 Corps, 16 Army) and the shipped root is 15
([14](../code-findings/14-supply-and-logistics.md) §2).

**Distance is almost irrelevant.** The decay is `distance^0.1` weighted at
0.15, which gives:

| Distance to the next link | Factor |
| --- | --- |
| 1 | 1.00 |
| 100 | 0.95 |
| 500 | 0.93 |
| 2,000 | **0.92** |

What costs you is **depth and your commander**, because every link multiplies:

| | Supply state |
| --- | --- |
| Corps, 800 from the entry point | 0.93 |
| Division under it, 200 away | 0.87 |
| Brigade under that, 50 away | 0.83 |
| …under a commander with 50 % administration | **0.72** |
| …under a commander with 0 % administration | **0.62** |
| A **detached** brigade with no corps above it | ×0.5 on top |

### Interdiction is binary, and it is judged at nightfall

If **any** enemy unit stands on the straight line from a unit to its parent,
its supply state is 0 — not reduced, zero — and a unit must also be inside its
commander's radius to draw supply at all.

And the chain is only recomputed **while the end-of-day cycle is running, or
while you have the supply overlay switched on**
([14](../code-findings/14-supply-and-logistics.md) §2,
[10](../code-findings/10-simulation-tick.md) §8):

> **You only need to hold the enemy's supply line at nightfall.** A line cut
> during the afternoon and cleared by dusk costs nothing; a line cut at dusk
> costs that brigade its entire night's resupply. Defending, the corollary is
> to clear your rear before the day ends — and the supply figures you read
> mid-battle are stale unless the overlay is on.

### The corps wagon train, and why battles end on the third day

A corps carries a fixed reserve and distributes it each night
([14](../code-findings/14-supply-and-logistics.md) §2):

| | Days carried |
| --- | --- |
| Small-arms ammunition | **1.0** |
| Artillery ammunition | **1.0** |
| Provisions | **2.5** |
| Forage | **2.5** |

Every night each brigade eats **one full day** of provisions, and cavalry and
artillery one of forage. Then the corps hands out, per brigade,
`deficit × that brigade's own supply state`, pro-rated if the pool cannot cover
every request.

> **Two and a half nights of food, and the corps cannot buy more.** A
> battlefield has no resupply source beyond the entry point, so a third night
> puts the army at the 5 % cliff. That, and not an explicit timer, is why long
> battles end.
>
> **Routed and surrendering brigades are skipped entirely** by the overnight
> distribution, so a unit that broke during the day gets nothing that night and
> starts the next day empty. **A brigade whose supply line was cut gets
> nothing either, however full the corps' wagons are** — the distribution is
> scaled by each brigade's own state, so interdiction does not slow resupply,
> it cancels it.

---

## 8. Readiness: the stat that decides what you are allowed to do

Readiness is a separate operational-tempo figure on each formation, and the
game expresses every restriction against four **hardcoded quartiles** rather
than against the number itself
([14](../code-findings/14-supply-and-logistics.md) §10):

| Band | Readiness | What becomes impossible |
| --- | --- | --- |
| Excellent | above 75 % | — |
| Mediocre | 50–75 % | **forced march is cancelled** |
| Low | 25–50 % | **raid, guard, patrol and fleet orders are cancelled**; movement onto enemy soil is refused; garrisons cannot be deployed |
| Deficient | 25 % or below | **offensive stance is impossible** |

Note where the lines fall: **garrison deployment needs above 50 %**, and
**moving onto enemy soil is refused below 50 %** unless the destination is
within regrouping range — an exemption the manual does not mention.

**Consumption is dominated by whose ground you are on.** Marching burns −8.5
per year in a fully loyal state and **−25 in a hostile one**: about 43 days
from full to empty against 15. An aggressive commander (×0.35) and a
war-experienced nation (×0.50) together cut that by 83 %. Winter snow
multiplies it by 4.

> **Rail and sea transport cost *less* readiness than marching, not more.** The
> embarked multiplier is 0.75 on a per-year rate, so an hour on a train costs
> three quarters of an hour of marching and covers far more ground. The manual
> states the opposite.

**Recovery is dominated by formation size.** The base 6.8 per year is
multiplied by `3 ÷ (attached units)^0.43`:

| Formation | Size multiplier |
| --- | --- |
| 4-brigade corps | **1.65×** |
| 8 brigades | 1.22× |
| 12-brigade army | 1.03× |

> **Split a big army into corps to rest it** — it is worth about 60 % more
> recovery per day. With a good administrator and well-drilled troops the full
> stack reaches about 22 per year, or **16 days from empty to full**; a large
> army under a poor commander and short of supply takes months.
>
> **Winter quarters slow readiness recovery by a quarter.** They help condition
> and drill; they do not help readiness.

Low readiness then feeds back out: command range −33 %, combat range −25 %,
order delay up to ×11, movement speed down to ×0.3, reinforcement speed ×0.25
— and withdrawal losses, where **every unit takes skirmish casualties
withdrawing at readiness 0 against one in five at full readiness.**

---

## 9. What to do differently

- **Build depots, upgrade them to 3, and take Supply Reform (115) early.** A
  level-3 depot resupplies four times faster than the best economic node and
  twenty-seven times faster than a distant one. Nothing else in the logistics
  model is worth as much.
- **Never leave a formation on forced march when it is low.** Forced march
  consumes supply, receives none, and stops reinforcements — and the game gives
  no indication that the second and third of those are happening.
- **Read all four supply bars, not the headline.** The percentage is your worst
  stock. If it is low and you do not know which one, you cannot fix it.
- **Treat anything above 5 % as a countdown, not a cushion.** Nothing happens
  until 5 %, and from full that is eleven days away for food.
- **Fight on your own ground when you can.** State support halves readiness
  consumption, and enemy-owned nodes forage ten times slower — and the
  Confiscation Act does not fix the second of those, it makes it worse.
- **Move by rail.** It costs less readiness per hour than marching, not more.
- **Split armies to rest them.** A four-brigade corps regains readiness 60 %
  faster than a twelve-brigade army.
- **Cut the enemy's supply line at dusk, and clear your own rear before it.**
  Battlefield interdiction is evaluated during the end-of-day cycle, so only
  the position at nightfall counts.
- **Plan battles around 2.5 nights of food**, and around ammunition rather than
  rations between battles — ammunition refills 3.3× slower than food.
- **Drill removes the overnight casualty shock entirely** and halves the
  condition cost of a supply failure. It is the cheapest insurance against
  everything in this file.
- **Pull broken brigades out of the line before nightfall.** Routed units are
  skipped by the overnight resupply and start the next day empty.

---

## 10. Still open

- **What campaign-map distance means in miles.** The foraging rates are keyed
  to raw map units, with 0 and 2,000 as the two calibration points, and the
  map's unit-to-mile scale is a scene value that needs an asset dump to read
  ([14](../code-findings/14-supply-and-logistics.md) §11). Until that is pinned
  the distance figures in §3 are **relative, not absolute**.
- **Fleet supply** takes a separate harbour-based path that has not been
  traced, so nothing in this file applies to a squadron.
- **Reinforcement and replacement flow** — how a formation's strength is
  actually restored, and how long a request takes. This is the largest
  remaining gap in the whole logistics subsystem.
- **Whether the forced-march cut-off is intended.** The unread
  *"max supply ratio … on forced march"* pref of 0.25 says the design wanted a
  cap; the code cuts supply entirely. Not decidable from the code.
- **Whether a brigade acting as its own supply root can resupply at all.** A
  detached brigade becomes its own top supply unit, and the overnight
  distribution loop excludes the top unit from its own handout. **Untested** —
  the test is a detached brigade in a multi-day battle, watching whether its
  ammunition recovers overnight.
