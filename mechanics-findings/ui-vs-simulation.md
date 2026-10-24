# What the game shows you, and what it actually does

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Translates:** [10](../code-findings/10-simulation-tick.md),
[11](../code-findings/11-combat-resolution.md),
[12](../code-findings/12-melee.md),
[13](../code-findings/13-morale-and-cohesion.md),
[16](../code-findings/16-enumerations.md),
[17](../code-findings/17-politics-and-support.md),
[19](../code-findings/19-commanders-and-perks.md),
[21](../code-findings/21-ai-battle.md),
[22](../code-findings/22-policy-and-project-catalogue.md),
[24](../code-findings/24-campaign-objectives.md),
[25](../code-findings/25-newspaper-and-events.md),
[26](../code-findings/26-naval-combat.md)

Every number in this game has two lives: the one the simulation computes with,
and the one the interface prints. Usually they are the same number. This file
is about the places where they are not.

It is first in this folder because it is the one thing decompiling gives you
that **no amount of play can**. A player can feel out casualty rates and learn
which policies pay. They cannot discover that a stat in the unit panel is
computed by a different formula than the one resolving the fight, because the
only evidence would be a mismatch they have no way to see.

Read it as a list of things not to trust, not as a list of things that are
broken. Most of the interface is honest — §4 is as long as §1 for a reason.

---

## 1. Numbers you are shown that the simulation does not use

### 1.1 Melee strength is a HUD number

The unit panel's melee strength has **exactly one consumer in the whole game:
the line that draws it.** Melee is resolved by a different function that reads
the raw weapon value and applies experience, fatigue, cohesion and formation
as *differences between the two units*, where the displayed figure applies them
as *multipliers on yours alone*.

The two correlate, so the stat is not useless. But three things are visible in
it that do not reach a single casualty:

- **the mounted melee multiplier** — so cavalry's displayed melee strength is
  inflated relative to its melee performance
- **Spirited charge (perk 4)** and **Cold steel (perk 9)** melee factors — both
  perks do help in melee, but through *morale* and through experience gain, not
  through the casualty formula
- **Close support (perk 14)**'s melee factor, which has **no effect anywhere
  outside the display**. Perk 14 is a real perk — it raises weapon strength and
  reload speed — but not melee.

And two things the resolution uses are **absent** from the display: the
cavalry-versus-formation bonus, and the speed shock of an actual charge.

> **What to do with this:** compare melee strength between two of *your own*
> infantry units in the same formation and it tells you something. Use it to
> predict a melee, especially cavalry against infantry, and it will mislead
> you. Charge momentum and the formation you catch the enemy in matter more
> than the number suggests.

Source: [12](../code-findings/12-melee.md) §3.

### 1.2 The high-attrition morale penalty is shown but never applied

When a formation's sick plus missing reaches **40 %** of strength, the army
panel's morale breakdown reports a penalty of **−9.0 per year**, which the
panel renders as roughly **−17.3 % per week** — the second-largest entry in
the whole breakdown, behind only losing your commander.

The simulation never subtracts it. Campaign morale slots 6, 7 and 9 each end
with a line that applies them to morale. Slot 8 — high attrition — is
computed, stored for display, and then the apply line is simply missing
between slot 7's and slot 9's.

> **What to do with this:** a worn-down army is less fragile than the panel
> claims. The attrition itself still costs you men, condition and combat
> power — but not the morale collapse the breakdown is predicting. Do not pull
> a formation out of the line on the strength of that one row.

The figure shown is averaged across the formation's brigades, so a corps with
one battered division in four shows about a quarter of the full penalty.

Source: [13](../code-findings/13-morale-and-cohesion.md) §9b; verified directly
in `Regiment.UpdateMoraleCampaign` and `CampaignArmyPanel` for this write-up.

### 1.3 The "what is hurting our support" list is always empty

State support is the tap your manpower comes out of, and the game tracks
**42 distinct reasons** it moves. The panel that breaks those reasons down has
two halves: what is helping, and what is hurting.

**The hurting half returns an empty string in every possible game.** The
recent-change tracker it filters on is written as the square root of the
change, and the square root of a negative number is `NaN` — so the one path
that could ever put a reason in the "hurting" list is the same path that makes
the value unreadable. The guard meant to catch it tests `x == NaN`, which is
false for every value in C# including `NaN` itself, so nothing clears it.

> **What to do with this:** you cannot ask the game why support is falling.
> Work from the list of causes instead —
> [17](../code-findings/17-politics-and-support.md) §2 has all 42 with their
> sources, until `the-manpower-tap.md` is written.

A second, quieter consequence: once a reason has *ever* cost you support in a
state, that reason's running total stops being reset, so the "helping" half
drifts out of date too.

Source: [17](../code-findings/17-politics-and-support.md) §2.

### 1.4 The strategic assessment is computed from the UI, not from the game

This one is backwards from everything else here. The *"we hold a commanding
position"* / *"evenly matched"* verdict is not derived from the campaign model.
It is derived from **the ten slider widgets on the strategy panel**, by
averaging their displayed values and then blending toward national morale as
the war polarises.

So the assessment cannot disagree with what it shows you, by construction. But
it has two consequences worth knowing:

- Anything reading it without the strategy panel loaded gets whatever the
  sliders last displayed. That is a real hazard for a mod, and the clearest
  case in the game of the simulation depending on its own interface.
- **One band is missing.** The verdict chain tests up to `5/6`, then resumes at
  `11/12`, so a balance between **0.833 and 0.917** matches nothing and the
  assessment comes back **blank**. If the strategic verdict is ever empty, you
  are in that gap — slightly ahead, and the game has no sentence for it.

Source: [17](../code-findings/17-politics-and-support.md) §6.

---
### 1.5 The weapon tooltip's weight is always 2 lb light

The weapon-icon tooltip in the unit panel adds the two weight fields together
and subtracts 1 to cancel the unused one's `-1` sentinel — but exactly one
field is ever populated, so cancelling it needs **+1**, not −1. Every weapon
in the game is therefore reported **2 lb lighter than its data**: a Springfield
rifle-musket (9 lb) reads 7, a 6-pounder field gun's 884 lb tube reads 882.
Nothing in the simulation uses either field, so this is cosmetic — but it is
the only place a player can read a weapon's weight at all.

Source: [11](../code-findings/11-combat-resolution.md) §2.

### 1.6 The Loss Resilience bar cannot be read as a percentage

The hearts bar is filled as `this unit's break threshold ÷ the threshold a
perfect unit of this type would have`, and the darker overlay as `losses so
far ÷ that same perfect-unit threshold`. So neither level is a fraction of the
unit's own strength, and a full-looking bar on an elite regiment and on a
green one mean different things.

Two smaller gaps in the same widget: the bar counts **prisoners** among the
losses while the rout test that it is displaying excludes them, so a unit that
has lost men captured reads closer to breaking than it is; and the
perfect-unit reference leaves out the ×0.5 skirmisher penalty, so a skirmisher
detachment's bar is drawn against a standard it can never reach. The
perfect-unit reference also does **not** substitute perfect drill — it
substitutes 1.0 for national military experience, unit experience and
commander leadership but leaves `GetTraining()` as the unit's own, so the
denominator moves as the unit trains.

**On a formation the bar shows its toughest brigade.** For anything above
brigade level the threshold is computed as the **maximum** over the attached
brigades, not the mean or the weakest — so a corps whose leading brigade is
elite reads as resilient while its green brigades are already at their own
limit, and the corps comes apart well before the bar suggests.

The underlying mechanic is sound and matches the manual — experience, drill and
the commander's leadership all raise the threshold, from 20 % losses for a raw
regiment to 66 % for a veteran one. It is only the readout that is hard to
interpret.

Source: [13](../code-findings/13-morale-and-cohesion.md) §6,
[manual-vs-code](manual-vs-code.md) §14.

### 1.7 Every enemy figure is distorted by the enemy's own generals

With fog of war on, every number the game reports about the enemy — strength,
guns, losses, the whole statistics screen — is multiplied by one variation
factor before it is shown. The factor is built from four inputs, and **three of
them belong to the enemy, not to you**:

| Input | Shipped span | What it does |
| --- | --- | --- |
| Share of enemy units you can see | 0.90 (none) → 0.10 (all) | the real signal; spotting works |
| **Enemy** commander's initiative | ×1.0 → ×0.2 | a bolder enemy general makes your estimate 5× tighter |
| **Enemy's** average intelligence gathering | ×0.3 → ×0.0 | the enemy scouting you sharpens your view of him |
| **Enemy** commander's cunning | bias +0.5 → 0.0 | a *less* cunning enemy makes you over-estimate him more |

So **appointing a cunning or energetic general of your own does nothing for
your intelligence picture.** The only lever that works is getting eyes on enemy
units. The config labels say the opposite was intended — *"when **having** full
initiative"*, *"if **we** have full intelligence"* — and under that reading the
directions make sense.

The same line has a second defect: the intelligence multiplier is computed as
`0.3 − intel × 0.3`, spanning `[0.3, 0.0]`, where the label describes
`1 − intel × 0.7`, spanning `[1.0, 0.3]`. As shipped the estimate is up to
**3.3× tighter than designed** with no scouting at all, and **exactly correct**
at full intelligence — which is why enemy figures in a well-scouted battle look
suspiciously precise.

Source: [19](../code-findings/19-commanders-and-perks.md) §2.6,
[manual-vs-code](manual-vs-code.md) §24.

### 1.8 Three rows of the Strategy panel measure something else

Three rows of the Strategy panel measure something other than what they are
called.

**European Relations** shows `GetInterventionProbability(…) × 100` — the raw
difference between the two sides' accumulators, divided by Britain's 1.35
handicap where applicable, divided again by the chapter's requirement, and
clamped to [0, 1]. Three things follow for a reader of that number:

- **in Chapter I it reads 0 for both sides no matter what**, because the
  chapter requirement is the sentinel value 100, which the function turns into
  a hard zero. Every point of pre-war diplomacy is invisible until the war
  starts;
- **the same lead displays as a different number in each chapter** — 40 points
  of Confederate advantage over Britain shows as 40 in Chapter II, 30 in
  Chapter III and 20 in Chapter IV;
- **the Union's figure is Spain alone.** Britain's and France's probabilities
  appear only in the row's tooltip, so the headline number tells a Union player
  nothing about the two powers that can intervene against them.

And when it does read 100 there is no roll: the intervention trigger is
`>= 1f` with no `Random` anywhere, so the next qualifying major victory brings
the power in.

**National Support** is a population-weighted average over **every town you
own**, of your support in that town's state — not an average over loyal
states. Big cities dominate, and every enemy town you capture joins the average
at your low support in enemy territory. Since this figure is the linear
multiplier on policy research speed, **taking enemy cities slows your own
legislature.**

**Loyal States**, in the row below, is a *share*: `yours / (yours + theirs) >
0.65`. A state at 20/5 counts as loyal; a state at 70/40 counts for neither
side. The recruiting list reads the same 0.65 as an **absolute** support level,
so a state can be loyal on this panel and neutral in the recruitment filter at
once.

Source: [17](../code-findings/17-politics-and-support.md) §2, §4a–4d, §6,
[manual-vs-code](manual-vs-code.md) §30, §33.

### 1.9 A fleet's Firepower, and its turret guns that are not

Two numbers on the fleet panel do not mean what they look like.

**Firepower** is built by calling each ship's firing-strength function with a
distance of **zero**: every gun counts as in range and the rate-of-fire term
takes its best value. The function that hands that total to a naval battle
takes the real fleet distance as an argument and **never reads it**. So the
figure you compare before committing to an action is both sides' close-action
strength, whatever range the battle is actually fought at — and it is also the
figure that sets the victory balance, while the per-ship damage rolls use the
real range.

**Guns in turrets** is counted one way for the panel and another for the
model. The panel counts every gun whose weapon system has any turret; the
rate-of-fire bonus counts only systems assigned to exactly **one** turret. The
three ships whose guns are divided between two turrets — the **Ocean Casemate
Ironclad**, the **Ironclad Turret Ship** and the **River Monitor** — are shown
with turret guns and get no turret bonus.

A third, smaller gap: the fleet divides four of its six strength-breakdown
figures by the number of attached ships **including the disabled ones**, so a
squadron with half its hulls in repair shows roughly half the average range and
rate of fire it will actually fight at.

Source: [26](../code-findings/26-naval-combat.md) §3, §4,
[manual-vs-code](manual-vs-code.md) §39, §40.

### 1.10 The speed panel cannot show the speed you are running at

The timer panel has four speed buttons and lights one of them only when
`gamespeed` is exactly equal to that button's ladder value — `{1, 2, 5, 20}` in
battle, `{1, 5, 20, 50}` in the campaign. The four number keys set exactly
those values, so the display is correct for them.

The **increase-speed and decrease-speed keys do not use the ladder.** They call
`SetGameSpeed(gamespeed ± 1f)`, clamped only at the array tail. So one press
of increase-speed from 1× in the campaign puts you at
**speed 2 — a rate the panel has no button for, with nothing highlighted at
all**. Speeds 3, 4, 6 … 49 are reachable the same way, and the game is then
running between two and fifty times normal with no indication of which.

Two related behaviours have no display at all:

- **Decrease-speed at 1× pauses the game**, because the arithmetic reaches
  zero and zero is the pause value. The previous speed is preserved, so the
  next unpause returns to 1×.
- **Holding Left Ctrl with a unit selected pauses the game** for as long as the
  key is down, so you can lay a path on a still map. This is deliberate and
  useful; it is simply undocumented, and the pause indicator does not
  distinguish it from a real pause.

A third is visible but easy to misread: a battle's overnight sequence sets
speed 1 and then pauses, so **the second morning of a multi-day battle always
resumes at 1×** rather than at the speed you were using the previous
afternoon.

Source: [10](../code-findings/10-simulation-tick.md) §8, §11,
[time-and-speed](time-and-speed.md) §2.
### 1.11 The morale panel's own arithmetic is off by three to five

The unit panel prints a full morale breakdown — *"Influences on morale per
hour"*, every contributing factor by name, and *"Overall change per hour"* as
the total. The factor list is honest: those are exactly the values the
simulation uses, with the right signs and the right labels.

The total is not. After the simulation has added up those same factors it takes
the net change and, **if it is negative**, multiplies it by
`5 − experience − leadership` before applying it — three times for an elite
brigade under the best general, **five times for a green one under a poor
general**. The panel never applies that step.

So a panel reading *"Overall change per hour: −40 %"* on a green brigade is
describing a unit actually losing **200 %** of its morale bar per game hour. On
an elite brigade the same reading means 120 %. The error is one-directional:
when a unit is gaining morale the panel is exact, because the amplifier only
fires on losses.

This is also why the morale bar sometimes seems to collapse out of nowhere.
The two largest factors in the table are both charge-related, at −1.00/hr and
−0.50/hr before amplification; a disordered brigade meeting a successful charge
is shedding three to five morale points per game hour, which empties a full bar
in twelve to twenty game minutes.

Source: [13](../code-findings/13-morale-and-cohesion.md) §4,
[why-units-break](why-units-break.md) §3.
### 1.12 "Disabled Soldiers" counts the dead, and its breakdown does not sum

The campaign army panel's **Disabled Soldiers** figure is
`losses[0] + losses[1] + losses[3] + sick` — **wounded + killed + prisoners +
sick**. Men who were killed are not disabled, and they never leave the total,
so the number a player reads as "men I could get back" permanently contains
every soldier the unit has ever lost dead or captured.

Its own tooltip then breaks the figure down into three lines — *currently
wounded*, *currently sick*, *deserted/MIA* — which **omit killed and
prisoners** (both in the total) and **add deserters** (not in the total). There
is no reading of the breakdown that reconciles with the headline, in either
direction.

Two more problems in the same widget:

| It says | What is true |
| --- | --- |
| *"Nearby hospitals reduce mortality of wounded by 300 %"* at a level-3 hospital | **75 %.** The coefficient (−3) is applied as a divisor — `mortality /= 1 − value × −3` — so a level-3 hospital divides mortality by 4. The tooltip prints the raw coefficient as a percentage, producing a figure that is not possible |
| *"…and increases the chance of recovery by 150 %"* | correct — recovery really is multiplied by 2.5 |
| the formation-level *deserted/MIA* line | it is desertions **above their own three-month trailing average**, a recent-rate measure rather than a count. The brigade-level line says so (*"(*trailing 3 months)"*); the formation line does not |

And one gap behind the numbers rather than in them: **deserters are absent from
the alliance attrition statistic**, because the line that adds to it reuses a
temporary still holding the recovered-sick count — so recovered sick are
counted into it twice and desertion not at all. The per-formation total-losses
figure also drops deserters on the campaign map while including them in a
battle.

> Net effect: **men who desert leave your army without appearing in any loss
> total you can read**, and men who are killed never stop appearing in a figure
> labelled as recoverable.

Source: [15](../code-findings/15-attrition-and-condition.md) §2a, §2c,
[what-kills-your-men](what-kills-your-men.md) §10.

### 1.13 "Estimated reenlistment ratio" is one unit's dice roll

The campaign top panel prints a national *Estimated reenlistment ratio*
alongside the current contract length. The field it reads is **assigned, not
accumulated**: whichever unit most recently reached the end of its contract
writes its own roll — a figure drawn from *that unit's home state's* support,
with a ±20 % random swing — into the alliance's slot, and the alliance pass
then smooths it into a trailing average.

So the number is not an average over your army, and it is not a forecast. It is
the last expiry's result, decayed. The **"men whose contracts expire soon"**
figure is computed from that same smoothed value against every unit whose term
lapses in the next quarter, so one roll in a 20 %-support state can make the
whole army's forecast look catastrophic — and one in a 95 % state can hide a
genuine problem.

Two related gaps in the same area:

| It shows | What is true |
| --- | --- |
| three reinforcement priorities, low / medium / high | **low is off.** The multiplier steps are 0, 0.5 and 1.0, so a unit on the lowest setting receives no replacements at all, ever |
| state support's "what is hurting us" breakdown | **permanently empty** — every support loss writes a NaN that the list's filter rejects. The gains list works (§3) |

Source: [17](../code-findings/17-politics-and-support.md) §5b, §2,
[14](../code-findings/14-supply-and-logistics.md) §3b,
[the-manpower-tap](the-manpower-tap.md) §8.

### 1.14 The 1864 election tooltips promise −20 and deliver the campaign

Two campaign objectives key off 9 November 1864, "Maintain Morale Above 50" for
the Union and "Union Morale below 50" for the Confederacy. **Each tooltip reads
*"Union national morale −20"*.**

Both are live in the same chapter, both are in their unmet branch when Union
national morale is below 50 % that day, and the objective sweep walks them in
id order inside one loop — so the Union is charged **−20 twice**. The charge
only happens from below 50 to begin with, so −40 lands the Union under the
25 % loss threshold, and "Break Union Morale" is evaluated in the same sweep,
which runs every ten game minutes.

| The tooltip implies | What happens |
| --- | --- |
| a −20 setback to recover from | −40, and the campaign ends that afternoon |
| the loss arrives at the March 1865 inauguration | that branch is unreachable in both directions |
| nothing about a threshold | it is a pass/fail test at exactly 50 points |

> **What to do with this:** for a Union player, 9 November 1864 is the single
> most important date in the campaign and the tooltip does not say so. Plan
> 1863–64 around being at 50 or better that week. Clearing it removes the cliff
> permanently — holding the line is exactly the condition that advances the
> chapter and switches both objectives off.

Two breakdown lines in the same panel cannot be read back to a cause either:
*"Battles (Objectives)"* collects invading the North, five consecutive land
victories, five consecutive naval victories and beating a European
intervention; *"Elections (Objectives)"* covers both this charge and the inert
1862 midterm rate.

Source: [24](../code-findings/24-campaign-objectives.md) §3.2, §6.1,
[how-you-lose](how-you-lose.md) §7.

### 1.15 The policy panel's two day counts, and its counter

The policy panel prints a remaining-days figure **on** each policy in research
and a different one **in** that policy's tooltip.

| Where | What it computes |
| --- | --- |
| On the policy | the nominal duration × days remaining — **no divisors at all** |
| In the tooltip | the same, divided by average state support, by the `N^1.1` parallelism penalty and by the Administration Reform bonus |

The tooltip is the honest one, and it even itemises the three penalties in
words. The figure printed on the policy is what the research would take at
100 % support with nothing else in the queue — a condition that essentially
never holds. **At 75 % average support with three policies in research the
printed number is 4.5× too optimistic**: a Military IV reading "75 days" is a
335-day project.

The same panel's `n / 12` counter is also not what it looks like. It reads as
a queue width — "you may research nine things at once" — and what it actually
counts is **every ladder policy you have ever completed**, because only *acts*
retire from the list when they finish.

| It looks like | What it is |
| --- | --- |
| how many policies you may research simultaneously | how many of your **20 ladder levels** your nation may ever hold, for the rest of the campaign |
| a limit you can raise later | a limit whose **reduction deletes finished policies** and everything downstream of them in the prerequisite graph |

> **What to do with this:** hover before you commit, research one policy at a
> time, and treat the counter as a permanent allowance — twelve slots buy the
> top of three of the five branches and no more. Never lower the politics
> subsidy slider once you are at the cap.

One more omission in the same area: the **projects** panel's "active effects"
summary is built for only 24 of the 37 reform projects. The eleven it never
lists include every structural reform — Cavalry Reform, Artillery Reform,
Organization Reform, Command Reform, Supply Reform, Horse Artillery, 6-gun
Batteries, Improvised Shipyards and Military Education — while it does list
Market Reform, which has no effect at all.

Source: [22](../code-findings/22-policy-and-project-catalogue.md) §3.1, §3.2,
§12.5, [policy-order](policy-order.md) §2.3 and §3.

## 2. Things the game tells you in words that are not true

### 2.1 The perk tooltip describes a system that does not exist

The in-game tooltip says *"the amount of available perks is limited for each
nation, depending on its policies and technologies."*

**No such gating exists.** The function that would enforce it returns `true`
unconditionally and is **never called**. What limits which perks you see is a
fixed list authored into the UI prefab — the same list in every campaign, for
both sides, regardless of policy or technology.

> **What to do with this:** do not research anything expecting to unlock perks,
> and do not read a missing perk as "not yet available to my nation." It is
> either on the prefab list or it is not.

Source: [19](../code-findings/19-commanders-and-perks.md) §1.6.

### 2.2 Every battle newspaper reaches the same verdict on morale

The newspaper after a battle writes its home-front paragraph by comparing the
battle's national-morale effect against two thresholds. Those thresholds ship
at **−1.0** and **−2.0**. The value they are compared against tops out around
**−0.026** for a 30,000-casualty bloodbath, on a scale that runs 0 to 1.

They are two orders of magnitude out of range, so the comparison has the same
answer after every battle ever fought. Headline four is always *"Support for
the … cause wavers."* The closing paragraph always has *"the public unmoved by
the recent events."* The authored alternatives — including an entire register
about a public *"in shock after the licking"* — are unreachable.

> **What to do with this:** the newspaper's morale language carries **no
> information**. It is the same sentence after a skirmish and after Antietam.
> Judge the political cost of a battle from the national morale figure itself.

**For modders this is a two-number fix in a data file** — and it is a trap.
Correcting the thresholds alone makes things worse, because the paragraph reads
the *player's* morale effect while narrating the *enemy's* home front, and the
exporter zeroes the winner's entry. Fix the index and the thresholds together
or not at all; the details are in
[25](../code-findings/25-newspaper-and-events.md) §5.5.

### 2.3 Your cavalry's status line does not say what you ordered

Order a foot unit to advance and its status reads **"Advancing."** Order
cavalry to advance and the game quietly remaps the order, and the status reads
**"Marching"** — the same word as an ordinary move, with the same
"Running" / "Double Quick" variants. Order cavalry to fall back and it reads
**"Retreating"** rather than "Falling back." The status label table was never
extended to cover the remap.

The first of those matters more than a wording quibble, because the mode
cavalry lands in **is the state a unit charges from**. A unit in it will pick
the nearest enemy in range, weigh the strength ratio, and launch a charge on
its own.

> **What to do with this:** *advance* is a more committal order for cavalry
> than for infantry — closer to "attack what you find" than "move up" — and
> the unit panel will not distinguish it from a march. If you want cavalry to
> reposition without charging, move it rather than advancing it.

Source: [16](../code-findings/16-enumerations.md), `movementmode`; the charge
promotion is `Regiment.UpdateChargeTarget`,
[21](../code-findings/21-ai-battle.md) §12.1.

### 2.4 The president is scenery

The game shows you a president, with a name, a title, an inauguration date and
a portrait, and it runs elections in 1862 and 1864 as campaign objectives.

**Nothing mechanical reads who the president is.** The value is recomputed from
a date table whenever it is needed, the scenario loader accepts a
"chosen president" argument and never looks at it, and the elections — which
are real, and do move national morale — do not change the office holder.

> **What to do with this:** elections matter; presidents do not. Play the
> election for its morale swing, not for who wins it.

Source: [19](../code-findings/19-commanders-and-perks.md) §8.

---

### 2.5 Two accuracy grades share a word, and one grade is never used

Every weapon's accuracy is one of five data values, turned into a word by a
five-entry label list. A float truncation in the index arithmetic means the
two worst values collide and the middle label is unreachable:

| Weapon accuracy | Word shown | Land weapons |
| --- | --- | --- |
| 0.8 | Poor | 8 |
| 0.9 | Poor | 4 |
| 1.0 | Mediocre | 22 |
| 1.1 | Very Good | 13 |
| 1.2 | Excellent | 6 |

So **"Poor" covers two different weapons grades**, and **"Good" is never
printed for anything in the game.** Comparing two weapons both labelled *Poor*
in the production list gives no way to tell that one is 12 % more accurate than
the other.

The official manual uses a six-grade scale starting at *Very Poor*, which maps
cleanly onto the five data values — so the shipped list looks like that ladder
with its first rung removed, and its index clamp was never retightened. The
visible consequence is that **the manual's word for a 1.1 or 1.2 weapon is one
grade below what the game prints**, which is most of the better weapons in the
game.

Worse, the number the label describes does almost nothing: see
[manual-vs-code](manual-vs-code.md) §11 for why the accuracy stat is inert for
every weapon rated 1.0 or below.

Source: [11](../code-findings/11-combat-resolution.md) §2,
[manual-vs-code](manual-vs-code.md) §10.

### 2.6 Rail transport is described as costing readiness and saves it

The readiness chapter says *"Transporting the unit using railroads or ships
will cost additional readiness."* The multiplier applied while a unit is
embarked is **0.75**, against a per-year consumption rate — so an hour on a
train costs three quarters of an hour's marching, and covers far more ground.
**Rail movement is the cheapest way to move an army in readiness terms.**

The same chapter lists *"Bad weather"* among the things that slow readiness
**recovery**. Weather does not appear in the recovery chain at all; it affects
consumption, and only as winter, at ×4.0. And *"Siege combat"* is listed as a
recovery malus when it is a −5 per year **consumption** — a besieging army's
readiness falls rather than merely failing to rise.

Source: [14](../code-findings/14-supply-and-logistics.md) §10,
[manual-vs-code](manual-vs-code.md) §18.

### 2.7 The Confiscation Act's supply bonus is a penalty

*"Supply efficiency of armies operating in enemy states + 15 %"*, says the
Union Acts table, and the config label agrees. The factor multiplies the
**time** an army takes to forage, so local supply on enemy soil becomes **13 %
slower** once the act is passed. The act's contraband-slaves half works
normally, so it is still worth taking — just not for this.

Source: [14](../code-findings/14-supply-and-logistics.md) §3a,
[manual-vs-code](manual-vs-code.md) §18.2.

### 2.8 The monthly intelligence report names the wrong European powers

The Secretary of War's monthly report carries one paragraph on European
relations, and it is unusable. Four defects in a single block:

- as the **Confederacy**, the line about France prints Britain's figure, and
  its "change during the recent month" subtracts France's stored value from
  Britain's current one;
- as the **Union**, the line is headed with Spain's name and prints **France's**
  pro-Confederate probability;
- that line's month-over-month baseline is written from an index that is
  always zero, so a Union player is **never** shown a monthly change.

The Strategy panel's tooltip has the correct per-power figures, so read those
instead.

Source: [17](../code-findings/17-politics-and-support.md) §4d,
[manual-vs-code](manual-vs-code.md) §31.3.

### 2.9 Three Acts promise a spending cap that no longer exists

Letters of Marque, Legal Blockade and Organized Blockade Running all advertise
raising *"maximum trade war subsidies"* by 20–40 %. **There is no Trade War
subsidy category** — it was cut, and its id is now used by Diplomacy. Two of
the three prefs are parsed and never read; the third was repurposed, and in
the Confederacy's favour: Organized Blockade Running actually makes **your own
ports 25 % harder to blockade**, which the description never mentions.

Source: [22](../code-findings/22-policy-and-project-catalogue.md) §10.1,
[manual-vs-code](manual-vs-code.md) §28.

### 2.10 Four sentences about naval combat describe the wrong mechanism

The ship-types chapter explains naval combat in one paragraph, and three of
its clauses point at mechanisms the code does not have:

| It says | What happens |
| --- | --- |
| *"The larger the ship, the easier it is to hit"* | target size is **not** in the hit probability. A big ship is **picked** as a target more often (`ceil(size)` entries in the draw) and takes proportionally **less** condition damage per hit (`hull = size × 50`), which roughly cancel |
| *"The stronger the armor, the less damage it will suffer when hit"* | armour sets the **penetration probability** only; a penetrating hit does the same damage whatever the armour was |
| *"armor can even deflect shots without damage"* | exactly right — this is the whole of what armour does |
| *"If armed with turrets, the ship can fire … with more accuracy"* | turrets give **rate of fire** (×1.4), never accuracy. And three of the six turret ships do not get even that (§1.9) |

One more, from the Fleet Information chapter: *"The fleet can travel only as
fast as its slowest ship"* is true, and incomplete — a ship's speed is scaled
by its condition down to 30 % of rated, so **one shot-up vessel slows the whole
squadron** until it is repaired or detached.

Source: [26](../code-findings/26-naval-combat.md) §3, §5,
[manual-vs-code](manual-vs-code.md) §37.
### 2.11 Artillery's cover discount applies in the open, not under cover

`unitprefs.txt` ships *"Morale effect reduction factor of incoming artillery
fire when behind full cover"* at 0.75, and the simulation does subtract 75 % of
the incoming-artillery morale penalty — but it multiplies that discount by
`(1 − cover)` rather than by the cover value.

`covervalue` rises with cover; it is the same field the panel's *"In cover"*
morale row reads to **add** morale for being protected. So the artillery
discount is **zero when the unit is in full cover and 75 % when it is standing
in the open**:

| Situation | Artillery morale penalty applied |
| --- | --- |
| Brigade behind a stone wall or in a fortification | **all of it** |
| Same brigade standing in an open field | **a quarter of it** |

The distance term in the same expression is correct — closer batteries
demoralise harder, from about −0.16/hr per battery at 25 yards to −0.01/hr at
400. Only the cover factor points the wrong way. Cover's protection against
actual **casualties** is a separate mechanism and is unaffected; this is the
morale row alone, printed in the panel as *"Under artillery fire"*.

**Untested:** Derived from the expression and the pref label. The test is a
brigade under sustained bombardment with and without cover, watching that row.

Source: [13](../code-findings/13-morale-and-cohesion.md) §3,
[why-units-break](why-units-break.md) §3.

## 3. Knobs that are exposed and do nothing

Each of these is labelled in a config file the game ships for players to edit,
and reaches nothing.

| Where | Label | Status |
| --- | --- | --- |
| `debug.txt` | "Use AI defensive fallbacks if outflanked" — ships **on** | The only function reading it is **never called**. The AI has no outflanked-fallback behaviour. |
| `unitprefs.txt` | "Cohesion increase by enemy infantry fire" (0.001) | Parsed, never read. |
| `unitprefs.txt` | "Cohesion increase by enemy artillery fire" (0.01) | Parsed, never read. |
| `unitprefs.txt` | "Cohesion increase by enemy cavalry shock" (0.15) | Parsed, never read. |
| `battleprefs.txt` **and** `campaignprefs.txt` | "Maximum Gamespeed" (10) | **No consumer anywhere**, in either file. The real ceilings are hardcoded: ×20 in battle, ×50 in the campaign — and the +/− keys reach every integer below them ([10](../code-findings/10-simulation-tick.md) §11). |
| `unitprefs.txt` | "Minimum support value that is taken into account for fighting spirit caps (before the leverage!)" (0.20) and "The leverage to the support value that caps the fighting spirit of the units" (2.0) | Parsed, never read — and the config is **honest**, marking both `-> inactive`. Their absence is why a unit's morale cap is a mild `1 − (1 − support) × 0.20` rather than the leveraged, floored curve the labels describe ([13](../code-findings/13-morale-and-cohesion.md) §9c). |
| `unitprefs.txt` | "Fatigue effect on reloading time for exhausted troops (linear interpolation)" (1.25) | Parsed into `fatigueforreloading`, which the **next pref in the same loader overwrites**. The field the reload penalty actually reads, `fatigueinfluenceonreloading`, is never parsed by any file and is permanently 0, so **exhausted troops do not reload more slowly** ([15](../code-findings/15-attrition-and-condition.md) §1a). |
| `aistrategies.dat` | night-retreat trigger (0.275) | Computed and discarded on the two paths that would use it. |
| commander prefs | subordinate command limit | No consumer — and the config is honest about it, marking the pref `[deactivated]`. |
| `battleprefs.txt` | "No micro ai charge if unit is outflanked or receiving flanking fire" — ships **true** | Parsed, never read. A unit under the Charge order will go in while outflanked. |
| `unitprefs.txt` | linear and exponential accuracy-over-distance terms | Parsed, never read. Range does degrade fire, but through aim scatter, not these. |
| `unitprefs.txt` | "Probability of commander dead / wounded if hit on group was detected" (0.005 / 0.02) | Parsed, never read. HQ commander losses use the brigade values scaled by hierarchy depth instead. |
| `campaignprefs.txt` | "Upkeep cost factor of transports for transportation subsidies" (0.5), "Increased IIP transport capacity…" (1.5), "Increased railroad construction speed…" (2.0) | Parsed, never read. **The Transportation subsidy category was removed**; its effect constants were left in the file. Seven more in the same block are dead for the same reason. |
| `campaignprefs.txt` | "Max supply ratio of units retreating or on forced march" (0.25) | Parsed, never read. Forced march cuts resupply to **zero**, not to a quarter. |
| `campaignprefs.txt` | "Readiness improvement factor based on intelligence value" (0.008) | Parsed, never read — and marked `[deactivated]`, unlike its live near-namesake. |
| `campaignprefs.txt` | "LettersMarque, add on to max trade war subsidies" (1.2) and "LegalBlockade, add on to max trade war subsidies" (1.2) | Parsed, never read — the same removed Trade War category. Both Acts advertise the cap rise in Appendix A. |
| `campaignprefs.txt` | "Improvement of intervention level per year of full diplomacy subsidies" (0.5) | Parsed, never read. European relations move only through Acts, policies and the Send Envoys project. |
| `campaignprefs.txt` | "ROF factor min distance (close combat)" (1.00) | Read, and unreachable. The distance ratio it blends against is clamped to **at least** 1 and can never exceed 1, so naval rate of fire is always the long-range factor (0.25). |
| `shiptypes.dat` | each record's ship-level `turrets` count | Parsed, never read. The live turret figures are the per-weapon-system `withinturret` field and the derived `gunsinturrets`. |
| `shiptypes.dat` | each record's three `requiredtechids` slots | **−1 in all 41 records.** Ship availability is carried entirely by the record's last line and by the twelve ship-granting projects. |
| `unitprefs.txt` | "Adjustment factor for experience to influence commander attributes (admin, initiative, cunning, leadership)… **[deactivated]**" (0.5) | **The opposite trap: it is live.** Every attribute getter multiplies by `exp × 0.5 + 0.5`, so a green commander really does operate at half his nominal attributes. The only `[deactivated]` label found so far that lies. |

The first four add up to one missing feature: **enemy fire does not disrupt
your formations.** The accumulator those three values would feed has no
increment path anywhere in the game — only the decay half is wired, so it sits
at zero forever. Cavalry shock still hits morale and melee; it just never
touches cohesion. Three config labels name infantry fire, artillery fire and
cavalry shock as disrupting a formation, and none of them do.

> **For players:** formations do not come apart under fire in this build.
> Cohesion loss comes from movement, charging and terrain.
>
> **For modders:** tuning those three numbers produces no effect, and the
> natural conclusion — "wrong file" — is wrong. This is the single clearest
> justification for the project.

Sources: [21](../code-findings/21-ai-battle.md) §12.4 and §15,
[13](../code-findings/13-morale-and-cohesion.md) §9a,
[10](../code-findings/10-simulation-tick.md),
[19](../code-findings/19-commanders-and-perks.md) §2.5.

**And one knob whose number is eaten on the way in.** `unitprefs.txt` ships
*"Fatigue effect on reloading time for exhausted troops… how much longer do
exhausted troops need to reload?"* = 1.25. The loader parses it into the wrong
field, a later line overwrites that field, and the field the reload formula
actually multiplies by is never loaded at all — so it stays zero and
**exhausted troops reload exactly as fast as fresh ones**. A sweep of all
1,972 `GamePrefs` constants found this to be the only gameplay value in the
game that is used in a formula and never loaded
([07](../code-findings/07-config-data-model.md),
[11](../code-findings/11-combat-resolution.md) §3f).

**One real setting behaves worse than it reads.** The options checkbox for
newspaper messages is checked *inside* the function that queues a paper, so
turning newspapers off **discards** them rather than suppressing the panel.
Papers skipped that way are gone, including ones that would have reported a
battle you fought ([25](../code-findings/25-newspaper-and-events.md) §1.5).

**And one knob that is read, and shipped at the value that means "off".**
`campaignprefs.txt` ships *"Adjustment of movement speed (factor) if state
support is zero [<1]"* as **1.0**. The term is a blend between 1 and that
factor, so at 1.0 it is the identity at every support level: the
hostile-territory movement penalty the manual describes in its Support
map-filter section does not exist, and the *"Low state support (−x %)"*
rolling text that would announce it can never print. This is the cheapest
restorable mechanic found so far — lowering the number switches it on
([17](../code-findings/17-politics-and-support.md) §2,
[manual-vs-code](manual-vs-code.md) §33).

---

## 4. What is honest

A list of discrepancies reads worse than the game deserves, so:

**Every gameplay setting in the in-game options panel is wired.** All eleven
checkboxes and sliders that touch the simulation — interventions, commander
initiative, continuous objective points, pause after battle, remain paused,
HQ in the saddle, downsized large battles, uniform variations, battle hints,
historical fonts, tooltips — reach a real consumer in the simulation or the
renderer. **The dead switches are all in the config files, not the UI.**
*(Swept across the whole assembly for this write-up.)*

**The weapon stats the UI shows are consistent with the ones the battle uses.**
The game stores every weapon measurement twice, imperial and metric, and the
layers disagree about which to read: **a land battle uses `range_m`, and every
number you are shown uses `range_yd`** — along with the AI's weapon valuation,
the economy's pricing and naval engagement ranges. Nothing in the code keeps
the pair in sync.

In the shipped data they are in sync **exactly**. All 87 weapons check out to
the last digit, on all eight imperial/metric pairs:

| Pair | Weapons with a value | Out of sync |
| --- | --- | --- |
| `range_yd` / `range_m` | 87 | 0 |
| `velocity_fts` / `velocity_ms` | 52 | 0 |
| `penetrationinch` / `penetrationmm` | 52 | 0 |
| `tubeweight_lb` / `tubeweight_kg` | 54 | 0 |
| `projectileweight_lb` / `projectileweight_kg` | 52 | 0 |
| `weight_lb` / `weight_kg` | 33 | 0 |
| `length_inch` / `length_mtrs` | 33 | 0 |
| `weight_powder_lb` / `weight_powder_kg` | 87 | 0 — but see below |

*(Checked against the shipped `Config/weapons.txt` for this write-up.)*

So **the displayed range of a weapon is the range it shoots at.** The hazard is
created only by editing: change `range_yd` and you change what the AI thinks
the weapon is worth, what it costs, how far ships engage and what the UI
says — and **not** how far a brigade shoots. Change `range_m` and you change
only the shooting. A weapon mod that touches one and not the other produces
exactly the gap this file is about.

> `weight_powder_kg` is consistent too, but it is **misnamed**: its values are
> exactly 453.6 × the pound figure, so the field holds **grams**. Harmless, as
> [11](../code-findings/11-combat-resolution.md) §2 found both powder fields
> are barely read — but a modder editing "kg" will be out by a factor of 1000.

**Two places the interface goes out of its way to be straight with you.** The
first-battle morale cap is stored into the morale breakdown specifically so the
panel can explain it, and the army panel surfaces the political cap on morale
as *"Morale is capped by the fighting spirit"* — the game telling you,
accurately, that political support in a unit's home state limits how well it
can fight.

Sources: [11](../code-findings/11-combat-resolution.md) §2,
[13](../code-findings/13-morale-and-cohesion.md) §5.

---

## 5. For modders: fifteen label traps

None is player-facing, but each will bite a tool that reports game state.

**"Stance" is two different enumerations, numbered in opposite directions.**
Formation stance runs `none, screen, defend, attack, assault` (0–4) and is what
the UI shows; the AI's own `macroai` runs `assault, attack, defend, retreat`
(0–3). A single integer called "stance" means nothing until you know which one
it is — and reading the wrong one shows *assault* where the game means *screen*
([21](../code-findings/21-ai-battle.md) §4).

**"Cohesion" is inverted between the code and the config labels.** In code,
`cohesion = 1` is perfect order. The config labels use "cohesion" to mean
*disorder*, so a label reading "cohesion increase" describes a formation coming
apart ([16](../code-findings/16-enumerations.md)).

**"Cavalry orders" is the campaign order mode for every formation.** The field
`cavalryorders` holds the Patrol (1), Raid (2) and Guard (3) modes and is set
on armies and corps of any composition — raiding efficiency sums a formation's
infantry *and* cavalry brigades. The name is left over from an earlier design
([14](../code-findings/14-supply-and-logistics.md) §9).

**"Intervention level" is not a relations score and `GetInterventionProbability`
is not a probability.** The accumulators are per-side, per-power and floored at
zero; the function returns their difference divided by a per-chapter
requirement and clamped, and `relationsintervention[0] = 100` is a sentinel
meaning "impossible", not a threshold. Nothing rolls against the result
([17](../code-findings/17-politics-and-support.md) §4a–4b).

**`statesupportownstate` (0.65) is read two ways.** The Strategy panel's Loyal
States count compares it against a *share* of the two sides' support; the
recruiting-list filters and the campaign AI compare it against an *absolute*
support level. One number, two meanings, and a state can satisfy one and not
the other ([17](../code-findings/17-politics-and-support.md) §6).

**`withinturret` is a turret count that one consumer reads as a flag.** A
weapon system's third field is the number of turrets its guns occupy; the ship
panel reads it as `> 0` and the rate-of-fire bonus as `== 1`
([26](../code-findings/26-naval-combat.md) §4).

**A ship's `armor` is a rating, not a thickness.** Appendix E prints it as
inches, which happens to line up from 2 upward; 0.5 is *Timber*, 1 is *Cotton
Bales* and the Tinclad Gunboat's 1.5 is described as half an inch
([26](../code-findings/26-naval-combat.md) §2).

**The two replacement-speed prefs say "distance to home state" and measure
something else.** `refillspeeddistance0` and `refillspeeddistance2000` are
interpolated on the distance to the brigade's chosen *supply node*, not to its
recruiting state, and the variable is initialised to the maximum so a unit with
no node in range gets the worst rate
([14](../code-findings/14-supply-and-logistics.md) §3b).

**The winter reinforcement penalty's label exempts encamped units and the live
code does not.** The config reads *"Lower reinforcements multiplier during
winter months if unit is not encamped [<1]"*; the live path blends on snow and
precipitation and never looks at the camp. A second routine implements the
label's rule — at 1 % rather than 25 % — and has no callers anywhere (§3b).

**`additionalrecruitssubsidies` is a slew rate wearing a target's label.** The
config calls its 2 *"additional recruits (volunteers) available for full
recruitment subsidies (1 = no change)"*, but the code uses it only to pace how
fast a factor converges, and the target that factor converges to has no subsidy
term at all ([17](../code-findings/17-politics-and-support.md) §5c). A modder
raising it makes the ramp faster, not the army bigger.

**Two `GamePrefs` fields are parsed from two different config files with
incompatible units, and the last loader wins.** `battledaylengthinmins` is 120
in `battleprefs.txt` and **1.25** in `campaignprefs.txt`, both labelled *"Day
Length in Realtime Minutes (calculated out of a day of 6-18h)"*
([10](../code-findings/10-simulation-tick.md) §5). `basemoraleincrease` is
**0.3** in `unitprefs.txt` as *"Base rally value increase per hour"* and
**0.02** in `campaignprefs.txt` as *"Base rally value increase per day
campaign"*, and the campaign path multiplies by 365 to reconcile them
([13](../code-findings/13-morale-and-cohesion.md) §2). Each scene ends up with
the value meant for it, but a tool reporting "the day length" or "the rally
value" has to say which scene it means — and a modder editing one file changes
only one scene.

**`influenceonnationalmoraletowns` is charged to the loser, not the
occupier.** The label reads *"influence on national morale when enemy towns are
occupied (multiplier to town importance)"*, which describes the occupier's
gain; the code bills the former owner and credits only the *original* owner on
recapture, so there is no occupier term at all
([17](../code-findings/17-politics-and-support.md) §3a).

**`bonusvolunteersmodifier`'s label is written for a slider the game does not
have.** *"50 bonus and value of 0.5 here means 25 % more volunteers"* implies a
0–50 difficulty bonus; the shipped slider maximum is **0.5**, and the
expression divides by 100, so the effect is a hundredth of the labelled one —
in the dead code path of §3a besides
([17](../code-findings/17-politics-and-support.md) §3a).

**`datefrom` on a project is not an availability date.** Nothing in the
appointment or listing path reads it; its only consumer *grants* dated projects
free at the start of a new campaign. A project labelled "from 1 January 1864"
can be bought in March 1861
([22](../code-findings/22-policy-and-project-catalogue.md) §9.3).

**The policy-slot counter counts completed policies.** `maxpolicies` looks
like a research queue width and bounds the number of ladder levels a nation may
ever hold; a tool reporting "policies in research" must filter on
`researchprogress < 1f` itself
([22](../code-findings/22-policy-and-project-catalogue.md) §3.2).

---

## 6. Open questions

1. **Is the melee-strength divergence intended?** Not decidable from code. A
   designer could reasonably want the HUD number to answer "how good is this
   unit at melee" rather than "what will this particular fight do". The mounted
   multiplier appearing in the display *and* in the charge decision but not in
   the casualty formula is the detail that reads most like an oversight.
2. **Does the missing attrition penalty matter in play?** *(Untested.)* The
   code is unambiguous, but a formation reaching 40 % sick-plus-missing is
   already in trouble, so the practical size of the effect is unmeasured. A
   save at high attrition would settle it.
3. **How many other tooltips describe systems that are not there?** §2.1 was
   found by chance while reading perks. Nobody has swept the game's tooltip
   text against the code behind it, and the perk case suggests it would pay.
4. **What else does the strategy panel compute from its own widgets?** §1.4
   found one. The pattern — UI as the source of truth — may not be isolated.
