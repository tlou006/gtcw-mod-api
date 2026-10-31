# How you lose

There is **one** loss condition in Grand Tactician, and it is not an army, a
capital or a date. It is a number: **national morale below 25 %.** When your
side crosses it, the campaign ends on the next ten-minute simulation pass — in
a Confederate victory if you are the Union, and the reverse if you are not.

Everything else in this file is about what moves that number, which of the
things you worry about move it a lot (very little moves it a lot), and the one
calendar date that can kill the Union outright.

Sources: [17-politics-and-support](../code-findings/17-politics-and-support.md)
§3 and §3a; [24-campaign-objectives](../code-findings/24-campaign-objectives.md)
§2–§3; and the chapter machine in
[22-policy-and-project-catalogue](../code-findings/22-policy-and-project-catalogue.md)
§4.

---

## 1. The short version

- **The floor is 25 %, for both sides, in every chapter.** Campaign objectives
  0 and 1 — "Break Confederate Morale" and "Break Union Morale" — are live from
  the first day to the last and test nothing but the other side's national
  morale ([24](../code-findings/24-campaign-objectives.md) §2.1).
- **There is no time limit.** The chapter machine has three transitions and no
  fourth chapter, and nothing anywhere ends a campaign on a calendar date
  ([22](../code-findings/22-policy-and-project-catalogue.md) §4). A war that
  nobody is losing runs indefinitely.
- **Battles are a rounding error.** A lost battle with ten thousand casualties
  costs **1.3 points**. You would have to lose sixty of them to die of it.
- **Towns and politics are the war.** State support drags national morale
  toward itself at **0.5 per annum**, which over four years is the whole
  distance; losing towns charges morale directly *and* bleeds the state's
  support at **−1.00 per annum**, which then drags morale down a second time.
- **For the Union there is a hard cliff on 9 November 1864.** National morale
  must be at **50 %** or better that day. Below it, two objectives each charge
  −20 points in the same pass, which puts you under the 25 % floor
  immediately — Lincoln's defeat is not a setback to recover from, it is the
  end of the campaign ([24](../code-findings/24-campaign-objectives.md) §3.2).
- **Nothing warns you.** There is no alert, no threshold colour and no
  newspaper language that changes as you approach the floor (§11).

---

## 2. Where you start, and how far the floor is

National morale is authored per side in each campaign start's state file, and
the later the start, the less slack both sides have
([17](../code-findings/17-politics-and-support.md) §3a):

| Start | Opening date | Union | Confederacy | Union points above the floor |
| --- | --- | --- | --- | --- |
| Spring 1861 | 23 February 1861 | **100** | **100** | 75 |
| Summer 1861 | 8 July 1861 | 95 | 95 | 70 |
| Spring 1862 | 21 March 1862 | 90 | 90 | 65 |
| Spring 1863 | 10 May 1863 | **82** | **85** | 57 |
| Spring 1864 | 1 May 1864 | 75 | 75 | 50 |
| Tutorial | 8 July 1861 | 95 | 95 | 70 |

Spring 1863 is the only start that is not symmetric: the Union begins three
points down.

Two things fall out of the table.

**A 1861 Union starts pinned at the ceiling, which means it cannot bank
anything.** National morale is clamped at 100 %, and the accounting is
clamp-first: a positive adjustment applied at the ceiling records *zero*
([17](../code-findings/17-politics-and-support.md) §3). For the first months of
a Spring 1861 campaign every favourable term in the game — high state support,
rising private wealth, captured forts — is simply thrown away. The number can
only fall, and the first battle you lose is the first thing that moves it.

**Spring 1864 is a much harder scenario than it looks**, and not because of the
armies. The Union starts at 75 and must be at **50 or better on 9 November
1864**, six months later. Twenty-five points is one capital, or five
mid-importance cities, or a state seceding plus a bad autumn of support.

> **Untested:** the opening values are read from the shipped `nations.dat` for
> each start folder, so they are what the scenario authors set. Whether the
> campaign-setup screen's difficulty slider then modifies them is a separate
> question with a surprising answer — see §10.

---

## 3. The whole ledger, in points

Every writer of national morale, with the shipped magnitudes converted to
points out of 100. Thirty-five breakdown lines exist; these are all of the ones
with a mechanism behind them
([17](../code-findings/17-politics-and-support.md) §3, §3a;
[24](../code-findings/24-campaign-objectives.md) §6.1).

| Mover | Points | When |
| --- | --- | --- |
| **State support convergence** | **±50 per annum × the gap** | continuously, every 12 game hours |
| **Private wealth growth** | **≈ ±0.4 per point of monthly growth** | continuously, resets monthly |
| Losing your capital | **−20** | once |
| Lincoln loses the 1864 election | **−40** (−20 charged twice) | once, 9 Nov 1864 |
| Bleed them out — 50,000 Union casualties | −10 | once, chapter 3 |
| Capturing 10 forts (Union) / 3 cities (CSA) | +5 | once |
| Cut the Confederacy in half | +5 | once |
| Five consecutive land victories | +5 | once |
| A state secedes to the enemy | −2.5 (W. Virginia) / −5 (Kentucky, Missouri) | once |
| Confederacy wins the first major battle | −5 Union | once |
| Union wins the first major battle | +2.5 Union | once |
| Invading the North (CSA holds Union soil) | −2.5 Union | once |
| Five consecutive naval victories | +2.5 | once |
| Beating a European intervention | +2.5 | once |
| **A town changes hands** | **−1.25 typical, −12.5 for the largest city** | per town |
| **A battle** | **−0.2 to −3.1** | per battle, **both sides** |
| A naval defeat | −0.035 per 100 firepower destroyed | per battle, loser only |
| Militia Act I not yet passed | −1.0 per **day** | daily, pre-war only |
| Enemy on Confederate soil | −0.5 per **week** Union | weekly, chapter 1 |
| 1862 midterms failed | −1 per **week** (−52 per annum), **for about a day** | ≈ −0.15 total |
| Policies | −5 to +10 each | on completion |

Read that table once and the shape of the game changes. **The two continuous
terms at the top dominate everything below them**, the scripted one-shots in
the middle are each worth more than a month of fighting, and the two things a
player spends the whole campaign doing — fighting battles and taking towns —
are at the bottom.

The policy row hides one asymmetry worth knowing: **the Union's Abolition act
gives the Confederacy +10 points of national morale and the Union only +5.**
USCT (+2 CSA) and Emancipation (+5 CSA) work the same way and are labelled as
such in config, so the direction is intended — the magnitude on Abolition is
the one that looks like a slip. "Industrialization (Policy)" has a breakdown
line, a config pref shipped at zero and no writer at all
([22](../code-findings/22-policy-and-project-catalogue.md) §8.1).

---

## 4. Battles barely matter

A battle's national-morale charge is computed from **absolute casualties
raised to the power 0.3** ([17](../code-findings/17-politics-and-support.md)
§3a). That exponent is the whole story: it flattens blood almost completely.

| Your casualties | Fought to a draw | Retreated | Lost the field | Surrendered |
| --- | --- | --- | --- | --- |
| 250 | −0.21 | −0.31 | −0.42 | −0.63 |
| 500 | −0.26 | −0.39 | −0.52 | −0.77 |
| 1,000 | −0.32 | −0.48 | −0.64 | −0.95 |
| 2,000 | −0.39 | −0.59 | −0.78 | −1.17 |
| 5,000 | −0.51 | −0.77 | −1.03 | −1.54 |
| 10,000 | −0.63 | −0.95 | −1.27 | −1.90 |
| 20,000 | −0.78 | −1.17 | −1.56 | −2.34 |
| 50,000 | −1.03 | −1.54 | −2.05 | −3.08 |

Five things to take from that table.

**Two hundred times the casualties for five times the morale.** Antietam and a
cavalry skirmish are within a factor of three of each other. Nothing about the
charge is relative to army size, either — the function computes the starting
strength and then uses it only in a debug line.

**Both sides pay, every time.** The charge is each side's own casualties, and
the winner is charged too. A victory in which you lose 5,000 men costs you half
a point; there is **no national-morale reward for winning a battle** anywhere
in the game except the scripted objectives (first major victory, five in a row)
in §3.

**The multipliers are where the difference is.** Losing the field doubles the
charge, a voluntary withdrawal multiplies it by 1.5, and **surrendering an army
triples it**. So the decision that matters for morale is not whether you fight
but how you leave: withdrawing from a lost position costs a quarter less than
being driven off it, and three times less than capitulating.

**A Lieutenant General commanding in person makes defeats worse.** If you have
passed the Lieutenant General act and your commander-in-chief is the officer
commanding the side that loses, the charge is multiplied by **1.2**. It is the
only commander term in the national-morale path, and it only ever hurts.

**Auto-resolve and fighting it out are not priced the same.** The battle scene
and the auto-calculator each have their own copy of the formula, and the
auto-calculator's is the older, shorter one: it has **no surrender multiplier,
no Lieutenant General term and no AI difficulty discount**
([17](../code-findings/17-politics-and-support.md) §3a). Auto-resolving a
battle you would have surrendered therefore costs a third of what fighting it
would, and auto-resolving against the AI strips the AI of its morale-loss
handicap (§10).

Naval battles are charged differently again: **only the loser pays**, at
`−0.035 points per 100 points of firepower destroyed`, and a raid against a
blockade produces **no national-morale effect at all** — consistent with
[26](../code-findings/26-naval-combat.md) §7.1 treating that engagement as a
skirmish rather than a fleet action.

> **What to do with this:** stop managing your national morale through battle
> outcomes. Sixty lost battles at ten thousand casualties each would take a
> 100 % Union to the floor; the war will be decided by towns and support long
> before. The one battle decision that is worth taking for morale reasons is to
> **withdraw rather than be broken, and never let an army capitulate.**

---

## 5. Towns, and the double charge

A town changing hands charges the side that lost it
`−0.125 × importance / 10` points, where importance is the town's share of the
richest town in the game, on a 0–10 scale
([17](../code-findings/17-politics-and-support.md) §3a).

| Town | Importance | Morale charged |
| --- | --- | --- |
| The largest city in the game | 10 | **−12.5** |
| A major city, a third of the largest | 4 | −5.0 |
| A substantial town, a tenth | 1 | −1.25 |
| Anything smaller | 1 (the scale rounds up) | −1.25 |

Because importance is `ceil(share × 10)`, **almost every town in the game is a
1** and costs its owner a flat 1.25 points. Only a handful of cities reach 4 or
more, and only one is ever a 10.

Four properties of that rule are worth knowing before you plan a campaign
around it.

**Taking an enemy town gives you nothing.** The charge is levied on the former
owner only. The attacker's morale is untouched — conquest pays through
objectives, the economy and the enemy's losses, never directly.

**Recapture refunds in full.** If a town returns to the side that held it at
the campaign's start, that side is credited the same figure back. So raiding
back and forth over the same town is morale-neutral over a round trip, for both
of you.

**The reference point is the richest town's all-time peak.** The denominator is
a high-water mark that is never reset downward, so as the war impoverishes the
country, every town's importance ratchets *down*. **Late-war town captures cost
less morale than early-war ones** for no reason a player can see.

**The real cost is the second charge, in the state.** A town in enemy hands
also bleeds its state's support at **−1.00 per annum × the share of the state's
towns occupied** — the single heaviest support term in the game
([17](../code-findings/17-politics-and-support.md) §3). A state with all its
towns taken loses **100 points of support a year**, i.e. it is at zero inside
four months, against a recovery pull of only +0.25 per annum. And a state at
zero support feeds §6's convergence, which drags your national morale down
again.

So a town is worth roughly **1.25 points at once, and a slow bleed in its state
for as long as it is held.** The occupation is the expensive half.

> One niche exception: the whole national-morale block is gated on the player
> being one of the two parties to the transfer. A town passing between two
> alliances that are both not you — a European intervention force taking a
> Confederate port, say — **moves no national morale at all**
> ([17](../code-findings/17-politics-and-support.md) §3a). **Untested:** needs
> an intervention to reach the map, which the shipped probabilities make rare.

---

## 6. The two taps that actually fill the tank

Two terms run continuously, every twelve game hours, for the Union and the
Confederacy only ([17](../code-findings/17-politics-and-support.md) §3).

### 6.1 State support, at 0.5 per annum

```
national morale moves toward average state support
at half the remaining gap per year
```

That is an exponential approach with a two-year time constant:

| Time | Share of the gap closed |
| --- | --- |
| 3 months | 12 % |
| 1 year | 39 % |
| 2 years | 63 % |
| 4 years | 86 % |

**Over a full four-year campaign, national morale ends up wherever your average
state support is.** Everything else in §3 is a perturbation on that line.

Two consequences that decide campaigns:

**If your average support stays above 25 %, the convergence alone can never
kill you.** It asymptotes to your support, not past it. Conversely, if average
support falls *below* 25 %, you will cross the floor eventually no matter how
well the armies do — the clock simply runs.

**Support is also your repair rate.** At 70 % support and 50 % morale the pull
is **+10 points a year**, about 0.2 a week — which heals one lost ten-thousand
casualty battle every six or seven weeks. At 40 % support and 50 % morale it is
**−5 points a year** and the same battles never heal at all.

But the number it converges on is not the one the map suggests. *National
Support* is **population-weighted and keyed on who owns each town**, not on
which side a state is loyal to
([17](../code-findings/17-politics-and-support.md) §2):

- a single large city outweighs several quiet states;
- **every enemy town you capture joins your own average at your support in
  that enemy state**, which is low — so conquest *lowers* your national
  support, and with it your national morale and your policy research speed;
- an occupied town of your own leaves your average entirely, so losing a
  low-support town can nudge the figure *up*.

> **This is the least intuitive mechanic in the political model.** A deep
> successful invasion drags your own political position down through a term
> that nothing in the interface attributes to conquest. It is indistinguishable
> from a loyalty-versus-ownership mix-up in the code, and it is still open as a
> question ([17](../code-findings/17-politics-and-support.md) §9).

### 6.2 Private wealth, at 10.0 per annum — on the *change*

The second tap reads the **month-over-month change** in average private income,
not its level, at the highest coefficient in the whole political model.

A nation whose average private income climbs by one point within a month earns
roughly **+0.4 points of national morale** that month, and loses the same if it
falls. The comparison is re-snapshotted on the first of each month, so
**sustained growth earns every month** while a permanently rich but flat
economy earns nothing at all. The term is suppressed in a campaign's first
month.

That makes the economy a continuous faucet or drain on the loss condition, and
it is the reason a blockade or a bad harvest reaches your national morale
without any battle being fought.

### 6.3 And it runs back the other way

National morale also pulls **every loyal state's support toward itself**, at
**0.25 per annum** — half the rate of the upward pull. So the two variables are
a feedback loop, not a one-way penalty:

```
support ──(0.5/yr)──► national morale ──(0.25/yr)──► support
```

**A collapse accelerates.** Falling morale drags support down, lower support
drags morale down faster, and lower support simultaneously shrinks your
recruiting pools, lowers your units' morale ceiling and multiplies desertion
([17](../code-findings/17-politics-and-support.md) §2). A recovery compounds
the same way, which is why a single good policy or a recaptured capital can
turn a campaign that looked lost.

---

## 7. The 1864 election is a cliff, not an event

Two campaign objectives key off **9 November 1864**: "Maintain Morale Above 50"
for the Union and "Union Morale below 50" for the Confederacy. Both tooltips
promise *"Union national morale −20"*
([24](../code-findings/24-campaign-objectives.md) §3.2).

What actually happens if Union national morale is below 50 % on that date:

1. Both objectives are live in the same chapter, both are in their unmet
   branch, and the sweep walks them in id order **in a single loop**. Objective
   26 charges −20, then objective 27 charges another −20. **The real penalty is
   −40 points.**
2. The charge only fires at all when morale is already under 50, so after it
   the Union is under 10 — well below the 25 % floor.
3. Objective 1, "Break Union Morale", is evaluated in the same sweep, which
   runs every **ten game minutes**. The campaign ends in a Confederate victory
   on the next pass.

So there is no four-month countdown to inauguration day, and no −20 setback to
play out of. **9 November 1864 is a pass/fail test at 50 points, and failing it
ends the campaign that afternoon.** The objective's own tooltip says nothing
about this.

The flip side is that **holding the line that day removes the cliff
permanently**. Objective 26 completing and 27 not completing is exactly the
condition that advances the campaign to chapter 3, which deactivates both
election objectives before either can charge anything. Chapter 3's own hazard
is a different objective — "Bleed them out", which charges the Union **−10**
once the Confederacy has inflicted 50,000 casualties.

| Union morale on 9 Nov 1864 | Outcome |
| --- | --- |
| ≥ 50 % | chapter 3 begins; both election objectives switch off; no charge |
| < 50 % | −40 points, floor crossed, campaign lost within ten game minutes |

> **For a Union player this is the single most important date in the game**, and
> the one planning constraint that outranks any operational decision: be at 50
> or better in early November 1864. For a Confederate player it is the whole
> strategy — you do not need to take Washington, you need Union morale under 50
> on one specific day.
>
> **For a modder** the two prefs are `lincolninfluenceunionmorale` and
> `demoralizevotersunionmoraleinfluence`, both −0.20. Zeroing *either* restores
> the −20 the tooltips promise and turns an instant loss back into the setback
> it was evidently designed to be.

---

## 8. The 1862 midterms are worth about a sixth of a point

The other election, objective 9 "Maintain Support Above 60", tests the Union's
average state support against 60 % on **4 November 1862** and charges −0.5
points per annum for as long as it is unmet
([24](../code-findings/24-campaign-objectives.md) §3.1).

Its chapter list says chapter 1, and the chapter machine advances to chapter 2
the moment the date passes 5 November 1862. So the penalty runs for **two
days** and totals about **−0.003 points** — three thousandths of one point.

The config label describes an open-ended weekly rate and the objective text
implies a political consequence. **As shipped, losing the 1862 midterms has no
mechanical effect whatsoever.** Its Confederate mirror has no effect term at
all.

Do not plan around it. If you are a Union player weighing a policy that costs
support against one that costs money in the autumn of 1862, the midterms are
not a reason to prefer either.

---

## 9. What low morale costs you before it kills you

National morale is not only the loss condition — five other systems read it, so
a sagging number is already hurting you well above 25 %.

### 9.1 Routed units surrender instead of rallying

In battle, a unit that routs evaluates surrender when **national morale ≤ 40 %
and the army's average morale ≤ 50 %**. The probability is
`0.01 / (national morale × army morale)^1.5`
([17](../code-findings/17-politics-and-support.md) §3a):

| National morale | Army at 50 % | at 40 % | at 30 % | at 20 % |
| --- | --- | --- | --- | --- |
| 40 % | 11 % | 16 % | 24 % | 44 % |
| 35 % | 14 % | 19 % | 29 % | 54 % |
| 30 % | 17 % | 24 % | 37 % | 68 % |
| 26 % | 21 % | 30 % | 46 % | **84 %** |
| 20 % | 32 % | 44 % | 68 % | **100 %** |

The roll is per rout, so a brigade that breaks twice is tested twice, and any
combination whose product falls below **0.046** surrenders with certainty.

> **This is the mechanic that makes a late collapse feel like a collapse.**
> Above 40 % national morale a rout is a rout and you rally it. Below 40 % your
> broken units start disappearing into prison camps, which costs you the men
> as well as the field — and a surrendered army triples the battle's own morale
> charge (§4), which lowers national morale, which raises the next
> probability.

### 9.2 Disintegrating formations desert harder

When a campaign formation disintegrates, every brigade still attached to it
loses a random share of its men, interpolated on national morale
([17](../code-findings/17-politics-and-support.md) §3a):

| National morale | Share of each attached brigade lost |
| --- | --- |
| 100 % | 30–50 % |
| 50 % | 40–65 % |
| 0 % | 50–80 % |

So the same accident costs about a quarter more men at half morale than at
full.

### 9.3 Your states' support is dragged down with it

§6.3's downward coupling at 0.25 per annum applies to **every state loyal to
you**. Low national morale is therefore a general tax on recruiting pools,
reenlistment rates, unit morale ceilings and desertion across the whole
country, not just a progress bar
([17](../code-findings/17-politics-and-support.md) §2;
[the manpower tap](the-manpower-tap.md) §6).

### 9.4 The AI attacks you more

The campaign AI divides its estimate of the force it needs by
`own national morale ÷ your national morale`
([20](../code-findings/20-ai-campaign.md) §4.2). When your morale falls, the
ratio rises, the AI perceives less threat, and it launches offensives with
smaller forces more readily. **Your political collapse is an input to the
enemy's operational aggression.**

A second, odder consequence: the AI will only recruit in a state whose support
is at least as high as *its own national morale*. A demoralised AI therefore
becomes *less* picky about where it raises men, and a confident one refuses its
own lukewarm states.

### 9.5 Military experience is paid in morale

Both sides' military experience from a battle is proportional to the total
national morale the battle destroyed — ×1.1 for the winner and ×0.7 for the
loser ([17](../code-findings/17-politics-and-support.md) §3a). Bloody battles
are how an army learns, and the tuition is charged to the loss condition.

---

## 10. The AI's thumb on the scale

The campaign difficulty slider's "bonus" is worth 0 to 0.5 and feeds a dozen
systems ([20](../code-findings/20-ai-campaign.md) §9). Two of them land on the
loss condition, and they behave very differently from each other.

**The AI's battle morale losses are discounted, by up to half.** In a battle
you fight, the enemy's national-morale charge is multiplied by
`1 − bonus`, floored at 0.1 — so at the hardest setting **the AI loses half the
national morale you do for the same defeat**
([17](../code-findings/17-politics-and-support.md) §3a). Since battles are
small change either way (§4) this matters less than it sounds, with one
exception: **the auto-calculator does not apply the discount at all.** If you
want the AI to feel a defeat at full price, resolve the battle automatically.

**The flat morale bonus the slider promises is dead in campaign play.** The
difficulty bonus is applied as a one-shot straight to the AI alliance's
national morale — up to **+50 points**, written directly to the field with no
clamp — but it is applied inside the *battle scene's* data import, and in a
campaign battle the very next statement reloads all alliance data from the
save, overwriting it ([17](../code-findings/17-politics-and-support.md) §3a).
It then re-fires and is re-discarded at every subsequent battle.

That also corrects this project's earlier reading in
[20](../code-findings/20-ai-campaign.md) §9.1, which described the one-shot as
applying at scenario load.

> **So the difficulty slider does not give the AI a morale head start**, which
> is just as well: +50 points on a 0–100 scale would pin a side at the ceiling
> for the entire war and make it unlosable. The slider's live effects on the
> loss condition are the battle discount above, the state-support loss
> reduction and the economic handicaps.

Two smaller findings in the same code worth a modder's attention. The bonus is
written to **the alliance whose id equals the AI's seat number in the battle**,
not to the AI's alliance — and the seat order is "whoever called the battle
first", so in a battle the AI initiated the write lands on the player. And the
companion volunteers bonus reads `bonus / 100`, which the config label was
plainly written for a 0–50 slider: at the shipped maximum of 0.5 it is worth
**0.5 % more volunteers instead of the labelled 50 %** — and is discarded
alongside the morale bonus, because `volunteersadj` travels in the same
alliance record.

---

## 11. What the panels tell you, and what they leave out

**The breakdown's total is a net change, and says so.** The Strategy panel's
first line is labelled *"Total cumulative"* and prints
`nationalmoraleinfluence[0]`, which is the sum of every adjustment ever
applied — not your national morale. A side that started at 100 and sits at 68
shows a total of −32. The label is honest; it is simply easy to misread as the
value.

**Each line also shows a trailing figure in brackets**, which is that
reason's cumulative figure minus its running average, i.e. roughly "how much of
this has arrived recently". It is not a rate and not a forecast.

**The war-morale bar is measured from the floor, not from zero.** It plots the
Confederacy's distance above 25 % as a share of both sides' distances above
25 % — so at 75/75 it reads centre, and at Union 50 / Confederacy 75 it reads
two thirds Confederate. **The bar moves roughly twice as fast as the raw
percentages**, which makes it a better early-warning instrument than the
numbers themselves. It is the one readout in this subsystem that is more
informative than it looks.

**Four unrelated mechanics share one breakdown line.** *"Battles
(Objectives)"* collects the Confederate invasion of the North, five
consecutive land victories, five consecutive naval victories and beating a
European intervention ([24](../code-findings/24-campaign-objectives.md) §6.1),
so the line cannot be read back to a cause. *"Elections (Objectives)"* likewise
covers both the inert 1862 rate and the 1864 double charge.

**The newspaper's verdict on morale never changes.** Its three tiers trigger
below −100 and −200 points of battle effect, against a real maximum of about
−3 — so every battle report reaches the same, most favourable conclusion. See
[what the game shows you](ui-vs-simulation.md) §2.2.

**Nothing warns you.** There is no alert, no colour change and no message tied
to approaching 25 %. The breakdown panel is the only instrument, and reading it
requires knowing that the convergence terms in §6 are the ones that matter —
which the panel does nothing to signal. The two most consequential lines in it
are the last two, *"State Support"* and *"Private Wealth"*.

---

## 12. Nine things to do differently

1. **Treat average state support as your real health bar.** National morale
   converges on it over two years, and if support stays above 25 % the
   convergence alone can never lose you the campaign. Everything else is
   perturbation.
2. **Union: be at 50 national morale in early November 1864.** Plan the whole
   1863–64 period around that one reading. There is no recovering from missing
   it, and once you clear it the cliff is gone for good.
3. **Confederacy: your war aim is one number on one day.** You do not need
   Washington or a European ally — you need Union morale under 50 % on
   9 November 1864, which means attacking Union *support* and *private wealth*
   rather than Union armies.
4. **Defend towns, and recapture promptly.** The 1.25-point charge is small;
   the occupation's −1.00-per-annum drain on the state's support is not. A town
   retaken is refunded in full on the morale side, and stops the support bleed.
5. **Do not plan deep conquests for their political effect.** Captured enemy
   towns join your own national support at your low support in that state, so a
   successful invasion drags your morale and your policy research down (§6.1).
   Conquer for the economy, the objectives and the enemy's support, not for
   yours.
6. **Never let an army capitulate, and withdraw before you are broken.** The
   battle charge triples on surrender and doubles on being driven off, against
   ×1.5 for a voluntary withdrawal — and below 40 % national morale your routed
   units start surrendering on their own (§9.1).
7. **Watch the economy as a morale instrument.** The private-wealth term reads
   month-over-month *growth* at the highest coefficient in the model, so a
   stalled economy stops paying even while it is still rich, and a recovering
   one pays every month.
8. **Ignore the 1862 midterms.** About a sixth of a point.
9. **Auto-resolve when you want the AI to feel a defeat.** The
   auto-calculator skips the AI's morale-loss discount, the surrender
   multiplier and the Lieutenant General penalty — so against the AI it is the
   harsher of the two resolutions, and for your own hopeless battles it is the
   cheaper one.

---

## 13. Modder's shortlist

Five one-line changes that each alter the loss condition materially, in
descending order of effect per line edited.

| Change | Pref | Effect |
| --- | --- | --- |
| Make Lincoln's defeat a setback again | `lincolninfluenceunionmorale` **or** `demoralizevotersunionmoraleinfluence` → 0 | removes the double charge, restoring the −20 both tooltips promise |
| Give the 1862 midterms teeth | objective 9's `ObjectiveChapters` → empty | the −1-a-week rate (−52 per annum) runs for the rest of the campaign instead of about a day |
| Make battles matter | `influenceonnationalmoralelossratefactor` 0.3 → 0.6 | 10,000 casualties goes from −0.6 to −4.0 points; the exponent is the single most leveraged number in the file |
| Reward conquest | add a positive branch to the town-capture path for the new owner | currently only the loser is charged |
| Slow the political clock | `convergencenatmoraletowardsstatesupport` 0.5 → 0.25 | doubles the time constant to four years, making the campaign decided by events rather than by support |

> Changing the first of those is the clearest "restore the designer's intent"
> fix found in this subsystem; changing the third is the clearest lever for
> anyone who wants a battle-driven rather than politics-driven campaign.

---

## 14. Still open

1. **Whether the difficulty slider's morale bonus is meant to survive.** As
   read, the campaign path discards it (§10). A mod that wanted it would have
   to move the write into the campaign scene — and scale it down by two orders
   of magnitude first.
2. **What the private-wealth term is worth in practice.** The ±0.4 points per
   point of monthly growth in §6.2 is derived from the formula on an assumed
   linear growth through the month; the absolute scale of average private
   income across a campaign has not been sampled. **Untested** — a save-file
   comparison a month apart would settle it.
3. **Whether `averagesupport`'s keying on town ownership is intended** (§6.1).
   Still open in [17](../code-findings/17-politics-and-support.md) §9.
4. **What a European intervention does to the loss condition.** The
   town-capture path moves no morale when neither party is the player (§5), and
   no shipped scenario reaches an intervention easily enough to observe it.
5. **Whether the 25 % floor is ever approached from above in AI play.** The AI
   has no model of its own loss condition anywhere in
   [20](../code-findings/20-ai-campaign.md) — it reads national morale only to
   size offensives and filter recruiting states. **Nothing makes the AI defend
   its own morale.**
