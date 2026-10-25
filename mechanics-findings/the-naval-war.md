# The naval war

What your ships actually do, and why the war at sea feels inconclusive. The
navy is the one part of the game with no tactical layer — every action is
resolved by arithmetic you never see — so almost everything that matters is a
decision you make before the two fleets meet.

Translated from [26-naval-combat](../code-findings/26-naval-combat.md) and
[30-naval-subsystems](../code-findings/30-naval-subsystems.md), with the
import projects from
[28](../code-findings/28-project-effect-reachability.md) §4.2 and the blockade
economics from [the war economy](the-war-economy.md) §4.

---

## 1. The short version

- **There is no naval battle to fight.** Fleet actions, fort bombardments and
  transport interceptions are all resolved by an off-screen calculator on a
  **21-minute** cycle. You choose the fleet, the orders and the moment; after
  that you watch.
- **Damaged ships always run.** Any ship below **35 % condition** leaves the
  action for a harbour on the cycle it crosses that line — with certainty, not
  a chance. This is the single most important fact about naval combat.
- **So fleet actions are indecisive and captures are rare.** You cannot take a
  prize unless a ship falls from above 35 % to disabled inside one cycle,
  because the capture threshold sits *below* the escape threshold.
- **Closing the range buys you nothing in rate of fire.** It does buy a much
  better hit chance — 6 % at opening range against 75 % at contact — and it is
  the only way to ram at all.
- **Only seven ships a side fire per cycle at sea, three on a river.**
  Everything beyond that is weight in the victory calculation and nothing else.
- **Armour is a pass/fail gate**, not damage reduction. A penetrating hit does
  full damage whatever the armour was; a non-penetrating one does nothing.
- **Your fleet is its worst ship** — slowest speed, and one sea-only hull costs
  the whole squadron its rivers.
- **A blockading fleet should be spread as wide as its range allows.** Covering
  four harbours blockades all four as hard as covering one, and dilutes the
  defender's guards fourfold.
- **One fleet on a sea lane aborts any amphibious movement it touches**,
  whether or not it hits anything.
- **Never buy your last imported hull.** The four ship-import projects restock
  about once a game day while any stock remains, and never again once it is
  empty.
- **The heaviest ships in the game are on the map from day one**, in five
  European squadrons, including five 92-gun ships of the line.

---

## 2. There is no naval battle to fight

Every naval engagement is an *autocalc*: a calculator attached to the units,
stepped once every **21 minutes of game time**, with a log you can read and no
controls. Four kinds exist:

| Engagement | Trigger |
| --- | --- |
| **Fleet action** | two fleets in contact |
| **Fort bombardment** | a fleet against a coastal fort garrison |
| **Transport interception** | a fleet against an army at sea (§8) |
| **Raiding / blockade** | a fleet on Raid or Blockade orders near a trade node (§7) |

What you control is everything before contact: which ships are in the fleet,
their condition, their admiral, their orders, and whether you accept the
engagement at all. There is no equivalent of taking a land battle manually.

Source: [26](../code-findings/26-naval-combat.md) §5,
[30](../code-findings/30-naval-subsystems.md) §1.

---

## 3. What decides a fleet action

Per cycle, for each side, the calculator walks the ship list in order. A ship
takes its turn only if it passes a die roll of about **one cycle in six**, so a
given ship fires roughly every two hours of game time. Then:

1. it picks a target, weighted by **size** — a bigger hull is entered into the
   draw more often and is more likely to be shot at;
2. it rolls to hit;
3. on a hit it rolls to **penetrate**, against the target's armour;
4. on a penetration it does damage, with a **2.5 %** chance of a magazine
   explosion that destroys the target outright.

### 3.1 Only seven ships a side fire per cycle

The loop stops after **seven** vessels have actually engaged a target at sea,
or **three** on a river. Ships beyond that take no part in the cycle.

> **A twenty-ship fleet does not fire twenty ships' worth of guns.** The extra
> hulls still count in full towards the victory balance that decides who wins,
> and they still soak hits as targets — but they add nothing to your output.
> Past about seven effective ships, more hulls buy you staying power, not
> firepower.

### 3.2 Rate of fire is a constant

The model carries a rate-of-fire term that was meant to rise as the range
closes — the config even labels the close-combat end of it. It never gets
there: the distance ratio it blends on can never exceed 1, so the term is
always pinned at its long-range value.

> **Closing the range does not make your guns fire faster.** Two things do: a
> **turret** ship fires 40 % faster, and a ship below **20 % ammunition**
> collapses to a fifth of its rate. Keep your fleets supplied and your rate of
> fire is a fixed property of the hulls.

### 3.3 Armour is a gate, not a shield

A hit rolls against the target's armour. Fail, and the log says *"no
effect!"* — nothing happens at all. Pass, and the damage is the full figure,
**entirely unreduced by the armour that was just penetrated**.

> So armour is **all or nothing**, and the right way to think about an armoured
> ship is not "it takes less damage" but "most shots bounce". Against a heavy
> ironclad, light guns are not weak — they are useless. Against an unarmoured
> hull, every hit tells.
>
> Penetration falls off very slowly with range: at long range a gun still has
> about **55 %** of its point-blank penetration, and two thirds of the drop
> happens in the first few hundred yards. So range mostly affects *whether* you
> hit, not *whether* you get through.

### 3.4 Hit chance is where range matters

The hit probability runs from about **6 % at the opening range to 75 % at
contact**, scaled by the gun's accuracy rating. Naval gunnery is the one place
where a weapon's accuracy stat does real work — in land combat it is discarded
for every weapon rated 1.0 or below
([manual against the code](manual-vs-code.md)).

> **Range is the whole of your tactical input, and it is set before the action
> starts.** The opening distance is the map distance when contact happens,
> scaled by your admiral's attributes: **initiative lengthens it by up to
> 30 %, cunning shortens it by up to 30 %**. A cunning admiral opens at better
> odds; an aggressive one opens at worse. In a fleet action the distance then
> closes on its own over roughly a day of game time.

### 3.5 Ramming does not roll

A ram ship rams on **every** cycle it is in contact, as long as its condition
is at or above **60 %**. There is no chance involved — the number the config
calls a probability is used as a damage scale.

Three consequences:

- **Ramming only happens at literal contact**, which takes most of a day of
  closing;
- a ram's `isram` rating is a **multiplier, not a flag** — the best rams do
  about **1.7×** the damage of the weakest at equal tonnage;
- **ironclad armour cuts ram damage by 70 % in both directions.** An ironclad
  ram against a wooden hull is the best case in the game; ironclad against
  ironclad is the worst.

> And the 60 % floor matters more than it looks. A ram ship stops being a ram
> after it has taken 40 % damage, which is well before it leaves the fight at
> 35 %. **Use your rams first, while they are fresh.**

Source: [26](../code-findings/26-naval-combat.md) §4, §5, §6.

---

## 4. Why fleet actions are indecisive, and prizes rare

This is the finding that explains the feel of the naval war. Four thresholds
sit on a ship's condition, and they are in the wrong order:

| Condition | What happens |
| --- | --- |
| 100 % → 60 % | fully effective; can ram |
| below **60 %** | **stops being able to ram** |
| below **35 %** | **leaves the action for a harbour — with certainty** |
| below 25 % | would stop firing, and would be capturable if disabled |

The escape step is supposed to be a chance, weighted by your ship's speed
against the enemy fleet's average — about **1.5 % per cycle** for an even speed
match, so a slow trickle of crippled ships limping away over hours. The roll
that implements it is broken: it compares against a constant zero, so it always
passes.

> **Every ship that takes 65 % damage leaves immediately.** Nothing survives to
> be sunk by gunfire except through the 2.5 % magazine explosion, and nothing
> survives to be **captured**, because capture needs a hull below 25 % *and*
> disabled — below the line at which it has already sailed away.
>
> So a fleet action is a damage-trading exercise that ends with both squadrons
> mostly intact and heading for repairs. The side that "wins" wins on the
> victory balance, not by destroying anything.

Two things follow for the player:

- **The 2.5 % magazine roll is your main source of outright kills.** It is
  per penetrating hit, so the way to sink ships is volume of *penetrating*
  fire: heavy guns against light hulls, at close range.
- **Prizes come from the margins, not from battle.** A ship that goes from
  healthy to disabled inside a single 21-minute cycle can be taken — which in
  practice means a magazine hit on an already-damaged hull, or a fort
  bombardment where ships are sent home unconditionally. The eight ship types
  no project grants are still obtainable this way
  ([26](../code-findings/26-naval-combat.md) §7.3), just much more rarely than
  the mechanism suggests.

### 4.1 And repairs are charged the moment the ship turns for port

A ship sent home bills your economy immediately, not when it arrives: a
fraction of its **full build cost** equal to the damage it has taken, plus a
top-up of coal, provisions and artillery ammunition to full.

> **A ship at 10 % condition costs 90 % of a new hull the instant it
> disengages**, and the bill lands at the harbour it is heading for. Since
> damaged ships leave automatically, this is not a cost you can decline — it is
> the real price of a fleet action, and it is why naval campaigns are
> expensive even when nothing sinks.

Source: [30](../code-findings/30-naval-subsystems.md) §3, §3.1.

---

## 5. Reading a ship's stat block

Three of the numbers on the ship panel do not mean what they look like.

**Firepower is measured at point-blank range.** The figure shown for a fleet is
the sum of its ships' firing strength computed at distance zero, so every gun
counts as in range and the rate-of-fire term takes its best value. The same
point-blank figure is what sets the **victory balance** of an engagement, while
the actual damage rolls use the real distance. So the panel is comparing two
fleets at a range neither of them is at, and the winner is decided the same
way.

**Armour is a rating, not inches.** The shipped values run 0, 0.5, 1, 1.5, 2,
2.5, 3, 4, 4.5, 4.7, 6, 7, 8. The manual's Appendix E reads them as inches,
which works from 2 upward; below that they are material names — 0.5 is timber,
1 is cotton bales — and the Tinclad's 1.5 is described as half an inch of iron.
**Treat it as a ladder, not a measurement**, and do not assume a 4 has twice
the protection of a 2.

**Three turret ships get no turret bonus.** The 40 %-faster rate of fire for
turret guns is applied only to ships whose guns sit in **exactly one** turret.
The three types that spread their guns across two — the **Ocean Casemate
Ironclad**, the **Ironclad Turret Ship** and the **River Monitor** — get
nothing, while the panel still reports their guns as turret-mounted. The ships
that do get it are the three single-turret types.

One more: the Cottonclad Ram is the only ship with an armour rating of exactly
1, and the display ladder's rung for that value is overwritten by the next one,
so its armour description is whatever the following band says. **Untested** —
the strings themselves are not in the config files.

Source: [26](../code-findings/26-naval-combat.md) §2, §3, §4.

---

## 6. A fleet is its worst ship

Fleet aggregation takes the minimum, not the average, of the things that
matter:

- **speed** is the slowest ship's speed, and a ship's speed is itself scaled by
  its condition — a damaged hull slows the whole squadron;
- **river capability** is an AND across every ship, so **one sea-only hull
  costs the fleet its rivers**;
- **sea capability** likewise.

Condition also drives the gun count: a fleet's guns are each ship's guns times
its condition, rounded up.

> **Detach your damaged and your deep-draught ships.** A single sea-only
> ironclad in a river squadron strands the whole force, and a single battered
> hull caps everyone's speed. This is also why the automatic escape at 35 %
> (§4) is a mercy as much as a loss: it removes the ship that was slowing the
> rest.

Source: [26](../code-findings/26-naval-combat.md) §3.

---

## 7. Blockade

Blockade is where a navy earns its keep, because the effect is economic and
permanent rather than a one-off action. The mechanics are in
[the war economy](the-war-economy.md) §4 and §4a; the naval reading of them:

**Spread your blockading fleets as wide as their range allows.** A fleet's
blockade value is applied **in full at every node it covers**, while the
defender's guards are **averaged** across all of them. Four harbours under one
squadron are blockaded as hard as one, and an enemy guard among those four
costs you a quarter of its strength at each.

**Closing to combat range multiplies a blockade by 2.5.** The test is whether
the blockader is within its own bugle range of the node, so pushing a squadron
right up against a port is worth two and a half of the same squadron standing
off — at the cost of exposing it.

**A fleet is only as good a blockader as its slowest ship**, because the value
is built from the squadron's lowest speed times its flagship value. A single
slow hull halves your blockade as surely as it halves your movement.

**Your admiral's cunning is worth up to 50 % more blockade.**

**Land cavalry on Guard orders defends a trade node against a fleet.** The
defence does not distinguish land from sea — a cavalry brigade on **Guard** and
a fleet on **Blockade** contribute the same guard strength against an enemy
raider. Guarding a coastal trade node with cavalry is a cheap answer to a
naval raider, and one the interface does not suggest.

**Sea Raiders lets a fleet blockade neutral trade nodes**, not just those of
powers you are at war with — which is how a commerce raider reaches European
trade without a declaration.

### 7.1 A raider forces a fair fight

When a fleet on **Raid** orders meets a fleet on **Blockade** orders, the
calculator **removes vessels from the blockading side at random until it has no
more ships than the raider**, with a floor of two. The removed ships are not
sunk — they simply take no part.

> **A three-ship raider engages a twenty-ship blockade squadron three against
> three.** So a small, fast, well-handled raiding force is not merely a
> nuisance to a big blockade — it fights it on even terms, repeatedly, and
> picks the moment.
>
> The same pairing also means the action **costs neither side national morale
> and prints no newspaper** ([30](../code-findings/30-naval-subsystems.md) §4,
> [25](../code-findings/25-newspaper-and-events.md) §5.1), so a raider that
> loses the exchange loses nothing politically.

---

## 8. One fleet closes a sea lane

A fleet that catches an enemy army at sea starts a **transport interception**.
What happens is short and decisive:

1. the convoy is slowed to **a tenth** of its speed;
2. one roll at **20 %** picks a single brigade of infantry, cavalry or
   artillery and inflicts casualties and a morale hit on it;
3. **the whole force is ordered to retreat to a depot** — whether or not the
   roll hit anything;
4. the engagement then ends, because the retreat order is what terminates it.

> **An amphibious movement meets one enemy fleet and turns around.** There is
> no fighting through an interception, no second chance in the same action, and
> no relation between the damage done and the outcome. The 20 % roll is the
> only thing in doubt, and it fires once.
>
> Two practical consequences. **Escort your transports** — an interception
> cannot start if the enemy fleet is already in a battle, and it is destroyed
> the moment that fleet enters one, so an escort that ties the raider up in a
> fleet action protects the convoy outright. And **a single cheap gunboat
> parked on a sea lane denies that lane entirely**, which is the best value in
> the naval game and works exactly as well against you.

The interception's hit chance is also supposed to rise as the intercepting
fleet closes, from 20 % to 100 %. It does not — the closing distance in this
one path never moves, so 20 % is all you ever face or inflict
([30](../code-findings/30-naval-subsystems.md) §1.2). And the brigade picked is
never the **last** one in the force's list, which is a quirk rather than
something you can use.

Source: [30](../code-findings/30-naval-subsystems.md) §1.

---

## 9. Where your navy comes from

### 9.1 Three gates on every ship type

Before you can lay down a hull, the type has to pass all three:

| Gate | Requirement |
| --- | --- |
| **Water** | you hold a **sea** port for a sea-going type, or a **river** port for a river type |
| **Port level** | your best harbour's level is high enough for the type |
| **Capacity** | your free shipyard tonnage is at least the hull's tonnage |

Capacity is the sum over your harbours of the port level's rated tonnage —
**1,500 / 2,000 / 3,000 / 5,000** by level — times each harbour's
**condition**, times one minus its **blockade**.

> **A blockaded or damaged harbour builds less.** A port at half condition
> under a 40 % blockade yields 30 % of its rated tonnage, so an enemy blockade
> attacks your shipbuilding directly and not just your trade. Repairing ports
> is shipbuilding capacity.

Two quirks worth knowing:

- **Losing your best harbour does not lose you the ship types its level
  unlocked.** The port-level requirement latches: once you have held a level-3
  port for one evaluation, every type it unlocked stays available for the rest
  of the campaign however that port is captured or levelled. Your *capacity*
  still falls, so you may not be able to afford the hull — but the type stays
  on the list.
- **Shipyard crowding never slows a hull that has been laid down.** Capacity
  decides whether you may *start* a ship; after that it builds at full speed
  however overloaded the yards are. The utilisation figure on the panel is
  honest and feeds nothing.

Source: [30](../code-findings/30-naval-subsystems.md) §6.

### 9.2 The import market, and the hull never to buy

Four projects — **British Warships**, **French Warships**, **Gloire Class** and
**Warrior Class** — do not unlock construction. They put a stock of **1 to 4**
purchasable hulls on the European market, rolled at the moment you buy, and
each hull you take is **paid out of the treasury, consumes nothing from your
economy, and is finished instantly**.

The stock then restocks itself about **once a game day** — but only while at
least one hull is still on offer.

> **Never take your last imported hull.** Leave one unbought and you get a
> free, instantly-completed ironclad roughly every three-quarters of a game
> day for the rest of the campaign. Buy it and the type is closed to you
> permanently.
>
> French Warships at $7 M is the cheapest way in. **Untested** in play; the
> arithmetic is in
> [28](../code-findings/28-project-effect-reachability.md) §4.2a and
> [policy order](policy-order.md) §8.4.

### 9.3 The five European squadrons are on the map from day one

Five named fleets exist in **every** campaign start, identical in all six, in
European hands:

| Fleet | Power | Strength |
| --- | --- | --- |
| **British Intervention Fleet** | Britain | 3 × Screw Ship-of-the-Line (**92 guns**) |
| **North American Squadron** | Britain | 2 × Screw Ship-of-the-Line, 1 × **Warrior Class**, 4 × Steam Corvette |
| **French Intervention Fleet** | France | 3 × Sail Spardeck Corvette, 2 × Converted Screw Frigate, 2 × Screw Spardeck Corvette |
| **Western Coast Fleet** | France | 1 × Ironclad Ship-of-the-Line (*Normandie*), 4 × Fast Screw Ship-of-the-Line (**80 guns**) |
| **Spanish Intervention Fleet** | Spain | 3 × Screw Schooner |

Twenty-five ships, including **five 92-gun ships of the line** — the largest
and most heavily armed hulls in the game, which no project sells you — and the
*Normandie* the newspaper names when France intervenes.

> **They are scenario data, not a reward.** They sit there from the first frame
> whether or not the intervention track ever fires; what intervention changes
> is whether their power goes to war, and therefore whether they are reachable.
>
> Which cuts both ways. If Britain joins the Confederacy, the Union faces five
> 92-gun ships it cannot match by building. If a European power joins *your*
> side and then withdraws, nothing of theirs stays. And if you beat one of
> these squadrons, the capture rule (§4) is the only route to a 92-gun hull in
> the game — rare, but the prize is the heaviest ship on the map.

Source: [30](../code-findings/30-naval-subsystems.md) §7.

---

## 10. Thirteen things to do differently

1. **Keep a blockading fleet spread, not concentrated.** Covering four
   harbours blockades all four at full strength and dilutes the defender's
   guards fourfold (§7).
2. **Push a blockader inside its own bugle range of the port.** That is worth
   ×2.5, and it is the largest single multiplier in the blockade model (§7).
3. **Guard your own coastal trade nodes with cavalry.** A brigade on Guard
   counts against an enemy raider exactly as a fleet on Blockade would, and
   costs a fraction as much (§7).
4. **Raid big blockades with small fleets.** A raider forces the blockading
   squadron down to its own numbers, so three ships fight three — and the
   action costs you no national morale whatever happens (§7.1).
5. **Escort your transports, or accept that any enemy fleet turns them
   around.** An interception cannot start against a fleet already in a battle
   (§8).
6. **Park a cheap gunboat on an enemy sea lane.** One hull denies amphibious
   movement through it entirely (§8).
7. **Use your rams in the first 40 % of their condition**, because below 60 %
   they stop ramming — and get them to contact, which takes most of a day of
   closing (§3.5).
8. **Match guns to armour, not to tonnage.** Armour is pass/fail: light guns
   against a heavy ironclad do literally nothing, and heavy guns against a
   wooden hull do full damage every time they hit (§3.3).
9. **Fight at close range for the hit chance, not the rate of fire.** 6 % at
   opening range against 75 % at contact; rate of fire does not change (§3.2,
   §3.4).
10. **Do not expect to sink anything.** Plan naval operations around position,
    blockade and denial, not around destroying a fleet — damaged ships always
    escape (§4).
11. **Detach damaged and deep-draught ships from a fleet.** One slow hull caps
    everyone's speed; one sea-only hull strands the squadron off rivers (§6).
12. **Repair and defend your harbours as shipbuilding infrastructure.**
    Condition and blockade both cut their rated tonnage directly (§9.1).
13. **If you buy a ship-import project, leave one hull on the market** — the
    stock restocks about once a game day while any of it remains and never
    again once it is empty (§9.2).

---

## 11. For modders

1. **Three defects would transform the naval war between them**, and each is a
   one-token change: the escape roll that compares against a constant zero
   (§4), the rate-of-fire clamp that points the wrong way (§3.2), and the
   transport interception's closing distance, which omits the scale factor its
   sibling carries (§8).
2. **Fix the escape roll first.** With it in place, two shipped prefs become
   live knobs, damaged ships start being lost, and ship capture becomes a real
   mechanic rather than an accident
   ([30](../code-findings/30-naval-subsystems.md) §9).
3. **The turret bonus needs `> 0` rather than `== 1`** to reach the three
   two-turret ships that currently get none (§5).
4. **Blockade is tunable entirely from config** — five prefs — except the node
   averaging in §7, which is code
   ([30](../code-findings/30-naval-subsystems.md) §9).
5. **`shiptypes.dat` has variable-length records**, so a positional editor has
   to track the resource-count line. The availability field is **inverted**:
   listing an alliance *removes* the type from it
   ([26](../code-findings/26-naval-combat.md) §1).
6. **The five European squadrons are scenario data.** Editing a campaign
   folder's ship and group files changes what an intervention brings, with no
   code involved (§9.3).
7. **A fleet ordered to retreat with no reachable friendly harbour is
   destroyed outright** — worth knowing before adding a scenario with sparse
   ports ([30](../code-findings/30-naval-subsystems.md) §5.1).

---

## 12. Still open

- **What a hit actually takes off a ship.** The damage formula is known but its
  scale depends on firing strength against hull size, and neither was evaluated
  against a real squadron, so **how many cycles a fleet action lasts is
  undetermined**. That is the one number a player would most want.
- **Whether the import market really restocks indefinitely** (§9.2). Derived
  from the code without ambiguity, never watched in play.
- **The ship panel's armour, propulsion and size words.** They resolve through
  a string table that is not in the config files, so the band structure is
  known and the text is not
  ([26](../code-findings/26-naval-combat.md) §10).
- **Where the five European squadrons start.** Their positions live in each
  scenario's regiment data, which was not read
  ([30](../code-findings/30-naval-subsystems.md) §10).
- **Whether a fleet can actually be stranded with no reachable harbour** on the
  shipped map, which is what would make §11's last point bite.
