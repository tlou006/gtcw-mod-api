# Fighting the AI

What the difficulty sliders actually change, what the AI knows that you do
not, when it will attack you and when it will not, and the dozen places where
its decision-making is exploitable. Two separate engines are involved — a
36-job campaign scheduler and a three-controller battle AI — and they have
opposite failure modes.

Sources: [20-ai-campaign](../code-findings/20-ai-campaign.md) and
[21-ai-battle](../code-findings/21-ai-battle.md), with the commander side in
[commanders worth having](commanders-worth-having.md) §3.2.

---

## 1. The short version

- **There are two sliders, not one.** *Difficulty* buys the AI a handicap
  package; *Aggressiveness* changes how it plays. They are independent, and
  only one of them makes the AI dangerous.
- **The biggest handicap is casualties.** At the hardest setting every AI unit
  takes **20 % fewer casualties from everything** — a single division in
  `SufferCasualties` that applies to combat, attrition and raids alike.
- **Concealment does not work on the AI.** One clause in the shared detection
  routine admits player units to an AI unit's enemy list whether or not they
  are in fog, in battle as well as on the campaign map.
- **The AI plans against where you are going, not where you are** — and it
  reads your destinations with the order-transmission delay explicitly
  bypassed, a delay that applies to you.
- **Fast-forwarding helps you.** The campaign AI gets `√(game speed)` thinking
  passes, so at 16× it does a quarter of the work per game-day; in battle the
  unit-level AI is frame-gated, so speeding up starves it while leaving the
  high-level decisions untouched.
- **The AI launches no offensive operations at all in chapter 0**, and one
  fifth as many in winter — the probability modifier is **×0.2**.
- **The Confederate AI attacks at half the Union AI's rate**, by a shipped
  per-side constant.
- **An aggressive general makes the AI attack at a numerical disadvantage**,
  and divides every battle threshold by up to 1.7.
- **Giving one of your own formations a stance hands it to the AI**, which will
  then fight it for you — including charging with it.
- Two of the ten hardcoded cheats are **discarded reads**, and one has an
  **inverted guard** that makes it apply only in AI-vs-AI games.

---

## 2. Two sliders, and what each one buys

The campaign setup screen offers **Difficulty** and **Aggressiveness**. They
feed two unrelated variables, both set once and saved with the campaign.

| Slider | Internal range | Labels, in bands |
| --- | --- | --- |
| **Difficulty** | 0 – 0.5 | Very Easy / Easy / Mediocre / Hard / Very Hard, one per 0.1 |
| **Aggressiveness** | 0.5 – 1.75 | Calm / Low / Mediocre / Elevated / Very High, one per 0.25 |

### 2.1 Difficulty is a handicap package

Everything it buys is a multiplier on the AI's side of some calculation,
scaled by the slider value `b`:

| What | Formula | At Very Hard (b = 0.5) |
| --- | --- | --- |
| **Casualties** | AI damage ÷ `(1 + 0.5 b)` | **−20 % casualties from everything** |
| **National morale from lost battles** | × `(1 − b)`, floored at 0.1 | **half the morale loss** |
| State support lost to its own casualties | ÷ `(1 + 0.3 b)` | −13 % |
| Battle morale recovery | × `(1 + 0.3 b)` | +15 % |
| Unit experience from battles | × `(1 + 0.3 b)` | +15 % |
| National military experience | × `(1 + 0.3 b)` | +15 % |
| Readiness recovery | × `(1 + 0.3 b)` | +15 % |
| Weapon production | × `(1 + 0.3 b)` | +15 % |
| **All AI spending** | × `(1 + 0.3 b)` | +15 % of every subsidy |

> **The casualty division is the one that matters**, because it is applied in
> `SufferCasualties` — the single entry point every loss in the game passes
> through. It is not a combat modifier: it covers disease, desertion, raiding
> and battle alike. At Very Hard an AI brigade is simply 20 % more durable
> than yours in every circumstance.
>
> The morale discount is the second-largest, and it only applies **in battles
> you fight manually** — the auto-calculator omits it, so auto-resolving is
> the one way to make the AI feel a defeat at full price
> ([how you lose](how-you-lose.md) §10).

### 2.2 Aggressiveness changes behaviour, not strength

| What | Effect |
| --- | --- |
| Offensive-operation probability | multiplied directly by the slider |
| **Recruiting target** | `1.25 + 0.17 × slider` — a strength ratio of 1.33 at Calm, **1.55 at Very High** |
| The economy's debt burden | **divided** by the slider, so an aggressive AI also carries its debt more easily ([the war economy](the-war-economy.md)) |

> So Aggressiveness at maximum asks the AI to out-recruit you by **three to
> two** and then attack with what it raises. **It is the slider that makes the
> campaign hard to win; Difficulty is the slider that makes battles hard to
> win.** They are worth setting independently.

---

## 3. The ten cheats, which are not in config

Separately from the sliders, ten constants live in the assembly with literal
defaults. All of them are disabled in AI-vs-AI games, which is how you can
tell they are meant as handicaps rather than behaviour.

| Cheat | Value | Effect |
| --- | --- | --- |
| No order delay in contact | on | **AI units in contact with the enemy get zero order delay** in battle |
| No morale loss while regrouping | on | zeroes the movement morale penalty for a moving AI unit |
| Slower morale loss falling back | ×0.7 | retreating-under-fire morale loss |
| Fallback morale multiplier | ×0.5 | the **whole** retreat morale term, applied *after* the 0.7 |
| Faster fallback speed | ×1.75 | withdrawal movement |
| Fatigue factor | ×0.55 | **march and terrain** fatigue only — not melee |
| Shifted bonus morale | ×1.65 | base morale recovery |
| Shifted morale-loss trigger | ×1.45 | the unbearable-losses threshold — **but see below** |
| Weapon purchases | ×2.0 | **dead** — the read is discarded |
| Lower momentary casualties | ×0.8 | **dead** — the read is discarded |

Three things follow.

**A retreating AI unit takes 35 % of the morale loss you would.** The 0.7 and
the 0.5 compound, and the zero-order-delay cheat means it also disengages
instantly. **Withdrawing is the thing the AI does best**, and trying to break
a retreating AI formation by pursuit is the least profitable fight on the
field.

**Two of the ten do nothing**, including the AI's weapon-purchase doubling —
so the AI buys weapons at your rate.

**One has an inverted guard.** The two shifted-morale cheats are structurally
identical code five thousand lines apart; one is disabled in AI-vs-AI like
every sibling, and the other is disabled **unless** the game is AI-vs-AI. So
the unbearable-losses relief applies **only in AI-vs-AI games and never
against a human** — you are fighting an AI with eight live cheats, not nine.

---

## 4. What the AI knows that you do not

Three structural advantages, none of them a tuning multiplier and none
affected by the difficulty slider.

### 4.1 It sees every formation on the map

The campaign AI is fed **every unit in the game** and filters on two things:
whether the object is active, and an echelon/rendering classification that
decides whether a formation is drawn on the campaign map at all. Concealment
is not consulted. **It enumerates your entire order of battle, always.**

### 4.2 It reads your destinations before your orders arrive

Every position the campaign AI evaluates — for threat, for stance, for
interception — is read as the **final corner of the unit's last ordered
path**, with the order-transmission delay explicitly bypassed.

> So the AI plans against where your formations are **going**, and it knows
> that before the orders have finished transmitting. The order delay you pay —
> readiness-scaled, courier-distance-scaled, tripled during a feud — is a cost
> the AI's planning does not share. This is its largest single advantage and
> there is no slider for it.
>
> The practical consequence: **the AI's defensive interception is pre-emptive
> by construction.** It only reacts to an enemy whose *destination* lies in a
> state it owns, which means a march toward one of its states triggers a
> response before you arrive, and a march that merely passes nearby does not.

### 4.3 Concealment works against you and not against the AI

The detection routine that fills every unit's enemies-in-range list contains
one clause:

> a candidate is admitted if it is **not in fog, or it belongs to the player's
> alliance, or fog is off**.

Read it from each side. A player unit scanning finds an AI unit, and fog hides
it. An AI unit scanning finds a *player* unit, and the second clause fires, so
it is admitted **whether or not it is concealed**.

> **Your concealment hides you from nobody; the AI's hides it from you.** One
> clause, in the shared detection routine, applying in battle and on the
> campaign map. Cunning, Expert Scouts and terrain still set the flag on *AI*
> units, so they still work in the direction that limits your own detection —
> they simply have no effect on what the enemy's units notice. Do not plan a
> flank march on the assumption that it is unobserved.

### 4.4 Your intelligence rating does not buy you better tracking

The one thing that does work in your favour on the campaign map is the
**last-seen mark** — the ghost marker a hidden enemy formation leaves behind.
The config says how often you should get one, and it says it depends on your
intelligence in that place: *"Time factor for enemy spottings, based on
intelligence"*, **0.1 game years** at intelligence 0 falling to **0.003** at
intelligence 1. Those are about **33 days** and **1 day** between marks.

Neither number is read. The formula builds its interval from a different array
in a different file — the per-unit-type **concealment** factors, which ship as
**−50** and **−60** — and because the result is negative it trips a floor of
0.001, inflating the chance of a mark by roughly a thousand times.

| | What the config describes | What the game does |
| --- | --- | --- |
| Intelligence 0 | a mark about every **33 days** | a mark as soon as it can |
| Intelligence 1 | a mark about every **day** | a mark as soon as it can |
| Time to the 90 % ceiling | 33 days / 1 day | **about 8 game hours** |

> **Intelligence does not make you track enemy formations more often.** Nor do
> the two perks built to shift it — Field Telegraph and the Bureau of Military
> Information push the same number in opposite directions
> ([commanders worth having](commanders-worth-having.md) §10), and at a
> thousand times its intended size the ceiling swallows both.
>
> What *is* live is distance: a formation has to have moved **40 units** from
> its last mark before a new one can appear. And intelligence still decides
> what the mark *says* — showing the unit's type needs intelligence of 0.6 or
> better.
>
> So the practical reading is the generous one: **you get a trail on anything
> that moves, almost regardless of your intelligence investment.** Recruit
> Agents, cavalry screens and the intelligence perks are worth having for
> readiness and for the enemy figures you are shown
> ([ui-vs-simulation](ui-vs-simulation.md) §1.7) — not for the frequency of
> last-seen marks.
>
> Source: [29](../code-findings/29-dead-prefs-audit.md) §4.1.

---

## 5. Fast-forwarding starves the AI

Both engines degrade when you speed the game up, in different ways.

| | Cadence | Effect of speeding up |
| --- | --- | --- |
| **Campaign AI** | `floor(√(game speed))` job passes per frame | at 16× it gets **4** passes, not 16 — **a quarter of the thinking per game-day** |
| **Battle macro AI** | every 0.015 game hours | unchanged in game time |
| **Battle micro AI** | one task per frame, 30 tasks per formation | **game-time cadence falls linearly** with speed |

The campaign figure is the consequential one. The AI advances **one job of
thirty-six per faction per pass**, so a full cycle — separating units,
recalculating zone values, checking stances, considering offensive and
defensive operations, recruiting, replacing commanders — takes 36 passes. At
1× that is 36 frames; at 50× it is still 36 passes but they cover fifty times
as much game time.

> **A fast-forwarded campaign is fought against a much slower opponent.** The
> AI's offensive-operation roll is scaled by elapsed time, so it does not get
> *fewer* attacks — but every piece of its situational awareness, from zone
> values to stance to commander replacement, refreshes far less often per
> game-week. Running at 50× to pass a quiet winter is free; running at 50×
> through a campaign is a difficulty reduction nothing in the interface
> mentions.

In battle the asymmetry inverts: the **macro** decisions (attack, defend,
retreat, flank) keep their game-time cadence while the **micro** behaviours —
halting, facing, charging, rallying, picking up guns, unlimbering — fall
behind.

> **So fast-forwarding a battle produces an enemy that still manoeuvres
> sensibly at the army level and handles its individual brigades badly.** At
> 20× a formation completes one full 30-task pass every twelve game minutes
> instead of every thirty-six game seconds.

---

## 6. When the campaign AI attacks

An offensive operation needs a target zone, an assembled force, and an
aggression roll. All three are exploitable.

### 6.1 The force it thinks it needs

```
required = enemy strength in the target zone
         × its average troop morale there
         × a weapon-quality factor
         ÷ (own national morale ÷ your national morale)
         × 0.8 to 1.0, by its commander's initiative
```

| Commander initiative | Force ratio it will attack at |
| --- | --- |
| 1.0 (bold) | **0.8** — a numerical disadvantage |
| 0.5 | 0.9 |
| 0.0 (timid) | 1.0 |

> **A bold AI general attacks when outnumbered four to five.** That is
> deliberate, and it is the single most aggressive setting in the file. It also
> means **your own national morale is a defensive asset**: the divisor is the
> ratio of the two national morales, so letting your morale slip makes the AI
> perceive less threat and attack more readily
> ([how you lose](how-you-lose.md) §9.4).

### 6.2 The aggression roll

```
threshold = aggressiveness[chapter] × elapsed × 10
          × per-side factor × difficulty slider × winter modifier
          + clamp((years since this force last moved)³, 0, 1)
```

| Chapter | Aggressiveness |
| --- | --- |
| **0 (pre-war)** | **0.00** |
| 1 | 0.75 |
| 2 | 1.50 |
| 3 | 2.00 |
| 4 | 2.50 |

| Modifier | Value |
| --- | --- |
| Union | **1.0** |
| Confederacy | **0.5** |
| Winter | **×0.2** |

Four readings, all actionable.

**The AI launches no offensive operations in chapter 0.** The phoney war is
enforced by a zero in the data, not by code. Nothing you do before Militia
Act I passes can provoke an attack.

**The Confederate AI attacks at half the Union AI's rate.** Playing the Union
against the Confederate AI is a materially quieter campaign than the reverse,
at identical slider settings.

**Winter is a five-fold reduction, not a twenty-fold one.** Offensive and
defensive operations are **both** multiplied by **0.2** between the
winter-quarters months, because the offensive path reads the defensive
constant. The game ships a dedicated ×0.05 offensive winter modifier and
**never reads it**
([29](../code-findings/29-dead-prefs-audit.md) §4.2). **Winter is still when
you reposition** — just not as safely as the config implies.

**And the cubic term is the anti-stall mechanism.** A force that has not moved
accumulates `t³` on its threshold, reaching certainty after about a year.
> **So an AI army that "should" not attack eventually will, purely because it
> has sat still.** If you have pinned an AI force by posture rather than by
> defeating it, expect it to come at you within a year whatever the arithmetic
> says — and note that the clock resets when it moves, so forcing it to
> reposition buys you another year.

### 6.3 One scripted exception

In the main campaign, from **early July 1861** until objective 6 ("Invade the
Confederacy") is accomplished, the Union AI's aggression roll is **bypassed
entirely**. The date and the scenario id are both literals.

> **The Union is forced to march on Manassas.** A Confederate player can rely
> on an attack in the summer of 1861 regardless of the balance of forces or
> the difficulty slider, and a Union player under autopilot will make it.

---

## 7. When it defends, and when it panics

### 7.1 Threat priority has a capital reflex

```
priority = min( distance(enemy force, its nearest enemy),
                distance(enemy force, our capital) / 3 )
```

sorted ascending. **Dividing the capital distance by three makes a force
heading for the capital look three times closer than it is** — one line that
gives the AI a capital-defence instinct without a dedicated subsystem (there
is a dedicated capital-guard job as well).

| Pref | Value |
| --- | --- |
| Response range, responder has enemies in range | 275 |
| Response range, no enemies in range | **650** |
| Probability per year, Union | 75 |
| Probability per year, Confederacy | 60 |
| Winter modifier | ×0.2 |
| Cancels below morale | 0.40 |
| Cancels below strength ratio | 0.8 of the force's initial strength |

> At 75 per year a Union defensive operation is considered about **every five
> days**; the Confederacy about every six. A force with no enemy in range will
> respond from **650 units away** — more than twice the contact-range figure —
> so a deep raid pulls formations from much further than it looks.
>
> **And defensive operations cancel themselves** below 40 % average morale or
> once the responding force has lost a fifth of its strength. Bleeding an
> interception is as effective as beating it.

### 7.2 Reloading makes the AI more stubborn

The cancellation baseline — the strength the operation started with — is
saved and then **never restored**, because the restore is guarded by a
condition that compares a count against itself and is always false. After a
load the baseline is re-accumulated from the force's *current* strength.

> **So saving and reloading mid-campaign resets every AI defensive operation's
> give-up threshold.** An interception you had worn down to 85 % of its
> starting strength starts counting again from where it is. Reloading to retry
> a battle quietly removes the progress you had made against the operation
> around it.

### 7.3 It swaps its whole agenda under stress

The AI's "personality" — tax focus, subsidy caps, policy queue and project
lottery — is **recomputed on every call**, not stored:

| Condition | Doctrine |
| --- | --- |
| Credit rating below **0.4** | **Credit Emergency** — the finance acts in order, 100 % project spending |
| Fewer than **4** states with recruits left, chapter ≥ 1, **and** strength ratio below 0.8 | **Recruit Emergency** — the militia/draft/bounty chain |
| otherwise | its authored doctrine |

Credit distress takes precedence. Both emergency doctrines **cancel anything
in research that is not on the emergency list**
([policy order](policy-order.md) §9).

> **Pressure on either axis costs the AI its current research outright**, and
> a crisis doctrine abandons its long-term plans for as long as the crisis
> lasts. Blockading its trade or taking the states it recruits from does more
> than deny it resources — it resets its policy programme. The newspaper
> announces the recruiting crisis, so you can see it land.
>
> And note the recruiting trigger needs **all three** conditions: an AI that
> is ahead of you in strength never panics about manpower, however few states
> it has left.

---

## 8. The battle AI: what commits it to a stance

The enemy army holds one of four army-level stances — **assault, attack,
defend, retreat** — decided from a single number:

```
force balance = our weighted force ÷ yours
              × (1 + our average morale − yours)
              × (1 + our average experience − yours)
```

and compared against thresholds that are **divided by the enemy commander's
initiative**:

| Stance | Threshold | Against a 1.0-initiative general |
| --- | --- | --- |
| Defend below | 0.80 | **0.47** |
| Attack at or above | 0.80 | 0.47 |
| Assault without flanking at | 3.0 | 1.76 |
| Assault with flanking at | 4.0 | 2.35 |

> **Initiative is the dominant personality dial in battle**, exactly as it is
> in the campaign. A bold enemy general will attack you at a force balance of
> 0.47 — less than half your strength on paper — and will assault at 1.76
> where a cautious one needs three to one.
>
> Note that **morale and experience enter the balance as differences, not
> ratios**, so a 20-point morale advantage is worth a flat 20 % on the balance
> regardless of the force sizes involved. Breaking the enemy's morale is the
> fastest way to push him from attack into defend.

A stance change takes **1.5 game hours** to commit, falling to **0.45 hours**
for a fully bold general. Battle type is decided by objective points: holding
**65 %** of them makes it a defensive battle for you and an attacking one for
him.

### 8.1 Retreat is nearly all-or-nothing

Two paths end an AI battle, and the shipped data makes one of them very hard
to reach:

| Path | Condition |
| --- | --- |
| **Collapse** | its victory-point share falls to **≤ 0.01** — effective annihilation |
| **Voluntary retreat on points** | behind on points by more than `max(0.01, 0.10 − 0.15 × initiative)` |

> **A general above 0.6 initiative never retreats voluntarily**, because the
> trigger clamps to 0.01 — the same figure as the collapse path. So against a
> bold enemy commander there is no "he will withdraw when behind": he fights
> until his points are gone. Against a cautious one, a 10 % point lead is
> enough to make him leave.
>
> The night-retreat threshold is shipped at 0.275 and **has no effect** — its
> only consumer computes it and discards it, so a night withdrawal is decided
> by force balance or annihilation like any other.

Once the AI commits to retreat, a one-hour timer runs and then the battle
ends — and **every formation of that alliance the AI engine had not claimed is
also sent off the field**, so there is no rearguard left behind by accident.

---

## 9. Your own stance button hands the formation to the AI

Setting any formation stance other than **none** enrols that formation in a
second AI controller that fights it for you.

| Stance | What the AI does with it |
| --- | --- |
| none | nothing — you retain full control |
| screen / defend / attack / assault | the full 30-task micro wheel runs on it |

What it will do: re-dress the line, pick up abandoned guns, choose perks,
deploy and recall skirmishers, unlimber and withdraw guns, rally its worst
unit, lie down under fire, fall back when morale cracks, **and charge with its
infantry**. In assault stance it will launch assault moves at the nearest
enemy.

Two things it will **not** do, both for shipped-data reasons:

| Behaviour | Why not |
| --- | --- |
| **March to the sound of the guns** | its enable flag ships `false` — for your units only; the enemy AI always runs it |
| **Re-face defensively when threatened from a flank** | the whole subsystem is gated off in the base game by two inverted guards, for both sides |

> **The five behaviour flags only restrain your own units.** Each guard is
> written "my alliance is the player's *and* the flag is off", so the enemy AI
> runs all five regardless. **Marching to the sound of the guns is something
> the enemy AI does and your automated formations do not.**
>
> This is the battle-layer twin of auto-managing your finances handing your
> treasury to the AI personality ([policy order](policy-order.md) §9).

And the stance enumeration is **reversed** between the two layers, which
matters if you read numbers out of a save or a mod tool:

| | 0 | 1 | 2 | 3 | 4 |
| --- | --- | --- | --- | --- | --- |
| Army-level stance | assault | attack | defend | retreat | — |
| **Formation stance, as the UI shows it** | none | screen | defend | attack | assault |

Note also that **only infantry-dominant formations ever assault** — a cavalry
or artillery formation caps at attack — and that a macro-level **retreat sets
every formation to screen**, not to any retreat stance, because there is no
such formation stance.

---

## 10. Where the battle AI is weak

Eight exploitable defects, in descending order of how much a battle they
decide.

**Its artillery always goes to the left wing.** The wing assignment is a
random draw between 1 and 2 with an exclusive upper bound, so it always
returns 1. **Look left for the enemy guns**, every battle, every time.

**Its reserve commitment is a coin flip.** The strength test that should
decide whether a flank needs help is bypassed entirely whenever two or more
reserves are on hand — a 50 % roll commits one regardless. So **a weak enemy
flank is reinforced half the time and a strong one is reinforced just as
often.** Pressure both flanks and the reinforcement will arrive in the wrong
place about half the time.

**Only one of its formations gets a reserve per decision tick**, because the
assignment routine returns after the first chain it handles — and the
artillery and screening steps for later chains are skipped on that tick too.
In a multi-corps battle its later corps are managed noticeably worse.

**Its left-wing outermost unit is never sent back to the reserves**, because
the routine that decides this assigns from the left wing and then
unconditionally overwrites both working variables from the right. So the enemy
left accumulates units that should have been withdrawn.

**Neither of its two cooldowns works.** Both the stance re-check and the
charge re-check compare "last time" against "now **plus** a positive interval",
which is always true, so neither ever blocks. Consequences: **a formation's
stance can flip on consecutive ticks** if its strength straddles a threshold,
and a unit whose charge was just cancelled for weakness **can be re-ordered to
charge immediately.** You can bait an AI brigade into repeated aborted charges
by sitting just inside its charge radius at a strength ratio near the trigger.

**Its charge test ignores strength.** The charge ratio weights strength,
experience, morale and freshness — but the strength weighting multiplies both
sides of the ratio and **cancels out entirely**. So the enemy decides whether
to charge on experience, morale and fatigue alone, at a trigger of **3.0**.
**A fresh, high-morale enemy brigade will charge a much larger force**, and a
tired one will not charge a much smaller one.

**A feuding formation never assaults.** The stance reroll draws from four
values with an exclusive bound, so the assault stance is unreachable for a
formation under the feud controller. A feuding enemy corps is strictly less
dangerous than a non-feuding one, on top of its tripled order delays
([commanders worth having](commanders-worth-having.md) §7).

**Its defensive-facing subsystem has never run.** About 230 lines of
behaviour, plus a `debug.txt` option advertising AI defensive fallbacks when
outflanked, are unreachable because the routine that would call them has no
callers and two guards are inverted.

> **Taken together: attack the enemy's right, pressure both flanks, break his
> morale rather than his numbers, and expect no reaction at all to being
> outflanked.** The last is the largest single gap — flanking an AI formation
> produces no repositioning, only the ordinary outflanked morale penalty.

---

## 11. Fourteen things to do differently

1. **Set the two sliders separately.** Aggressiveness makes the campaign hard;
   Difficulty makes battles hard. If you want a tense campaign with fair
   fights, raise one and leave the other.
2. **Do not fast-forward through an active campaign.** The AI gets
   `√(game speed)` thinking passes, so 16× quarters its awareness per
   game-week. Fast-forward quiet winters, not operations.
3. **Do not fast-forward a battle you care about.** The enemy's army-level
   decisions keep their pace; only its brigade handling degrades — which makes
   it manoeuvre well and fight badly, a combination that can cost you more
   men than a competent opponent would.
4. **Never assume a flank march is unobserved.** Your concealment does not
   affect what AI units detect, and the AI plans against your destinations
   before your orders have transmitted.
5. **Keep your national morale up for operational reasons.** The AI's threat
   estimate is divided by the ratio of the two national morales, so your
   decline invites attacks.
6. **Use winter, but do not trust it completely.** Both offensive and
   defensive operations are multiplied by **0.2** between the winter-quarters
   months — one in five of the attacks you would otherwise face, not one in
   twenty. It is still the best window to reposition in.
7. **Expect nothing before Militia Act I** and expect Manassas after early
   July 1861 as the Confederacy.
8. **Force a stalled AI army to move**, or it will attack you within a year.
   The cubic anti-stall term resets when the force repositions.
9. **Bleed interceptions rather than beating them.** A defensive operation
   cancels itself at 40 % morale or after losing a fifth of its strength.
10. **Do not save-scum a battle in a contested theatre.** Reloading resets the
    give-up baseline of every AI defensive operation around it.
11. **Attack its credit rating or its recruiting states to reset its research.**
    Either emergency doctrine cancels everything in research that is not on
    its crisis list.
12. **Look left for the enemy guns**, and pressure both flanks so its coin-flip
    reserve commitment lands in the wrong place.
13. **Break morale, not numbers.** Morale and experience enter the enemy's
    force balance as flat differences, and its charge test ignores strength
    altogether.
14. **Auto-resolve when you want the AI to feel a defeat.** The
    auto-calculator skips its national-morale discount, its surrender
    multiplier and the Lieutenant General penalty
    ([how you lose](how-you-lose.md) §4).

---

## 12. Still open

1. **What `iswithinfog` is actually derived from on the campaign map.** §4.3
   shows the AI is exempt from concealment; how much that exemption is worth
   depends on what sets the flag, which was not traced
   ([20](../code-findings/20-ai-campaign.md) open question 8).
2. **The theatre constraint.** Every AI operation is bounded by a per-unit
   theatre position — probably the AI's answer to "do not send the Army of the
   Potomac to Missouri" — and where that is assigned was not followed.
3. **The tail of an offensive operation.** Three scheduler jobs handle
   micro-movement within an offensive, final positions and rolling up
   objectives in a captured zone; none was traced.
4. **Army Group management.** The AI has a dedicated job for it and Army
   Groups bypass the normal appointment path entirely.
5. **Whether the AI ever picks a battle strategy from `aistrategies.dat` in a
   campaign battle.** The override that would pin a side's stance is
   initialised to −1 and **never assigned anywhere in the assembly**, which
   makes it a one-line hook but leaves the authored strategies' role unclear.
6. **The campaign is not reproducible.** The global random stream is reseeded
   from the wall clock in three places inside the campaign AI, so two loads of
   the same save diverge. That is a modder's problem rather than a player's,
   but it means none of the probabilities above can be verified by repetition.
