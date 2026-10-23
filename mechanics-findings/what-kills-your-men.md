# What kills your men

Combat is not the largest column in the ledger, and it is not even the largest
part of a battle's own bill. This is where a brigade's men actually go — in men
per week — and which of it you control.

**Translates:** [`15-attrition-and-condition`](../code-findings/15-attrition-and-condition.md)
and [`11-combat-resolution`](../code-findings/11-combat-resolution.md) §6.
**Summary:** [summary-logistics](../code-findings/summary-logistics.md).
Conventions, including the Derived / Observed / Untested tags, are in
[`README.md`](README.md).

Figures below are for a **full-strength 3,000-man infantry brigade**, because
the wounded rates are keyed to the establishment size rather than to current
strength (§3). Scale them by establishment for other types: cavalry 2,500,
artillery 240, skirmishers 200, scouts 150.

---

## 1. The short version

| Source | Men per week, baseline | Men per week, worst case |
| --- | --- | --- |
| **Disease** | **8** | **35** (winter, bad weather, forced march) |
| Desertion | 0 above 80 % morale | 19 (broken morale, no support, poor general) |
| Combat | 0 between battles | hundreds in a day |
| **Wounded who die afterwards** | — | **see §3; this is the one you control** |

Two things matter more than the table:

- **Over a campaign year, disease alone costs a brigade 14 % of its strength
  in the field and 7 % in camp** — and that is before any fighting.
- **Of 100 men hit by musket fire, 54 end up dead if you keep marching and 31
  if you camp.** The enemy's volley decides who is hit; **you** decide how many
  of them die.

---

## 2. A battle casualty is three dice, rolled per man

Every man lost in combat gets his own roll
([11](../code-findings/11-combat-resolution.md) §6):

1. He starts as **wounded**.
2. A roll against the ammunition's *dead ratio* may make him **killed**.
3. A roll against a flat 15 % may make him **missing** — and this one
   overwrites the previous result.

So the outcome depends on what hit him:

| Hit by | Killed | Wounded | Missing |
| --- | --- | --- | --- |
| Musket fire, or melee | **13 %** | **72 %** | 15 % |
| Artillery shell (explosive) | 17 % | 68 % | 15 % |
| Solid shot | 43 % | 43 % | 15 % |
| **Canister** | **51 %** | 34 % | 15 % |

> **Canister kills four times the share that musketry does.** Guns firing
> canister at close range are not just more damaging per hit, they convert a
> far larger fraction of their hits into graves rather than hospital cases.
> Conversely, three out of four men hit by rifle fire are wounded, not dead —
> which is why §3 is the biggest number in this file.

Two notes on the same roll:

- **Missing overwrites killed**, so the missing share is a flat 15 % whatever
  hit the man, and the killed and wounded shares are both scaled by the
  remaining 85 %.
- A **prisoner** is a different loss type again, and it is the one the game's
  own loss tests exclude. A routed unit in melee has all its losses recorded as
  prisoners ([12](../code-findings/12-melee.md) §2), which is why routing in
  contact is so expensive in men and so cheap in *recorded* casualties.

---

## 3. The wounded pool is where most of the dying happens

A wounded man is not a casualty yet. He sits in the unit's wounded pool and
every campaign tick two absolute rates compete for him — one kills him, one
returns him to duty ([15](../code-findings/15-attrition-and-condition.md) §2a):

| State | Mortality | Recovery | **Share of your wounded who die** |
| --- | --- | --- | --- |
| In the field | 0.66/yr | 0.50/yr | **57 %** |
| **Encamped** | 0.33/yr | 1.00/yr | **25 %** |
| Encamped, level-3 hospital in range | 0.08/yr | 2.50/yr | **3 %** |
| In the field, level-3 hospital in range | 0.17/yr | 1.25/yr | 12 % |

(Rates are per year multiplied by the **establishment size**, so for a brigade
3,000 × 0.66 = 1,980 men a year. The pool drains fast: a 300-man wounded pool
is fully resolved in about a month either way. What changes is not the speed,
it is the split.)

> **Encamping converts one in three of your wounded from a death into a
> returned soldier.** Take 100 men hit by musket fire: 15 missing, 13 killed
> outright, 72 wounded. Keep marching and 41 of those 72 die — **54 dead in
> total, 31 back in the ranks**. Camp instead and 18 of the 72 die — **31 dead
> in total, 54 back in the ranks**. Same battle, same volleys: **43 % fewer
> deaths and 75 % more men returned**, decided entirely by what you do the day
> after.

A level-3 hospital within range turns that into 3 % mortality — effectively
every wounded man comes back. This is the single highest-return building in the
game for an army that fights, and it pays twice: treating wounded also raises
state support in the unit's home state (§8).

**Winter quarters count as encamped** for this, because they are a kind of camp
building. Entrenchment does **not** — entrenching without camping leaves you on
the field rates.

---

## 4. Disease is a steady state, not a spiral

Sickness is not an accumulator. The unit converges on a *sickness level* set by
its circumstances and stays there, with men flowing in from the ranks and out
again — a third of them into graves
([15](../code-findings/15-attrition-and-condition.md) §2b).

The target is **10 % of current strength**, multiplied by:

| Condition | Multiplier |
| --- | --- |
| Forced march | **×1.66** |
| Cold weather (average temperature ≤ 50 °F) | **×1.80** |
| Bad weather (rain, snow, storms), at worst | ×1.40 |
| **Encamped** | **×0.50** |

capped at 50 % of strength. The pool itself turns over at 2.5/yr mortality
against 5.0/yr recovery, so **one sick man in three dies** and the average case
lasts about seven weeks.

Putting the two together gives the steady-state bill for a 3,000-man brigade:

| Situation | Men sick at equilibrium | **Deaths per week** | Per year |
| --- | --- | --- | --- |
| Good weather, encamped | 86 | **4** | 7 % of the brigade |
| Good weather, in the field | 171 | **8** | 14 % |
| Winter, encamped | 154 | 7 | 13 % |
| Winter, in the field | 309 | 15 | 26 % |
| Winter, bad weather, in the field | 432 | 21 | 36 % |
| **Winter, bad weather, forced march** | **717** | **35** | **60 %** |

> **Winter marching costs nearly five times the disease deaths of winter
> camping**, and a brigade kept on forced march through a bad winter loses
> more men to sickness in a year than most brigades lose to the enemy. The
> shipped monthly average temperatures put **five months below the 50 °F
> threshold** — January, February, March, November and December — so the ×1.8
> applies for nearly half the year. (Temperature is read per position from the
> weather system, so the exact months vary with latitude; the five-month figure
> comes from the single shipped average table. **Untested** in play.)

The reassuring half: because it is target-seeking, **marching out of bad
weather lowers the target and the pool drains**. Winter campaigning has a
standing cost, not a runaway one, and a week in camp recovers most of it.

---

## 5. Desertion is morale multiplied by politics

Desertion has a hard floor: it is driven by a **drive** figure that is zero
unless morale is low ([15](../code-findings/15-attrition-and-condition.md) §2c):

```
drive  = clamp(1 − morale − 0.2 × commander fame − 0.2 × commander admin, 0, 1)
rate   = 0.1 × drive² × strength × supportFactor   men per year
```

An average commander (fame and administration both 0.5) subtracts 0.2, so
**a unit above 80 % morale under an average general loses nobody at all.**
Below that, the square makes it bite quickly:

| Morale | Average general | Poor general (no fame, no admin) |
| --- | --- | --- |
| 0.8 | **0 /week** | 0.8 /week |
| 0.6 | 0.8 /week | 3.4 /week |
| 0.4 | 3.4 /week | 7.6 /week |
| 0.2 | 7.6 /week | 13.5 /week |

(3,000-man brigade from a state at 50 % support.)

The political multiplier is the part worth knowing:

```
supportFactor = (5.0 − 1) / (1 + state support) + 1
```

| Home-state support | Factor |
| --- | --- |
| 0 % | **×5.0** |
| 50 % | ×3.7 |
| 100 % | **×3.0** |

> **Full political support does not remove the desertion multiplier, it only
> reduces it from ×5 to ×3.** The pref is labelled *"Additional desertion
> factor if state support … is 0"*, which reads as though the factor should be
> 1 at full support; the formula cannot reach 1. A unit raised in a state that
> has gone over to the enemy deserts 67 % faster than one raised in a loyal
> state, which is a real effect — but desertion never switches off.

Three levers: **commander fame and administration are subtracted directly from
the drive**, so a famous administrator holds a demoralised army together; the
**Guard order** subtracts a further amount scaled by the formation's guard
efficiency; and morale itself is the square term, so the first ten points of
morale you recover are worth more than the next thirty.

Compared with disease, desertion is small — **until morale breaks**, at which
point it roughly doubles your weekly bill and keeps rising.

---

## 6. Condition: the hinge between supply and everything else

Supply never damages a unit directly. It reaches combat through **condition**,
of which there are three separate pools — men, horses and guns
([15](../code-findings/15-attrition-and-condition.md) §1). Each is a 0–100 %
figure moving at these rates per year:

| | Men | Horses | Guns |
| --- | --- | --- | --- |
| Recovery, always on | **+13** | +11 | +9 |
| Forced march | −20 | −16 | **−12** |
| Out of provisions or forage (≤ 5 % in stock) | **−40** | −30 | — |

Two things the labels get wrong or leave out:

- The recovery line is labelled *"when not moving"* and is in fact applied
  **unconditionally**, every tick, marching or not. **Ordinary marching costs
  no condition at all** — only `running` and `doublequick` do.
- Drill buffers the penalties: the forced-march and out-of-supply figures are
  multiplied by `1 − 0.6 × drill`, so a fully drilled unit takes **40 %** of
  them. **Except for guns**, whose forced-march penalty is applied without the
  buffer.

Netting it out for a brigade's men:

| Situation | Net condition change | Time from full to empty |
| --- | --- | --- |
| In supply, any normal march | **+13/yr** (recovers) | — |
| Forced march, undrilled | −7/yr | 52 days |
| **Forced march, fully drilled** | **+5/yr — it recovers** | never |
| Out of provisions, undrilled | −27/yr | 13 days |
| Out of provisions, fully drilled | −3/yr | 122 days |
| Out of provisions **and** forced march, undrilled | −47/yr | **8 days** |
| Guns on forced march, any drill | −3/yr | 122 days |

> **A fully drilled brigade's men gain condition on a forced march while its
> guns always lose it.** Drill is the difference between starving for two weeks
> and starving for four months.

What condition then does: **gun** condition cuts accuracy (up to −60 %) and
slows reloading (up to +40 %); **men** and **horse** condition multiply fatigue
accrual by up to 1.5 each and scale the drill rate; all three feed campaign
morale penalties below 25 %
([why units break](why-units-break.md) §3).

---

## 7. Fatigue: five minutes to exhaust, a hundred to recover

Fatigue accrues from marching and from contact, and drains continuously at
**0.6/hr** whether the unit is moving or not
([15](../code-findings/15-attrition-and-condition.md) §3). Marching accrual is
the terrain's own figure times a pace factor:

| Moving through | Per hour | …at double-quick (×5) | …running (×7) |
| --- | --- | --- | --- |
| **Road** | 0.20 | 1.00 | 1.40 |
| Grassland | 0.25 | 1.25 | 1.75 |
| Fences / walls | 0.20 | 1.00 | 1.40 |
| Bridge, pontoon | 0.30 | 1.50 | 2.10 |
| Ford | 0.40 | 2.00 | 2.80 |
| Creek | 1.00 | 5.00 | 7.00 |
| **River** | **1.50** | 7.50 | 10.50 |

(The normal-pace column is already multiplied by the basic factor of 2.5.)

Set against the flat 0.6/hr recovery, that gives:

> **Marching at normal pace on a road makes a unit fresher, not more tired.**
> 0.20 × 2.5 = 0.50/hr of accrual against 0.60/hr of recovery, so fatigue
> falls. On grass it is 0.625 against 0.60 — a standing draw. **Ordinary
> marching is free; it is the pace and the water that cost you.**

| Doing this | Time from fresh to exhausted |
| --- | --- |
| Normal march on a road | never — fatigue falls |
| Double-quick on grass | 1 h 32 m |
| Running on grass | 52 m |
| Double-quick through a creek | 14 m |
| Running through a river | 6 m |
| **Melee** | **5 m** |

Melee is the outlier: it accrues at **12/hr** scaled by the fraction of the
unit's blocks in contact, with no terrain factor, so a fully engaged brigade is
exhausted in five game minutes — and then needs **1 h 40 m** of standing still
to recover. Sustained firing adds a smaller amount on top.

Experience buffers accrual by up to **40 %**, and low condition multiplies it
by up to 1.5 (men) and 1.5 again (horses, for cavalry, artillery and scouts) —
up to 2.25× for a worn-out battery.

What fatigue costs: **33 % of accuracy** at full exhaustion (non-artillery), a
morale penalty above 70 %, a melee differential, more casualties in
auto-resolve, and speed. Speed least of all — the curve is
`(1 − fatigue)^0.2`, so a half-exhausted unit moves at 87 % and a **completely
exhausted one still moves at 40 %**. Fatigue is a combat penalty dressed as a
movement penalty.

---

## 8. Drill, and why you cannot get it in the field

Drill is the most broadly useful stat in the game — it buffers condition wear,
cohesion disruption and fatigue, raises the unbearable-losses trigger, lifts
the first-battle morale cap and speeds marching. It accumulates at a rate set
by one of four states, and **the states override each other rather than
stacking** ([15](../code-findings/15-attrition-and-condition.md) §4), in this
order, last match winning:

| State | Drill per year | Time from 0 to fully drilled |
| --- | --- | --- |
| In the field | 0.20 | **5 years** |
| **Encamped (summer camp)** | **0.60** | **1 year 8 months** |
| Winter quarters | 0.30 | 3 years 4 months |
| Entrenched (overrides all) | 0.40 | 2 years 6 months |

> **You cannot drill a brigade to full in the field — five years is longer than
> the war.** Only a camp gets you there inside a campaign, and the override
> order has two counter-intuitive consequences: **entrenching a camp cuts its
> drill rate from 0.60 to 0.40**, and **winter quarters are the worst camp to
> drill in** at 0.30, because winter quarters are a camp building and the
> winter-quarters branch overrides the encamped one.

Two multipliers on top: the rate is scaled by **`conditionmen`**, so a worn-out
army cannot drill its way back, and by the **Training Manuals project (118)**,
which is a verified case of a project reaching the unit simulation.

---

## 9. The political bill for your dead

Casualties are not only a manpower cost. At the end of every battle, each
unit's **killed** are charged against political support **in the state that
raised it**, at −0.00004 per death, with a **×1.5 multiplier for the side
fighting on enemy soil**
([15](../code-findings/15-attrition-and-condition.md) §2a,
[11](../code-findings/11-combat-resolution.md) §6).

That is four points of state support per thousand dead — six on enemy soil. A
battle that kills 10,000 of your men drawn from one state costs **40 points of
support there**, and state support is what caps those units' battle morale and
multiplies their desertion ([why units break](why-units-break.md) §5, §5 above).

> **The feedback loop is the thing to notice.** Dead men lower support in their
> home state; low support lowers the morale ceiling of every unit raised there
> and raises its desertion by up to 67 %; lower morale raises desertion again
> by the square term. A bloody campaign fought with troops from a handful of
> states compounds against itself.

Running it the other way: **treating wounded raises support** in the home state
at +0.00001 per wounded man per year, and the project that reduces the
casualty support hit (107) multiplies it down. So hospitals buy back both the
men and the politics.

---

## 10. What the panels get wrong about all this

Four separate problems, all in the attrition readouts
([`ui-vs-simulation`](ui-vs-simulation.md) §1.12):

- **"Disabled Soldiers" includes the dead and the captured.** The headline
  figure is wounded + killed + prisoners + sick. Men who are killed are not
  disabled, they are gone, and they never leave that total.
- **Its own breakdown does not sum to it.** The tooltip lists *currently
  wounded*, *currently sick* and *deserted/MIA* — omitting killed and
  prisoners, which are in the total, and adding deserters, which are not. No
  reading of the three lines reconciles with the headline.
- **The hospital tooltip overstates the benefit by four times.** It prints
  *"reduce mortality of wounded by 300 %"* at a level-3 hospital, which is not
  a possible percentage. The real reduction is **75 %** — the coefficient is
  applied as a divisor, not subtracted. The recovery half of the same sentence
  (*"increases the chance of recovery by 150 %"*) is correct.
- **Deserters are missing from the alliance attrition statistic**, and
  recovered sick are counted into it twice, because of a reused temporary in
  the attrition block. The per-formation total-losses figure also excludes
  deserters in the campaign (it includes them in a battle). Men who desert
  leave the army without appearing in your loss totals.

One more, in the simulation rather than the display: **exhausted troops do not
reload more slowly.** The field the reload penalty reads is never written by
any config file, and the config line intended for it is parsed into a
neighbouring field which the very next pref then overwrites
([15](../code-findings/15-attrition-and-condition.md) §1a). The accuracy
penalty for exhaustion is live; the reload penalty is not.

---

## 11. What to do differently

- **Camp after a battle, before anything else.** It is worth more than any
  other single decision: 43 % fewer deaths among your wounded and 75 % more men
  back in the ranks, from the same casualty list.
- **Build hospitals, and build them where your armies fight.** A level-3
  hospital in range takes wounded mortality from 25 % to 3 %, and pays again in
  state support.
- **Do not entrench a camp you are trying to drill in**, and do not count on
  winter quarters for drill. Entrenchment drops the rate from 0.60 to 0.40,
  winter quarters to 0.30.
- **Keep forced march for emergencies, and never combine it with a supply
  failure.** Alone it costs an undrilled brigade 52 days of condition; with
  empty provisions it costs eight.
- **Winter in camp.** A brigade kept marching through a bad winter loses 60 %
  of its strength to disease in a year; camped, 13 %.
- **March on roads, at normal pace.** Road marching recovers fatigue. The pace
  and the water are what tire troops, not distance.
- **Drill before you need it.** Every penalty in this file is reduced by drill,
  and drill cannot be bought quickly — only a year and a half in camp gets a
  brigade to full.
- **Disperse your recruiting.** Dead men cost support in their own home state,
  and that state's units pay for it twice over in morale ceiling and desertion.
- **A famous administrator is a manpower policy.** Fame and administration each
  subtract directly from the desertion drive; a good one keeps a 60 %-morale
  army from leaking at all.

---

## 12. Still open

- **Replacements.** This file accounts for men leaving; the other half —
  how strength is restored from the recruit pool, and how long a request takes
  to arrive — has not been traced
  ([15](../code-findings/15-attrition-and-condition.md) §6).
- **The five cold months.** Temperature is read per position from the weather
  system; the ×1.8 threshold is 50 °F and the shipped monthly average table
  puts five months below it, but the geographic spread has not been measured.
  **Untested.**
- **What a hospital's range and level actually cover.** The treatment value is
  read from a filter map keyed to your own side's colour, maxed against a
  per-unit range search; the radius and the level-to-value mapping
  (0.4 / 0.65 / 1.0) are known, but which positions on the map a given hospital
  reaches is not.
- **Whether the sickness equilibrium holds in play.** The figures in §4 are
  steady-state solutions of the two rates, not observations. Weather varies, so
  a real brigade oscillates around them. **Untested** — a season in camp and a
  season on the march with the same brigade would settle it.
