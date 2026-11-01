# Perks

*Source: [34-perks](../code-findings/34-perks.md), which traces every line here to the code. An earlier version of this page followed the older reading in [19](../code-findings/19-commanders-and-perks.md) and took most campaign perks at face value. The 34 audit found that many of them do nothing or hurt you, and this page now follows it.*

## 1. How perks work

There isn't one perk list but **three**, and which one a unit draws from depends only on what kind of unit it is:

| Holder | Slots you can fill | List |
| --- | --- | --- |
| Brigades and other fighting units | **1** | 19 battle perks |
| Divisions, corps, armies and army groups | **up to 3** | 18 campaign perks |
| Fleets | **up to 3** | 11 fleet perks |

So a brigade holds **exactly one perk for the whole war**, while a corps can hold several. Formations and fleets have four slots in the game's data, but the perk panel only has room for three, so you never get to fill the fourth (the AI can). The manual says three for armies and two for corps and fleets; how many rows the panel really shows is **Untested**.

* **Earning a slot.** An empty slot slowly fills with experience. When it's full, you pick a perk. In battle, empty slots fill fastest for artillery, then cavalry, then infantry. On the campaign map they fill twice as fast for fleets as for armies.
* **Slots unlock in order.** A formation's second slot doesn't start filling until the first has a perk, and so on. Your first pick is the one that has the longest to grow.
* **Levels.** A perk starts at **level I** the moment you choose it, and climbs to **level II** and **III** on its own as the unit does the thing the perk rewards (taking casualties, firing canister, building, marching, and so on). You can't stop a perk from levelling.
* **Auto-resolve levels everything.** Every auto-resolved fight gives experience to all of a unit's slots, empty or filled, whatever the perk rewards. On paper that's more than half a level per day of fighting, far faster than any perk's own rule. **Untested**: the real rate depends on how many fights a unit is in.
* **Artillery learns at double speed.** All perk experience an artillery unit earns is doubled.
* **US Regulars start ready.** Units raised by the US Regulars policy start with a perk ready to pick.
* **Corps inherit from their army.** An army's (or army group's) campaign perks also apply to its corps, and where the corps holds the same perk the two usually **multiply**: a corps with Field Telegraph III under an army with Field Telegraph III gets ×0.7 twice, about half the usual order delay. Divisions don't inherit from their corps. The exceptions are the siege bonuses of Siege Train and Balloon Corps, which add up instead (see Section 3), and **Engineers & Mechanics**, where the army's level replaces the corps' own.
* **Merging and detaching.** When two units merge, the survivor keeps its own perk and level, and their experience is averaged by strength. Detached skirmishers and scouts copy their parent's perk; anything else detached starts blank. When a detachment rejoins, its experience is averaged back in by its share of the men, and any level it gained while away is lost.
* **The AI picks at random.** AI formations and fleets pick perks at random, and never the last one on each list: AI armies never get River Expedition, and AI fleets never get Port Defender.
* **No real perk limits.** The tooltip says *"the amount of available perks is limited for each nation, depending on its policies and technologies"*. That system doesn't exist.

> **A warning on the numbers below.** Of the 48 perks, **17 do what the manual says, 11 work differently, and 20 have a headline effect that does nothing, works backwards or lands on another unit**. Some bonuses are only shown on screen, some get **worse** as they level up, accuracy bonuses only help troops whose aim is already good (Section 2), two campaign perks hurt your own raiders, and several hand their effect to an unrelated perk (Section 5). They're flagged in each table. These come from reading the game's files and code, and **haven't been confirmed in play yet**.

---
&nbsp;

## 2. Battle perks

One slot per fighting unit, for the whole campaign. Values are for levels I / II / III. **Bold** perks do their main job; plain ones don't.

### Infantry (the only eight an infantry brigade can pick)

| Perk | Effect at I / II / III |
| --- | --- |
| **Sharpshooters** | Accuracy **×1.1 / 1.25 / 1.5**, for line infantry only (a detached skirmisher company keeps the perk but loses the bonus), **but only for good troops, see below**. Range ×1 / 1 / **1.25**. **Swapped with Zouave in auto-resolve, see below** |
| **Ace of Spade** | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / **2** / 2. Reloads ×1 / 1 / **1.25** faster behind cover |
| **Deadly volley** | Casualties inflicted ×1.05 / 1.10 / **1.25**. From level II, no morale penalty for firing at an enemy in cover. **Level III backfires, see below** |
| **Expert skirmishers** | Concealment +10 / +25 / **+50**. Reloads ×1 / 1.05 / 1.1 faster, in any formation. At level III, no morale penalty for low cohesion |
| **Zouave** | Can run. Fatigue ×1 / 0.75 / **0.5**. Immune at every level to the morale penalty for exhaustion. **Swapped with Sharpshooters in auto-resolve, see below** |
| **Engineering** | Needed to build pontoons. Deployment construction time and obstacle-building speed ×**2 / 3 / 4**. Pontoon building ×1 / 1.25 / **2** faster. The only perk that grows from building pontoons and obstacles |
| Iron discipline | Cohesion moves ×1.25 / 1.5 / **2** faster, **falling as well as recovering**. At level III, immune to the "men doubt their chances" melee penalty. **Level II backfires, see below** |
| Spirited charge | The melee bonus (×1.1 / 1.2 / 1.5) is **shown on screen but never used**. Its only real effect is at level III: enemies fighting it in melee doubt their chances more, unless they have Spirited charge II or better themselves |

* **Deadly volley III makes sudden losses hurt more, not less.** It's meant to halve the morale hit from sudden heavy casualties. Instead it halves the protection that **drill** gives against that hit. An undrilled brigade sees no change, but a fully drilled one takes **more than twice** the morale hit it would without the perk. Levels I and II are fine.
* **Iron discipline II and III have a nasty side effect.** Normally, when a friendly unit routs nearby, the morale penalty fades within about 15 minutes. From level II, that penalty **never fades** for the rest of the battle. Next to a crumbling line, an Iron discipline brigade keeps losing morale every hour. Its cohesion bonus also cuts both ways: under fire, cohesion drops faster by the same factor.
* **Accuracy bonuses only help troops whose aim is already good.** The game rounds any shot's accuracy below a fixed level up to that level, and the average rifle-musket brigade sits below it. A perk's accuracy bonus only counts for the part that lifts a shot above it. For an average brigade, Sharpshooters I and II change nothing and III tightens its aim by about 15%; a veteran brigade gets the full ×1.1 / 1.25 / 1.5 *(calculated)*. The same limit applies to Stormers, Skilled laying and Grand battery.
* **Sharpshooters and Zouave swap places in auto-resolve.** Auto-resolved battles still use an older numbering for these two. There, a Zouave brigade gets the sharpshooting bonus (nothing at level I, and a small penalty in a retreat stance), and a Sharpshooters brigade gets Zouave's fatigue reduction and immunity to exhaustion.
* **Spirited charge spends your brigade's only slot on a number that does nothing** until it reaches level III. In battle it only levels from melee against line infantry.
* **Engineering's speed rubs off.** On an obstacle line, brigades without the perk that work after an Engineering brigade often build at its speed too.

---
&nbsp;
### Cavalry (six)

| Perk | Effect at I / II / III |
| --- | --- |
| **Mounted rifles** | Cuts the accuracy and reload penalties for firing from the saddle. **It's strongest at level I and gets weaker as it levels, see below** |
| **Expert scouts** | Concealment +10 / +25 / **+50**. Sight range ×1 / 1.25 / **1.5**. **Levels very slowly, see below** |
| Cold steel | From level II, enemies charged by it take **double** the morale hit from a cavalry charge, unless they're in square. Its melee bonus (×1.1 / 1.2 / 1.5) is **shown on screen but never used** |
| **Lightning brigade** | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / **2** / 2. Reloads ×1 / 1 / **1.25** faster in cover |
| **Stormers** | Can run. Accuracy in loose formation ×1 / 1.1 / **1.25**, which only helps a near-veteran regiment (see Infantry). At level III, no morale penalty for firing at an enemy in cover |
| **Vanguard** | **At every level, no morale penalty for being outflanked, surrounded, or fired on from the flank or rear.** From level II, none for falling back under fire; at level III, none for retreating under fire either. Its promised level I effect, no penalty for fighting detached, is missing |

* **Expert scouts levels very slowly.** Only a detached scout company earns its experience, by revealing enemy units, and that's averaged back into the regiment when it rejoins. Its level III morale bonus never applies. The concealment and sight range work fine, and on the campaign map it also picks up a bonus meant for Ambulance Corps (Section 5).
* **Cold steel does nothing until level II**, and in battle it only levels from melee against cavalry. A regiment that only ever charges infantry won't get there outside auto-resolve.
* **Vanguard is better than the manual says.** Its immunity to being flanked, which the manual saves for level II, works from level I, and covers fire from the rear too.

**Mounted rifles is backwards.** Firing mounted normally costs **40%** accuracy and makes reloading **25%** slower. With Mounted rifles, the penalty falls to:

| Level | Accuracy penalty | Reload penalty |
| --- | --- | --- |
| No perk | 40% | 25% |
| I | **10%** | **6%** |
| II | 20% | 12.5% |
| III | 32% | 20% |

It's always better than having no perk, but each level up makes it worse. And only the reload half matters in practice: mounted fire is always below the accuracy floor described under Infantry, with or without the perk *(calculated)*.

---
&nbsp;
### Artillery (five)

| Perk | Effect at I / II / III |
| --- | --- |
| **Close support** | Canister strength **×2 at every level**. Reloads ×1 / 1.1 / 1.2 faster, with any ammunition. Its melee bonus is **shown on screen but never used** |
| **Skilled laying** | Counter-battery accuracy ×1.1 / 1.2 / **1.5**, best with rifled guns and seasoned crews (see Infantry). Morale loss from incoming artillery ×1 / 0.5 / **0, complete immunity** |
| **Grand battery** | Bombardment accuracy ×1.1 / 1.2 / 1.3, with the same limit. Morale hit on enemies under its fire ×1 / 1.5 / 2 as labelled, **but really about ×1 / 2.25 / 4, see below** |
| **Flying battery** | Speed ×1.15 / 1.3 / **1.5**. From level II, no morale penalty for falling back under fire; at level III, none for retreating either |
| **Sapper gunners** | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / 2 / 2. Reloads ×1 / 1 / 1.25 faster (despite the description, in the open too) |

* **Grand battery is stronger than it says.** The morale hit from artillery fire depends on how close the guns firing at a unit are on average, and Grand battery counts its own guns extra times without counting their distance again, so the average distance shrinks. When it's the only battery firing at a target from range, the hit grows about **×2.25 at level II and ×4 at level III**, not ×1.5 and ×2 *(calculated)*.

> **Close support is the strongest perk in the game at level I.** You get its full ×2 canister bonus the moment you pick it, and canister is already artillery's deadliest ammunition. **Skilled laying III** makes a battery completely immune to morale loss from incoming artillery.

> **Seven standalone battles use older numbers.** 1st Manassas (Regt.), New Market, Wilson's Creek, Stones River, Appomattox, Glorieta Pass and Olustee ship their own unit settings. In those battles Engineering gives ×1.25 / 1.5 / 2 instead of ×2 / 3 / 4, and Close support levels six times faster. Except at Stones River, Skilled laying and Grand battery also level three times faster, Sapper gunners twice as fast, and a battery's empty slot fills a quarter faster. The same fields fought from a campaign use the normal numbers. **Untested**: which battle modes on those fields load the older settings.

---
&nbsp;

## 3. Campaign perks

Up to three slots you can fill per division, corps, army and army group. Corps also get their army's perks (see Section 1). Most of them work on the campaign map and in auto-resolved battles; few touch the battles you fight yourself.

### Work as described, or close

| Perk | Effect at I / II / III |
| --- | --- |
| **Field Telegraph** | Order delays ×0.9 / 0.8 / **0.7** |
| **Expert Scouts** | Intelligence gathering ×1.25 / 1.5 / **2**, while the formation is stationary |
| **Bureau of Military Information** | Intelligence gathering ×1.1 / 1.25 / 1.5, while stationary |
| **Skilled cartographers** | Movement ×1.05 / 1.1 / 1.2. Intelligence gathering ×1.05 / 1.1 / 1.2, while stationary |
| **Balloon Corps** | Intelligence gathering ×1.25 / 1.25 / 1.5, while stationary. **Order delays ×1.25 / 1.1 / 1.1, slower, see below**. Sieges +2.5 / 5 / 7.5 %, shared out like Siege Train's |
| **Flying Column** | Movement ×1.1 / 1.1 / 1.25. Uses supplies 1.5 / 2 / **2.5** times more slowly, so it lasts that much longer when cut off. It doesn't carry any more |
| **Siege Train** | Siege weapon strength **+12.5 / 25 / 50 %**, half what its ×1.25 / 1.5 / 2 label suggests, **see below** |

### Half work

| Perk | What works | What doesn't |
| --- | --- | --- |
| **River Expedition** | Movement on creeks, rivers and river crossings ×1.25 / 1.5 / **2** | Its siege assault bonus is overwritten and does nothing |
| **Foot Cavalry** | Speed ×1.1 / 1.2 / 1.3 on **every** campaign move, not just forced marches | Its forced-march attrition cut never reaches the formation (Section 5) |
| **Ambulance Corps** | Speed penalty from disabled soldiers ×0.75 / 0.5 / 0.25 | Its cut in wounded who die goes to cavalry brigades with Expert scouts instead (Section 5) |
| **Land Torpedoes** | Enemy movement inside its command range ×0.9 / 0.8 / 0.7 | Its skirmishing bonus is overwritten and does nothing |
| Embedded reporters | Fame that the commanders of corps and armies **under** the holder earn from battles ×1.25, at every level | The holder's own commander gets nothing, and the bonus never rises past level I. It also magnifies fame lost in a bad battle |
| Pontoon train | Route planning counts creeks as ×0.75 / 0.5 / 0.25 as costly, so units detour less. They don't cross any faster | Its "supply speed" runs the whole supply clock 10 / 20 / 30 % fast: the formation uses supplies and pays upkeep faster, not more efficiently |

### Don't work

| Perk | What it says | What it really does |
| --- | --- | --- |
| Partisan Brigades | Raiding casualties ×1.25 / 1.5 / 2. Raiding morale hit ×1.1 / 1.25 / 1.5 | **Hurts you.** Both apply to the casualties and morale hit **your own** raiding brigade takes, not the enemy's |
| Bushwhackers | Raiding casualties ×1.25 / 1.2 / 1.1. Enemy support lost to raids ×1.1 / 1.25 / 1.5 | **Hurts you.** The casualty factor applies to your own raiders' losses. Raiding an enemy state already costs **your** side support there and gains the enemy a little, and the support factor makes that backlash bigger |
| Sappers and Miners | Siege assaults ×1.25 / 1.5 / 2 | Nothing: the bonus is overwritten. By mistake it gets River Expedition's other bonus instead, so in auto-resolved fights on creeks, rivers, crossings or at sea its units fight at half strength rather than a quarter |
| Limelights | Night combat (sieges and skirmishing) ×1.25 / 1.5 / 2 | Works only in auto-resolved field battles at night, the one case it doesn't mention |
| Engineers & Mechanics | Construction ×1.25 / 1.5 / 2 faster | A fort, depot or telegraph station reports itself finished early (a fort gets its garrison early), then goes back under construction and finishes at the normal speed. **Untested** |

* **Balloon Corps makes your orders slower.** Its order-delay figure sits right next to Field Telegraph's and works the same way, but it's above 1 at every level, so it **adds** 25% to order delays at level I and 10% after that. That's the cost the manual lists for it, not a bug. Its intelligence bonus is real. If you want faster orders, take Field Telegraph.
* **Siege Train is half as strong as it looks.** A siege's weapon strength starts from a baseline of 2, and the perk adds to that baseline rather than multiplying it, so Siege Train III adds 50%, not 100%, when it's the only holder. With several formations on your side the bonus is averaged across all of them, and an army's Siege Train adds to its corps' own rather than multiplying *(calculated)*.
* **The raiding perks punish the raider.** Neither Partisan Brigades nor Bushwhackers does anything to the enemy. Keep both off any formation you raid with.
* **Two "information" effects do nothing.** Field Telegraph and the Bureau of Military Information are each described as changing how quickly formations are tracked through fog of war. That figure is already maxed out after about eight game hours, whatever the perks do. Field Telegraph is an order-delay perk and the Bureau an intelligence perk, nothing more.

> **Take Field Telegraph first** if you plan to manoeuvre. Slots unlock in order, so your first perk has the longest to grow, and 30% off order delays at level III is the biggest everyday benefit on the list. It's also one of the few campaign perks that does everything it says, and a corps that holds it under an army that holds it too gets its order delays roughly halved.

---
&nbsp;

## 4. Fleet perks

| Perk | Effect at I / II / III |
| --- | --- |
| **Battery Runner** | Fighting forts ×1.25 / 1.5 / **2**. Not slowed by the forts it's bombarding. Only levels if the fleet has an ironclad |
| **Port Defender** | Fighting with one of your own harbours in range ×1.25 / 1.5 / **2** |
| **Torpedoes** | Ramming damage ×1.25 / 1.5 / **2** |
| **Limelights** | Night combat, against ships and forts, ×1.25 / 1.5 / **2** |
| **Blockading Squadron** | Blockade strength ×1.1 / 1.25 / **1.5** |
| **Blockade Busters** | Fighting blockading fleets ×1.1 / 1.2 / 1.3. Enemy blockades of your own ports within its range ×0.9 / 0.8 / 0.7, **but only partly, see below**. **Choosing it makes the admiral famous on the spot** |
| **Supply Colliers** | Provisions use ×0.85 / 0.7 / **0.5** (not coal or ammunition), but fleet speed ×0.7 / 0.8 / 0.9 |
| **Sea Raiders** | Lets the fleet blockade trade nodes, neutral ones included. The price rise there (×1.1 / 1.25 / 1.5) is **only shown on the map, never applied**. **At level III the admiral becomes famous**. Also gets Foot Cavalry's speed bonus by mistake (Section 5) |
| **Amphibious Attack** | **Without it, a fleet can't capture a fort at all.** Capture chance ×1 / 1.1 / **1.5** |
| **Balloon Barge** | Intelligence ×1.25 / 1.35 / 1.5. Sieges ×1 / 1 / 1.05 |
| **Mortar Boats** | Siege bombardment ×1.05 / 1.1 / 1.25 |

* **Battery Runner and Port Defender are mirror images**, the two ×2 picks: one for attacking forts, one for defending your harbours. Decide which war your navy is fighting.
* **Supply Colliers trades speed for supplies.** At level I it costs **30%** of the fleet's speed for a 15% saving on provisions. Only at level III does it reach a 50% saving for a 10% speed cost. Give it to a squadron that sits on a blockade, never to a raider.
* **Blockade Busters only partly lifts a blockade.** The game adds up the ships blockading a port one fleet at a time, and the perk only discounts the fleets it counts before the Blockade Busters fleet, so some blockaders are unaffected. It also levels while the fleet is blockading, not while it fights blockaders as the manual says.
* **Two fleet perks make their admiral famous**, which is worth ×1.4 on his fame: Blockade Busters as soon as you pick it, and Sea Raiders at level III.

---
&nbsp;

## 5. Perks that pick up another perk's effect

Perk numbers are shared across the three lists, and in a few places the game checks the number without checking which list it belongs to. So some perks get a bonus meant for an unrelated perk, and two campaign perks lose theirs.

| If you hold | You also get |
| --- | --- |
| Expert scouts (cavalry brigade) | Ambulance Corps' cut in wounded who die, on the campaign map. The Ambulance Corps formation doesn't get it |
| Ace of Spade (infantry brigade) | Foot Cavalry's cut in forced-march attrition. The Foot Cavalry formation doesn't get it. **Untested**: it may never trigger at all |
| Ambulance Corps (formation) | Expert scouts' concealment and sight range |
| Bureau of Military Information (formation) | Expert skirmishers' concealment |
| Blockade Busters (fleet) | Expert scouts' concealment and sight range |
| Balloon Barge (fleet) | Expert skirmishers' concealment |
| Sea Raiders (fleet) | Foot Cavalry's speed bonus, ×1.1 / 1.2 / 1.3 |
| Sappers and Miners (formation) | River Expedition's bonus for fighting on rivers (Section 3) |
| Balloon Corps or Land Torpedoes (formation) | Its headquarters can run in battle |

---
&nbsp;

## 6. Quick picks

| Unit | Take | Avoid |
| --- | --- | --- |
| Infantry | Sharpshooters for experienced brigades, or Ace of Spade for troops who fight from cover | Spirited charge (nothing until level III), Iron discipline (backfires from level II), Deadly volley on well-drilled brigades (backfires at level III) |
| Cavalry | Expert scouts for scouting, Vanguard for brigades that get flanked, Mounted rifles to fight mounted (best at level I) | Cold steel, unless it will meet enemy cavalry (nothing until level II) |
| Artillery | Close support, Grand battery (stronger than it says), or Skilled laying for guns under counter-battery fire | — |
| Formations | Field Telegraph first, then the intelligence perks, Flying Column or Foot Cavalry for speed, and Siege Train for sieges (at half its stated strength) | Partisan Brigades and Bushwhackers (they hurt your own raiders); Sappers and Miners, Limelights and Engineers & Mechanics (nothing that matters); Balloon Corps if you want fast orders |
| Fleets | Battery Runner or Port Defender, Blockading Squadron for blockaders, Amphibious Attack for any fleet meant to take forts | Supply Colliers on anything that has to move; Sea Raiders for its price effect (it has none) |
