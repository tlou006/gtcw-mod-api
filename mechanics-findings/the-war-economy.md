# The war economy

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Translates:** [18](../code-findings/18-economy.md),
[14](../code-findings/14-supply-and-logistics.md),
[22](../code-findings/22-policy-and-project-catalogue.md),
[23](../code-findings/23-game-state.md)

The economy is the part of this game people give up on. Not because it is
badly made — it is the most carefully built system in the assembly — but
because almost none of it is explained, and several of its most important
numbers are invisible or mean something other than they look like.

So this file is longer than it wants to be. The short version:

> **There is no national market.** Every infrastructure point on the map keeps
> its own stock of all 38 goods and sets its own price from how full it is.
> Goods trickle between neighbours along transport capacity, pulled by price
> gaps. Your army buys at local prices wherever it stands. Your treasury is
> filled by five taxes whose ceilings are set by policy, not by you, and
> emptied automatically — you never choose to borrow. And the boom/bust cycle
> is a seven-point curve authored into the scenario file, identical in every
> campaign, which no decision you make can bend.

---

## 1. Prices are local, and the curve is brutal in the middle

Each node (the game calls them IIPs — towns, ports, bridges, ferries, passes,
trading routes, railroad lines) is a market with its own `stock[38]`. Price
comes from one thing: how full that stock is, relative to a per-good
**abundance threshold**.

```
fill     = stock / threshold          (threshold = "minimum price reached")
scarcity = 1 − fill, clamped to [0,1]
price    = scarcity / (scarcity + e^(3.5 − 7 × scarcity))   × the good's max price
```

Worked out, with the shipped constants, as a percentage of the good's maximum
price:

| Stock, as a share of the abundance threshold | Price |
| --- | --- |
| 100 % or more | 1 % (the floor) |
| 80 % | 2 % |
| 70 % | 7 % |
| 60 % | 17 % |
| 50 % | 33 % |
| 40 % | 55 % |
| 30 % | 74 % |
| 20 % | 87 % |
| 10 % | 94 % |
| empty | 97 % |

**Read that table before anything else in this file.** It explains most of
what feels arbitrary about the economy:

- **Between full and 70 % stock, prices barely move** — 1 % to 7 %. A region
  can lose nearly a third of its stock of something and look fine.
- **Between 60 % and 30 %, price goes up 4½ times.** This is where a shortage
  turns into a crisis, and it happens fast.
- **Below 20 % it flattens again.** A nearly empty market and a completely
  empty one cost about the same. Once you are in the ceiling there is no
  further warning signal — the price has stopped telling you how bad it is.

> **What to do with this:** act on a regional shortage while the price is still
> in the teens. By the time a good is visibly expensive you are already past
> the steep part of the curve, and the price will keep climbing for a while
> even after supply recovers, because the recovery has to climb back through
> the same steep band.

Two more things about pricing:

**Large purchases pay their own average, not the starting price.** Buying
10,000 units reprices against the depleting stock one unit at a time and
charges you the volume-weighted average. Buying a lot genuinely costs more per
unit — this is real slippage modelling, and it is why a big army arriving
somewhere makes its own supply expensive.

**"Minimum price reached" is a quantity, not a price.** It is the stock level
at which the price bottoms out, and it doubles as the scale for almost
everything else — depot capacity, trade volume caps, production output. The
name misleads; see [18](../code-findings/18-economy.md) §2.

### The 38 goods, and the eight that are military

| | Goods |
| --- | --- |
| **Supply goods (8)** | Artillery, Artillery Ammunition, **Forage**, **Horses**, **Provisions**, Small Arms Ammunition, **Uniforms**, **Weapons** |
| Raw / agricultural | Coal, Copper Ore, Cotton, Crops, Fish, Gold, Iron Ore, Lead, Livestock, Niter, Rice, Salt, Sugar, Tobacco, Wood, Wool |
| Manufactured | Bricks, Bronze, Clothing, Flour, Food, Gunpowder, Iron, Leather, Locomotives, Machine Parts, Steel, Textiles, Transport Ships |

The eight supply goods are the ones your depots buy and your army consumes
([14](../code-findings/14-supply-and-logistics.md)). Everything else matters
only as an input to them, as tax base, or as export income.

Raw goods carry an explicit maximum price in the data. **Manufactured goods
derive theirs from their recipe** — the sum of their inputs' maximum prices —
so a manufactured good is automatically worth more than what went into it, and
cost inflation propagates up the chain on its own.

---

## 2. How goods actually move

Trade is **pull-driven, bilateral and slow**. Each node, on its own update,
looks at every node it is connected to and for each good computes an
attractivity:

```
attractivity = (myRelPrice − theirRelPrice × tariffFactor / envoyFactor × noise)
             × (their stock / threshold)          both terms clamped to [0,1]
```

Then the volume actually moved is

```
flow = mean(transport capacity of both ends)
     × elapsed time × 50000
     × attractivity³ / Σ attractivity
     ÷ max(1, distance)
     × the good's threshold
     × (1 − the worse of the two nodes' blockade)
```

Six consequences, all of which show up in play:

**1. A node only ever imports what is dearer at home.** The attractivity is
clamped at zero, so goods never flow from an expensive market to a cheap one.
There is no central distribution: if nowhere near your army is short, nothing
moves toward your army.

**2. Trade concentrates violently on the biggest price gap.** That cube is not
decoration. One good with a gap of 1.0 and nine with gaps near zero moves at
nearly full rate. Ten goods each with a gap of 0.5 move at **about a quarter**
of the total rate — `0.5³/5 = 0.025` each, 0.25 in all. **Broad, shallow
shortages are transported far worse than one sharp one.** A region short of
everything recovers more slowly than a region short of one thing, even with
identical transport capacity.

**3. Distance divides flow directly**, and transport capacity is the average
of both endpoints — so **one bottleneck node poisons every route through it**,
and upgrading a port helps every connection it has.

**4. There is a hard per-tick ceiling** of 2 % of the good's threshold per
connection. Nothing can arrive faster than that, regardless of price gap,
capacity or desperation. This is the real reason a strangled region recovers
slowly even after you clear the problem.

**5. Your own tariffs suppress your own imports.** The `tariffFactor` is
`1 + tariffRate` on cross-alliance trade, so a 30 % tariff makes every foreign
good look 30 % dearer in the attractivity calculation and reduces the volume
that moves. Tariff revenue and import volume are directly opposed, and the
only thing that offsets it is **project 121 Trade Deals**, which divides the
same term.

**6. Blockade cuts flow linearly, using the worse end.** See §4.

### Who may trade with whom — and it never changes

Trade requires an agreement, and agreements are **fixed scenario data**. The
function that sets them has exactly one caller: the file loader. **No policy,
project, event or diplomatic action alters a trade agreement at runtime.**

The shipped matrix:

| Alliance | Has agreements with |
| --- | --- |
| Union | **everyone**, the Confederacy included |
| Confederacy | **everyone**, the Union included |
| Great Britain, France, Spain, Mexico, Sea Trade, Neutral States | the Union and the Confederacy only |

So the foreign powers are hubs that both belligerents can reach, and they do
not trade with each other at all.

### The line between North and South is semi-permeable

Because the Union and the Confederacy hold an agreement with each other, their
border nodes *do* trade — but through two filters:

- at war, flow is multiplied by **0.1**, so cross-line trade runs at a tenth
  of normal volume;
- and **all eight supply goods are hard-zeroed between alliance 0 and 1.**

> **What to do with this:** cotton, tobacco, salt, food and the other thirty
> civilian goods seep across the line at 10 % volume all war, which is a real
> if small relief valve for whichever side is short. **Weapons, ammunition,
> provisions, uniforms, forage, horses and artillery never cross it, ever.**
> No amount of price pressure will pull military supply over the line. If you
> are short of those, the only sources are your own production and the foreign
> powers' ports.

Imports from a foreign power are additionally scaled by **0.7**, and raised by
project 121.

---

## 3. Where goods come from

Production is per-building. Each building type makes a fixed set of goods, and
you choose how to split its effort between them.

| Building | Makes |
| --- | --- |
| Farms | Crops, **Forage**, **Horses**, Livestock, Wool |
| Plantations | Cotton, Rice, Sugar, Tobacco |
| Mines | Coal, Copper Ore, Gold, Iron Ore, Lead, Niter, Salt |
| Foundries | Bronze, Iron |
| Iron Works | **Artillery**, **Artillery Ammunition**, Steel, **Weapons**, **Small Arms Ammunition** |
| Factories | Clothing, Food, Gunpowder, Leather, Locomotives, Machine Parts, **Provisions**, Textiles, **Uniforms** |
| Mills | Flour |
| Lumber Mills | Wood |
| Brickworks | Bricks |
| Saltworks | Salt |
| Ports | Fish, Transport Ships |

Note where the military goods live: **Iron Works make the weapons and the
ammunition; Factories make the provisions and the uniforms.** Those are two
different building types with two different input chains, and a shortage of
one tells you nothing about the other.

### Specialising a building is worth real output

Output for a good is proportional to `share × (1 + share^0.4)`, where `share`
is the fraction of the building's effort you assign to it. That exponent is
below 1, so concentration wins:

| Split | Total output (one good at 100 % = 1.00) |
| --- | --- |
| One good at 100 % | **1.00** |
| Two goods at 50 % | 0.88 |
| Three at 33 % | 0.82 |
| Four at 25 % | 0.79 |
| Nine at 11 % | 0.70 |

> **What to do with this:** a factory told to make one thing produces about
> **14 % more in total** than the same factory split two ways, and **27 % more**
> than one split four ways. Specialise your industry by region and let trade
> move the output, rather than making a little of everything everywhere. The
> penalty for spreading effort is compounded by §2's cube rule, which also
> punishes diffuse production.

Output is then multiplied by the building's level, its **condition**, the
**relative workforce available** on its map square, and the good's own
production multiplier. Damage and manpower shortages both hit output directly.

### The harvest calendar is real, and it is not symmetric

Agricultural goods carry a per-month multiplier. The shipped curves:

| Good | Jan–May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Crops** | **0** | 0.5 | 0.5 | 1 | 3 | **4** | 3 | **0** |
| **Forage** | 1 | 0.5 | 0.5 | 1 | 3 | **4** | 3 | **0** |
| Cotton | 0 | 1 | 1.5 | 2.5 | **3** | 2.5 | 1.5 | 0 |

Two important differences hide in that table:

- **Crops produce nothing at all from December through May** — seven months of
  zero. Roughly **70 % of the annual crop arrives in September, October and
  November.**
- **Forage is the exception**: it produces at normal rate January to May.
  Only December stops it. (In the data those months are marked with a sentinel
  that means "no seasonal modifier", not "zero" — an easy thing to read
  backwards.)

> **What to do with this:** the food economy has one filling phase and one long
> drawdown. Stock built in the autumn has to last until the following
> September, and a campaign that burns or occupies farmland in **August** costs
> the enemy the whole year's crop, while the same raid in January costs
> almost nothing. December is the only month in which forage production stops
> everywhere — the worst month to have cavalry and artillery teams in the
> field far from a depot.

### Two project bonuses that do not work

The project panel advertises a production bonus for farm and plantation
mechanisation. Neither reaches production:

| Project | Advertised | Actually |
| --- | --- | --- |
| **113 Farm Mechanization** | +10 %/level crop production **and** +10 %/level recruitment | The **recruitment half works**. The crop half is applied inside a test that compares the loop counter against the resource id of Crops (9) — a farm only ever produces 5 goods, so the test is never true. |
| **114 Plantation Mechanization** | +10 %/level cotton production | Same mistake against Cotton's id (8); a plantation produces 4 goods. This is the project's **only** effect anywhere in the assembly, so **project 114 does nothing at all.** |

> **What to do with this:** do not research 114. Research 113 only if you want
> the volunteer bonus — which is a real and useful effect, just not the one the
> panel leads with.

**Project 104 Subsidize Industry excludes the Factories.** Its +5 %/level
efficiency applies to building types "Factory", Iron Works and Foundries.
"Factory" (a distinct type from "Factories") produces nothing at all, so in
practice the project boosts **Iron Works and Foundries only** — your weapons
and metals, not your provisions and uniforms. Three other places in the game
that group industrial buildings all include the Factories; this one does not,
which reads like an oversight rather than a design choice.

Agriculture's equivalent, **105 Subsidize Agriculture**, correctly covers both
Farms and Plantations.

---

## 4. The blockade

A node's blockade level is a number from 0 to 1, rebuilt every update from the
enemy units standing near it:

- a **cavalry unit on raiding orders** blockades any node that is not a trading
  route;
- a **fleet on blockade orders** blockades a **harbour**;
- a fleet whose commander has the **sea raider** perk can also blockade a
  **trading route**, and additionally raises the prices that route charges;
- each contributor adds its own blockade efficiency, multiplied up by the
  **blockading squadron** perk and by **project 106 Trade Warfare**;
- your own fleet with the **blockade buster** perk in the same radius reduces
  it, and so does **project 106 on the defending side** — the same project
  improves both blockading and blockade running;
- an enemy with **letters of marque** sets a floor under every harbour's
  blockade, so you cannot get a blockaded port entirely clear while that is in
  force.

What the number does is simple and harsh: **trade flow through that connection
is multiplied by `1 − blockade`, using the worse of the two endpoints.** A
fully blockaded port stops trading. There is no partial routing around it
within the model — the connection itself is throttled.

### The "losses through blockades" figure is a tax number

The strategy panel reports losses "through blockades & privateering". It is
natural to read that as the value of the trade you are losing. It is not.

The figure is computed as *the tariff revenue you failed to collect* on the
trade that did not happen — the volume shortfall times the price times **your
own tariff rate**. Two consequences:

- **It scales with your tariff rate.** A nation with low tariffs shows small
  blockade losses even when its trade is being strangled; raise tariffs and the
  reported losses jump without the blockade changing at all.
- **It includes infrastructure damage**, not just blockade: a node at reduced
  condition books losses into the same figure.

> **What to do with this:** use the per-node blockade percentage (on the node's
> own tooltip) and your goods prices to judge how badly you are being
> squeezed. The headline dollar figure is a revenue line, not a measure of
> economic damage, and it moves when you change your tax policy.

> **A defect worth knowing if you mod:** the blockade-buster reduction and the
> organisation-reform modifier are applied **inside** the loop over nearby
> units, so with several units in range they compound, and they only scale the
> contributions accumulated so far — making the result depend on the order of
> the unit list. ([18](../code-findings/18-economy.md) notes the method; this
> was traced for this write-up.)

---

## 5. Money in: five taxes you mostly do not control

Revenue per tax type is

```
revenue = base × your slider × the policy-set cap × a fixed multiplier
```

The slider scales a **ceiling that policy sets**. That is the single most
important thing to understand about taxation here.

| Tax | Taxed on | Cap at start | Raised to | By |
| --- | --- | --- | --- | --- |
| Tariffs | import volume | 30 % | **50 %** | Tariff Act |
| Excise | domestic trade volume | 5 % | — | nothing |
| **Income** | private income × population | **0 %** | 5 → 15 % | Revenue Acts I–III |
| **Corporate** | corporate profits | **0 %** | **10 %** | Revenue Act **III** only |
| Land sales | company foundations | 20 % | **50 %** | Feed Europe I–II |

> **Income tax and corporate tax raise nothing at the start of the campaign,
> whatever you do with the sliders**, because their ceilings are zero until the
> Revenue Acts pass. The panel is honest about it — it will show 0.0 % — but it
> does not tell you why, and corporate tax needs Revenue Act **III**
> specifically, not just any Revenue Act.

So early-war finance rests on **tariffs, excise and land sales**, and the
Revenue Acts are not an optimisation, they are the thing that unlocks the two
largest tax bases in the game.

### The administration trap: turn off the taxes that collect nothing

Administration costs are

```
cost = population × $2.50–$3.00 per head per year
     × (1 + 5 % per tax type charged + 5 % per subsidy type funded)
```

Base cost is **$3.00 per head** at zero support and **$2.50** at full support,
interpolated — so a popular government runs about 17 % cheaper.

And here is the trap. A tax counts as "charged" if its **slider is above
zero** — the cap is not consulted. Every alliance starts with all five
sliders at maximum. So from the first day you pay a **25 % administration
surcharge for five taxes, two of which are collecting nothing at all.**

> **What to do with this:** before the Revenue Acts, set the income and
> corporate tax sliders to **zero**. You lose no revenue — their caps are zero,
> so they were producing nothing — and you cut 10 percentage points off the
> administration multiplier. For a 22-million-population Union that is on the
> order of **$6 million a year** for two slider drags. Put them back up when
> the Revenue Acts pass.
>
> *(Derived from the code. The one thing it assumes is that the slider's
> minimum is zero rather than some small positive value — worth confirming in
> game, since the test is strictly "greater than zero". **Untested** on that
> point only.)*

The same 5 % applies per **subsidy** category funded, and there are eight of
them, so a government funding everything carries a 40 % surcharge on top.
Subsidies you are not actually relying on are worth switching off.

---

## 6. Debt: you never choose to borrow

There is no borrowing decision in this game. Twice per game day, for every
alliance including yours, the Treasury checks:

- **treasury below zero** → issue **$25 million** of bonds, add $25 million to
  the debt, and send you a message about it;
- **treasury above $50 million** → buy back $25 million of bonds.

That is the whole mechanism. Your cash balance oscillates in a band roughly
between $0 and $50 million, and **your debt is simply the running total of
your deficit**, in $25 million steps. The messages from the Secretary of the
Treasury are notifications, not decisions.

One hard limit: **bonds cannot be issued at the worst credit notch.** If your
rating bottoms out, the automatic issue stops and your treasury goes
negative and stays there. That, not the interest bill, is the real fiscal
failure state.

### The rating is a risk index, and the scale moves every year

```
burden = 4 × debt − 1.25 × annual surplus        (a deficit adds to it)
notch  = the worst rung whose threshold the burden exceeds
       + policy rating modifiers
       + project 97 (−1 notch per level)
```

There are **26 notches**, AAA down through six separate rungs all labelled D.
*(The config comment says 20; the code reads 26.)* **A higher notch number is
worse** — the number is a risk score, and the "credibility" figure shown in
the strategic assessment is just `(26 − notch) × 5`, i.e. the same thing
inverted.

Debt is weighted **four times** as heavily as the annual surplus, so paying
down principal moves your rating far more than running a modest surplus does.

And every threshold is multiplied by a year factor:

| Year | 1860 | 1861 | 1862 | 1863 | 1864 | 1865 |
| --- | --- | --- | --- | --- | --- | --- |
| Threshold multiplier | 0.4 | **0.60** | **3.25** | 3.70 | 4.25 | 4.50 |

> **1861 is on a leash about five times tighter than 1862.** The same debt
> that leaves you at BBB in 1862 would have put you several rungs worse the
> year before. Borrowing heavily in the first year of the war is punished much
> harder than borrowing heavily later, and the relaxation between 1861 and
> 1862 is the single largest jump in the whole table.

### The interest rate inverts, and then floors out

```
rate = ( base + (notch / 100)² × 2 ) ÷ (1 + bank funding)

base = max(0.01, 0.04 × (1 + (economy factor − 1) × 20))
```

That leverage of **20** on the economy factor is enormous, and the economy
factor declines all war (§7). The result:

| Date | Economy factor | Base rate |
| --- | --- | --- |
| 1861 | 1.070 | **9.6 %** |
| 1862 | 1.033 | 6.6 % |
| 1863 | 0.995 | 3.6 % |
| ~Nov 1863 onward | ≤ 0.9625 | **1.0 % — the floor** |
| 1865 | 0.920 | 1.0 % |

From roughly **November 1863** the economy-driven part of the interest rate is
pinned at its minimum and stops responding to anything. After that date your
rating alone sets your borrowing cost.

The rating adds `(notch/100)² × 2`: nothing at AAA, 1.3 % at BBB, 2.4 % at BB−,
3.9 % at B−, 12.5 % at the bottom.

> **Put together, late-war debt is remarkably cheap.** $50 million of debt in
> 1861 with a balanced budget sits at A− and costs about **10.3 %**. Half a
> *billion* in 1864 sits at BBB and costs about **2.3 %** — ten times the debt
> for a little over twice the annual interest bill. Whether this is intended
> is impossible to say from the code, but the practical advice follows either
> way: **be careful with the first year's budget and relaxed about the
> last's.** Deficits in 1861 are expensive in a way deficits in 1864 are not.

Bank subsidies (project 96) divide the whole rate, so they are worth most when
the rate is high — which is to say, early.

The rate does not drift gradually, incidentally: each update it jumps to the
new target with about ±1 % of noise, so a rating change shows up immediately.

---

## 7. The boom/bust cycle is on rails

The economy factor that drives all of the above — and that the game reports to
you as "Depression" through "Peak" — is **not simulated**. It is a seven-point
curve read from the scenario file and interpolated by date:

| 1850 | 1853 | 1856 | 1859 | 1861 | 1865 | 1870 |
| --- | --- | --- | --- | --- | --- | --- |
| 1.15 | 0.90 | 0.91 | 1.08 | **1.07** | **0.92** | 1.00 |

**It is identical in all six shipped scenarios, it is the same number for both
sides, and nothing you do changes it.** Across the war it falls in a straight
line from 1.07 to 0.92, which walks the label down one rung per year:

| Year | Factor | What the game calls it |
| --- | --- | --- |
| 1861 | 1.070 | Expansion |
| 1862 | 1.033 | Recovery |
| 1863 | 0.995 | Contraction |
| 1864 | 0.958 | Recession |
| 1865 | 0.920 | Depression |

It affects exactly **two** things: the interest-rate base (§6), and **civilian
consumption in towns**, which it scales directly.

> **What to do with this:** the economic cycle is weather, not a scoreboard —
> do not read "Recession" as a judgement on your management, and do not expect
> any policy to lift it. Its one useful signal is the second effect: as the
> factor falls, civilian demand falls with it, which **frees up goods and eases
> prices** for your army over the course of the war. The late-war economy is
> poorer but its markets are slacker, and combined with the interest floor
> that makes the second half of the war financially much easier than the first.

---

## 8. Why your depots will not fill

This is the question the economy gets asked most often, and the answer is in
[14](../code-findings/14-supply-and-logistics.md) §4 rather than here — but it
is a pricing answer, so it belongs in the summary.

A depot's fill rate is multiplied by

```
max(0.1, 1 − local relative price) ^ 0.5
```

So the **local price of supply goods throttles how fast a depot stocks up**:

| Local supply price (share of max) | Fill rate |
| --- | --- |
| 10 % | 95 % |
| 30 % | 84 % |
| 50 % | 71 % |
| 70 % | 55 % |
| 90 % or worse | **32 %** (the floor) |

A depot at comfortable prices fills in about **27 days**; the same depot in an
expensive market takes about **87**. On top of that, depot level sets both
capacity and speed — a level-1 depot fills at **half** the rate of a level-3
one.

> **What to do with this:** this is the feedback loop that makes a shortage
> self-sustaining. Your army arrives, buys heavily, pushes the local price up
> the steep part of the §1 curve, and the depot that is supposed to be
> refilling now does so at a third of its rate. **Build the depot and let it
> stock before the army arrives**, not after, and prefer to site depots where
> several trade connections meet rather than nearest the front.

---

## 9. What to actually do

The decisions this file supports, in rough order of value:

1. **Pass the Revenue Acts.** They unlock the two biggest tax bases in the
   game, and corporate tax needs Revenue Act III specifically.
2. **Zero the income and corporate tax sliders until those Acts pass** (§5).
   Free money, two clicks.
3. **Switch off subsidy categories you are not relying on** — 5 % of
   administration cost each.
4. **Be strict with the 1861 budget and relaxed with the 1864 one** (§6). The
   credit leash loosens 5× after the first year and the interest base floors
   out from late 1863.
5. **Specialise production by region** (§3) and let trade move it. ~14 % more
   output from a focused factory, and the trade model rewards sharp price gaps
   over diffuse ones.
6. **Build depots early and behind the front**, not at it (§8).
7. **Stock food in the autumn.** 70 % of the crop arrives September–November
   and nothing at all arrives December–May (§3). August is the month a raid on
   farmland costs the enemy most.
8. **Keep December in mind for cavalry and artillery** — the only month forage
   production stops.
9. **Upgrade bottleneck ports and bridges** rather than adding capacity where
   it is already adequate; capacity is averaged across each connection's two
   ends (§2).
10. **Do not research project 114**, and research 113 only for its recruitment
    half (§3).
11. **Read regional shortages at 15–20 % price, not at 50 %** (§1).

---

## 10. For modders

- **Trade agreements are load-time only.** Adding a diplomacy mechanic that
  opens or closes trade means patching `SetTradeAgreementWithNation` in, since
  only the loader calls it.
- **The economy cycle lives at the tail of each scenario's `battledata.dat`** —
  a count followed by (date, factor) pairs. Changing it needs no code, and it
  is the single highest-leverage number in the economy because of that ×20
  interest leverage. ([18](../code-findings/18-economy.md) listed its source as
  an open question; it is resolved here.)
- **The two production-project bugs are one-token fixes each** — `i` should be
  `producedgoods[i]` in both tests (§3) — and project 104's building list wants
  the manufactory id adding.
- **Transport-capacity policies key off Unity scene object names**
  (`"Trade_SantaFe"` and friends), so a new trade route cannot receive those
  policy effects without reusing the exact names
  ([18](../code-findings/18-economy.md) §4).
- Prices come from exactly two constants, `3.5` and `7.0`. Reshaping the price
  curve for a whole mod is a two-number change, and §1's table is the
  before-picture.

---

## 11. Open questions

1. **Workforce.** Output is multiplied by the relative workforce on a
   building's map square, and the workforce maps are the most expensive code
   in the economy ([18](../code-findings/18-economy.md) §8 q3). How workforce
   is allocated, and whether a player can meaningfully compete for it, is not
   traced. This is the largest remaining hole in the production story.
2. **Does the 10 % cross-line civilian trade matter in practice?**
   *(Untested.)* The mechanism is certain; the volumes are not. A save
   inspected at the border would settle whether it is a real relief valve or a
   rounding error.
3. **Whether the late-war debt cheapness is intended.** §6 states the
   arithmetic; the design intent is not recoverable from code. Worth asking
   the developers rather than guessing.
4. **Wage costs are $1 per worker per year and $0.33 per slave**, and
   buildings carry a slavery demand fraction. How much that actually shifts
   relative production costs between North and South is unquantified.
5. **The excise and tariff plumbing at resource level** remains untraced
   ([18](../code-findings/18-economy.md) §8 q4) — the per-good accumulators
   exist and are committed each pass, but the path from them into the treasury
   total was not followed here.
