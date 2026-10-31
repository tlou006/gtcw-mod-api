# Why units break

Morale and cohesion are two different things, and the game asks you to manage
both with one word. This is what each one actually is, what moves it, the two
thresholds a unit breaks at, and what rallying can and cannot get back.

**Translates:** [`13-morale-and-cohesion`](../code-findings/13-morale-and-cohesion.md)
and [`12-melee`](../code-findings/12-melee.md), with the loss-resilience
material from [`11-combat-resolution`](../code-findings/11-combat-resolution.md).
**Summary:** [summary-morale](../code-findings/summary-morale.md).
Conventions, including the Derived / Observed / Untested tags, are in
[`README.md`](README.md).

---

## 1. The short version

A unit can leave the fight three ways, and they are separate mechanisms:

| | What breaks | Threshold | Recoverable? |
| --- | --- | --- | --- |
| **Morale rout** | the men's willingness | morale below **25 %** under fire, **50 %** in contact | yes, slowly |
| **Unbearable losses** | the unit's tolerance for casualties | **20 %–66 %** of its men, depending on quality | **no** |
| **Fighting-spirit cap** | nothing — it is a ceiling | 50 %–100 %, set by campaign politics | n/a |

Cohesion is not on that list. **Cohesion never breaks a unit.** It is the
tightness of the formation, and it works entirely through other things: it
feeds the morale penalty for being disordered, it decides whether you win or
lose a charge comparison, and it is a differential in melee casualties. A unit
at zero cohesion is in serious trouble, but by way of morale, not directly.

---

## 2. Morale: the thresholds

Two break points, picked by whether the unit is in contact
([13](../code-findings/13-morale-and-cohesion.md) §6):

- **Under fire, a unit routs below 25 % morale.**
- **In contact, it routs below 50 %.**

> **Contact doubles the break threshold.** A unit sitting at 40 % morale is
> completely steady under a bombardment and routs the instant one of its
> blocks is touched. This is the single most useful number in this file.

The morale icon starts blinking at exactly **50 %**, which makes the blink a
far more specific warning than it looks: it means *"this unit will break if
anything reaches it."* Below 25 % the symbol changes again to the broken
marker.

### Routing sets morale to zero

A rout does not drop a unit just under the line — `Rout()` sets morale to
**0** and the climb back starts from nothing. Two things happen on the way
out: routing cavalry is **remounted first**, and a unit in square is reformed
into line, so a square that breaks cannot re-form as a square while fleeing.

**Campaign units never rout.** Routing is a battle-only state; on the campaign
map low morale stops a formation from seeking contact and, below 25 %, cripples
it in the next battle (§5), but it never produces a running unit.

---

## 3. What actually moves morale

Eighteen things, all of them rates per game hour, and the unit panel names each
one ([13](../code-findings/13-morale-and-cohesion.md) §3). Ranked by what they
are worth:

| Rate/hr | What |
| --- | --- |
| **−1.00** | being **charged** by a foe with better cohesion or numbers |
| **−0.60** | losing more than 4 % of your men in the last nine game minutes |
| **−0.50** | enemy **firing into your rear**; enemy **cavalry in your formation** |
| **−0.25** | enemy firing into a flank (each); enemy standing in your rear; enemy in cover |
| −0.20 | out of ammunition |
| −0.15 | retreating under fire |
| −0.10 | low ammunition; cohesion below 30 %; cut off from your commander; exhausted |
| −0.075 | falling back under fire |
| −0.01 to −0.16 | per enemy battery firing at you, worse the closer it is |
| **−0.008** | enemy merely **standing** on a flank |
| **+0.15** × cover | being in cover yourself |
| +0.05 | each flank or rear secured by a friend; friendly cavalry within 75 |
| +0.025 to +0.225 | a commander within 50, scaled by his leadership and fame |

Three of those deserve attention.

**Being outflanked is nearly free until they shoot.** An enemy *standing* on
your flank costs 0.008/hr. The same enemy *firing* into it costs 0.25/hr —
thirty-one times more. In the rear the gap is only 2×. So the flank value is
an order of magnitude out of family with its three neighbours, and the
practical reading is that **a flanking move does nothing until it opens fire**,
while a move into the rear starts hurting on arrival.

**Artillery's cover discount is backwards.** The incoming-artillery penalty is
discounted by 75 % — but the discount is multiplied by `(1 − cover)`, so it
applies when you are in the **open** and vanishes when you are in full cover
([13](../code-findings/13-morale-and-cohesion.md) §3). A brigade behind a stone
wall takes the full artillery morale penalty; the same brigade standing in a
field takes a quarter of it. The config label says the opposite is intended.
The protection cover gives against actual casualties is unaffected; this is
only the morale term.

> **Untested:** Derived from the expression and the pref label, not watched in
> play. The test is a brigade under long bombardment with and without cover,
> reading the *"Under artillery fire"* row in its panel.

**Everything above is understated by a factor of three to five.** When a
unit's net morale change for a tick is negative, the game multiplies it by
`5 − experience − leadership` before applying it
([13](../code-findings/13-morale-and-cohesion.md) §4):

| Unit | Real loss, as a multiple of the table above |
| --- | --- |
| Green brigade, poor general | **×5.0** |
| Both at half | ×4.0 |
| Elite brigade, best general | **×3.0** |

Gains are never amplified. And **the unit panel's own "Overall change per
hour" does not include it** — it sums the same slot values the simulation
uses and stops there, so a panel reading −0.4/hr on a green brigade describes
a unit actually shedding 2.0/hr. If the morale bar is falling faster than the
tooltip says, this is why.

So the real cost of being charged while disordered is **−3 to −5 morale per
hour**: a unit at 80 % loses the lot in twelve to twenty game minutes.

---

## 4. What rallying can and cannot recover

Three channels, and the differences matter more than the rates.

### Base recovery, and why veterans are different

Morale regenerates continuously at
`0.30/hr × [1 − (1 − experience) × 0.66]`
([13](../code-findings/13-morale-and-cohesion.md) §2):

| Unit | Recovery | Time to climb from 0 to the 25 % rally line |
| --- | --- | --- |
| Green | 0.102/hr | **2 h 27 m of game time** |
| Half-experienced | 0.20/hr | 1 h 15 m |
| Elite | 0.30/hr | **50 m** |
| Elite under a matching West Point general | 0.375/hr | 40 m |

A battle's fighting day is nine game hours in January and sixteen in July
([time and speed](time-and-speed.md) §1), so a green brigade that routs has
spent a quarter of a winter day getting back to the point where it will stand
at all.

Stack that against §3 — a veteran loses morale at 3/5 the rate *and* regains
it at 2.94× the rate — and the staying-power gap between a green brigade and
an elite one is about **five to one** at the same morale reading. Experience
is earned at 0.0002 per enemy casualty the unit inflicts, so **a brigade has
to inflict about 5,000 casualties to go from green to elite**. That is several
battles' worth of killing, which is why the gap persists all campaign.

### Recovery above 50 % is effectively nil

Full-rate recovery only runs up to whatever the unit entered the battle at, and
never below 50 %. Above that line the rate drops to **15 %** of itself. A green
brigade recovering from 50 % toward 90 % does so at 0.015/hr — **twenty-six
game hours**, which is two or three battle days.

> **You cannot rebuild morale inside a battle, only restore it.** Whatever a
> unit walks onto the field with is its practical ceiling for the day; the
> system is built to give you that back and no more. Morale above the starting
> level is a campaign-map project.

### The commander's rally order

Ordering a commander to rally adds **+0.20/hr** on top of the unit's own
recovery — a 67 % boost for a veteran and nearly a tripling for a green unit.
Three rules the interface does not state
([13](../code-findings/13-morale-and-cohesion.md) §6):

- It stops at **90 %** morale.
- **It is cancelled the moment you give that unit a move order.** If you are
  rallying a brigade, leave it alone.
- When the target reaches the ceiling, the order **re-targets itself** at the
  worst-off remaining unit in the command — skipping skirmishers and anything
  that is moving. One rally order will work its way through a whole division
  if you let it, worst first.

### What rallying cannot recover

- **A rout under fire.** The rout only clears when morale climbs back above
  25 % on its own, which is the 2 h 27 m / 50 m above.
- **A rout in contact rallies on distance instead.** If the unit broke out of
  a melee it un-routs as soon as no enemy is within 40, *whatever its morale*,
  and is set to 26 %. **Hand-to-hand routs come back quickly and bombardment
  routs do not** — the opposite of what most players expect.
- **Unbearable losses.** Nothing recovers this (§6).
- **Anything above the fighting-spirit cap** (§5).
- **A unit that arrived under 25 % campaign morale.** Pinned at 30 % all
  battle (§5).

---

## 5. The cap that comes from the campaign map

`fightingspirit` is a hard ceiling on morale, applied in both manual battles
and auto-resolve. It is built on the campaign map, in four steps
([13](../code-findings/13-morale-and-cohesion.md) §5).

**Step 1 — your recruiting state's political support.** Not the support value
directly; a lerp:

| Raised as | Cap at 0 % support | at 50 % | at 100 % |
| --- | --- | --- | --- |
| **Volunteers** | 80 % | 90 % | 100 % |
| **Drafts** | **60 %** | 80 % | 100 % |

and the **Bounties** policy subtracts a further 5 points at level 1 and 10 at
level 2. So the worst a unit can be capped at is a drafted brigade from a
zero-support state under Bounties 2: **50 %** — exactly the melee crack level,
meaning that brigade can never stand in contact at all.

> This is much gentler than "support is your morale ceiling", because two of
> the prefs that would sharpen the curve — a support floor and a ×2 leverage —
> are **parsed and never read**, and the config marks both `-> inactive`
> ([13](../code-findings/13-morale-and-cohesion.md) §9c). At 50 % support a
> volunteer brigade is capped at 90 %, not 50 %.

**Step 2 — your formation's average drags you down.** A brigade's cap is the
**lower** of its own and the mean of its parent formation's brigades. It is
never lifted above the average, only pulled down to it.

> **Keep formations homogeneous by recruiting state.** Attaching a brigade
> raised in a 100 %-support state to a corps whose brigades average 70 % caps
> it at the corps average and wastes the difference. The game never mentions
> this.

**Step 3 — your commander, measured against your own army.** Experience, fame
and leadership enter as *differences from your side's average*, so a veteran
brigade in a veteran army collects nothing. The weights shift with hierarchy:

| Level | Experience | Fame | Leadership |
| --- | --- | --- | --- |
| Brigade | 0.33 | 0.05 | **0.20** |
| Division | 0.33 | 0.10 | 0.15 |
| Corps | 0.33 | **0.15** | 0.10 |
| Army | 0.33 | **0.20** | 0.05 |

> **Put your famous generals at the top of the chain and your good leaders at
> the bottom.** Leadership is worth four times fame at brigade level and a
> quarter of it at army level. Experience matters identically everywhere. A
> wounded or killed commander has his fame and leadership halved for this
> purpose.

**Step 4 — invading costs you.** Fighting in a state your side is at war with,
other than a unit's own home state, subtracts **30 points** at national
military experience 0, easing to 10 points at maximum. This is the mechanical
form of "defending your own ground is easier".

**None of this applies in a standalone historical battle.** There, every unit
on a side takes one fighting-spirit number straight from the scenario file, and
all four steps are discarded
([13](../code-findings/13-morale-and-cohesion.md) §5). It affects only the
custom-battle scenarios, not campaign battles.

### Two more ceilings

- **First battle ever: capped at 60 %**, with drill and the commander's
  leadership each buying back up to 20 points, so a fully drilled brigade under
  a brilliant general enters uncapped. An undrilled one is capped at 60 % — ten
  points above the melee break line, which is not much room.
- **Arriving under 25 % campaign morale: pinned at 30 % all battle.** The game
  raises the unit to 30 % and caps it there. 30 % is above the 25 % rally line,
  so it can come back from a rout — but it is **below the 50 % melee line**,
  so it breaks on contact every single time, with no path back, for the whole
  battle.

> **Never bring a brigade under 25 % morale to a battle.** It is not a weak
> unit, it is a unit that cannot hold ground for the rest of the day. And note
> the threshold just above it: a formation whose **average** morale is under
> **40 %** will not accept a field battle at all. If yours is the attacker it
> declines and you get a "retreating" message; if it is the defender it
> **withdraws to a depot** instead of fighting. So the band from 25 % to 40 %
> is "will not fight in the open", and the band from 40 % to 50 % is "will
> fight, and will break the moment it is touched".
>
> The same threshold excludes individual brigades from a battle that does
> start, so a corps at 45 % average can go into action with its worst brigades
> sitting out.
>
> **You cannot force a battle on a demoralised enemy in open country** — below
> 40 % it slips away to a depot. Garrisons are the exception: the test is
> skipped for a unit with a garrison reference, so a demoralised garrison still
> has to fight.

---

## 6. Unbearable losses: the one that is final

Separate from morale, and not recoverable. A unit whose casualties exceed its
trigger panics and leaves the field
([13](../code-findings/13-morale-and-cohesion.md) §6). The trigger is set by
four things, all of which raise it:

| Unit | Breaks at |
| --- | --- |
| Green, undrilled, poor general, national military experience 0 | **20 % losses** |
| All four at half | 46 % |
| Elite, fully drilled, best general, military experience 1 | **66 %** |
| A skirmisher detachment | half of the above |

That is a **3.3× range on the hardest cliff in the battle model**, and it is
the strongest single argument for drill: drill is the one input of the four you
can buy directly.

Units also **toughen as a multi-day battle goes on** — the trigger rises 10 %
per elapsed day for green troops and 30 % for veterans, added linearly. A
second day is measurably easier to survive than the first.

The **Loss Resilience** bar in the unit panel is this stat, and it cannot be
read as a percentage of the unit's strength, for three separate reasons
([13](../code-findings/13-morale-and-cohesion.md) §6):

- The test excludes men lost as **prisoners**; the bar includes them, so a unit
  that has lost prisoners reads closer to breaking than it is.
- The bar is normalised against the trigger a *perfect* unit of that type would
  have, not the unit's own.
- **For a formation the trigger is the maximum over its brigades**, not the
  mean or the weakest — so a corps's bar shows its toughest brigade, and the
  corps will come apart well before the bar suggests.

Treat the bar as a relative indicator between units of the same type and
nothing more.

---

## 7. Cohesion: what it is for

Cohesion is the formation's tightness, from 1 (perfect) down to 0. It is
**rendered** — the sprites visibly spread as it falls — and it recovers fast,
at 2 per game hour, so a unit standing still re-forms in about half an hour.

**Only movement disorders a formation.** Standing in rough ground costs
nothing at all; the penalty is applied only while the unit has a path. Here is
what moving through each thing costs, as the **ceiling** cohesion can reach
while you are in it ([13](../code-findings/13-morale-and-cohesion.md) §8):

| Moving through | Cohesion ceiling |
| --- | --- |
| Grassland, Road | 1.00 |
| Bridge, pontoon | 0.90 |
| Fences / walls | 0.60 |
| Creek | 0.55 |
| Ford (river crossing) | 0.40 |
| **River** | **0.00** |

Plus a flat 0.15 for moving at all, and **0.25 for having another of your own
units walk through you** — which decays quickly but is the single avoidable
item on this list.

> **Drill does not help with the obstacle.** Training reduces the general
> disruption of movement, but the terrain figure is also applied as a hard
> ceiling that bypasses the training term entirely. A fully drilled brigade
> crossing a river is at zero cohesion exactly like a green one.

### Why zero cohesion is dangerous

Cohesion has four consumers, and three of them bite at once when it is low:

- **Below 30 %** the unit takes a morale penalty (−0.10/hr before
  amplification) and its symbol changes.
- **It is the main term in whether you lose a charge.** The "charged by a
  determined foe" morale hit — the largest single number in §3 at −1.00/hr —
  is scaled by how your cohesion and numbers compare to the attacker's. A
  disordered unit loses that comparison badly.
- **It is a differential in melee casualties**, so you take more.
- The displayed **melee strength** multiplies by `(1 + cohesion)` — but that
  number is a HUD stat that does not feed melee resolution at all
  ([`ui-vs-simulation`](ui-vs-simulation.md) §1.1).

Put together: **the worst moment to be charged is while crossing water**, and
the fix is not drill or a better general, it is halting on the far bank long
enough to re-form. Half a game hour buys back a full formation.

### Fire does not disorder anyone

`unitprefs.txt` ships three values for infantry fire, artillery fire and
cavalry shock disrupting cohesion. **All three are parsed and never read**, and
the accumulator they would feed has no increment path anywhere in the game
([13](../code-findings/13-morale-and-cohesion.md) §9a). Being shot at does not
loosen your formation in this build. Cavalry shock still hits morale and melee;
it just never touches cohesion.

> A modder tuning those three numbers sees nothing happen and reasonably
> concludes they have the wrong file.

---

## 7a. Where a broken unit goes, and what the journey costs

A unit that routs or is ordered to withdraw does not simply move away. A
retreat is a **queued order** with a destination worked out when it is issued,
and the destination search has a ladder of fallbacks that decides how much the
retreat costs you.

**In a battle**, three tiers, in order:

1. **It retraces its own advance.** A unit routing out of melee walks back
   along the positions it came forward through, which is the one case where the
   ground is guaranteed passable.
2. **It rallies on a parent headquarters** — the nearest division, corps or
   army HQ above it **that has no enemy within 70**. That is the rally-point
   rule, and it is why keeping your HQs back from the line is worth something
   concrete: an HQ in contact cannot take your broken brigades.
3. **It just goes backwards** along its retreat angle, with no check that
   anything is there.

> And if no path is found at all, the unit **surrenders — but only if an enemy
> is within its own frontage**. A pocketed unit with the enemy a little further
> off neither escapes nor surrenders; it stands there with no path. That is the
> "stuck" state you occasionally see in a closed pocket.

**In the campaign**, the search looks for the nearest friendly **town, fort,
supply depot, trade node or unrouted friendly unit**, in a ring 35 to 300 map
units out from a probe point 70 units behind the unit, preferring candidates
within 90° of straight back. Then it moves there **at double-quick, with no
order delay** — a retreat is the one order that is not slowed by your
commanders' order-transmission times, and it pays the double-quick condition
cost ([what kills your men](what-kills-your-men.md)).

> **Keep a depot, fort or garrisoned town within about 300 units behind your
> armies.** It is not only a supply anchor
> ([supply in practice](supply-in-practice.md) §4) — it is the thing that
> catches a beaten formation. The next two subsections are what happens when
> there is nothing there.

### 7a.1 Surrender: fighting spirit halves the prisoners, experience trims them

When a unit surrenders, the share of its men taken prisoner starts at
**60 % ± 20**. Full fighting spirit halves it; full experience cuts it by 30 %
([31-retreat](../code-findings/31-retreat.md) §5.3):

| Unit | Prisoners |
| --- | --- |
| Green, no fighting spirit | **40–80 %** |
| Full experience only | 28–56 % |
| Full fighting spirit only | 20–40 % |
| Full experience **and** full fighting spirit | **14–28 %** |

> **Nearly a threefold spread, and nothing in the interface mentions it.**
> Fighting spirit is the bigger lever, which makes this the clearest argument
> for keeping it up on units you expect to be cut off: it is not only about
> whether they break, but about what breaking costs. An earlier version of this
> section said experience also halved the prisoners and called the spread
> fourfold; experience's multiplier ships at 0.7, not 0.5.

### 7a.2 Disintegration: 30–80 % desert, and readiness goes to zero

If a retreat finds no path at all in the campaign, the formation
**disintegrates**. What that means depends entirely on whether a friendly town
is in reach:

- **With a town**: every brigade loses a random share of its men to
  **desertion** and the survivors are relocated to that town. The share is
  read off your **national morale** — **50–80 %** at national morale 0, falling
  to **30–50 %** at 100 — and **readiness drops to zero** for the formation
  and everything attached to it, which forbids most orders until it recovers
  ([supply in practice](supply-in-practice.md) §8).
- **With no town**: the formation is **deleted outright**. Every man is gone,
  with no casualty accounting at all.

> So national morale is not only the loss condition
> ([how you lose](how-you-lose.md)) — **it is also the exchange rate on your
> own collapses.** A disintegration at 30 % national morale costs about 20
> percentage points more of the formation than the same disintegration at
> 90 %, and the difference between losing two thirds of a corps and losing all
> of it is whether a friendly town is in range.

Source: [31-retreat](../code-findings/31-retreat.md) §3, §4, §5.

### 7a.3 Two things to know about the retreat queue itself

**A cut-off unit can be sent toward the enemy.** When the campaign search finds
no friendly candidate at all, what it returns is the position of the **nearest
enemy**, and four of the six paths that queue a retreat do not catch that — so
the unit is waypointed at the enemy, at double-quick. The situation needs a
force with nothing friendly within 35–300 units of a point 70 to its rear,
which is a lone brigade deep in enemy territory. **Untested** in play, but the
derivation is unambiguous.

**Holding the right mouse button suspends every retreat in the game.** The
queue and the pathfinding machinery both stop while that button is down, on
both sides, for as long as the drag lasts. Right-drag is the camera control, so
this is easy to do without meaning to — and easy to do on purpose.

Source: [31-retreat](../code-findings/31-retreat.md) §2, §4.1.

---

## 8. What to do differently

- **Watch the blink, not the bar.** The morale icon starts blinking at 50 %,
  which is exactly the threshold where contact breaks the unit. A blinking
  brigade can hold a firing line and cannot hold ground.
- **Never commit a brigade under 25 % campaign morale.** It is capped at 30 %
  for the whole battle and will break on contact every time.
- **Pull disordered units out of contact, not shaken ones.** A unit broken in
  melee rallies on distance in seconds; a unit broken by shellfire needs an
  hour or more of game time. Rallying the second kind is a real investment.
- **Give a rally order and then leave the unit alone** — any move order
  cancels it.
- **Re-form on the far bank.** Crossing a river zeroes cohesion and nothing
  but time fixes it. Half a game hour at a halt restores a formation; arriving
  disordered in front of a fresh enemy loses the unit.
- **Flanking only pays when it shoots.** A unit parked on an enemy flank is
  worth almost nothing; the same unit firing is worth 31× as much. Get into
  the rear if you want position alone to hurt.
- **Keep formations homogeneous by recruiting state**, and send volunteers
  rather than drafts into states that will be contested.
- **Famous generals high, good leaders low.** The hierarchy weights invert
  between brigade and army level.
- **Do not try to force a battle on a shaken enemy in the open.** Below 40 %
  average morale a formation withdraws to a depot rather than fight; corner it
  against a garrison or cut its depot instead.
- **Drill is the cheapest insurance against the final break.** It is the only
  one of the four inputs to the unbearable-losses trigger you can buy outright,
  and that trigger is unrecoverable when it fires.
- **Keep a depot, fort or garrisoned town within about 300 map units behind
  your armies.** It is what catches a beaten formation, and the difference
  between a disintegration that costs you two thirds of a corps and one that
  deletes it (§7a.2).
- **Keep your HQs out of contact.** A parent headquarters only offers itself as
  a rally point if no enemy is within 70, so an HQ in the line cannot take your
  broken brigades (§7a).
- **Fighting spirit is worth keeping up on units that may be cut off**, not
  only on units that may break. It halves the prisoners a surrender costs, and
  experience halves them again (§7a.1).

---

## 9. Still open

- **Fatigue.** It costs 0.10/hr of morale above 70 % fatigue, only while morale
  is above 30 %, and perk 2 suppresses it entirely — but the rule that raises
  and lowers fatigue has not been traced, so this file cannot say what a march
  costs you in morale terms. **Untested.**
- **Where drill comes from.** Experience is traced (0.0002 per enemy casualty
  inflicted); `training` has no equivalent accumulator in the morale code, and
  the drill mechanic that feeds it belongs with attrition and condition.
- **Whether the inverted artillery cover term changes outcomes in play.** The
  expression is unambiguous; the size of the effect against a real bombardment
  has not been measured. **Untested.**
- **The DLC training ratchet.** In a DLC scenario, a player unit's drill
  appears to climb to its maximum within a few frames, because the accessor
  that reads it also writes to it
  ([13](../code-findings/13-morale-and-cohesion.md) §9d). Base-game play is
  unaffected. **Untested.**
