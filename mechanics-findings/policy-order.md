# Policy order

What to research, in what order, and what you are actually buying. The policy
system is smaller than it looks — five ladders of twenty levels, sixty-odd
acts, and one research pipe whose speed is your political support — and three
of its most important rules are nowhere in the interface.

Sources: the catalogue in
[22-policy-and-project-catalogue](../code-findings/22-policy-and-project-catalogue.md)
and the dispatch in
[09-policy-and-project-effects](../code-findings/09-policy-and-project-effects.md),
with the tax side in [the war economy](the-war-economy.md) §6 and the
recruitment side in [the manpower tap](the-manpower-tap.md) §4.

---

## 1. The short version

- **Policies cost time, not money.** There is no monetary price on a policy.
  The only currency is research time, and the only thing that buys speed is
  average state support.
- **Researching two policies at once is slower than one after another.** The
  rate divides by `N^1.1` where N is the number in research, so total
  throughput *falls* as you queue more. Five at once take **5.9×** as long
  each.
- **The number of days printed on a policy is not the number of days it will
  take.** The figure on the policy itself ignores support, parallelism and
  Administration Reform; the figure in its *tooltip* includes all three. At
  75 % support with three in research the real time is **4.5×** the printed
  one.
- **The 7-to-12 "policies" counter is a lifetime budget, not a queue width.**
  Completed ladder policies never leave the queue, and there are **twenty
  ladder levels per side**. So you can hold at most 12 of 20 — enough to max
  out **three of the five branches** — and only 7 if you never fund the
  politics subsidy.
- **Acts are free of that budget entirely.** They bypass the cap, retire out
  of the queue when finished, and never come back. The only cost of an act is
  the research time, and the drag it puts on everything else while it is in
  research.
- **Cutting the politics subsidy cancels policies**, in-research first and
  then completed ones — and each cancellation zeroes **everything downstream
  of it in the prerequisite graph.**
- **A ladder level does not just raise a ceiling; it raises your bill.** The
  subsidy slider keeps its percentage, and the category's annual cost scales
  with the level you just finished.
- **Three ladders are gated on a pre-war choice** you make before the campaign
  begins, and you get three picks. The tooltips say so, in capitals — but the
  arithmetic means a **Union player must spend all three picks on ladder keys**
  to reach the top of all five branches, where the Confederacy needs only two.

---

## 2. One pipe, and its speed is your state support

Every policy in research advances at

```
progress per year = average state support
                  ÷ (number of policies in research) ^ 1.1
                  × (1 + 0.05 per level of Administration Reform)
                  ÷ the policy's own duration
```

Three consequences, each worth more than most operational decisions.

### 2.1 Support *is* research speed

At 100 % average support a policy takes exactly its printed duration. Below
that it stretches in direct proportion. The nominal durations are short — 10
to 75 days — but *National Support* is a population-weighted figure that
rarely sits near 1.0, and [how you lose](how-you-lose.md) §6.1 is the reason
you cannot simply ignore it.

| Printed | Nominal | 90 % | 75 % | 60 % | 45 % | 30 % |
| --- | --- | --- | --- | --- | --- | --- |
| 0.027 (Militia Act I) | 10 d | 11 | 13 | 17 | 22 | 33 |
| 0.055 (most cheap acts) | 20 d | 22 | 27 | 33 | 44 | 67 |
| 0.068 (ladder I) | 25 d | 28 | 33 | 42 | 56 | 83 |
| 0.082 (ladder II) | 30 d | 33 | 40 | 50 | 67 | 100 |
| 0.110 (Bank Act) | 40 d | 44 | 53 | 67 | 89 | 133 |
| 0.123 (ladder III) | 45 d | 50 | 60 | 75 | 100 | 150 |
| 0.164 (ladder IV) | 60 d | 67 | 80 | 100 | 133 | 200 |
| 0.205 (Military IV) | 75 d | 83 | 100 | 125 | 167 | 250 |

> Conquest makes this worse rather than better. *National Support* is keyed on
> **town ownership**, so every enemy town you take enters your average at your
> low support in that enemy state — a deep invasion slows your own research
> ([17](../code-findings/17-politics-and-support.md) §2).

### 2.2 Queueing is a net loss

The `N^1.1` divisor means total throughput across N policies is `N^−0.1`,
which *decreases* with N:

| In research | Each takes | Total throughput |
| --- | --- | --- |
| 1 | ×1.00 | 1.00 |
| 2 | ×2.14 | 0.93 |
| 3 | ×3.35 | 0.90 |
| 4 | ×4.59 | 0.87 |
| 5 | ×5.87 | 0.85 |
| 7 | ×8.50 | 0.82 |
| 12 | ×15.4 | 0.79 |

So parallel research buys exactly one thing: several policies finishing at
about the same time. It costs 7 % of your total output at two, and 21 % at
twelve.

**Acts count in that divisor even though they ignore the slot cap.** Queueing
acts past the cap is therefore a way to slow your own research with no
compensating limit — the one case where the interface will happily let you do
something strictly bad.

> **The AI never pays this.** `CheckAIPolicyChange` returns immediately if
> anything is already in research, so the AI holds **exactly one policy at a
> time**, always at ×1.00. The parallelism penalty is a player-only handicap
> that the player opts into by clicking
> ([22](../code-findings/22-policy-and-project-catalogue.md) §13.2).

### 2.3 Two different day counts for the same policy

The policy panel prints a remaining-days figure **on** each policy and another
**in** its tooltip, and they are computed differently:

| Where | Formula | Includes |
| --- | --- | --- |
| On the policy | `duration × 365.25 × (1 − progress)` | nothing |
| In the tooltip | the same, divided by support, by `1/N^1.1` and by the Administration Reform factor | all three |

The tooltip is right, and it even itemises the three penalties in words
("−25 % research speed due to lower state support", and so on). The number
printed on the policy is the nominal duration and nothing else.

> At 75 % support with three policies in research, the number on the policy is
> **4.5× too optimistic** — a Military IV that reads "75 days" is a 335-day
> project. Always hover. This corrects an earlier reading of
> [22](../code-findings/22-policy-and-project-catalogue.md) §3.1, which took
> the agreement between tooltip and simulation to cover the panel as a whole.

---

## 3. The slot counter is a lifetime budget

The policy panel shows `n / maxpolicies`. What it counts is **every ladder
policy you hold or are researching, completed ones included** — because only
*acts* are retired from the research list when they finish. A completed ladder
level stays in the list forever, because that is where its effect is replayed
from every tick.

There are **twenty ladder levels per side**: Government Funding I–III (3),
Bread Basket or King Cotton I–IV (4), Industrialization I–IV (4), Military
I–IV (4) and Diplomacy I–V (5).

And the cap is:

```
maxpolicies = 7 + round(5 × the politics subsidy slider)
```

| Politics slider | Slots | Annual cost |
| --- | --- | --- |
| 0 % | 7 | $0 |
| 20 % | 8 | $2 M |
| 30–50 % | 9 | $3–5 M |
| 60 % | 10 | $6 M |
| 70–90 % | 11 | $7–9 M |
| 100 % | **12** | $10 M |

(The rounding is half-to-even, so 40 % and 50 % both buy 9 slots and 80 % and
90 % both buy 11 — two bands where you pay more for nothing. **Derived**.)

### 3.1 Which means you can max out three branches, not five

A ladder top requires every level below it, so the branches cost:

| Branch | Levels | Slots for the top | Top subsidy factor |
| --- | --- | --- | --- |
| Government Funding | I–III | 3 | 1.0 |
| Agriculture (Bread Basket / King Cotton) | I–IV | 4 | 2.0 |
| Industrialization | I–IV | 4 | 2.0 |
| Military | I–IV | 4 | 2.0 |
| Diplomacy | I–V | **5** | **3.0** |
| *all five* | | **20** | |

With twelve slots the affordable combinations are three branches and change —
Military IV + Industrialization IV + Government Funding III is 11 with one
spare, Diplomacy V + Military IV + Government Funding III is exactly 12. With
seven slots you can top out **two** branches at most, and the only pair that
fits is one four-level ladder plus Government Funding.

> **This is the single most consequential fact about the policy system and the
> interface never states it.** The counter looks like a queue width — "you may
> research 9 things at once" — and is in fact the number of ladder levels your
> nation may ever hold. Decide which three branches you want before you spend
> the first slot, because getting one wrong costs the whole campaign's
> progress in a branch.

### 3.2 And cutting the politics subsidy destroys policies

The cap is enforced retroactively. Whenever subsidies are recalculated — which
is every alliance pass — the game recomputes `maxpolicies` and, while the
count exceeds it, repeatedly removes the ladder policy with the greatest
`duration + (1 − progress)`, up to fifty removals per pass.

Two things make that dangerous rather than merely annoying.

**In-research policies always score higher than completed ones** (their
`1 − progress` term is up to 1.0 against a duration of at most 0.205), so the
game eats your current research first. Good. But once everything is complete,
it starts cancelling **finished** ladder levels.

**And a cancellation cascades over the whole prerequisite closure.** Removing a
policy sets its progress to zero *and* zeroes every policy that transitively
requires it, completed ladder levels included. Cancelling Government Funding I
therefore wipes Government Funding II and III, and zeroes every incomplete act
downstream of them — Print Notes, Tariff Act, Confiscation Act, War Bonds, the
Bank Acts and all three Revenue Acts. **Completed acts survive**; everything
else in the subtree does not.

> **Never lower the politics subsidy slider once you are at the cap.** Pulling
> it from 100 % to 60 % does not "free up budget" — it deletes two ladder
> levels and everything that depended on them. If you need the $4 M, take it
> from any other category.

---

## 4. What a ladder level actually buys

Nothing but one number: the multiplier on its category's subsidy. That
multiplier does two jobs at once.

```
annual spend on a category  = slider × base cost × ladder factor
project budget accrued      = the same figure, 1:1
```

So the ladder factor is simultaneously **how much you may spend per year** in
that category and **how fast project funding accrues** in it. The money leaves
the treasury and arrives in the category's budget at the same rate.

| Factor | Held level | Economy | Agriculture / Industry / Military / Diplomacy |
| --- | --- | --- | --- |
| **0.1** | **none** | $5 M/yr | $5 M/yr |
| 0.25 | I | $12.5 M | $12.5 M |
| 0.5 | II | $25 M | $25 M |
| 1.0 | III | $50 M | $50 M |
| 2.0 | IV | — | $100 M |
| 3.0 | V (Diplomacy only) | — | **$150 M** |

Politics is the exception: **there is no Politics ladder**, its factor is never
written and stays at 1.0, so the politics category accrues at up to **$10 M a
year from the first day of the campaign.**

Three things follow.

**Before you have any policy in a branch, that branch accrues at a tenth of
its headline rate.** Five of the six categories start at $5 M a year even with
the slider at maximum. Politics starts at $10 M. So **politics projects are the
only ones you can meaningfully buy early**, and the first ladder level in a
branch is worth more than it looks — it multiplies that branch's throughput by
2.5 immediately.

**Finishing a ladder level silently raises your bill.** The slider keeps its
percentage and the factor doubles, so completing Military III at a 60 % slider
takes your military spending from $15 M to $30 M a year without you touching
anything. Check the finance panel after every ladder completion.

**Diplomacy V is the single largest subsidy unlock in the game** — the only
factor of 3, and the only five-level ladder. Whether it is worth five of your
twelve slots depends entirely on whether you intend to buy weapons from Europe
(§7).

> Six further effects that the config describes for these ladders **do not
> exist**: the agriculture ladder's European-intervention and loyal-state
> support terms, the industry ladder's transport-subsidy and national-morale
> terms, and the military ladder's recruitment, civil-order, trade-war,
> railroad-speed, transport-upkeep and military-experience terms. Ten prefs
> are parsed and read by nothing
> ([22](../code-findings/22-policy-and-project-catalogue.md) §10). **Military
> I–IV advertises seven effects and has one.**
---

## 5. The three pre-war keys

Before the campaign starts you choose **three** of ten grand policies. They
are researched instantly, cost nothing, and never occupy a slot. Five of the
twenty — three Union, two Confederate — are **prerequisites for the upper half
of a wartime ladder**:

| Grand policy | Unlocks |
| --- | --- |
| 75 Bread Basket (Union) | Bread Basket **III–IV** |
| 76 Industrialization (Union) | Industrialization **III–IV** |
| 74 Enforce Neutrality Act (Union) | Diplomacy **IV–V** |
| 175 King Cotton (CSA) | King Cotton **III–IV** |
| 176 Southern Industrialization (CSA) | Industrialization **III–IV** |

The game is honest about this: each of those five policies' descriptions says
so, and the gated wartime policies carry a capitalised "REQUIRES … PRE-WAR
POLICY" line in their own tooltips. The problem is arithmetic, not disclosure.

> **The Union has three keys and three picks.** Taking all of 74, 75 and 76
> is the only way to keep every ladder reachable — and it spends your entire
> pre-war allowance, giving up Underground Railroad, Kansas, Go West!, Support
> Abolitionism, Security Measures, Indian Wars and the Union Pacific
> altogether. **The Confederacy has two keys and three picks**, so it gets one
> free choice the Union does not. Confederate Diplomacy IV has no pre-war
> requirement where the Union's does.

Since §3.1 says you can only top out three branches anyway, the Union's
sensible lines are:

- **take two keys and a free pick** — e.g. 76 Industrialization + 74 Neutrality
  + one of Security Measures (sinks Confederate ships and multiplies
  Confederate weapon stocks down) or Union Pacific (railroads plus a permanent
  credit-rating modifier) — and plan to top out Industrialization, Diplomacy
  and Government Funding, leaving agriculture at level II;
- **or take no keys at all** and top out Military and Government Funding, which
  need none, spending all three pre-war picks on their own one-shot effects.

The second line is more attractive than it sounds, because **the Military
ladder is ungated and the Military category funds most of the useful
projects** (§7). What you give up is the agriculture and diplomacy tops, and
Diplomacy's unique ×3.

### 5.1 What the other seven are worth

Ranked by what they do to the simulation, with the shipped magnitudes.

| Grand policy | Side | Effect |
| --- | --- | --- |
| **77 Security Measures** | Union | **Sinks 50 % of the ships in the Confederate starting pool and halves every Confederate artillery and rifle stock**, plus −5 CSA national morale |
| **175 King Cotton** | CSA | Every plantation one level up, +5 CSA support in all slave states, **+0.20 European intervention** — the largest intervention step of any pre-war policy — *and* keys the agriculture ladder |
| 177 Arms Agents | CSA | **×1.5 on all Confederate artillery and rifle stocks**, −5 Union national morale |
| 70 Underground Railroad | Union | **−0.05 on the Confederate volunteer factor** — a third of its 0.15 pre-war base — plus a slavery-factor reduction that keeps growing at −0.05/yr |
| 79 Union Pacific Railroad | Union | **−2 credit-rating notches** (an improvement, permanently), ×1.25 railroad transport capacity and construction speed |
| 179 Southern Pacific | CSA | the same rating modifier, plus the Pacific trade node |
| 72 Go West! | Union | California and Oregon become **recruitable**, +25 support in both, ×1.20 population, ×2.0 transport capacity at Oregon and Santa Fe |
| 172 Slavery to the West | CSA | New Mexico and Arizona join the Confederacy and become recruitable; every Texas harbour one level up |
| 173 Apostles of Disunion | CSA | +20 CSA support in every slave state — but **+10 Union support in every free state** |
| 71 / 171 Kansas | both | +25 support in Kansas and +5 across your own states |
| 73 Support Abolitionism | Union | **−10 Confederate support in every Union state** |
| 174 Filibustering | CSA | Cuba and Nicaragua join the Confederacy and become recruitable, +25 support there; gates Spanish intervention |
| 178 / 78 Indian Wars | both | +5 your national morale and +5 your military experience — **and +5 military experience to the enemy as well** |

Three readings worth stating.

**Security Measures is the strongest single pre-war pick in the game.** Half
the Confederate navy and half its weapon stockpile, before a shot is fired. Its
Confederate counterpart Arms Agents gives +50 % — so the two together leave the
Confederacy at 0.75× its authored stocks, and Security Measures alone leaves it
at 0.5×.

**Indian Wars hands the enemy the same military-experience grant it gives
you**, and gives the Confederacy no morale at all. For a Union player it is +5
morale and a wash on experience; for a Confederate player it is +5 experience
for the enemy and nothing else.

**Apostles of Disunion is the only grand policy that raises the enemy's
support on purpose.** +20 in your own slave states against +10 in all of theirs
is still a good trade on a population-weighted average, but it is the one pick
whose downside is in the same currency as its upside.

---

## 6. The acts, and the one that decides the war

Acts are the other sixty-odd policies. They do not consume a slot once
finished, they bypass the slot cap while in research, and they are where every
interesting one-shot effect lives. Their durations run 20 to 55 days nominal.

### 6.1 Militia Act I is the war trigger, and going first is expensive

Policy 36 (Union) / 136 (Confederate) is **ten nominal days** and the shortest
research in the game. Completing it hands over every fort, reassigns every
contested state to **whichever side it supports more**, and declares war
([22](../code-findings/22-policy-and-project-catalogue.md) §5).

While only one side holds it, the other side bleeds. The rates are not
symmetric:

| Who moved first | What it costs, per annum |
| --- | --- |
| Union first | **+2.00** Confederate support in every slave state, +0.40 CSA national morale |
| Confederate first | +0.75 Union support in every **free** state, +0.75 CSA support in every slave state, **+0.40 Union national morale** |

> **Going first is 2.7× worse for the Union**, and a Union player who passes
> it **before 14 April 1861** additionally hands every slave state a one-shot
> **+10 Confederate support** — which lands in the *same call* as the
> allegiance cascade, before the comparison that fixes every state's side for
> the rest of the campaign ([17](../code-findings/17-politics-and-support.md)
> §5a). That one decision can give the Confederacy a border state permanently.
>
> **So do not rush Militia Act I as the Union.** Let the Confederacy call out
> its militia; it closes the window in about ten days anyway, and you collect
> +0.75/yr of free-state support and +0.40/yr of national morale for waiting.
> The Confederacy has the opposite incentive and should move immediately.

### 6.2 The acts that are worth the research time

| Act | Side | Days | Why |
| --- | --- | --- | --- |
| **Regulars Act** (48 / 147) | both | 20 | Creates **seven named regular regiments** with a **99-month contract** — immune to the reenlistment roll that costs everyone else two-thirds of a brigade — plus seeded training, experience and perk experience, and a permanent military-experience bonus |
| **Revenue Acts I–III** (26–28) | both | 30–50 | The only thing that unlocks income and corporate tax at all; corporate needs **III specifically** ([the war economy](the-war-economy.md) §6) |
| **USCT** (33) / **CSCT** (133) | both | 45 | Free brigades: a whole Corps and Division plus `usctbrigadestobeadded` brigades raised at the capital, and a running volunteer bonus |
| **Enrollment / Conscription Acts** (39/40, 139/140) | both | 30–45 | The **only** way to switch the draft on — every campaign ships `mobilizationrate = 0` ([the manpower tap](the-manpower-tap.md) §2) |
| **Print Notes I–II** (20/21) | both | 20–25 | **+$50 M/yr of treasury each**, cumulative, against ×0.9 public wealth each |
| **Legal Blockade** (41) | Union | 20 | +0.20 European intervention toward the Union and a lasting shift in European demand |
| **Organized Blockade Running** (142) | CSA | 30 | A real **25 % reduction in the blockade of your own harbours** that the description never mentions |
| **Letters of Marque** (43 / 143) | both | 20 | Floors every enemy harbour's blockade ratio at 0.015 — the cheapest trade-war act, at −0.10 intervention |
| **Arming of Civilian Ships** (35 / 135) | both | 20 | Raises port transport capacity and unlocks civilian shipping |

### 6.3 And the ones to think twice about

**The Lieutenant General Act (42) makes your defeats worse.** It sets the
commander-in-chief's rank to 8 directly, raises the campaign-rank clamp, and
sets a flag that multiplies the national-morale charge of any battle **his**
side loses by **1.2** ([how you lose](how-you-lose.md) §4). It is a Union-only
act; the Confederacy has no equivalent, and no equivalent penalty.

**Abolition of Slavery (44) gives the Confederacy twice the morale it gives
you** — +10 CSA against +5 Union, on the same reason code
([how you lose](how-you-lose.md) §3). USCT and Emancipation are the same shape
at smaller magnitudes, and the config labels say so, so the direction is
intended; the ratio on Abolition is the outlier.

**Burn Cotton (130) multiplies every owned node's cotton stock** by
`burncottonstock` — a one-way destruction of your own most valuable export in
exchange for +0.15 intervention. Read the pref before taking it.

**Restrict Cotton Trade (131) is the only policy in the game that can be
cancelled**, and its teardown divides rather than restoring, so cancelling it
repeatedly compounds the error
([22](../code-findings/22-policy-and-project-catalogue.md) §6.2).

**Northern Routes (30) matches five state *names* as strings** and has two
effect sites whose state lists disagree. It works in the shipped game, but it
is the most fragile effect in the file and the first thing a renamed state
breaks ([22](../code-findings/22-policy-and-project-catalogue.md) §11.2).

**The Confiscation Act (25)'s supply bonus has an inverted sign** relative to
the manual's description ([the manual against the code](manual-vs-code.md)).

**Three acts still advertise a Trade War subsidy category that was cut from
the game.** Letters of Marque and Legal Blockade each ship a
`…tradewarsubsidies = 1.2` pref that nothing reads; what they actually do is
listed in §6.2.

> **The Confederacy's recruiting ladder is both shorter and later.** It has no
> Militia Act IV and no Lieutenant General Act, and its Militia Acts II and III
> are gated to chapter 1 against the Union's chapter 0 and 1 — with nothing
> added in compensation.

---

## 7. Projects: the half of the system that costs money

Projects are bought with the per-category budget that your subsidy sliders
accrue (§4). A project's price is authored per project, and the levels get
**cheaper per level, not dearer**:

```
cost of level n = √n × the first level's cost
```

| Level | Price | Cumulative |
| --- | --- | --- |
| 1 | ×1.00 | ×1.00 |
| 2 | ×1.41 | ×2.41 |
| 3 | ×1.73 | ×4.15 |
| 4 | ×2.00 | ×6.15 |
| 5 | ×2.24 | ×8.38 |

So a repeating project at $1 M a level costs $8.4 M for five levels. **How
many levels you may take is bounded by a UI sprite array**, whose length is
inspector data and not in the assembly — the one number in this system that
still needs an asset dump
([22](../code-findings/22-policy-and-project-catalogue.md) §9.2).

### 7.1 Politics is the category you can afford first

Every category except politics starts at a ladder factor of **0.1**, so even
with the slider at maximum it accrues $5 M a year until you finish a ladder
level. Politics has no ladder, its factor is permanently 1.0, and it accrues
up to **$10 M a year from day one**.

The politics category holds: Propaganda ($5 M), Counter-propaganda ($2.5 M),
Occupation Administration ($1.5 M), Suppress Population ($1 M),
**Administration Reform ($1 M)**, Railroad Construction ($1.5 M) and Hall's
Carbines ($0.75 M).

> **Buy Administration Reform first.** It is $1 M, repeating, in the one
> category that is funded from the first day, and each level makes **every
> policy you will ever research 5 % faster**. Five levels cost $8.4 M for
> +25 % research speed, and research speed is the scarcest resource in the
> game's first year.

### 7.2 The "available from" dates gate nothing

Every project carries a per-side availability date — Military Railroad from
1 January 1864, Sharps Rifles from 1 January 1863, Repeating Rifles from none
at all. **Nothing enforces them.** The only reader of that field in the whole
assembly is a routine that runs **once, at frame 32 of a new campaign**, and
its job is the opposite of gating: it *grants* every dated project whose date
has already passed.

Two consequences.

**In a 1861 campaign you can buy anything you can afford, whenever you can
afford it.** Neither the project list's filter nor the appointment check looks
at the date. Military Railroad in March 1861 is legal.

**A later campaign start is handed a pile of free projects.** At frame 32 the
grant walks the project list and adds every dated, affordable, not-yet-held
project directly — **without debiting the funding it just checked**. So a
Spring 1864 start, which ships $12 M of Union military funding and $16 M of
industry funding, collects essentially every dated military project at once:
the two artillery reforms, both cavalry reforms, 6-gun Batteries, Supply
Reform, Horse Artillery, Organization Reform, Military Railroad and most of the
weapon unlocks. A Spring 1861 start, which ships **all zeros**, gets nothing.

| Start | Union subsidy funding at frame 32 (politics / economy / agri / industry / military / diplomacy), $M |
| --- | --- |
| Spring 1861 | 0 / 0 / 0 / 0 / 0 / 0 |
| Summer 1861 | 0.2 / 0.2 / 0.15 / 0.35 / 0.25 / 0.15 |
| Spring 1862 | 1.5 / 2 / 0.75 / 4 / 3 / 1.2 |
| Spring 1863 | 3 / 4 / 1.5 / 8 / 6 / 2.4 |
| Spring 1864 | 6 / 8 / 3 / 16 / 12 / 4.8 |

> **This is a real difference in scenario difficulty that nothing describes.**
> The later starts are not merely further along historically; they are handed
> the reform projects for free, and the 1861 starts must buy every one of them
> out of a budget that begins at zero. **Untested:** the exact set granted
> depends on the funding comparison at that single frame, which a save-file
> inspection one minute into a Spring 1864 campaign would settle.

---

## 8. Which projects to buy

Prices are the **first level**; multiply by √level for later ones. Category is
which subsidy slider pays for it.

### 8.1 The ones that change what your army can do

These are not modifiers — without them, your units are structurally worse.

| Project | Cat. | Price | What you lose without it |
| --- | --- | --- | --- |
| **Cavalry Reform** (110) | military | **$0.85 M** | Every cavalry brigade's maximum size is multiplied by **0.2**, and cavalry sits a rung lower in the command hierarchy |
| **Artillery Reform** (112) *or* **6-gun Batteries** (124) | military | **$0.85 M** | Artillery maximum size × **0.375** and a lower echelon. Either project alone suffices |
| **Organization Reform** (90) | military | $2 M | The campaign-rank clamp stays at 15, and supply consumption stays 11 % higher. **Project 90 is the live one**; 89 is the same reform authored for a configuration this build does not use |
| **Command Reform** (88) *or* Organization Reform | military | $1.5 M | **Army Groups**. Either unlocks them, given the alliance flag |
| **Supply Reform** (115) | military | $1.5 M | Supply **depots** — the only thing that resupplies four times faster than an economic node ([supply in practice](supply-in-practice.md) §1) |
| **Cavalry Reform II** (111) | military | $1 M | Horse artillery (project 123 instead, in scenario 002) |

> **Buy all six early.** Together they are about $8 M of military funding and
> they are the difference between full-size brigades with depots and a
> structurally crippled army. At the 0.1 ladder factor that is nearly two
> years of military accrual at a maximum slider — which is the real argument
> for taking **Military I** as one of your first policies, since it alone takes
> military accrual from $5 M to $12.5 M a year.

### 8.2 The repeating modifiers worth stacking

| Project | Cat. | Price | Per level |
| --- | --- | --- | --- |
| **Administration Reform** (95) | politics | $1 M | **+5 % policy research speed** |
| **Logistics Reforms** (100) | military | $3.5 M | **−5 % supply consumption and transport upkeep** |
| Training Manuals (118) | military | $1.5 M | +5 % training speed |
| Recruitment Offices (109) | military | $1 M | +5 % recruitment |
| Recruit Agents (108) | military | $1 M | +5 % intelligence gathering |
| Civil Order (107) | military | $1 M | **−5 % support loss from casualties** — its raid half is inert (§8.3) |
| Trade Warfare (106) | military | $1 M | +5 % blockade and blockade-running efficiency |
| Infrastructure Reform (99) | industry | $2.5 M | +5 % node transport capacity |
| Subsidize Industry (104) | industry | $2.5 M | +5 % building productivity |
| Subsidize Agriculture (105) | agri | $2.5 M | +5 % building productivity |
| Weapon Production (102) | industry | $2.5 M | +5 % weapon production |
| Railroad Construction (119) | politics | $1.5 M | +5 % construction speed |
| Improve Credit Rating (97) | economy | $10 M | **−1 credit-rating notch** — a notch, not a percentage, despite what the panel prints |
| Subsidize Banks (96) | economy | $5 M | +5 % funding improvement; needs a Bank Act |
| Trade Deals (121) | diplomacy | $1.25 M | +5 % trade volume |
| Send Envoys (103) | diplomacy | $2.5 M | **+0.05 European intervention per appointment** |
| Military Education (122) | military | $3 M | A **count**, not a factor: it gates how many military schools you may hold |
| Cotton is King (116) / Corn is King (117) | agri | $1.25 M | +0.025 intervention per appointment |
| Propaganda (91) | politics | $5 M | +1 point of support in your own states, per appointment |
| Counter-propaganda (92) | politics | $2.5 M | −1 point of enemy support, per appointment |
| Occupation Administration (93) | politics | $1.5 M | +1 your support in states you occupy |
| Suppress Population (94) | politics | $1 M | −1 enemy support in states you occupy |

Note that the four support projects (91–94) are **repeating and fire once per
appointment**, so each level is a one-off step of ±1 point of support, not a
rate. At $5 M a level Propaganda is the most expensive point of support in the
game; Suppress Population at $1 M is the cheapest, and it only works where you
are already occupying enemy territory.

### 8.3 The ones that do nothing, or are mis-described

| Project | Problem |
| --- | --- |
| **Market Reform** (98) | **Inert.** The one place that reads its level discards both the level and its pref in bare expressions. The Market *building* works; the project that claims to improve it does not |
| **Plantation Mechanization** (114) | Inert — its effect sits inside a condition that cannot be true ([the war economy](the-war-economy.md)) |
| **Civil Order** (107) | **Half inert.** Its casualty term works; its raid term is computed in two places and the result thrown away both times, so raiding costs you the full support hit however many levels you hold |
| **Farm Mechanization** (113) | Its crop-production half is inert; its **recruitment** half works |
| Improve Credit Rating (97) | Works, but the effect summary formats a rating-notch count as "+100 %" per level |
| 88, 89, 90, 110, 111, 112, 115, 120, 122, 123, 124 | **Eleven real projects that never appear in the active-effects panel at all** — including every structural reform in §8.1 |

> So the panel that lists what your projects are doing omits every project in
> §8.1 and includes one that does nothing. Treat it as a partial list.

---

## 9. What the AI does, and where you can beat it

The AI's policy agenda is an **ordered queue per personality** — it walks the
list and researches the first eligible entry, one at a time, forever
([22](../code-findings/22-policy-and-project-catalogue.md) §13).

Three exploitable asymmetries.

**The AI never parallelises**, so it never pays the `N^1.1` penalty. If you
queue three policies you are researching at 90 % of its throughput and each
item arrives 3.35× later than its printed estimate. Research serially and you
match it; research serially *and* buy Administration Reform and you beat it.

**The AI's project selection is a weighted random draw** per subsidy category,
not a plan. It buys whatever ticket it pulls once the category's funding covers
the cost — so it will buy Propaganda before Cavalry Reform if the dice say so.
Its weights come from its personality, and one ticket at the end of every draw
list is unreachable because of an off-by-one.

**Two emergency personalities override the queue.** Falling below four small
brigades switches the AI to a recruiting agenda; a credit rating below 0.4
switches it to the finance acts in order. Both **cancel anything in research
that is not on the emergency list** — so pressure on either axis costs the AI
its current research outright.

> The player's own side runs through the same code when *automanage policies*
> or *automanage projects* is on, including the one-at-a-time rule — so an
> automanaged research programme always runs at ×1.00, though it follows the
> personality's queue rather than your priorities. It will not undo a queue you
> built by hand; it only adds when nothing is in research.

---

## 10. Eleven things to do differently

1. **Research one policy at a time.** Two is 7 % of your output, twelve is
   21 %, and every item in the queue arrives later than if you had done them
   in sequence.
2. **Hover every policy for its real duration.** The number printed on the
   policy ignores support, parallelism and Administration Reform; the tooltip
   does not.
3. **Decide your three branches before spending the first slot.** Twelve slots
   against twenty ladder levels means three tops, and the choice is permanent
   in practice.
4. **Fund the politics subsidy to 100 % and never lower it.** It is $10 M a
   year for five extra slots, it is the only category that accrues at full rate
   from day one, and cutting it deletes finished policies and everything
   downstream of them.
5. **Buy Administration Reform immediately**, and keep buying levels. $1 M for
   +5 % on every policy for the rest of the campaign, out of the one budget you
   actually have in 1861.
6. **Take Military I early even if Military is not one of your three tops.**
   One level takes military accrual from $5 M to $12.5 M a year, and the
   military category pays for the six structural reforms.
7. **Buy Cavalry Reform, Artillery Reform (or 6-gun Batteries), Organization
   Reform, Command Reform and Supply Reform before anything repeating.** About
   $8 M for full-size cavalry and artillery, Army Groups, a lower supply bill
   and the ability to build depots.
8. **Union: do not rush Militia Act I.** Going first costs 2.7× what waiting
   does, and going first *before 14 April 1861* can hand the Confederacy a
   border state for the whole campaign.
9. **Confederacy: pass Militia Act I at once**, and take King Cotton pre-war —
   it is the only grand policy that does four useful things.
10. **Check the finance panel after every ladder completion.** The slider keeps
    its percentage and the category's bill scales with the new level, so
    finishing Military III doubles your military spending without a click.
11. **Ignore the availability dates on projects.** They gate nothing; buy on
    affordability alone.

---

## 11. For modders

The dispatch is unusually friendly. `LaunchPolicyEffect` is a 117-arm switch
with **no `default:`**, and neither project dispatcher has one either, so an
unrecognised id falls through and the game still accepts, researches, appoints
and tracks it. A postfix therefore *composes* with the shipped effects instead
of replacing them ([09](../code-findings/09-policy-and-project-effects.md)).

| Want to change | Where |
| --- | --- |
| A policy's duration, chapter, prerequisites or text | `Config/policies.dat` — all four are read from the file and overwrite the prefab |
| A new project | `projects.dat` is the whole definition: no prefab, and ids 20–29 and 42–87 ship as `PLACEHOLDER` |
| A new policy | not from config — the scene owns the id list. Clone a `Policy` under `UI` and call `Policy.InitializeStatics()` ([22](../code-findings/22-policy-and-project-catalogue.md) §15) |
| The six advertised ladder effects that do not exist | ten `float[4]` prefs are already parsed and shipped with sensible values; they need consumers, not data |
| Mutual exclusion or date gating | `Policies.IsDeactivated` is two `if`s and the whole exclusion system |
| The slot cap behaving as a lifetime budget | `GetActivatedPolicies()`'s default form counts completed non-acts; a postfix that filtered on `researchprogress < 1f` would turn the counter into the queue width it looks like |

The cheapest balance fixes, in order of effect per line edited: give the
politics category a ladder (or stop counting completed policies against the
cap); point `project_marketeffects` at the paint call that already reads and
discards it; and give Military I–IV any one of its six missing effects.

---

## 12. Still open

1. **How many levels a repeating project may reach.** The cap is the length of
   a UI sprite array, which is inspector data. Everything in §8.2 is priced
   per level without knowing where the ladder stops. **Untested** — an asset
   dump, or appointing one project until it refuses.
2. **Exactly which projects a late start is granted.** §7.2 gives the
   mechanism and the funding each scenario ships, but the set depends on a
   single frame's comparison and the file order of the project list.
3. **Whether the subsidy sliders have detents.** The slot table in §3 assumes a
   continuous 0–1 slider; if the UI steps in tenths, two of the plateaus are
   unreachable rather than merely wasteful.
4. **What `maxslaveryfactor` does.** Underground Railroad and Slavery
   Restriction both move it and no consumer is documented
   ([22](../code-findings/22-policy-and-project-catalogue.md) §16).
5. **Whether the one-time project effects re-run on save load.** If they do,
   the four support projects' steps reapply every time you reload
   ([22](../code-findings/22-policy-and-project-catalogue.md) §16).
