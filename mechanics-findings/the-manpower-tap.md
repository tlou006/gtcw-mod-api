# The manpower tap

Where your army's men come from, what they cost, and why the number the
recruiting list offers you shrinks for the rest of the war once you have spent
it.

Sources: [17](../code-findings/17-politics-and-support.md) for state support
and the recruiting formulas, [23](../code-findings/23-game-state.md) §5.2 for
the pools themselves, [14](../code-findings/14-supply-and-logistics.md) §3b for
replacements, [15](../code-findings/15-attrition-and-condition.md) for what
drains a unit once raised, [22](../code-findings/22-policy-and-project-catalogue.md)
§8 and §12.3 for the policies and projects that move the numbers.

---

## 1. The short version

- **Every state holds a separate recruiting pool for each side**, computed from
  that state's population and *that side's* support in it. Both sides have a
  support figure in every state at once; they are not two ends of one slider.
- **Below 30 % support a state gives you nothing** — no volunteers, no drafts.
- **Drafting is impossible until you pass a Draft Act.** Every state ships a
  mobilization rate of **zero**, and only the Draft Acts raise it.
- **You must hold a town in a state to draft there. You do not need one to
  recruit volunteers.**
- **The pool is a lifetime quota, not a rate.** It is a ceiling minus every man
  you have *ever* raised there, and your dead are never credited back.
- **Drafting burns the support it depends on**: about **625 men per point**.
  From 90 % support you can draft roughly **37,500 men** out of a state before
  drafting shuts itself off.
- **Replacements trickle in at about 26 men a week** for a full-size brigade in
  the best case, and **stop completely** if you leave its reinforcement
  priority on *low*.
- **Contracts expire, and the men go home.** Reenlistment runs from **10 % at
  zero support to 60 % at full support** — so a contract expiry in an
  indifferent state costs you most of the unit.

> The practical shape of the whole system: **volunteers are slow and free,
> drafts are fast and self-limiting, and everything is capped by one number per
> state that the war itself keeps pushing down.**

---

## 2. Two taps per state, and only one of them is free

### Volunteers

The ceiling for one state, for one side, is the state's population times a flat
**1.6 %**, times *your* support there, times the policy and project multipliers
— and then put through a power law that compresses large pools
([23](../code-findings/23-game-state.md) §5.2).

The policy multiplier is the one you control. It starts at **0.15** before the
war and each Militia Act adds to it permanently — for the Union
+0.08, +0.15, +0.12, +0.10 across the four Acts, for the Confederacy
+0.25, +0.20, +0.10 across three. A second multiplier *grows while the Act is
active*: +0.50 per year for the first Act and +0.15 per year for each later
one, starting from 1.0 ([17](../code-findings/17-politics-and-support.md) §5c).

So the volunteer pool is not a fixed national ceiling; it is a figure that
climbs as long as the Acts stay in force. With the growth multiplier held at
its starting value, the shipped numbers look like this:

| State (1860 pop, support) | Pre-war | After Militia Act I | …one year later | All Acts passed |
| --- | --- | --- | --- | --- |
| New York (3.23 M, 90 %) | 8,600 | 12,700 | 18,300 | 30,100 |
| Pennsylvania (2.42 M, 85 %) | 6,300 | 9,300 | 13,400 | 22,000 |
| Ohio (1.95 M, 82 %) | 5,000 | 7,400 | 10,700 | 17,600 |
| Virginia (1.22 M, 85 % CSA) | 3,400 | 5,000 | 7,200 | 11,900 |
| Georgia (1.06 M, 95 % CSA) | 3,300 | 4,900 | 7,000 | 11,600 |
| South Carolina (0.70 M, 100 % CSA) | 2,400 | 3,500 | 5,100 | 8,400 |
| Delaware (94 k, 75 %) | 300 | 440 | 640 | 1,050 |

Two things to read off it. **Support is a straight multiplier inside the power
law**, so halving support costs you about 46 % of the pool, not 50 %. And **the
power law multiplies small pools up rather than down** — the compression only
starts to bite above a raw figure of about 59,000 men, which no single state
reaches at shipped rates. The config calls it a *"reduction of volunteers pools
for higher recruiting numbers"*; for every state in the game it is an
*increase* ([23](../code-findings/23-game-state.md) §5.2).

Two projects add to the volunteer side: **Recruitment Offices** at +5 % per
level and **Farm Mechanization** at +10 % per level
([22](../code-findings/22-policy-and-project-catalogue.md) §12.3).

### The draft, which starts switched off

The draft ceiling is population × **4 %** × the state's *mobilization rate* ×
a growth multiplier. **Every one of the 53 shipped states has a mobilization
rate of zero for both sides**, and the only things that raise it are the two
Draft Acts (+0.5 and +0.3, applied to every state at once), Emancipation on the
Confederate side, and the Mexico policy for Texas alone
([17](../code-findings/17-politics-and-support.md) §5c).

That has a consequence worth stating plainly: **in a 1861 campaign you cannot
draft a single man until a Draft Act is researched**, whatever a state's
support or population. The Spring 1863 and Spring 1864 starts ship their Draft
Acts already researched, so they begin with a mobilization rate of 0.5 (and the
Confederacy in 1864 with 0.8); the Union never starts with its second Act, in
any scenario.

The draft's own growth multiplier accrues **+15 % of base per year per Act**
and is never reset, so a late-war draft ceiling is enormous. Four years with
both Acts active puts New York's draft pool over **200,000 men** — far beyond
anything the volunteer side reaches. The draft is not limited by its ceiling.
It is limited by what it does to support.

### Who may recruit where

Four separate gates, and they are not the same gate:

| Gate | Volunteers | Drafts |
| --- | --- | --- |
| Support at least 30 % | yes | yes |
| You own a town in the state | **no** | **yes** |
| The state is flagged recruitable | yes (list only) | yes (list only) |
| Before the war, only your own states | yes | n/a |

**You can raise volunteers in a state where you hold nothing**, provided
support is at or above 30 %; you cannot draft there. And the *recruitable*
flag is authored per state: seven states — California, Oregon, Nevada, Utah,
Dakota, Nebraska and the unorganised territory — are shipped unrecruitable and
**nothing in the game ever flips them on**, so they never contribute a man to
either side. Ten more (Arkansas, Indian Territory, Kansas, Kentucky, Missouri,
North Carolina, Tennessee, Virginia, West Virginia, New Mexico) are unlocked
**for both sides at once** by the Union's Militia Act I, which is also what
starts the war ([17](../code-findings/17-politics-and-support.md) §5c).

> The flag only filters the recruiting list and the AI's choice of state.
> Replacements for an existing unit draw on its home state's pool without
> consulting it ([14](../code-findings/14-supply-and-logistics.md) §3b).

---

## 3. The pool is a lifetime quota

The number the recruiting list shows you is not a rate. It is

> **ceiling now − every man you have ever raised in this state**

and the subtrahend only ever goes up ([23](../code-findings/23-game-state.md)
§5.2). Three consequences the interface never spells out:

- **Your dead are not credited back.** A brigade raised in Ohio and destroyed
  in Tennessee still counts against Ohio's quota forever. The pool recovers
  only because the *ceiling* grows — through population growth, through the
  Militia Acts' growing multiplier, and through support recovering.
- **Disbanding a unit refunds its survivors, not its losses.** Disband a
  2,000-man brigade that is down to 600 men and 600 go back into the pool.
- **Pardoning prisoners of war takes men out of the pool** rather than putting
  them back. The release path credits the *released* men against the pool of
  their own home states, scaled by the reenlistment ratio, and no unit gains
  any strength from it — so a prisoner exchange is a straight charge against
  future recruitment ([23](../code-findings/23-game-state.md) §5.2). **Untested
  in play**: the sign is unambiguous in the code, but whether the exchange UI
  presents this as a cost is not checked.

The same quota pays for **both** new units and replacements for existing ones
([14](../code-findings/14-supply-and-logistics.md) §3b), which is the single
most important thing to know about it. A corps bleeding 200 men a week in
replacements is consuming the same number that would otherwise raise a new
brigade, from the same states, and it is doing so automatically.

> **The men also cost workers and goods.** Every man raised permanently
> removes five workers from his home state's labour pool
> ([production-and-workforce](production-and-workforce.md)), and every man
> raised or replaced is paid for in uniforms, rifles, horses and gun pieces
> bought at local market prices ([the-war-economy](the-war-economy.md)). Those
> purchases enter the national demand figure for each of those goods, so a
> general mobilization bids up its own costs.

---

## 4. Contracts: the ninety-day men are in the code

Every unit carries an enlistment date and a contract length in months. When the
term runs out, the unit keeps only a fraction of its men — and that fraction is
read off **support in the unit's home state**
([17](../code-findings/17-politics-and-support.md) §5b):

| Support in the home state | Reenlisting | Leaving |
| --- | --- | --- |
| 0 % | 10 % | **90 %** |
| 30 % (the recruiting gate) | 25 % | 75 % |
| 50 % | 35 % | 65 % |
| 80 % | 50 % | 50 % |
| 100 % | 60 % | 40 % |

with a ±20 % random swing on the result. The men who leave are **gone from the
pool as well as from the unit** — the quota is not credited back — and the
unit's wounded pool is scaled down by the same fraction, so a contract expiry
quietly disposes of the men in hospital too. The unit then signs a fresh
contract at your *current* national contract length and starts again.

The campaign ships both sides on **12-month contracts**, and the Militia Acts
rewrite that figure:

| Act | Union contract | Confederate contract |
| --- | --- | --- |
| *(start)* | 12 months | 12 months |
| Militia Act I | **3 months** | 12 months |
| Militia Act II | 12 months | 24 months |
| Militia Act III | 24 months | 36 months |
| Militia Act IV | 36 months | — |

> **The Union's first Militia Act shortens its contracts to ninety days.** This
> is the historical ninety-day men, faithfully modelled and easy to walk into:
> every unit raised between Militia Act I and Militia Act II will put its
> reenlistment roll three months later, and in a state at 50 % support that
> roll costs you about two-thirds of the brigade.

The trade-off is explicit, and it runs the other way: **the volunteer pool's
growth multiplier is throttled by contract length** — full rate at 3 months,
×0.9 at 12, ×0.6 at 24, ×0.3 at 36. Short contracts grow your pool fastest and
churn your units hardest.

The Draft Acts touch contracts too, and **asymmetrically**. Each one resets
*every existing unit's* enlistment date to the day the Act passes — which for
both sides is a free restart of the current term. But only the Confederacy's
units have their term *lengthened*: Confederate Draft Act I raises every
contract to **36 months** and Draft Act II to **99 months**, which is the rest
of the war. The Union's equivalent values are shipped in the config and
**never read**, so a Union Draft Act buys a restart and nothing more
([17](../code-findings/17-politics-and-support.md) §5b).

> Both Draft Acts also cost **2 points of national morale** apiece, once.

---

## 5. Replacements arrive at twenty-six men a week

Replacements are automatic. Any unit below its establishment draws men from its
home state's pool continuously, with no order from you, and pays for their
equipment as it goes ([14](../code-findings/14-supply-and-logistics.md) §3b).
The base rate is **a full unit's worth of men in 1.5 years** at the home state,
stretching to **4.0 years** at the far end of the map — so for a 2,000-man
brigade:

| Situation | Men per week |
| --- | --- |
| At home, high priority, supplied, rested | **26** |
| At the far end of the map | 10 |
| Medium reinforcement priority | 13 |
| **Low reinforcement priority** | **0** |
| At half supply | 13 |
| At zero readiness | 6 |
| In deep snow or heavy rain | 6 |
| After one Draft Act | 38 |
| After two Draft Acts | 51 |
| **Sent home to reform** | **256** |

Five things follow.

**Low priority means none.** The three priority steps are 0, 0.5 and 1.0, so
the lowest setting does not slow replacements down — it switches them off
entirely. Any unit you have ever set to low priority and forgotten is receiving
nothing.

**Reforming is worth about seven weeks of ordinary replacement in one month.**
Sending a unit home to reform multiplies its intake by **ten** and treats it as
fully supplied regardless of its actual supply state, for one month. It comes
back at roughly **55 % of establishment** if it left empty, and it is off the
map for the whole month plus the travel at each end. For a brigade below about
half strength this is strictly better than standing it in the line and waiting.

**Everything else about the unit is a multiplier on the same rate.** Supply
state multiplies directly, so a half-starved brigade replenishes at half rate;
readiness scales the rate from ×1.0 down to **×0.25** at zero, which is the
quiet cost of running an army ragged; and winter weather scales it to **×0.25**
at full snow or full rain.

**The winter penalty ignores winter quarters.** The config label says the
penalty applies *"if unit is not encamped"*, and the live path never looks at
the camp — a brigade in winter quarters takes the same quartering of its
replacement rate as one in the open. There is a second, unused routine in the
assembly that *does* distinguish them, and it is harsher still (1 % of normal
outside winter quarters, 25 % inside); it has no callers
([14](../code-findings/14-supply-and-logistics.md) §3b). So the published rule
is implemented twice and neither implementation matches the label.

**Drafted replacements cost support; volunteer replacements do not.** Each unit
has a reinforcement *type* — volunteers, drafts, or both — and when the drafted
channel delivers men, the home state pays the drafting support penalty for them
exactly as if you had raised a new unit there. A unit set to "both" will draw
volunteers first and fall through to drafts only when the volunteer pool is
dry, which is the sane default.

> **Both pools are checked against the unit's *home* state, not where it is
> standing.** A Massachusetts brigade fighting in Georgia draws Massachusetts
> men and charges Massachusetts' support. The *speed* at which they arrive,
> though, is set by the distance to the supply node the brigade is drawing
> from, not by the distance home — the config label says "distance to home
> state" and the code measures something else
> ([14](../code-findings/14-supply-and-logistics.md) §3b). A brigade with no
> node in range is treated as being at maximum distance, and replenishes at
> about a third of the home rate.

---

## 6. What moves support

Support is tracked per state, per side, on a 0–100 scale, with a breakdown of
**42 numbered reasons** behind it. Twenty-three of those reasons are policies,
which is the real answer to what a policy *is* in this game: most policy
effects are support adjustments
([17](../code-findings/17-politics-and-support.md) §2,
[22](../code-findings/22-policy-and-project-catalogue.md) §8).

The shipped magnitudes, in the units a player can act on:

| What you did | Support in that state |
| --- | --- |
| Drafted 1,000 men | **−1.6 points** |
| 1,000 of your men killed, battle at home | **−4.0 points** |
| 1,000 of your men killed, battle on enemy soil | −6.0 points |
| 1,000 wounded treated for a year | +1.0 point |
| Raiding there for a year, at full efficiency | −2.5 points to you, +1.0 to your enemy |
| Every town in the state in enemy hands, for a year | **−100 points** |
| Appointing a political general to a command | +0.5 to +5.25 points, while he holds it |

and the two numbers that pull everything back toward the middle:

| Standing pull | Rate |
| --- | --- |
| Loyal states, toward national morale | 25 points a year, closing the gap |
| National morale, toward population-weighted average support | 50 points a year, closing the gap |

> **Occupation outruns recovery four to one.** A state with every city taken
> loses support at 100 points a year while national morale pulls it back at
> 25. Nothing else in the table is in that league — losing one state's towns
> costs more support than killing ten thousand of its sons.

### What this means in practice

**The draft eats its own base.** At 1.6 points per thousand men, a state at
90 % support can yield about **37,500 drafted men** before it crosses the 30 %
gate and stops yielding any at all. Printing works in the state soften this —
the penalty is divided by `1 + 0.2 × √(printing works)` — which is the one
building that directly buys draftable manpower.

**Casualties are charged to the home state, so where you raise a unit decides
where its losses hurt.** Spreading recruitment across many states spreads the
political bill; raising everything from your two best states concentrates it
exactly where your pools are largest.

**Defending a state is worth more support than any policy in the game.** The
occupation term is per-town and continuous: it does not wait for the state to
fall, it bills you for the fraction of its towns currently in enemy hands. A
raid that takes two towns in a ten-town state costs 20 points a year for as
long as they are held.

**Treating your wounded is the only support term that rewards attrition.** At
+1 point per thousand wounded per year it will not outrun the −4 per thousand
dead, but hospitals and camps also decide how many of the wounded become dead
in the first place ([what-kills-your-men](what-kills-your-men.md)).

**And the loop runs both ways.** High support raises national morale, which
raises support in every loyal state, which raises volunteers. The same loop
runs in reverse after a lost campaign, which is why morale collapses in this
game look like an accelerating slide rather than a step down
([17](../code-findings/17-politics-and-support.md) §3).

### What support does *not* do

**It never changes a state's loyalty.** State allegiance is decided once, by a
bare majority of support, on the single day the Union's Militia Act I starts
the war — and after that nothing in the game revisits it. A state can sit at
zero support with every city occupied and stay loyal
([17](../code-findings/17-politics-and-support.md) §5a). Only three scripted
objectives (Kentucky, Missouri, West Virginia) and a handful of pre-war
policies ever move a state again.

**And it does not slow your marching.** The movement-speed penalty for crossing
a hostile state exists in the code and is shipped with a value that makes it
exactly nothing, so the *"Low state support (−x %)"* message can never appear
([17](../code-findings/17-politics-and-support.md) §2). It is a one-number fix
for a modder and the cheapest restorable mechanic found so far.

---

## 7. What support buys besides men

Manpower is the headline, but support is wired into five other systems, and
three of them are combat-facing:

| Where it lands | What it does |
| --- | --- |
| Battle morale | Sets the **ceiling** on a unit's morale, which no commander, drill or terrain lifts ([why-units-break](why-units-break.md)) |
| Desertion | Multiplies desertions, from ×3.0 at full support to **×5.0 at zero** ([what-kills-your-men](what-kills-your-men.md)) |
| Battle refusal | Below 40 % morale a unit will not accept a field battle at all, and support is what caps that morale |
| Readiness | Marching across a state consumes readiness on a curve from −8.5 to **−25 per year** on *that state's* support ([supply-in-practice](supply-in-practice.md)) |
| Policy research | The national average multiplies research speed **linearly** ([22](../code-findings/22-policy-and-project-catalogue.md) §3.1) |
| Reenlistment | §4 above |

> **So a politically neglected army is slower to march, quicker to desert,
> morale-capped in battle, harder to reinforce and cheaper to break.** That is
> five penalties from one number, and the number is the one the player has the
> most direct control over through policy.

One trap worth knowing: the national average that drives policy research is
**population-weighted across towns you own**, not averaged over your states.
Capturing enemy towns therefore drags your own average *down*, because your
support in enemy states is low — so a run of conquests slows your policy
research ([17](../code-findings/17-politics-and-support.md) §2).

---

## 8. What the panels get wrong

Five readouts in this area do not mean what they appear to
([ui-vs-simulation](ui-vs-simulation.md) has the full list):

- **The "what is hurting our support" list is permanently empty.** Every
  support *loss* writes a NaN into the tracker the list filters on, and the
  guard meant to catch it can never fire, so the negative breakdown returns an
  empty string in every game. The positive list works. This is the hook
  library's best first target: a one-method fix with no balance effect.
- **A "loyal state" on the Strategy panel is a *share*, not a level.** It
  counts states where your support is more than 65 % of the two sides' support
  *combined* — so a state at 20/5 counts as loyal and one at 70/40 counts for
  neither side. The same config number is read as an absolute support level by
  the recruiting list's own/enemy/neutral filters, so a state can be loyal on
  one panel and neutral on the other, from one figure.

- **"Men fielded" in the recruiting list is your own men only**, counted from
  that state, not the state's contribution to the war. It also walks the entire
  unit tree once per state to produce the column
  ([23](../code-findings/23-game-state.md) §5.3).
- **"Estimated reenlistment ratio" on the top panel is the last expiring
  unit's roll**, smoothed. The field it reads is assigned rather than
  accumulated, so whichever unit most recently reached the end of its contract
  sets the national figure — and the "men whose contracts expire soon"
  forecast is built on that same number
  ([17](../code-findings/17-politics-and-support.md) §5b).
- **A state you once fully occupied keeps its occupation bonuses forever.** The
  full-occupation flag is never cleared, so the two occupation projects keep
  paying out and the enemy-soil penalties keep *not* applying in a state you
  have since lost entirely ([23](../code-findings/23-game-state.md) §5.4).

And one knob that promises something it does not deliver: the config's
*"additional recruits (volunteers) available for full recruitment subsidies"*
is shipped at **2**, implying full funding doubles your volunteers. The live
code uses that 2 as the *speed* at which the project effect ramps in, and the
target it ramps toward contains no subsidy term at all — only Recruitment
Offices' +5 % per level. **The military subsidy buys projects, not men**
([17](../code-findings/17-politics-and-support.md) §5c).

---

## 9. What to do differently

1. **Pass Militia Acts early and keep an eye on the contract clock.** The Acts
   are the single largest multiplier on your volunteer pool — 0.15 to 0.60 over
   four of them for the Union — and the pool keeps growing while they are in
   force. But the Union's first Act cuts contracts to ninety days, so either
   push on to Act II quickly or expect to re-raise your first army.
2. **Research a Draft Act before you need one.** With no Act, your draft pool
   is zero in every state, and the Act's effect on existing contracts (a free
   restart for both sides, a 36-month extension for the Confederacy) is applied
   once, at the moment it passes.
3. **Spend the draft on the states that can afford it.** 625 drafted men cost a
   point of support wherever you take them, so take them from your highest-
   support, highest-population states and leave marginal states to volunteers,
   which cost no support at all.
4. **Audit every unit's reinforcement priority.** Low priority is not slow, it
   is off. A corps with a brigade on low priority will never fill that brigade.
5. **Send gutted brigades home rather than feeding them in the line.** A month
   reforming is worth about seven weeks of ordinary replacement and ignores
   supply state while it runs.

6. **Keep brigades supplied and rested if you want them refilled.** Supply
   state multiplies the replacement rate directly and readiness scales it to a
   quarter at the bottom, so the units that most need men get them slowest.
   Winter quarters will not help — the winter penalty applies regardless.
7. **Treat defence of your own towns as a manpower policy.** The occupation
   term is worth more support than anything in the policy tree, and support is
   what your recruiting, your morale ceiling and your desertion rate are all
   priced from.
8. **Build printing works in the states you intend to draft**, and appoint your
   political generals to commands in states whose support you care about: both
   are direct, if modest, purchases of support where it is spent.

---

## 10. Still open

- **The recruiting panel's own arithmetic** has not been read against these
  formulas, so whether the figure it offers is the pool, the pool clamped at
  the maximum unit size, or something else is unverified. **Untested**: the
  test is raising one brigade and watching the listed figure move.
- **Whether a drafted replacement's support penalty is visible at all** in the
  state support breakdown given the NaN defect in §8 — the positive list works
  and drafting is negative, so probably not.
- **How reinforcement priority interacts across echelons.** Setting it on a
  corps writes it down to every attached unit, but what happens to a brigade
  transferred in afterwards is not traced.
- **The campaign map's distance scale.** The replacement-rate distance figures
  here are relative for the same reason the supply ones are
  ([supply-in-practice](supply-in-practice.md)): 2,000 map units is not yet
  pinned to miles.
- **The Confederate manpower ceiling as a whole.** The shipped 1860 censuses
  are in the data and the formulas are now known, so a full both-sides
  mobilization curve is computable — the arithmetic has not been done.
