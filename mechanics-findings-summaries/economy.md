# The economy

*Source: [The war economy](../mechanics-findings/the-war-economy.md) and [Production and workforce](../mechanics-findings/production-and-workforce.md), with pieces from [The manual against the code](../mechanics-findings/manual-vs-code.md) (Part I), [Policy order](../mechanics-findings/policy-order.md) (§4 and §7), [How you lose](../mechanics-findings/how-you-lose.md) (§6.2) and [Trade warfare](../code-findings/33-trade-warfare.md). Almost all of it comes from reading the game's code and data files, not from play.*

## How it works, in brief

* **There's no national market.** Every town, port, bridge, ferry, pass, trade route and railroad line on the campaign map is a market of its own, with its own stock of 38 goods and its own prices.
* **Prices come from how full the local stock is.** The curve is flat at the top, steep in the middle and flat again at the bottom.
* **Goods move slowly, between neighbours, toward wherever they cost more.** Nothing is centrally distributed.
* **Buildings make goods.** How much depends on local labour, the land, the season and how focused the building is.
* **Industry builds itself**, but only where goods are scarce, and only if you fund the subsidies that pay for it.
* **Your treasury runs on five taxes** whose ceilings are set by policy, not by you. **Borrowing is automatic**, and your **credit rating is a set of switches** that can shut down recruitment and construction.
* **The boom and bust are scripted.** It's the same curve in every campaign, for both sides.
* **The economy feeds national morale** through how fast your citizens' wealth is changing.

---
&nbsp;

## 1. Prices: every market for itself

Each market holds a stock of every good, and each good has a "full" level (the game's data calls it *minimum price reached*). The price depends on nothing but how full the stock is:

| Stock, as a share of full | Price, as a share of the good's top price |
| --- | --- |
| Full or more | 1% (the floor) |
| 80% | 2% |
| 70% | 7% |
| 60% | 17% |
| 50% | 33% |
| 40% | 55% |
| 30% | 74% |
| 20% | 87% |
| Empty | 97% |

That table explains most of what feels arbitrary about the economy:

* **From full down to 70%, prices barely move.** A market can lose almost a third of its stock and still look fine.
* **Between 60% and 30%, the price more than quadruples.** This is where a shortage turns into a crisis, and it happens fast.
* **Below 20% it flattens again.** A nearly empty market and an empty one cost about the same, so the price stops telling you how bad things are.

> **Act on a shortage while the price is still in the teens.** By the time a good looks expensive you're already past the steep part of the curve, and recovery has to climb back through it.

* **Big purchases pay the average, not the price on the label.** Buying in bulk drains the stock as you go, and you pay the average price along the way. An army arriving in a market makes its own supplies expensive.
* **Raw goods have a set top price. Manufactured goods inherit theirs** from the sum of their inputs, so a finished good is always worth more than what went into it.
* **Eight goods are military supply goods**, the ones depots stock and armies consume: **Artillery, Artillery Ammunition, Forage, Horses, Provisions, Small Arms Ammunition, Uniforms and Weapons**. The other 30 matter as inputs, as taxable trade and as exports.

---
&nbsp;

## 2. Trade: how goods move

Each market looks at the markets it's connected to and, good by good, pulls in whatever is cheaper next door. That simple rule has some sharp consequences.

* **Goods only flow toward higher prices.** If no market near your army is short, nothing moves toward your army.
* **Trade chases the biggest price gap.** One good that's badly short moves at nearly full speed. Ten goods that are each moderately short move at about **a quarter** of that rate between them. **A region short of everything recovers far more slowly than a region short of one thing.**
* **Distance divides the flow**, and a connection's transport capacity is the **average** of its two ends. One poor port or bridge slows every route through it, and upgrading it helps every connection it has.
* **Each connection has a hard cap** on how much it can carry at a time, however big the price gap. That's why a strangled region recovers slowly even after the cause is gone.

---
&nbsp;

### Tariffs work against your own imports

Trade with another alliance pays your tariff, and the trade engine counts the tariff as part of the price. A 30% tariff makes every foreign good look 30% dearer, so less of it comes in. **Tariff revenue and import volume pull in opposite directions.** Every import from another alliance into a Union or Confederate market, including across the front line, is also cut to 70% of normal. The **Trade Deals** project (121) offsets both.

> **At border markets, the tariff leaks onto your own trade.** Because of a bug, once a market has priced its first foreign connection, it prices every later connection as if it paid your tariff, including connections to your own markets. A high tariff can starve a border town of goods from its own hinterland.

---
&nbsp;

### Who trades with whom

Trade needs an agreement, and agreements are fixed when the scenario loads. **No policy, project, event or diplomatic action changes them.**

| Side | Trades with |
| --- | --- |
| Union | Everyone, the Confederacy included |
| Confederacy | Everyone, the Union included |
| Britain, France, Spain, Mexico, Sea Trade, Neutral States | The Union and the Confederacy only |

So the foreign powers are hubs that both sides can reach, and they don't trade with each other at all.

---
&nbsp;

### Across the front line

Because the Union and the Confederacy have an agreement, markets along the border keep trading during the war, through two filters:

* trade across the line runs at **10%** of its normal volume;
* **the eight military supply goods never cross**, at any price.

> Cotton, salt, tobacco, food and the rest of the civilian goods seep across the line all war. Weapons, ammunition, provisions, uniforms, forage, horses and artillery don't. If you're short of those, your only sources are your own production and foreign ports. Whether the 10% trickle amounts to much in a real game is **Untested**.

---
&nbsp;

## 3. Production: what buildings make

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

**Iron Works make the weapons and ammunition; Factories make the provisions and uniforms.** They're different buildings with different input chains, so a shortage of one tells you nothing about the other.

---
&nbsp;

### Focus each building on one good

You choose how each building splits its effort between the goods it can make, and concentrating pays:

| Split | Total output (one good at 100% = 1.00) |
| --- | --- |
| One good | **1.00** |
| Two goods, 50% each | 0.88 |
| Three goods, a third each | 0.82 |
| Four goods, 25% each | 0.79 |

> **A factory making one thing produces about 14% more than the same factory split two ways, and 27% more than one split four ways.** Specialise by region and let trade move the goods. Trade favours one sharp shortage over many small ones anyway (Section 2).

---
&nbsp;

### What else sets a building's output

* **Level.** Each level makes more, but needs three times the labour (Section 4).
* **Condition.** Damage cuts output directly.
* **Labour.** How much workforce is available on its patch of the map (Section 4).
* **Land.** Some farm and mining goods have a fertility map, and the game applies it **twice**, so it's squared: land rated 0.8 yields **64%**, and land rated 0.5 yields **25%**. Which goods have a fertility map isn't known yet.
* **Bonus inputs.** **Coal doubles** the output of Iron, Bronze and Gunpowder, and **salt raises Food output by half**, but only while the bonus good costs at most **35%** of its top price locally, and the building burns one unit of it per unit made. An expensive coal market loses you the bonus.
* **Season**, for crops, forage and cotton:

| Good | Jan–May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Crops** | **0** | ×0.5 | ×0.5 | ×1 | ×3 | **×4** | ×3 | **0** |
| **Forage** | ×1 | ×0.5 | ×0.5 | ×1 | ×3 | **×4** | ×3 | **0** |
| Cotton | 0 | ×1 | ×1.5 | ×2.5 | **×3** | ×2.5 | ×1.5 | 0 |

* **Crops grow nothing from December to May**, and about **83%** of the year's crop arrives in September, October and November *(calculated)*. Food stocked in the autumn has to last until the next September, so a raid that burns or occupies farmland in August costs the enemy nearly the whole year's crop, while the same raid in January costs almost nothing.
* **Forage keeps growing through the winter and spring.** **December** is the only month it stops, everywhere: the worst month to have cavalry and artillery teams far from a depot.

---
&nbsp;

### Recipes the manual gets wrong

| Good | The manual says | The game uses |
| --- | --- | --- |
| Flour | 4 Crops | **3 Crops** |
| Locomotives | 4 Iron, Steel and Machine Parts | **8 Iron, 2 Steel, 2 Machine Parts** |
| Transport Ships | 80 Wood, 2 Machine Parts | **160 Wood, 4 Machine Parts** |
| Bronze | Made in Mines | **Made in Foundries** |

A transport ship costs twice the wood the manual says, and so does a locomotive in iron. Plan your timber accordingly.

---
&nbsp;

## 4. Labour: the hidden limit on every building

The campaign map is divided into a **32×32 grid of labour**. Towns add workers to their square, buildings draw them out of theirs, and every building's output is multiplied by what's left. The effect only spreads to the neighbouring squares, so when the game's economy alert warns against building or upgrading companies "nearby", it means nearby.

* **A building runs at full output** while its square has labour to spare, and drops as the square empties. The game warns you when a building falls to 50%. Even in the emptiest parts of the map a building usually keeps about **30%**.
* **Big buildings, not population, are what empty a region.** A fully upgraded plantation draws more labour than a city of 100,000 supplies, and a fully upgraded mine about as much as a city of 200,000.
* **Every level triples the labour bill:**

| Building | Level 0 | Level 1 | Level 2 | Level 3 | Level 4 |
| --- | --- | --- | --- | --- | --- |
| Plantations | 150 | 450 | 1,350 | 4,050 | 12,150 |
| Mines | 100 | 300 | 900 | 2,700 | 8,100 |
| Farms | 40 | 120 | 360 | 1,080 | 3,240 |
| Iron Works, Ports | 30 | 90 | 270 | 810 | 2,430 |
| Factories, Foundries, Mills and the like | 20 | 60 | 180 | 540 | 1,620 |

> **Spread upgrades across regions instead of stacking levels in one place.** Two level-2 factories in different towns can out-produce one level-4 factory in a town that can't staff it.

---
&nbsp;

### Every soldier costs five workers

Each soldier you raise takes **five people** out of the workforce of the state that raised them. The town tooltip shows it as *"Fielded soldiers reduce workforce by X %"*.

Disbanding a unit gives back only its survivors. **The killed, the deserted, and the wounded of a disbanded unit never come back**, so every casualty lowers the labour around every town in that state for the rest of the campaign.

> A bloody offensive costs you industry as well as men. Spread recruitment across states rather than drawing again and again on your best industrial ones.

---
&nbsp;

### Slave labour

The share of a building's labour done by slaves **ignores the labour map entirely**. That share is:

> **the building's slavery demand × the slavery factor × Confederate support in the state** (zero where slavery isn't legal)

| Slavery demand | Buildings |
| --- | --- |
| 95% | **Plantations** |
| 80% | Saltworks |
| 75% | **Farms, Mines** |
| 50% | Brickworks, Mills |
| 40% | Lumber Mills |
| 30% | Ports |
| 25% | Supply Depots |
| 20% | Foundries, Markets |
| 15% | **Factories** |
| 10% | **Iron Works**, News Agencies |
| 0% | Banks, Hospitals, Forts, Military Academies, Telegraph Stations, Prison Camps |

* **Plantations are almost immune to labour shortage, and farms and mines nearly so.** Factories and iron works aren't.
* **A slave-worked building still draws labour from the map at its full size**, which hurts every free-labour building around it.
* **Slave labour is cheaper**: wages are $1.00 a year per free worker and $0.33 per slave.
* **The exemption wears away.** It shrinks as Confederate support in the state falls. The **Underground Railroad** policy cuts the slavery factor by 0.10 at once and 0.05 a year after that, **for both sides**, taking it from 1.0 to about 0.7 over a four-year war. The Confederacy's own **Slavery Restriction Act** cuts its factor by a further 0.25.

> **Playing the Confederacy, the labour limit barely touches your raw materials and applies in full to your manufacturing.** Put plantations, farms and mines wherever you like, and keep factories and iron works in or next to large towns. Playing the Union, your own Underground Railroad also erodes slave labour in the border states and in the territory you capture.

Population drifts slowly toward jobs: it grows where labour is scarce and shrinks where there's a surplus.

---
&nbsp;

## 5. How new industry appears

You don't build most of your economy. **Industry grows by itself**: twice a game day, the game may found a new company for each side.

### Where it looks

* **Only where a good is nearly sold out**, meaning its local price is above **72.5%** of its top price (stock at about 30% of full or less, *calculated*).
* **Only if the good is scarce across your whole country too.** One expensive town in a well-supplied country won't get a factory.
* It's more likely where there's **spare labour**, **good land**, **banks** nearby (up to ×1.5) and a high **corporate tax slider** (up to ×1.35). The corporate bonus reads the slider itself, so you get it from day one, even though corporate tax collects nothing until Revenue Act III (Section 6).
* **Each market proposes one building at a time**, and industry competes with other needs: a **Market** where transport is congested, a **Hospital** where your wounded have none in reach, a **Prison Camp** when you hold more prisoners than the enemy, a **Military Academy** when the enemy's military experience is ahead, a **Bank** when your interest rate is higher than the enemy's, and a **News Agency** where the draft is heavy.

---
&nbsp;

### What gets built

* **Productive buildings are paid for from your Agriculture and Industry subsidy budgets.** The moment a budget holds the **full** cost, the game founds the best site in that category, as a **new** building at the lowest level:

| Budget | Buildings it pays for |
| --- | --- |
| **Agriculture** | Farms $2.5M, Plantations $3.0M, Mills $1.63M, Saltworks $1.5M |
| **Industry** | Factories $4.5M, Iron Works $3.75M, Mines $3.25M, Foundries $2.5M, Lumber Mills $1.75M, Brickworks $1.5M, Ports $7.5M |

* **Productive buildings never upgrade themselves.** Upgrades, and the tripled labour bill that comes with each level (Section 4), only happen when you press the button.
* **Building by hand before the budget is full costs a premium.** The treasury pays the shortfall at five times its value, so a building placed with an empty budget costs five times its price in cash *(calculated)*. The game warns that these "extraordinary payments are much more expensive than waiting".
* **Buildings no subsidy pays for** (Banks, Markets, Hospitals, News Agencies, Military Academies, Prison Camps) go ahead only while your credit rating is above CCC+ (Section 8). They're the ones that get upgraded instead when one of the same type stands close by.
* **Mines and ports never grow by themselves.** Your coal, iron ore, lead, niter, salt and copper output, and your harbour capacity, only grow if you build and upgrade them by hand.
* **It only happens with auto-manage on.** The building list has one checkbox per tab, private and government. Untick one and that whole category stops growing.

> **If the Agriculture and Industry subsidy sliders aren't funded, your economy can't grow at all.** Those two sliders aren't a bonus; they're the construction budget for every productive building in the game.

**Land sales tax is your growth gauge.** It's charged on the game's running estimate of new companies, so it rises when industry is expanding and dries up when prices are comfortable.

---
&nbsp;

### Banks

A bank raises the **available capital** of the area around it, and the **Subsidize Banks** project (96) makes that 5% stronger per level. Capital:

* raises private wealth by up to **25%** (Section 10);
* makes new companies up to **1.5×** as likely to be founded nearby;
* cuts the **running** costs of nearby buildings by up to **three-quarters**. Despite the manual, it does **not** lower construction costs.

Banks also lower your **national interest rate**: the more bank levels you own, the cheaper your debt (Section 8). Nothing else in the economy helps this many things with no downside.

---
&nbsp;

## 6. Money in: taxes

Each tax raises **your slider × a ceiling that policy sets**. The slider only scales the ceiling, and you can't raise the ceiling yourself.

| Tax | Charged on | Ceiling at the start | Raised by |
| --- | --- | --- | --- |
| Tariffs | Trade with other alliances | 30% | Tariff Act, to **50%** |
| Sales | Trade within your own markets | 5% | Nothing |
| **Income** | Your towns' private wealth | **0%** | Revenue Act I to 5%, Revenue Act II to 15% |
| **Corporate** | Company profits | **0%** | Revenue Act **III** only, to 10% |
| Land sales | New companies | 20% | Feed Europe I–II, to **50%** |

* **Income and corporate tax raise nothing at the start, whatever the sliders say.** Early-war finance rests on tariffs, sales tax and land sales, and the Revenue Acts are what unlock the two biggest tax bases in the game. Corporate tax needs Revenue Act III specifically.
* **Sales tax has no downside.** The manual says a high sales tax pushes up prices and slows trade. It does neither. Keep it at maximum.
* **Tax rates cost no support.** The political price is paid **once**, as a drop in support across your states when each Revenue Act passes. After that, running a tax at its ceiling is free.
* **Printing money.** Print Notes I and II each add **$50M a year** to the treasury, at the cost of your citizens' private wealth (Section 10).

---
&nbsp;

### The administration bill

Running the government costs **$3.00 per head per year** at 0% support, falling to **$2.50** at 100%. On top of that, every tax whose slider is above zero adds **5%**, and so does every subsidy category you fund.

Both sides start with all five tax sliders at maximum. So from day one you pay a **25% surcharge for five taxes, two of which are collecting nothing.**

> **Until the Revenue Acts pass, set the income tax slider to zero.** You lose no revenue, since its ceiling is zero, and you save 5% of your administration costs: about **$3M a year** for the Union *(calculated)*. Turn it back up when the Acts pass.
>
> **Don't do the same to the corporate slider.** It looks like the same case, but it's the slider the game reads for the +35% to industrial growth (Section 5). Leave it at maximum.
>
> **Untested:** this assumes the slider goes all the way to zero. The game only drops the surcharge for a slider at exactly zero.

---
&nbsp;

## 7. Money out: subsidies and projects

Each subsidy slider is both an **expense** and a **budget**. Money leaves your treasury and lands in that category's budget at the same rate. You buy that category's projects from the budget, Agriculture and Industry also pay for new buildings (Section 5), and the Industry budget pays for railroads too.

How fast a budget fills depends on the highest level you hold in that category's policy ladder:

| Ladder level held | Budget per year at a full slider |
| --- | --- |
| None | **$5M** |
| I | $12.5M |
| II | $25M |
| III | $50M |
| IV (not Economy) | $100M |
| V (Diplomacy only) | **$150M** |
| Politics (no ladder) | **$10M from the first day** |

* **A branch without a ladder level gets a tenth of its headline budget.** At $5M a year, your Industry budget saves up for about one factory a year, and it buys that branch's projects from the same pot. The first ladder level in a branch multiplies its budget by 2.5.
* **Politics is the only category that's properly funded in 1861.** It's the one you can buy projects from early.
* **Finishing a ladder level raises your bill.** The slider keeps its percentage while the budget behind it doubles, so check the finance panel after each one.
* **Never lower the politics slider once you're at the policy cap.** The game enforces the cap by cancelling policies, finished ones included (see [Policy order](../mechanics-findings/policy-order.md), §3.2).

---
&nbsp;

### Economic projects: which ones work

| Project | What it does |
| --- | --- |
| **96 Subsidize Banks** | Works. Makes everything your banks do 5% stronger per level, including their cut to your interest rate (Sections 5 and 8) |
| **97 Improve Credit Rating** | Works exactly as described: one notch better per level |
| 98 Market Reform | **Nothing at all.** Market buildings do have a real effect of their own; the project that claims to improve them doesn't |
| 104 Subsidize Industry | Advertised as +5% per level, but it boosts only the bonus for focusing a building (Section 3), so it adds **at most 2.5%** to output. And only at **Iron Works and Foundries**: Factories are left out |
| 105 Subsidize Agriculture | Works on Farms and Plantations, at the same half strength: **at most +2.5%** output per level |
| **106 Trade Warfare** | Works both ways: better blockades for you, and better blockade running against the enemy's (Section 12) |
| 113 Farm Mechanization | The **recruitment** bonus works; the **crop** bonus never applies |
| 114 Plantation Mechanization | **Nothing at all** |
| **121 Trade Deals** | Works. Offsets your tariffs' drag on imports and raises imports from foreign powers |

---
&nbsp;

## 8. Debt and credit

### You never choose to borrow

Twice a game day, the treasury checks itself:

* **below zero**: issue **$25M** of bonds, and add it to the debt;
* **above $50M** while you're in debt: buy back **$25M** of bonds.

That's the whole mechanism. While you're in debt your cash swings between $0 and $50M, and **your debt is just the running total of your deficits**. The messages from the Secretary of the Treasury are notifications, not decisions.

---
&nbsp;

### The credit rating

There are **26 notches**, from AAA down through six separate rungs all labelled D. Your rating comes from:

> **4 × your debt − 1.25 × your annual surplus** (a deficit counts against you)

So **debt weighs four times as much as surplus**: paying down debt moves your rating far more than running a modest surplus does. The thresholds also relax every year:

| Year | 1861 | 1862 | 1863 | 1864 | 1865 |
| --- | --- | --- | --- | --- | --- |
| Threshold multiplier | **0.60** | **3.25** | 3.70 | 4.25 | 4.50 |

> **1861 is on a leash about five times tighter than 1862.** The same debt that leaves you at BBB in 1862 would have put you several notches lower the year before.

**The Improve Credit Rating project** (97) lifts you one notch per level, and **Union Pacific** and **Southern Pacific** each lift you two notches for good. The "credibility" figure in the strategic assessment is the same rating turned upside down.

---
&nbsp;

### Where the rating bites

| Rating | What stops |
| --- | --- |
| **CCC+ or worse** | **Recruitment, new ships, weapon orders and private construction**, all at once |
| The bottom notch | Bond issues. Your treasury goes negative and stays there |

The manual says recruitment and construction stop "below B". The game's own tooltips say "below B−", and the tooltips are right: B− still works.

> **The credit rating isn't a score; it's a set of switches.** Falling to CCC+ doesn't make the war expensive, it makes it unfightable.

---
&nbsp;

### Interest

Your interest rate is a base rate that follows the business cycle (Section 9), plus a premium for a poor rating, divided down by the banks you own.

| Year | Base rate |
| --- | --- |
| 1861 | **9.6%** |
| 1862 | 6.6% |
| 1863 | 3.6% |
| From about November 1863 | **1.0%, the floor** |

The rating premium is nothing at AAA, **+1.3%** at BBB, +2.4% at BB−, +3.9% at B− and **+12.5%** at the very bottom.

> **Late-war debt is cheap.** $50M of debt in 1861 with a balanced budget sits at A− and costs about **10.3%** a year. Half a *billion* in 1864 sits at BBB and costs about **2.3%**: ten times the debt for a little over twice the yearly interest. **Be strict with the 1861 budget and relaxed with the 1864 one.** Banks cut the whole rate, so they're worth most early, when rates are high: ten banks, none of them upgraded, take about 10% off it *(calculated)*.

---
&nbsp;

## 9. The business cycle is scripted

The game reports the economy as anything from *Depression* to *Peak*. That isn't simulated. It's a fixed curve read from the scenario file, **identical in all six scenarios, the same for both sides, and nothing you do changes it**. Across the war it walks down one rung a year:

| Year | Economy factor | What the game calls it |
| --- | --- | --- |
| 1861 | 1.070 | Expansion |
| 1862 | 1.033 | Recovery |
| 1863 | 0.995 | Contraction |
| 1864 | 0.958 | Recession |
| 1865 | 0.920 | Depression |

It does exactly two things: it sets the base interest rate (Section 8), and it scales how much your **towns consume**.

> **"Recession" isn't a verdict on your management**, and no policy will lift it. Its one useful effect: as it falls, civilian demand falls with it, which **frees up goods and eases prices** for your army. Together with the interest floor, that makes the second half of the war financially much easier than the first.

---
&nbsp;

## 10. Private wealth, and how the economy reaches national morale

Every town has a **private wealth** level, and your income tax is charged on it. It's built like this:

* **It starts from prices** at the town's nearest market. The cheaper the goods its people buy, the wealthier the town. With everything at top price, it falls to **25%**.
* **Jobs multiply it**: up to **×1.6** where labour is scarce (about ×1.4 at the labour map's usual floor), down to **×0.9** where there's a surplus.
* **Banks** multiply it by up to ×1.25.
* **Public wealth** multiplies it. Only a few policies move that: **Print Notes I and II** (×0.9 each), **War Bonds** and **Restrict Cotton Trade**.
* **It's capped at 100%.**

> **In a town where labour is scarce, wealth stays at the cap until the goods it buys average more than about a third of their top price. In a town with surplus labour, wealth can never pass 90%, and every price rise shows** *(calculated, without banks or Print Notes)*.

**Labour counts twice in the national figure.** When the game averages private wealth across your country (the figure national morale reads, below), it weighs each town by population and also scales a town with surplus labour down by up to another **30%**, with no cap. So surplus labour is good for your factories (Section 4) and bad for your citizens twice over.

---
&nbsp;

### National morale reads the change, not the level

Throughout each month, the game compares your average private wealth with its level at the start of the month. **Rising wealth earns national morale, falling wealth costs it, and flat wealth earns nothing**, however rich you are.

* If average private wealth climbs one point over a month, national morale gains about **0.4 points**. A fall costs the same.
* For scale, losing a battle with 10,000 casualties costs about **1.3 points** of national morale (see [How you lose](../mechanics-findings/how-you-lose.md)).

> **This is how a blockade or a bad harvest drains your national morale without a battle being fought**: shortages raise prices, prices lower private wealth, and the fall is charged to morale. Steady growth earns morale every month.

---
&nbsp;

## 11. Depots: where the economy meets the army

Depots buy the eight supply goods from their local market, and **the local price slows how fast they fill**:

| Local price of supply goods | Fill speed |
| --- | --- |
| 10% of top price | 95% |
| 30% | 84% |
| 50% | 71% |
| 70% | 55% |
| 90% or more | **32%** (the floor) |

A depot in a comfortable market fills in about **27 days**; the same depot in an expensive one takes about **87**. A level-1 depot also fills at **half** the speed of a level-3 one.

> **This is the loop that makes a shortage feed itself.** Your army arrives, buys heavily, pushes the local price up the steep part of the curve (Section 1), and the depot that's meant to be refilling slows to a third of its speed. **Build depots and let them fill before the army arrives**, and put them where several trade connections meet rather than nearest the front.

---
&nbsp;

## 12. Blockades and raids

### What blockades a market

* **Cavalry on raiding orders** blockades any market except a trade route.
* **A fleet on blockade orders** blockades a harbour.
* **A fleet with the Sea Raiders perk** can also blockade a trade route.

A market's blockade runs from 0 to 100%, and **trade over each connection is cut by the blockade at its worse end**. A fully blockaded port stops trading.

---
&nbsp;

### Making a blockade stronger or weaker

* **Spread a blockading fleet as wide as its range allows.** Its full strength applies at every market in range; it isn't divided between them. Only the defender's guards are averaged across all the markets it covers, so one squadron covering four harbours blockades each as hard as if it covered one.
* **Close in.** A fleet inside its own combat range of the market counts **×2.5**.
* **Guards.** Cavalry on **Guard** orders counts against a raiding fleet just as one of your own fleets on **Blockade** orders would: a cheap answer to an enemy raider off your coast.
* **Perks and projects.** The **Blockading Squadron** fleet perk strengthens a blockade, and the **Blockade Busters** perk weakens enemy blockades of your ports, though only partly (see [Perks](perks.md)). **Trade Warfare** (106) helps both your blockades and your blockade running.
* **Policies.** The Confederacy's **Organized Blockade Running** cuts the blockade of its own harbours by **25%**, which its description never mentions. **Letters of Marque** keeps every enemy harbour at least **1.5%** blockaded, a token effect.
* **Sea Raiders' price rise** on a blockaded trade route is shown on the map and never applied.

---
&nbsp;

### Reading the commerce-war panel

The panel is only a ledger. Nothing in the game reads it back; the damage it describes has already been done to your markets and taxes. Read it with care:

* **"Losses through blockades & privateering" is lost trade priced at your tariff rate.** It takes the trade that didn't happen and multiplies it by **your own tariff rate**, so raising tariffs inflates it with no change in the blockade. It counts trade between your own markets too, which pays no tariff, and it also counts infrastructure damage and, at a blockaded or damaged market, a neighbour simply running out of stock. To judge how badly you're being squeezed, use each market's own blockade percentage and your prices.
* **"Losses through raids" counts enemy cavalry raids the wrong way round.** When enemy cavalry raids your markets, your displayed raid losses go **down**, and with enough raiding the figure goes negative. That skews the panel's total and its "who's winning the commerce war" bar. The raided market's own tooltip leaves cavalry raids out entirely.

---
&nbsp;

## 13. Quick advice

| Do | Why |
| --- | --- |
| **Pass the Revenue Acts**, all the way to III | They unlock income and corporate tax, the two biggest tax bases (Section 6) |
| **Zero the income tax slider until they pass; keep corporate and sales tax at maximum** | Income tax collects nothing yet; the corporate slider drives industrial growth; sales tax has no downside (Sections 5 and 6) |
| **Fund the Agriculture and Industry subsidies** | They're the construction budget for every productive building (Sections 5 and 7) |
| **Switch off subsidies you don't need** | Each funded category adds 5% to your administration bill (Section 6) |
| **Be strict with the 1861 budget, relaxed with the 1864 one** | The credit thresholds loosen about fivefold after 1861, and interest bottoms out from late 1863 (Section 8) |
| **Never let your rating reach CCC+** | Recruitment, ships, weapon orders and private construction all stop at once (Section 8) |
| **Focus each building on one good, and specialise by region** | Up to 27% more output, and trade moves sharp shortages best (Sections 2 and 3) |
| **Build mines and ports yourself** | Nothing else will (Section 5) |
| **Spread upgrades and recruitment across regions** | Each level triples the labour bill, and every casualty costs five workers for good (Section 4) |
| **Put farms and plantations on the best land** | Fertility is squared: land rated 0.8 yields 64% (Section 3) |
| **Build banks** | Wealth, new companies, lower running costs and cheaper debt, with no downside (Sections 5 and 8) |
| **Stock food in the autumn, and keep cavalry near depots in December** | No crops from December to May; no forage anywhere in December (Section 3) |
| **Build depots early, behind the front** | A depot in an expensive market fills at a third of the speed (Section 11) |
| **Upgrade bottleneck ports and bridges** | A connection's capacity is the average of its two ends (Section 2) |
| **Act on shortages when prices are in the teens** | By 50%, you're already in the steep part of the curve (Section 1) |
| **Spread blockading fleets wide, and guard coastal markets with cavalry** | Blockades apply in full at every market in range; guards are averaged (Section 12) |

| Don't | Why |
| --- | --- |
| Research **Market Reform** (98) or **Plantation Mechanization** (114) | They do nothing (Section 7) |
| Research **Farm Mechanization** (113) for crops | Only its recruitment bonus works (Section 7) |
| Expect much from **Subsidize Industry** (104) or **Subsidize Agriculture** (105) | Each level adds at most 2.5% output, and 104 skips your Factories (Section 7) |
| Read the **blockade losses** figure at face value | It moves with your tariff rate and counts more than the blockade (Section 12) |
| Run a high tariff at a border market you need supplied | A bug lets the tariff choke its domestic trade too (Section 2) |
| Read **"Recession"** as a judgement on your play | The business cycle is scripted (Section 9) |
| Lower the **politics subsidy** at the policy cap | It cancels finished policies (Section 7) |
