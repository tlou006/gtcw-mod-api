# Perks

## 1. How perks work

There isn't one perk list but **three**, and which one a unit draws from depends only on what kind of unit it is:

| Holder | Slots | List |
| --- | --- | --- |
| Brigades and other fighting units | **1** | 19 battle perks |
| Divisions, corps, armies and army groups | **4** | 18 campaign perks |
| Fleets | **4** | 11 fleet perks |

So a brigade holds **exactly one perk for the whole war**, while a corps can hold four.

* **Earning a slot.** An empty slot slowly fills with experience. When it's full, you pick a perk. Empty slots fill fastest for artillery, then cavalry, then infantry, and twice as fast for fleets as for armies.
* **Slots unlock in order.** A formation's second slot doesn't start filling until the first has a perk, and so on. Your first pick is the one that has the longest to grow.
* **Levels.** A perk starts at **level I** the moment you choose it, and climbs to **level II** and **III** on its own as the unit does the thing the perk rewards (taking casualties, firing canister, building, marching, and so on). You can't stop a perk from levelling.
* **Artillery learns faster.** Every bit of perk experience an artillery unit earns is boosted.
* **Corps inherit from their army.** An army's (or army group's) campaign perks also apply to its corps, and where the corps holds the same perk the two usually **multiply**: a corps with Siege Train III under an army with Siege Train III gets ×2.0 twice. Divisions don't inherit from their corps. The one exception is **Engineers & Mechanics**, where the army's level replaces the corps' own instead of stacking.
* **Merging and detaching.** When two units merge, the survivor keeps its own perk and level. Detached skirmishers and scouts copy their parent's perk; anything else detached starts blank.
* **No real perk limits.** The tooltip says *"the amount of available perks is limited for each nation, depending on its policies and technologies"*. That system doesn't exist.

> **A warning on the numbers below.** Several perks are wired up wrong in the shipped game. Some bonuses are only shown on screen, some get **worse** as they level up, and one has a side effect that hurts. They're flagged in each table. These come from reading the game's files and code, and **haven't all been confirmed in play yet**.

---
&nbsp;

## 2. Battle perks

One slot per fighting unit, for the whole campaign. Values are for levels I / II / III.

### Infantry (the only eight an infantry brigade can pick)

| Perk | Effect at I / II / III |
| --- | --- |
| **Sharpshooters** | Accuracy **×1.1 / 1.25 / 1.5**. Range ×1 / 1 / **1.25** |
| **Ace of Spade** | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / **2** / 2. Reloads ×1 / 1 / **1.25** faster behind cover |
| **Deadly volley** | Casualties inflicted ×1.05 / 1.10 / **1.25**. From level II, no morale penalty for firing at an enemy in cover. **Level III backfires, see below** |
| **Expert skirmishers** | Concealment +10 / +25 / **+50**. Reloads ×1 / 1.05 / 1.1 faster. At level III, no morale penalty for low cohesion |
| **Zouave** | Can run. Fatigue ×1 / 0.75 / **0.5**. **See below for auto-resolve** |
| **Engineering** | Engineering points 2 / 3 / 4. Pontoon building ×1 / 1.25 / **2** faster. The only perk that grows from building pontoons and obstacles |
| **Iron discipline** | Cohesion recovers ×1.25 / 1.5 / **2** faster. At level III, immune to the "men doubt their chances" melee penalty. **Level II backfires, see below** |
| **Spirited charge** | The melee bonus (×1.1 / 1.2 / 1.5) is **shown on screen but never used**. Its only real effect is at level III: enemies fighting it in melee doubt their chances more, unless they have Spirited charge II or better themselves |

* **Deadly volley III makes sudden losses hurt more, not less.** It's meant to halve the morale hit from sudden heavy casualties. Instead it halves the protection that **drill** gives against that hit. An undrilled brigade sees no change, but a fully drilled one takes **more than twice** the morale hit it would without the perk. Levels I and II are fine.
* **Iron discipline II and III have a nasty side effect.** Normally, when a friendly unit routs nearby, the morale penalty fades within about 15 minutes. From level II, that penalty **never fades** for the rest of the battle. Next to a crumbling line, an Iron discipline brigade keeps losing morale every hour.
* **Zouave doesn't work in auto-resolve.** In auto-resolved battles the game checks for **Sharpshooters** where it means Zouave, so Sharpshooters brigades get Zouave's fatigue reduction instead.
* **Spirited charge spends your brigade's only slot on a number that does nothing** until it reaches level III.

---
&nbsp;
### Cavalry (six)

| Perk | Effect at I / II / III |
| --- | --- |
| **Mounted rifles** | Cuts the penalty for firing from the saddle. **It's strongest at level I and gets weaker as it levels, see below** |
| **Expert scouts** | Concealment +10 / +25 / **+50**. Sight range ×1 / 1.25 / **1.5** |
| **Cold steel** | From level II, enemies charged by it take **double** the morale hit from a cavalry charge. Its melee bonus (×1.1 / 1.2 / 1.5) is **shown on screen but never used** |
| **Lightning brigade** | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / **2** / 2. Reloads ×1 / 1 / **1.25** faster in cover |
| Stormers | Accuracy in loose formation ×1 / 1.1 / **1.25**. At level III, no morale penalty for firing at an enemy in cover |
| Vanguard | From level II, no morale penalty for falling back under fire. At level III, none for retreating under fire either. Nothing else |

* **Expert scouts has a level III morale bonus that never applies.** The concealment and sight range work fine.

**Mounted rifles is backwards.** Firing mounted normally costs **40%** accuracy and makes reloading **25%** slower. With Mounted rifles, the penalty falls to:

| Level | Accuracy penalty | Reload penalty |
| --- | --- | --- |
| No perk | 40% | 25% |
| I | **10%** | **6%** |
| II | 20% | 12.5% |
| III | 32% | 20% |

It's always better than having no perk, but each level up makes it worse.

---
&nbsp;
### Artillery (five)

| Perk | Effect at I / II / III |
| --- | --- |
| **Close support** | Canister strength **×2 at every level**. Reloads ×1 / 1.1 / 1.2 faster. Its melee bonus is **shown on screen but never used** |
| **Skilled laying** | Counter-battery accuracy ×1.1 / 1.2 / **1.5**. Morale loss from incoming artillery ×1 / 0.5 / **0, complete immunity** |
| **Grand battery** | Bombardment accuracy ×1.1 / 1.2 / 1.3. Morale hit on enemies under its fire ×1 / 1.5 / **2** |
| Flying battery | Speed ×1.15 / 1.3 / **1.5**. From level II, no morale penalty for falling back under fire; at level III, none for retreating either |
| Sapper gunners | Cover value ×1.1 / 1.2 / **1.5**. Morale bonus from cover ×1 / 2 / 2. Reloads ×1 / 1 / 1.25 faster (despite the description, in the open too) |

> **Close support is the strongest perk in the game at level I.** You get its full ×2 canister bonus the moment you pick it, and canister is already artillery's deadliest ammunition. **Skilled laying III** is the only perk that grants a complete immunity.

---
&nbsp;

## 3. Campaign perks

Four slots per division, corps, army and army group. Corps also get their army's perks (see Section 1).

| Perk | Effect at I / II / III |
| --- | --- |
| **Field Telegraph** | Order delays ×0.9 / 0.8 / **0.7** |
| **Siege Train** | Siege weapon strength ×1.25 / 1.5 / **2** |
| **Sappers and Miners** | Siege assaults ×1.25 / 1.5 / **2** |
| **Engineers & Mechanics** | Construction ×1.25 / 1.5 / **2** faster |
| **Partisan Brigades** | Raiding casualties ×1.25 / 1.5 / **2**. Raiding morale hit ×1.1 / 1.25 / 1.5 |
| **Limelights** | Night combat (sieges and skirmishing) ×1.25 / 1.5 / **2** |
| **River Expedition** | River movement ×1.25 / 1.5 / **2**. Siege assaults ×1.1 / 1.25 / 1.5 |
| **Expert Scouts** | Intelligence gathering ×1.25 / 1.5 / **2** |
| **Embedded reporters** | Fame your commanders earn from battles ×1.25 / 1.5 / **2** (corps and above) |
| **Ambulance Corps** | Wounded who die ×0.9 / 0.75 / **0.5**. Speed penalty from disabled soldiers ×0.75 / 0.5 / 0.25 |
| Flying Column | Movement ×1.1 / 1.1 / 1.25. Supplies last ×1.5 / 2 / **2.5** as long |
| Foot Cavalry | Forced-march speed ×1.1 / 1.2 / 1.3. Forced-march attrition ×0.9 / 0.8 / 0.7 |
| Pontoon train | Cost of crossing creeks ×0.75 / 0.5 / 0.25. Supply speed ×1.1 / 1.2 / 1.3 |
| Land Torpedoes | Enemy movement inside its command range ×0.9 / 0.8 / 0.7. Skirmishing ×1.05 / 1.1 / 1.2 |
| Skilled cartographers | Intelligence gathering and movement, both ×1.05 / 1.1 / 1.2 |
| Bureau of Military Information | Intelligence gathering ×1.1 / 1.25 / 1.5 |
| Balloon Corps | **Order delays ×1.25 / 1.1 / 1.1, slower, see below**. Intelligence when stationary ×1.25 / 1.25 / 1.5. Sieges ×1.05 / 1.1 / 1.15 |
| Bushwhackers | Enemy support lost to your raids ×1.1 / 1.25 / **1.5**. **Raiding casualties ×1.25 / 1.2 / 1.1, falling, see below** |

* **Balloon Corps makes your orders slower.** Its order-delay figure sits right next to Field Telegraph's and works the same way, but it's above 1 at every level, so it **adds** 25% to order delays at level I and 10% after that. Its intelligence and siege bonuses are real. If you want faster orders, take Field Telegraph.
* **Bushwhackers gets worse at raiding as it levels.** Its raiding casualties bonus falls from ×1.25 to ×1.1. Its other half, wrecking the enemy's state support, rises properly to ×1.5. Treat it as a political perk, not a military one.
* **Two "information" effects do nothing.** Field Telegraph and the Bureau of Military Information are each described as changing how quickly formations are tracked through fog of war. That figure is already maxed out after about eight game hours, whatever the perks do. Field Telegraph is an order-delay perk and the Bureau an intelligence perk, nothing more.

> **Take Field Telegraph first** if you plan to manoeuvre. Slots unlock in order, so your first perk has the longest to grow, and 30% off order delays at level III is the biggest everyday benefit on the list.

---
&nbsp;

## 4. Fleet perks

| Perk | Effect at I / II / III |
| --- | --- |
| **Battery Runner** | Fighting forts ×1.25 / 1.5 / **2** |
| **Port Defender** | Fighting inside your own port's radius ×1.25 / 1.5 / **2** |
| **Torpedoes** | Ramming ×1.25 / 1.5 / **2** |
| **Limelights** | Night combat ×1.25 / 1.5 / **2** |
| **Blockading Squadron** | Blockade strength ×1.1 / 1.25 / **1.5** |
| **Blockade Busters** | Fighting blockading fleets ×1.1 / 1.2 / 1.3. Enemy blockades of your own ports within its range ×0.9 / 0.8 / 0.7. **Choosing it makes the admiral famous on the spot** |
| **Supply Colliers** | Supply use ×0.85 / 0.7 / **0.5**, but fleet speed ×0.7 / 0.8 / 0.9 |
| **Sea Raiders** | Lets the fleet blockade trade nodes, neutral ones included. Prices there ×1.1 / 1.25 / 1.5. **At level III the admiral becomes famous** |
| Amphibious Attack | Chance of taking a fort ×1 / 1.1 / **1.5** |
| Balloon Barge | Intelligence ×1.25 / 1.35 / 1.5. Sieges ×1 / 1 / 1.05 |
| Mortar Boats | Siege bombardment ×1.05 / 1.1 / 1.25 |

* **Battery Runner and Port Defender are mirror images**, the two ×2 picks: one for attacking forts, one for defending your harbours. Decide which war your navy is fighting.
* **Supply Colliers is the only perk with a built-in penalty.** At level I it costs **30%** of the fleet's speed for a 15% supply saving. Only at level III does it reach a 50% saving for a 10% speed cost. Give it to a squadron that sits on a blockade, never to a raider.
* **Two fleet perks make their admiral famous**, which is worth ×1.4 on his fame: Blockade Busters as soon as you pick it, and Sea Raiders at level III.

---
&nbsp;

## 5. Quick picks

| Unit | Take | Avoid |
| --- | --- | --- |
| Infantry | Sharpshooters, or Ace of Spade for troops who fight from cover | Spirited charge (nothing until level III), Iron discipline (backfires from level II) |
| Cavalry | Expert scouts for scouting, Cold steel for shock, Mounted rifles to fight mounted (best at level I) | Vanguard (morale only) |
| Artillery | Close support, or Skilled laying for guns under counter-battery fire | — |
| Formations | Field Telegraph first, then Siege Train or Sappers and Miners for sieges | Balloon Corps if you want fast orders |
| Fleets | Battery Runner or Port Defender, Blockading Squadron for blockaders | Supply Colliers on anything that has to move |
