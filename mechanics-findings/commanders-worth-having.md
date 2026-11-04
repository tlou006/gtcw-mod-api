# Commanders worth having

Which officers matter, which of their numbers matter, and which perks touch
the simulation at all. The commander model is five attributes, a rank ladder, a
reputation system and three separate perk namespaces — and roughly a third of
what the game shows you about a general has no mechanical effect.

Sources: [19-commanders-and-perks](../code-findings/19-commanders-and-perks.md)
and, for every perk, the per-perk audit in [34-perks](../code-findings/34-perks.md),
with the morale side in [why units break](why-units-break.md) and the
policy-side couplings in [policy order](policy-order.md) §6.

---

## 1. The short version

- **A green commander operates at half his printed attributes.** Every
  attribute is multiplied by `0.5 + 0.5 × experience`, so the numbers on the
  portrait are a ceiling he reaches only at full experience.
- **A wounded, captured or dead commander halves them again** — and he keeps
  his post until the replacement pass reaches him.
- **Three of the five attributes never improve.** Leadership, administration
  and cunning move only through **military schools in the commander's home
  state**; nothing a commander does in the field changes them.
- **Rank is never lowered.** Not by the game, not by you. Being assigned below
  your station costs fame and *gains* experience, and the man keeps his stars.
- **Changing a commander costs every attached unit 10 points of readiness**,
  and a succession cascade pays it at every level it touches.
- **Feuds are an initiative gap, in either direction.** The hourly chance is
  `|Δinitiative| + Δfame − 0.3`, so a subordinate quarrels as readily for being
  too cautious as too bold — and a **political** general is twice as likely to
  charge off as a West Pointer, four times as likely as a volunteer.
- **Fighting units get one perk slot; formations get four**, of which the perk
  panel lets you fill three. They draw on different lists, and the lists are
  not interchangeable.
- **Most campaign perks don't do what they say.** Of the 18, only seven work
  as described or close; **Partisan Brigades and Bushwhackers raise your own
  raiders' losses**, and Sappers and Miners, Limelights and Engineers &
  Mechanics do nothing that matters (§10). An earlier version named Balloon
  Corps and Bushwhackers as the two broken ones; Balloon Corps' slower orders
  are its designed cost.
- **Several battle perks are broken too.** **Mounted rifles weakens with every level**,
  **Deadly volley III doubles the sudden-losses morale hit on drilled troops**,
  and **Iron discipline II stops the nearby-rout penalty from ever fading**.
  Three melee bonuses — Spirited charge, Cold steel and Close support — are
  shown on the unit panel and never applied, and auto-resolve swaps
  Sharpshooters and Zouave.
- **`Veteran` does nothing. The engineer branch does nothing. The president
  does nothing.** Three visible, plausible-looking attributes with no consumer
  in the base game.

---

## 2. What the portrait number actually means

The five attributes — fame, leadership, initiative, administration, cunning —
all run 0 to 1 (0 to 100 on screen), and **nothing in the simulation reads the
stored value.** Everything goes through a getter that applies four
modifications in sequence:

| Step | Effect |
| --- | --- |
| Experience | × `0.5 + 0.5 × exp` — **a span of [0.5, 1.0]** |
| Out of duty | × **0.5** if wounded, captured or killed |
| Superior influence | × `1 + (superior − self) × 0.33` |
| DLC staff | + a bonus, DLC only |

So a commander with 80 leadership and 20 experience is operating at
`0.80 × 0.6 = 0.48` — worse than a 60-leadership veteran at
`0.60 × 1.0 = 0.60`. **Experience is worth as much as any attribute**, and it
is the only one that grows reliably: `0.2 per year of service`, capped by your
nation's military experience.

### 2.1 Your best general drags your worst up, and vice versa

The superior-influence term compares a commander with his direct parent — or
with the **commander-in-chief**, if the formation is out of the parent's
radius — and closes a third of the gap:

| Superior | Subordinate | Subordinate operates at |
| --- | --- | --- |
| 0.9 | 0.5 | 0.5 × 1.13 = **0.57** |
| 0.5 | 0.9 | 0.9 × 0.87 = **0.78** |
| 0.9 | 0.9 | 0.9 |

The gain is additionally scaled by `(1 − exp)` when the subordinate is the
*better* man, so an experienced subordinate is dragged down less by a poor
superior. Two things follow:

**Your commander-in-chief's attributes reach every formation out of its
parent's radius.** On a quiet front that is most of your army. Picking the
C-in-C is not a prestige decision.

**And the trap:** if a formation's parent has **no commander assigned**, the
influence factor returns **zero**, and because it is a multiplier that
collapses the subordinate's leadership, initiative, administration *and*
cunning to nothing. It cannot happen through the game's own UI, but it is the
first thing a mod or an edited save breaks.

---

## 3. Which attribute for which job

Every live consumer, grouped by the decision it should change. The full site
list is [19](../code-findings/19-commanders-and-perks.md) §2.6.

### 3.1 Leadership — the defensive attribute

| Buys | Shipped weight |
| --- | --- |
| **`fightingspirit`**, the hard ceiling on a unit's battle morale | 0.33 of the attribute blend |
| The morale bonus for having a commander nearby — **1.7× the weight of fame**, and 2.5× when he is killed | 0.125 against fame's 0.075 |
| Damps every amplified morale **drop**, as `(2 − leadership)` | — |
| Raises the unbearable-losses threshold | 0.75 |
| Softens the first-battle morale penalty | 0.5 |
| **Losses while retreating**: `× (1 + (1 − leadership) × 0.6)` | 0.6 |
| **Naval firepower**, fleet-vs-fleet and fleet-vs-fort | 0.3 |

> **Leadership is what keeps a formation on the field**, and it is the only
> attribute that reduces casualties on a retreat — a 0.9-leadership general
> loses 6 % extra men pulling out where a 0.3 one loses 42 %. Put your best
> leadership in the corps you expect to be pushed.

### 3.2 Initiative — the widest reach, and the one that causes feuds

| Buys | Shipped weight |
| --- | --- |
| **Command range and combat range of every campaign formation** | 0.5 and 0.33 |
| Readiness consumption — **capped by the commander-in-chief's initiative** | 0.35 |
| The AI's stance thresholds and how often it re-picks a macro stance | — |
| Offensive-operation gating on the campaign map | 0.6 |
| Naval engagement start range, **widened** | 1.3 |
| Naval ramming probability | 1.5 |
| **Feuds**, as the absolute difference against the superior | — |

> **Initiative is the campaign attribute.** Command range decides how much of
> your army is inside its commander's influence at all — which is what the
> whole §2.1 chain hangs on — and readiness consumption decides what orders
> you are allowed to give ([supply in practice](supply-in-practice.md) §6).
> It is also the only attribute with a real cost: see §7.

### 3.3 Administration — logistics and ships

| Buys | Shipped weight |
| --- | --- |
| **Readiness recovery** | 1.33 |
| **Battlefield** supply line quality (not campaign supply) | — |
| Overnight desertion reduction | 0.2 |
| Damps the fatigue penalty from poor condition of men and horses | 0.25 |
| Fleet movement, repair and resupply rotation | 0.35 |
| **The number of ships an admiral may command** — doubled at full administration | — |

> One quirk worth knowing: the ship-command limit reads the **raw** attribute,
> not the getter. **A green admiral commands as many ships as a veteran of the
> same nominal administration** — the only place in the model where experience
> does not matter.

### 3.4 Cunning — detachment work

| Buys | Shipped weight |
| --- | --- |
| **Concealment** — fed straight into the fog-of-war visibility test | 0.5 |
| **Intelligence gathering** | 2.0 — the heaviest single attribute weight in the game |
| Raiding and guarding efficiency | 0.5 and 0.4 |
| Siege effectiveness | 0.5 |
| Skirmishing casualty reduction | 0.25 at full cunning |
| **Own casualties when the unit is cavalry** | 0.8 |
| Slipping a garrison away rather than losing it | 0.5 |
| Whether the AI burns a depot it is about to lose | 0.4 |

> **Cunning belongs on cavalry and on detached commands.** Its ×2.0 on
> intelligence gathering is the single largest attribute coefficient anywhere,
> and its 0.8 on cavalry casualties is the only attribute that directly saves
> men in combat.

### 3.5 Fame — reputation as a stat

| Buys |
| --- |
| `fightingspirit`, at the same 0.33 weight as leadership |
| Overnight desertion reduction, at 0.2 |
| The morale bonus for a commander nearby, at 0.075 — **less than half leadership's weight** |
| **State support, if the commander is `political`** |
| `campaignranking`, which decides promotions, succession and the commander-in-chief |
| **Feuds**, as the signed difference against the superior |

> Fame is the only attribute that is *also* a political resource, and the only
> one that makes a commander harder to manage. A famous subordinate under a
> less famous superior feuds; see §7.

### 3.6 And what never improves

| Attribute | Can it grow? |
| --- | --- |
| **Experience** | Yes — 0.2/yr of service, capped by national military experience; plus promotion effects |
| **Fame** | Yes — battle results, promotions, scripted events |
| **Initiative** | Only downward, when a wounded commander returns |
| **Leadership** | **No** |
| **Administration** | **No** |
| **Cunning** | **No** |

The one exception is `IncreaseAttributes`, which has exactly one caller:
**military schools in the commander's home state.** It draws one random value
and adds it to administration, leadership, cunning *and* initiative at once,
scaled by `militaryschools ^ exp × lin` for that state.

> **So the only way to improve a general's leadership, administration or
> cunning is to build military schools in the state he came from.** Nothing he
> does in the field touches them, and because the gain bypasses the tracking
> arrays it never shows up in the attribute-breakdown tooltip either — those
> three breakdowns display only the superior-influence line, and the experience
> breakdown is **always empty**.

---

## 4. Rank, and the things you cannot do

Ten army ranks (Volunteer to General) and five navy ones (Master to Flag
Officer). Every shipped commander starts at **rank −1** and is given a rank at
campaign start from his authored promotion dates — so the officer corps begins
as lieutenants and rises on a historical schedule, while officers *in post* are
promoted by the rank window of the command they hold.

| Formation | Minimum rank | Maximum |
| --- | --- | --- |
| Brigade (and every fighting unit) | Colonel | Brigadier General |
| Artillery battalion | Captain | Lieutenant Colonel |
| Division | Colonel → **Brigadier** after Organization Reform | Major General |
| Corps | Brigadier → **Major General** after the reform | General → Lt. General |
| Army | Major General → **Lt. General** after the reform | General |
| Army Group | Lieutenant General | General |
| Fleet | Lieutenant | Flag Officer |

Three rules the interface does not state.

**Rank is never lowered.** Only the "below the minimum" branch writes rank.
Assigning a general to a brigade costs him fame and *gains* him experience,
records a lower ceiling for hysteresis, and leaves his stars alone. Manual
demotion routes through the same function, so **you cannot demote anyone
either.** Appointing a man three or more levels below his rank's minimum
instead gets him **defamed**.

**The Union cannot be promoted past Major General.** A hardcoded line caps the
promotion path at rank 7 for alliance 0, whatever the rank table says —
historically apt, since there was no Union lieutenant general before Grant, but
it is in code rather than data. The Lieutenant General Act is what lifts it.

**Organization Reform moves the whole ladder up one step** for divisions,
corps and armies, which is a real cost: the officers who were qualified before
the reform are under-ranked after it, and the ones who were over-ranked become
correctly placed. It is **project 90** in a stock build
([policy order](policy-order.md) §8.1).

### 4.1 Promotion is substitutable, and time wins on its own

An AI commander is promoted when

```
exp × 0.6  +  fame × 0.525  +  years in grade × 0.5  >  1
```

| Route | What it takes |
| --- | --- |
| Merit alone | full experience **and** full fame — 1.125, immediate |
| Experience alone | impossible: 0.6 at the cap |
| **Time alone** | **two years in grade** |

> **So every AI officer who survives two years in a post is promoted,
> regardless of how he performed.** Experience and fame only accelerate it.
> Separately, `CheckVerticalReplacement` swaps a worse commander for a better
> one once the attribute gap exceeds 0.1 — which is the mechanism that
> actually improves the AI's command over a long campaign.

---

## 5. Fame, defame, and the one-a-battle rule

After every battle the game scores every commander who was on the field:

- his **kill ratio** — casualties inflicted over losses taken, capped at
  **5 : 1**, and counted as **1 : 1** if his men inflicted none at all;
- **plus** the experience he gained in the battle;
- **minus the fraction of his subordinates that routed**.

It then scales the score by his echelon (brigade **0.005**, division 0.0065,
corps 0.009, army **0.013**), by **×1.2** if he was wounded and the score is
positive, by your printing works and, for corps and armies, by the Embedded
Reporters perk (§10): ×1.25 when a formation **above** them holds it, and the
perk's own ×1.25 / 1.5 / 2 when a top-level army holds it itself. **Every
commander's fame moves by that score** — a brigade commander trading 2 : 1
gains about one point of fame, an army commander about 2.6. Then:

- the **single best** scorer becomes **famous**, if he clears the threshold;
- the **single worst** becomes **defamed**, if he falls below it.

The thresholds depend on how the battle ended **for the player** — the same
row is applied to both sides' commanders:

| Battle result for the player | Famous above | Defamed below |
| --- | --- | --- |
| Minor victory | 0.025 | −0.0018 |
| Major victory | **0.020** — the lowest bar | **−0.0027** — the deepest floor |
| Minor defeat | 0.030 | −0.0016 |
| Major defeat | **0.040** — the highest bar | **−0.0015** — the shallowest floor |

> **Both columns run the obvious way.** Fame is easiest to win after a major
> victory and hardest after a major defeat; defame is the reverse. Because the
> echelon factor is applied first, the bar is really a kill ratio that depends
> on the command: an army commander needs about **1.5 : 1** to become famous in
> a major victory and 3.1 : 1 in a major defeat, while a brigade commander
> needs **4 : 1** in a major victory and cannot clear either defeat bar on kill
> ratio alone. And because the row is chosen by the *player's* result, the
> enemy's generals in a battle you won big are judged against the same
> easy-fame, hard-defame bar as yours.
>
> **Corrected.** An earlier version of this section read the thresholds
> backwards — "easier to become famous in a minor victory than a major one,
> easier to be defamed in a major victory than a major defeat". The comparisons
> are `score > famous threshold` and `score < defamed threshold`, so the lower
> famous bar and the deeper defamed floor of a major victory make fame easier
> and defame harder there, not the other way round.

To be defamed at all, the score must be **negative**, which takes both a kill
ratio below 1 : 1 *and* a large share of routed subordinates. A commander whose
men inflicted nothing is scored as an even trade, so it takes repeated routs to
defame him.

What fame and defame are worth:

| State | Effect |
| --- | --- |
| Famous | **× 1.4** on his fame |
| Defamed | **× 0.7** |
| Famous, then defamed | × 0.7 while the defame lasts; the × 1.4 returns when it clears |

**Famous is permanent and once only.** Nothing ever clears it, and a
commander who is already famous cannot be made famous again — and if the best
scorer of a battle is already famous, **nobody** becomes famous from that
battle. Becoming famous clears a past defame.

**Rehabilitation is almost immediate.** A defamed commander rolls for
rehabilitation in the commander tick with a threshold that opens after
`0.4 years` and should saturate at `1.5 years` — but the elapsed-time factor
multiplies the **random draw** instead of the threshold, where every sibling
check in the same method multiplies the threshold. Because the elapsed slice is
a tiny fraction of a year, the draw is almost always under the threshold as
soon as it rises at all. The roll is only made while he is **on duty**, so a
wounded or captured commander stays defamed until he returns.

> **In practice: defame lasts about five months and then clears.** The
> `commanderehabilitationmax` pref — misspelled in the game, with no `r` — is
> very nearly inert.

**And it never comes back.** Rehabilitation, and becoming famous, both clear
the defame by writing a far-future sentinel date rather than zeroing it — and
both the battle defame and the over-rank defame (§4) are gated on that date
being *unset*. So **once a commander's defame has cleared he can never be
defamed again** by a battle or an appointment. Only the scripted first-major-
victory milestones, which defame the losing general unconditionally, still
can.

The famous-commander guard in the same block compares a **commander id**
against a **fame score**, so the check that was meant to stop one man being
both the best and the worst of a battle does nothing.

---

## 6. Losing a commander, and what replacing him costs

### 6.1 The casualty rolls overwrite rather than compete

Per casualty event, with `p = casualties / (strength + casualties)`:

| Context | Rolls, in order |
| --- | --- |
| Firefight | killed, **then** wounded |
| Melee | killed, then wounded, then — if routed — **captured** |

Because the wounded roll runs *after* the killed roll and assigns
unconditionally on success, **a commander is only killed when the killed roll
hits and the wounded roll misses**, and in melee a surrender overrides both.
The effective death rate is therefore well below the nominal one.

Two divergences between fighting a battle and auto-resolving it:

| | Manual battle | Auto-resolve |
| --- | --- | --- |
| Rolls per casualty event | once | **twice** — roughly double the risk |
| Superior commanders at risk | no | **yes**, up to 10 levels up, at `1 / depth³` |

> **Auto-resolving is how your army commanders get killed.** In a battle you
> fight, only the commander of the unit taking casualties is ever rolled for.

### 6.2 Recovery

| State | Returns at | Cost of returning |
| --- | --- | --- |
| Wounded | **0.7 per year** | fame and initiative × a random **0.70 to 0.95** |
| Captured, junior | 0.05 per year | — |
| Captured, senior | **0.40 per year** | — |

> A captured general is exchanged **eight times faster** than a captured
> colonel, which is historically right and mechanically generous. A wounded
> commander comes back in about 17 months on average and **permanently loses 5
> to 30 % of his fame and initiative** — so a twice-wounded general is a
> materially worse officer than he was.

### 6.3 Succession costs readiness at every level

When a commander is lost, the game promotes the best of his **direct
subordinates** into the vacancy, then recurses to fill the hole that leaves.
Every promotion applies `−0.10 readiness` to **every attached fighting unit**.

> **A corps commander killed at the head of three divisions costs you readiness
> three times over** — the corps vacancy, the division that fills it, and the
> brigade that fills *that*. Readiness is what decides which orders you may
> give, so a decapitated corps is slow for reasons the casualty report does not
> mention.

If no subordinate qualifies, the game searches the officer pool through four
widening tiers and, failing all of them, **invents a commander**:

| Dummy commander | Attributes |
| --- | --- |
| American | random in **[0.1, 0.7]** |
| European (alliances 2+) | random in **[0.5, 0.9]** |

with experience set from your national military experience and a name assembled
from the dummy name pools.

> **Basic garrison units can never draw a historical officer** — the candidate
> test excludes them everywhere — so every garrison in your army is commanded
> by an invented man averaging 0.4 in everything.

---

## 7. Feuds: the cost of a bold subordinate

Every formation rolls once a game hour:

```
chance = |initiative(self) − initiative(superior)|
       + (fame(self) − fame(superior))
       − 0.3
```

| Subordinate | Superior | Hourly chance |
| --- | --- | --- |
| init 0.5, fame 0.5 | init 0.5, fame 0.5 | none |
| init 0.9, fame 0.5 | init 0.5, fame 0.5 | **10 %** |
| init 0.2, fame 0.5 | init 0.8, fame 0.5 | **30 %** |
| init 0.9, fame 0.9 | init 0.5, fame 0.4 | **60 %** |

Two readings fall straight out. **The initiative term is an absolute
difference**, so an over-cautious subordinate quarrels exactly as readily as an
over-eager one. And **the fame term is signed**, so a subordinate more famous
than his superior feuds and a less famous one does not.

**These are hourly figures, which makes even the small ones certain.** A 10 %
hourly chance is a feud within a working day; the table's bottom row feuds
within two hours. Any visible gap in the formula means the feud *will* happen —
the only question is when.

While a formation is feuding it is handed to a **third AI controller** that is
neither you nor the enemy, given a random stance, and held for
`5 × duration` hours. Its order delays are multiplied by **3.0**. And it may
charge the nearest enemy within 250 units on its own initiative:

```
p = initiative² × 1.0
  × 2.0   if the commander is political
  × 0.5   if he is neither political nor West Point
```

| Commander | Relative chance of charging off |
| --- | --- |
| **Political** | **×2.0** |
| West Point | ×1.0 |
| Volunteer | ×0.5 |

> **A high-initiative political general is the most insubordinate thing on the
> field**, and the most useful one off it — political commanders are the only
> source of state support from appointments. The resolution is to give them
> commands **under a famous, high-initiative superior**, which collapses both
> terms of the feud formula at once.

> **And this is the one real cost of initiative.** Nothing else in the model
> punishes a bold general; the feud system punishes a bold *subordinate* under
> a cautious chief. Match initiative down the chain rather than maximising it
> at every level.

No commander attribute reaches any order delay directly — the battle order
delay is a flat processing time plus courier geometry plus a readiness term.
**Initiative reaches order delays only by making feuds more likely**, and a
feud triples them.

---

## 8. Perks: three separate systems

There is not one perk list but **three**, and which one a unit draws from is
decided entirely by what kind of unit it is:

| Holder | Slots | List |
| --- | --- | --- |
| Fighting units and low echelons | **1** | 19 battle perks |
| Divisions, Corps, Armies, Army Groups | **4** (3 you can fill) | 18 campaign perks |
| Fleets | **4** (3 you can fill) | 11 fleet perks |

So a brigade will hold **exactly one perk for the whole war** and a corps can
hold several. The perk panel has rows for only three, so the fourth slot is
never offered to you; the campaign AI fills it anyway
([34](../code-findings/34-perks.md) §2.3). How many rows the panel really
enables (the manual says three for armies, two for corps and fleets) is
**Untested**. The lists do not overlap in meaning even where they share a
number — but the code doesn't always check which list it is reading (§10.4).

**Slots unlock in order.** A slot earns experience only if it is the first or
its predecessor is already filled, so a formation must choose its first perk
before the second even begins to progress. An unassigned slot accrues at a
flat rate until it reaches 1.0, at which point you may assign a perk; an
assigned slot only accrues from the activity that perk rewards.

**Perk levels cap at three**, and a level costs a full 1.0 of perk experience.

**Artillery learns at double speed**: every perk-experience gain for an
artillery unit is multiplied by 2.

**Auto-resolve levels everything.** Every auto-resolved pairing grants
experience to all of both units' slots, empty or filled, whatever the perk
rewards — on paper more than half a level per day of fighting, far faster than
any perk's own rule (**Untested**: the real rate depends on how many pairings
a unit sits in). Units raised by the **US Regulars** policy start with a perk
ready to pick ([34](../code-findings/34-perks.md) §2.2).

**Only Corps inherit, and only one level.** An Army's or Army Group's campaign
perk also applies to its **Corps** — a Division does not inherit from its
Corps. Where the game checks both the unit's own perk and its parent's, the
two usually **stack multiplicatively**, which is the strongest thing you can
do with the system: a Corps holding Field Telegraph at level 3 under an Army
holding it at level 3 gets ×0.7 twice, about half the order delay.

> **Two exceptions worth knowing:** the siege bonuses of Siege Train and
> Balloon Corps are **added** into a shared siege strength and averaged, not
> multiplied (§10.1); and the Engineers & Mechanics bonus is written as an
> *assignment*, twice, so the **parent's level overwrites the Corps' own**.
> An earlier version used Siege Train as the example of ×2.0 twice, and called
> Engineers & Mechanics the only exception.

**Merging units** strength-weights the perk experience but keeps the surviving
unit's perk and level. **Detaching a skirmisher or scout** copies the parent's
perk, level and experience; anything else spawns blank. Reattaching is a
merge, so a level the detachment gained on its own is lost.

**The AI picks at random**, from the whole list and never the last entry, so
AI armies never hold River Expedition and AI fleets never hold Port Defender
([34](../code-findings/34-perks.md) §2.3).

> The tooltip claim that *"the amount of available perks is limited for each
> nation, depending on its policies and technologies"* describes a system that
> does not exist. The function that would enforce it returns `true`
> unconditionally and is never called
> ([what the game shows you](ui-vs-simulation.md) §2.1).

---

## 9. The 19 battle perks, by what they are worth

One slot per fighting unit, for the whole campaign. Values are levels I / II /
III; a perk starts at level I the moment it is chosen.

A note on level gates: the code's perk level is **0-based**, so a test written
`GetPerkLevel(n) >= 1` opens at **level II** and `>= 2` at **level III**. An
earlier version of this section read several of them one level low.

### 9.1 Infantry (the only eight an infantry brigade may choose)

| Perk | Effect at I / II / III |
| --- | --- |
| **Sharpshooters** | accuracy **×1.1 / 1.25 / 1.5**, line infantry only (a detached skirmisher company keeps the perk and loses the bonus); range ×1 / 1 / **1.25**; swapped with Zouave in auto-resolve, see below |
| **Ace of Spade** | cover value ×1.1 / 1.2 / **1.5**; morale behind cover ×1 / **2.0** / 2.0; reload behind cover ×1 / 1 / **1.25** |
| **Deadly volley** | casualties inflicted **×1.05 / 1.10 / 1.25**; from **level II**, no "enemy in cover" morale penalty; **level III inverts the drill protection against sudden losses** — see below |
| **Expert skirmishers** | concealment **+10 / +25 / +50**; reload ×1 / 1.05 / **1.1**; **level III** suppresses the low-cohesion morale penalty |
| **Zouave** | able to run; fatigue **×1 / 0.75 / 0.5**; immune at every level to the "men are exhausted" morale term — in manual battles only, see below |
| **Engineering** | required for pontoons; deployment engineering time and obstacle-building speed **×2 / 3 / 4**; pontoon building ×1 / 1.25 / **2**; the only perk that gains experience from construction work |
| Iron discipline | cohesion change **×1.25 / 1.50 / 2.00**, falling as well as recovering; **level III** floors the "men doubt their chances" melee term; **from level II the nearby-rout penalty never decays** — see below |
| Spirited charge | melee bonus ×1.1 / 1.2 / 1.5 — **display only** ([12](../code-findings/12-melee.md) §3); its one live effect is at **level III**, raising the "doubt their chances" melee penalty of an opponent without Spirited charge II+ |

> **Ace of Spade and Sharpshooters are the clean firefight picks.** Ace of
> Spade only pays behind cover, but it pays three ways at once there.
> Sharpshooters pays in proportion to how good the brigade already is (below).
> Deadly volley is good through level II — the cover-morale immunity is a
> real second effect — and then turns against drilled troops.

**Accuracy perks only help troops whose aim is already good.** The game
floors every shot's accuracy at a fixed level before turning it into aim
scatter, and an average rifle-musket brigade sits below that floor
([manual vs code](manual-vs-code.md) §11,
[11](../code-findings/11-combat-resolution.md) §3e). A perk's accuracy factor
is applied last, so it counts only for the part that lifts the shot above the
floor *(calculated, [34](../code-findings/34-perks.md) §2.4)*:

| Brigade | Sharpshooters I | II | III |
| --- | --- | --- | --- |
| Green | nothing | nothing | nothing |
| Average (half experience, 70 % morale) | nothing | nothing | scatter −15 % |
| Veteran | −9 % | −20 % | −33 % |

The same applies to Stormers, Skilled laying and Grand battery; rifled guns
(rated 1.1–1.2) clear the floor more easily than Napoleons (1.0). An earlier
version of this section took all four at face value.

**Deadly volley III makes the sudden-losses hit worse.** The high-momentary-
casualties term is `−0.6`, reduced by drill as
`term −= term × training × 0.7 × f`, and Deadly volley III sets `f` to its
`0.5` pref ("reduction of morale hit … (factor)"). So the perk halves the
**drill's protection**, not the hit: an undrilled brigade is unaffected, while
a fully drilled one goes from −0.18 to **−0.39**, **2.2× the hit** it takes
without the perk. The label describes multiplying the term by 0.5.

**Iron discipline II and III freeze the nearby-rout penalty.** Each friendly
rout within 60 adds −0.075 to a per-hour morale term that normally recovers at
0.3 an hour — gone in about fifteen minutes. From level II the recovery is
multiplied by zero, so **the penalty stays for the rest of the battle**, and
each further rout nearby adds to it (to a floor of −1 per hour), before the
3-to-5× amplification of morale losses
([why units break](why-units-break.md) §2). The intent was presumably to zero
the *impact*, not the recovery.

**Sharpshooters and Zouave swap places in auto-resolve.** All three
auto-resolve sites use an older numbering of the two
([34](../code-findings/34-perks.md) §6). So in auto-resolve **Sharpshooters**
brigades get Zouave's fatigue reduction and its immunity to the exhaustion
morale hit, and **Zouave** brigades get the sharpshooting casualty bonus —
nothing at level I, and a small penalty in a retreat stance. An earlier
version reported only the fatigue half.

**Engineering's speed rubs off.** On an obstacle line, the speed factor is set
by the last Engineering brigade processed and not reset, so perkless brigades
working the same line after it build at its speed too.

> **Spirited charge is close to a trap:** its multiplier is shown in the unit
> panel and never applied, and its only live effect waits for level III.

### 9.2 Cavalry (six)

| Perk | Effect at I / II / III |
| --- | --- |
| **Mounted rifles** | recovers **75 % / 50 % / 20 %** of the mounted-fire penalty — **weakest at level III**, see below |
| Cold steel | from **level II**, ×2.0 on the morale hit enemies take from its charge (not in square); melee ×1.1 / 1.2 / 1.5 is **display only** ([12](../code-findings/12-melee.md) §3); levels in battle only from melee against cavalry |
| **Expert scouts** | concealment **+10 / +25 / +50**; visibility range ×1 / 1.25 / **1.5**; levels only through detached scout companies |
| **Lightning brigade** | cover ×1.1 / 1.2 / **1.5**; morale in cover ×1 / **2.0** / 2.0; reload in cover ×1 / 1 / 1.25 |
| **Stormers** | able to run; loose-formation accuracy ×1 / 1.1 / **1.25**; level III ignores enemy cover morale |
| **Vanguard** | at **every level**, no morale penalty for being outflanked, surrounded, or under flanking or rear fire; from **level II**, no falling-back-under-fire penalty; at **level III**, no retreating-under-fire penalty either. The level I effect the manual promises, no penalty for fighting detached, is missing |

> **Corrected:** Vanguard was first listed with only its level II and III
> gates, and Stormers without running ([34](../code-findings/34-perks.md)
> §3.12–3.13). Vanguard's flank and rear immunity from level I makes it a real
> pick for any brigade that gets flanked.

**Mounted rifles points the wrong way.** Both sites compute
`factor = (1 − base) × pref + base`, so the pref is the share of the penalty
*recovered*, and it ships 0.75 / 0.5 / 0.2 — values labelled as a shrinking
"malus factor". With the shipped bases (accuracy ×0.6, reload time ×1.25):

| Level | Accuracy when mounted | Reload time when mounted |
| --- | --- | --- |
| No perk | ×0.60 | ×1.25 |
| I | **×0.90** | **×1.06** |
| II | ×0.80 | ×1.13 |
| III | ×0.68 | ×1.20 |

> It is always better than no perk, and level I is genuinely strong — but every
> level up takes some of it away. An earlier version of this section called it
> "the largest single number in the battle list" at level III. Only the
> reload column matters in practice: mounted accuracy stays under the
> accuracy floor (§9.1) with or without the perk *(calculated)*.

**Expert scouts' level III morale branch is unreachable**: it tests perk 8 and
then reads perk **9's** level, and a fighting unit has only one slot, so the
condition can never both hold. Its concealment and visibility range work. It
**levels very slowly**: only a detached scout company earns its experience,
by revealing enemy units, and that is averaged back into the regiment by
strength share when it rejoins.

### 9.3 Artillery (five)

| Perk | Effect at I / II / III |
| --- | --- |
| **Skilled laying** | counter-battery accuracy ×1.1 / 1.2 / **1.5**; demoralization under artillery fire ×1 / 0.5 / **0.0 — complete immunity** |
| **Close support** | canister weapon strength **×2.0 at every level**; reload ×1 / 1.1 / 1.2, with any ammunition; melee ×1 / 1.25 / 1.5 is **display only** ([12](../code-findings/12-melee.md) §3) |
| **Grand battery** | bombardment accuracy ×1.1 / 1.2 / 1.3; enemy morale malus ×1 / 1.5 / **2.0** as labelled, but about **×1 / 2.25 / 4** in effect, see below |
| **Flying battery** | unit speed ×1.15 / 1.3 / **1.5**; from **level II**, no falling-back morale penalty; at **level III**, no retreating penalty |
| **Sapper gunners** | cover ×1.1 / 1.2 / **1.5**; morale in cover ×1 / 2.0 / 2.0; reload ×1 / 1 / 1.25 — labelled "in cover", but `Regiment` ~10012 has no cover test |

**Grand battery is stronger than its label.** The incoming-artillery morale
term divides by the average distance of the guns firing at a unit, and Grand
battery adds `malus − 1` to the gun count without adding distance, so the
average shrinks by the same factor. For a lone battery firing from 20 or more
the term grows **×2.25 at level II and ×4 at level III** *(calculated,
[34](../code-findings/34-perks.md) §3.17)*.

> **Close support is the strongest perk in the game at level I.** Its ×2.0 on
> canister weapon strength applies at *every* level, so you get the whole
> effect the moment you choose it — and canister already kills four times the
> share musketry does ([what kills your men](what-kills-your-men.md) §1).
> **Skilled laying III** gives zero demoralization from incoming artillery.
> An earlier version called it "the only perk anywhere that grants an
> immunity"; Zouave, Vanguard, Deadly volley II and Iron discipline III each
> switch off a morale term too.

> **Seven standalone battles ship older perk numbers**: 1st Manassas (Regt.),
> New Market, Wilson's Creek, Stones River, Appomattox, Glorieta Pass and
> Olustee. There Engineering is ×1.25 / 1.5 / 2 instead of ×2 / 3 / 4 and
> Close support levels six times faster; except at Stones River, Skilled
> laying and Grand battery level three times faster and Sapper gunners twice
> as fast. The same fields fought from a campaign use the normal numbers
> ([34](../code-findings/34-perks.md) §1.1; which battle modes load them is
> **Untested**).

---

## 10. The 18 campaign perks, and which ones work

Four slots per formation (three you can fill), inherited one level down to
Corps. They act on the campaign map and in auto-resolved battles. Every one
reaches a config value, but following each value to what it changes
([34](../code-findings/34-perks.md) §4) leaves **seven that work as described
or close, six that half work, and five that do nothing useful or hurt you**.

> **Corrected:** an earlier version of this section said "every one of the
> eighteen has a real effect" and listed each perk by its config label. The
> labels for Siege Train, Sappers and Miners, Partisan Brigades, Limelights,
> Engineers & Mechanics, Ambulance Corps, Foot Cavalry, Pontoon train,
> Embedded reporters and the siege halves of River Expedition and Land
> Torpedoes do not describe what the code does.

**Work as described, or close**

| Perk | Effect at I / II / III |
| --- | --- |
| **Field Telegraph** | **order delays ×0.9 / 0.8 / 0.7**; information delay ×1.0 / 0.75 / **0.5** — but that half reaches nothing (§10.3) |
| **Expert Scouts** | intelligence gathering ×1.25 / 1.50 / **2.00**, while stationary |
| **Bureau of Military Information** | intelligence gathering ×1.1 / 1.25 / 1.5, while stationary; information delay ÷1.0 / ÷0.75 / ÷0.5 — inert (§10.3) |
| **Skilled cartographers** | movement ×1.05 / 1.10 / 1.20; intelligence gathering ×1.05 / 1.10 / 1.20, while stationary |
| **Balloon Corps** | intelligence while stationary ×1.25 / 1.25 / 1.50; siege +2.5 / 5 / 7.5 % (§10.1); **order delays ×1.25 / 1.10 / 1.10, slower** — its designed cost (§10.5) |
| **Flying Column** | movement ×1.1 / 1.1 / 1.25; supply consumption ÷1.5 / ÷2.0 / **÷2.5**, so it lasts that much longer when cut off (it carries no more) |
| **Siege Train** | siege weapon strength **+12.5 / 25 / 50 %** for a lone holder, not ×1.25 / 1.50 / 2.00 (§10.1) |

**Half work**

| Perk | What works | What doesn't |
| --- | --- | --- |
| **River Expedition** | movement on creeks, rivers and crossings ×1.25 / 1.50 / **2.00** | siege assault ×1.1 / 1.25 / 1.5 is overwritten; the AI never picks it |
| **Foot Cavalry** | speed ×1.1 / 1.2 / 1.3 on **every** campaign move, not just forced marches | forced-march attrition ×0.9 / 0.8 / 0.7 is read from the brigades' battle perk instead (§10.4) |
| **Ambulance Corps** | speed penalty from disabled soldiers ×0.75 / 0.50 / 0.25 | wounded-to-killed rate ×0.9 / 0.75 / 0.5 goes to cavalry brigades with Expert scouts instead (§10.4) |
| **Land Torpedoes** | enemy movement in command range ×0.9 / 0.8 / **0.7** | skirmish ×1.05 / 1.10 / 1.20 is overwritten |
| Embedded reporters | fame from battles ×1.25 for the commanders of corps and armies **below** the holder, at every level; a top-level holder's own commander ×1.25 / 1.5 / 2 | a holder under another formation gets nothing, and the formations below it never get more than ×1.25; it magnifies fame lost in a bad battle too |
| Pontoon train | route planning counts creeks ×0.75 / 0.5 / 0.25 as costly, so units detour less — they don't cross faster | "supply speed" ×1.1 / 1.2 / 1.3 runs the whole supply clock fast, so the formation uses supplies and pays upkeep faster |

**Do nothing useful, or hurt you**

| Perk | Label | What it does |
| --- | --- | --- |
| Partisan Brigades | raiding casualties ×1.25 / 1.50 / 2.00; raiding morale hit ×1.10 / 1.25 / 1.50 | **raises the casualties and morale hit your own raiders take** (§10.2) |
| Bushwhackers | skirmishing ×1.25 / 1.20 / 1.10; support drop from raiding ×1.1 / 1.25 / 1.50 | **raises your own raiders' losses, and amplifies the support backlash** of raiding (§10.2) |
| Sappers and Miners | siege assault ×1.25 / 1.50 / 2.00 | overwritten; by mistake it gets River Expedition's embarked bonus instead (§10.4) |
| Limelights | night combat (sieges and skirmishing) ×1.25 / 1.50 / 2.00 | works only in auto-resolved field battles at night, the one case it doesn't list |
| Engineers & Mechanics | construction speed ×1.25 / 1.50 / 2.00 | the building reports itself finished early (a fort gets its garrison early), then goes back under construction and finishes at the normal rate — **Untested** |

### 10.1 Siege Train is half as strong as it looks

A siege's weapon strength starts from a baseline of **2**, and Siege Train
**adds** `factor − 1` to it rather than multiplying, so a lone level III
holder takes it from 2 to 3: **+50 %, not ×2**. With several formations on a
side the additions are divided by their number, so the bonus is averaged
across them, and an army's Siege Train adds to its corps' own rather than
compounding. Balloon Corps' siege factor goes through the same sum, which
makes its 1.05 / 1.10 / 1.15 worth **+2.5 / 5 / 7.5 %** *(calculated,
[34](../code-findings/34-perks.md) §4.1, §4.3)*.

### 10.2 The raiding perks punish the raider

When a formation raids, the game runs two blocks: one damages the raided enemy
and reads **no perks**, and one inflicts return casualties and a morale hit on
one of the **raider's own** brigades. Partisan Brigades' and Bushwhackers'
multipliers both sit in the second block
([34](../code-findings/34-perks.md) §4.13–4.14).

Bushwhackers' other half scales the state-support change a raid causes. In
the shipped data that change is a small **loss for the raider's side** in the
raided state and a smaller **gain for the owner** — so the perk makes the
backlash bigger and does nothing to the enemy's support.

> **Keep both off any formation you raid with.** An earlier version of this
> section called Bushwhackers "a political raiding perk" and the strongest
> support lever in the game, and read its declining skirmishing array
> (×1.25 / 1.20 / 1.10, which the manual also authors) as the defect. Because
> that factor lands on your own losses, the decline actually shrinks a
> penalty.

### 10.3 The two information-delay perks act on a saturated number

Field Telegraph and the **Bureau of Military Information** both act on the same
figure — the chance that an enemy formation leaves your side a *last-seen
mark* — and they push it in opposite directions, the telegraph concealing its
own formation and the Bureau exposing it. Neither does anything in the shipped
build.

The figure they multiply is about **a thousand times** larger than intended,
because the formula that builds it reads a per-unit-type concealment array
(shipping −50 and −60) where it wants two spotting-interval values (0.1 and
0.003 game years), and the negative result trips a floor. It therefore sits at
its 90 % ceiling after roughly **eight game hours** whichever way a perk pushes
it.

> **So Field Telegraph is an order-delay perk and nothing else**, and the
> Bureau of Military Information is an intelligence-gathering perk whose
> tracking half is inert. Take Field
> Telegraph for the ×0.7 on order delays, which is real and large
> ([fighting the AI](fighting-the-ai.md) §4.4 for the full arithmetic).
>
> Source: [29](../code-findings/29-dead-prefs-audit.md) §4.1.

### 10.4 Perks that read the wrong list

Perk numbers are shared across the three lists, and in several places the game
tests the number without checking which list the holder draws from
([34](../code-findings/34-perks.md) §7). Two campaign perks lose their effect
to a battle perk, and several perks pick up one they were never given:

| If you hold | You also get |
| --- | --- |
| Expert scouts (cavalry brigade) | Ambulance Corps' cut in wounded who die, on the campaign map — the Ambulance Corps formation doesn't get it |
| Ace of Spade (infantry brigade) | Foot Cavalry's cut in forced-march sickness — the Foot Cavalry formation doesn't get it (**Untested**: it may never fire) |
| Ambulance Corps (formation) | Expert scouts' concealment and sight range |
| Bureau of Military Information (formation) | Expert skirmishers' concealment |
| Blockade Busters (fleet) | Expert scouts' concealment and sight range |
| Balloon Barge (fleet) | Expert skirmishers' concealment |
| Sea Raiders (fleet) | Foot Cavalry's speed, ×1.1 / 1.2 / 1.3 |
| Sappers and Miners (formation) | River Expedition's embarked strength: half rather than a quarter when caught on creeks, rivers, crossings or at sea in auto-resolve |
| Balloon Corps or Land Torpedoes (formation) | its headquarters can run in battle |

### 10.5 Balloon Corps makes your orders slower, by design

Its order-delay factor is a multiplier on the delay, and it ships **above 1.0
at every level**:

| Perk | Level I | Level II | Level III |
| --- | --- | --- | --- |
| Field Telegraph | ×0.9 | ×0.8 | ×0.7 |
| **Balloon Corps** | **×1.25** | **×1.10** | **×1.10** |

Balloon Corps **lengthens order delays by 25 % at level I and 10 %
thereafter**. The manual lists exactly that as the perk's cost, so it is
working as designed; an earlier version of this section called it inverted.

> **Take Balloon Corps for intelligence, not orders.** If you want faster
> orders, Field Telegraph is the perk, and at level III it is a genuine 30 %
> improvement — about half the delay for a Corps that holds it under an Army
> that holds it too.

---

## 11. The 11 fleet perks

| Perk | Effect at I / II / III |
| --- | --- |
| **Battery Runner** | fighting efficiency against forts **×1.25 / 1.50 / 2.00**; not slowed by the forts it is bombarding; levels only if the fleet has an ironclad |
| **Port Defender** | fighting efficiency with a friendly harbour in range **×1.25 / 1.50 / 2.00**; the AI never picks it |
| **Torpedoes** | ramming efficiency **×1.25 / 1.50 / 2.00** |
| **Limelights** | night combat, against ships and forts, **×1.25 / 1.50 / 2.00** |
| **Blockading Squadron** | blockade efficiency ×1.1 / 1.25 / **1.50** |
| **Supply Colliers** | provisions consumption ×0.85 / 0.70 / **0.50** (not coal or ammunition) — but fleet speed **×0.7 / 0.8 / 0.9** |
| **Balloon Barge** | intelligence ×1.25 / 1.35 / 1.50; siege ×1.0 / 1.0 / 1.05 |
| **Mortar Boats** | siege bombardment ×1.05 / 1.10 / 1.25 |
| **Blockade Busters** | fighting efficiency against blockaders ×1.1 / 1.2 / 1.3; enemy blockade of **your own** ports within its radius ×0.9 / 0.8 / 0.7, but only for the blockaders counted before it; **choosing it immediately makes the commander famous**; levels while it blockades, not while it fights blockaders |
| **Amphibious Attack** | **required for any fort capture** — without it the capture roll never runs; probability ×1.00 / 1.10 / **1.50** |
| Sea Raiders | lets the fleet blockade **trade nodes**, neutral ones included; prices there ×1.1 / 1.25 / 1.5 **on the map tooltip only**; **at level III the commander becomes famous**; also Foot Cavalry's speed (§10.4) |

> **Corrected:** Balloon Barge was first listed with a level II morale effect.
> That test (`Regiment` ~18352) is battle perk 3, Expert skirmishers, in the
> fighting-unit morale code, which a fleet never runs. Blockade Busters was
> first listed without its own-port effect, which is live in `IIP` (~673).
> Sea Raiders' price factor was later also called live; it is stored in
> `IIP.priceadjustment`, whose only reader is the map tooltip, so no price
> changes ([34](../code-findings/34-perks.md) §5.7).

Four things to take from the list.

**Supply Colliers carries a standing penalty**, as Balloon Corps does on land.
At level I it costs you **30 % of your fleet's speed** for a 15 % saving on
provisions; you have to reach level III before the speed cost falls to 10 %
and the saving reaches half. Take it on a blockading squadron that does not
need to move, never on a raider. An earlier version called it "the only perk
in the game with a standing penalty".

**Any fleet meant to take forts needs Amphibious Attack.** The fort-capture
roll is gated on it outright.

**Two perks make their commander famous**: Blockade Busters the moment you
select it, and Sea Raiders on reaching level III (`IIP` ~650). Fame is ×1.4
wherever the game reads a commander's fame through its fame getter — not in
the state support a political general earns, which reads raw fame
([19](../code-findings/19-commanders-and-perks.md) §9) — so this is a free
attribute boost attached to a modest combat multiplier.

**Battery Runner and Port Defender are the two ×2.00s**, and they are exact
mirrors: one for attacking forts, one for defending your own harbours. Decide
which war your navy is fighting and take the matching one.

> **A cross-namespace leak that doesn't bite:** the test that lets Sea Raiders
> hit neutral trade nodes is written `HasPerk(6)` with no check that the holder
> is a fleet. Campaign perk 6 is **Foot Cavalry**, so an army formation holding
> it satisfies the same test. Trade nodes sit at sea, so no land formation can
> reach one in the shipped map. An earlier version called this the one place
> the namespaces are ambiguous at runtime; §10.4 lists the ones that do bite.

---

## 12. What is decoration

Five things the game displays prominently about a commander and never reads.

| Shown | Reality |
| --- | --- |
| **Veteran** | **No mechanical consumer at all.** It is displayed, filtered on in the commanders list, and read by the DLC's item requirements. 456 of the 1,906 shipped commanders carry it |
| **Engineer branch** | **No effect anywhere.** The branch description promises better siege and obstacle work; the only code that reads branch 3 is the avatar panel, a list filter and the DLC. 61 commanders carry it |
| The president | A name, a title, an inauguration line and a portrait. Elections move national morale but do not change who holds the office, and nothing mechanical reads him |
| The experience breakdown | **Always empty** — its tracking array has no producer anywhere |
| The leadership, administration and cunning breakdowns | Show **only** the superior-influence line, because the military-school gains that are the sole source of growth bypass the trackers |

**West Point and `political`, by contrast, both do real work:**

| Flag | Effect |
| --- | --- |
| **West Point** | Branch bonuses to accuracy, rate of fire and morale recovery — **in manually fought battles only**, the auto-calculator drops the West Point requirement; **half the experience cost** of being over-promoted; preferred for appointments except artillery, where the game deliberately prefers non-West-Pointers |
| **Political** | Generates state support while holding a command, scaled by fame (×1.0 to ×3.5) and echelon (×1.0 to ×3.0); **doubles the feud chance**; qualifies for a relaxed appointment tier |

The political-commander support formula has two quirks. **An Army Group
commander earns less than an Army commander** — the echelon multiplier's
x-axis stops at four entries and Army Group falls off the end onto the Fleet
value of 2.0, below Army's 3.0. And **the last commander in the list never
contributes at all**, because the commit is gated on reaching the end of the
round-robin and then zeroes the accumulator.

> So the ranking of a political appointment is: **a famous general commanding
> an Army** (×3.5 × 3.0), then a Corps (×2.0), then an Army Group (×2.0 — the
> same as a fleet), then a Division. Fame matters more than echelon: an
> unknown Army commander earns ×3.0 where a famous divisional one earns ×5.25.

---

## 13. Twelve things to do differently

1. **Read experience before attributes.** A 60/full-experience general beats
   an 80/green one. The portrait number is a ceiling.
2. **Choose your commander-in-chief for initiative and leadership**, not fame.
   His attributes reach every formation outside its parent's radius, and his
   initiative caps every unit's readiness consumption.
3. **Match initiative down the chain.** The feud formula punishes any gap in
   either direction, hourly, and a feud triples the formation's order delays
   and hands it to an AI that may charge the nearest enemy.
4. **Put political generals under famous, bold superiors.** That zeroes both
   feud terms and keeps the state support they generate.
5. **Build military schools in the states your best generals come from.** It
   is the only way leadership, administration or cunning will ever improve.
6. **Give leadership to the corps you expect to be driven back** — it is the
   only attribute that reduces casualties on a retreat, and the difference
   across its range is 6 % against 42 % extra losses.
7. **Give cunning to cavalry and to anything detached.** ×2.0 on intelligence
   and 0.8 on cavalry casualties are the two biggest attribute coefficients in
   the game.
8. **Fight your important battles manually.** Auto-resolve doubles commander
   casualty rolls and is the only path that can kill your army commanders.
9. **Avoid chains of succession.** Every promotion in the cascade costs every
   attached unit 10 points of readiness, and readiness decides which orders you
   may give.
10. **Take Close support on artillery and Sharpshooters (on experienced
    brigades) or Ace of Spade on infantry.** Close support delivers its whole
    ×2.0 canister bonus at level I.
    Deadly volley is sound through level II, and then its level III morale
    effect turns against drilled troops.
11. **Never take Spirited charge or Iron discipline** — the first's multiplier
    is displayed and never applied, the second freezes the nearby-rout penalty
    from level II — and never take **Balloon Corps** expecting faster orders,
    which it makes slower. On formations, **never take Partisan Brigades or
    Bushwhackers** (they raise your own raiders' losses), and don't spend a
    slot on Sappers and Miners, Limelights or Engineers & Mechanics.
12. **Take Field Telegraph as a formation's first perk** if you intend to
    manoeuvre. Slots unlock in order, so the first choice is the one that
    compounds, and 30 % off order delays at level III is the largest
    quality-of-life effect in the campaign list — and one of the few campaign
    perks that does everything it says.

---

## 14. Still open

1. **Whether all 18 campaign perks and all 11 fleet perks are actually
   offered.** The availability lists are authored in the Unity scene rather
   than config, so which perks the panel presents could not be read from the
   assembly ([19](../code-findings/19-commanders-and-perks.md) §1.6). Nor
   could how many perk rows the panel enables (§8).
   **Untested** — a run through the perk panel on a live build would settle it.
2. ~~**What the AI's perk choices are.**~~ **Answered:** the campaign AI picks
   uniformly at random from the whole list except its last entry, for every
   formation and fleet, and can fill the fourth slot; the battle AI picks at
   random from the unit's branch list while it holds none
   ([34](../code-findings/34-perks.md) §2.3).
3. **Whether `Veteran` was meant to do something.** 456 commanders carry it and
   nothing branches on it.
4. **Whether the Statistics panel can clobber the command ranking.** There are
   two incompatible producers of `campaignranking` — a packed sort key where
   higher is better, and a 1-based ordinal where lower is better — and only a
   runtime check can establish whether the second ever runs in a campaign
   scene. If it does, the commander-in-chief election would briefly pick the
   worst man in a rank bucket ([19](../code-findings/19-commanders-and-perks.md)
   open question 4).
5. **The Army Group promotion path.** Army Groups bypass the normal
   appointment search entirely and have their own promotion routine, which was
   not followed.
6. **Whether historical commanders can be famous at all.** The shipped data
   gives 87 commanders a historical fame date and 76 a defame date, and a new
   campaign pushes those dates to the same far-future marker that makes
   rehabilitation permanent (§5) — locking each one it touches out of that
   state for the whole campaign. Whether it touches only the dates after the
   scenario start, or all of them, depends on load order.
   **Untested** — a new 1861 campaign in which Sherman (historically famous
   from September 1864) wins a major battle would settle it
   ([19](../code-findings/19-commanders-and-perks.md) §4.2).
7. **The three inverted battle perks in play.** Mounted rifles, Deadly volley
   III and Iron discipline II (§9) are derived from the code and the shipped
   prefs, not watched. **Untested** — the cleanest checks are a mounted
   brigade's accuracy readout at levels I and III, and an Iron discipline II
   brigade's morale breakdown for an hour after a neighbour routs.
8. **Engineers & Mechanics and the auto-resolve experience rate in play**
   (§8, §10). Both are derived; watching one fort built by a holder, and one
   brigade's perk experience across a day of auto-resolved fighting, would
   settle them ([34](../code-findings/34-perks.md) open questions).
