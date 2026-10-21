# What the game shows you, and what it actually does

**Build:** see [00-build-provenance](../code-findings/00-build-provenance.md)
**Translates:** [11](../code-findings/11-combat-resolution.md),
[12](../code-findings/12-melee.md),
[13](../code-findings/13-morale-and-cohesion.md),
[16](../code-findings/16-enumerations.md),
[17](../code-findings/17-politics-and-support.md),
[19](../code-findings/19-commanders-and-perks.md),
[21](../code-findings/21-ai-battle.md),
[25](../code-findings/25-newspaper-and-events.md)

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
detachment's bar is drawn against a standard it can never reach.

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

## 3. Knobs that are exposed and do nothing

Each of these is labelled in a config file the game ships for players to edit,
and reaches nothing.

| Where | Label | Status |
| --- | --- | --- |
| `debug.txt` | "Use AI defensive fallbacks if outflanked" — ships **on** | The only function reading it is **never called**. The AI has no outflanked-fallback behaviour. |
| `unitprefs.txt` | "Cohesion increase by enemy infantry fire" (0.001) | Parsed, never read. |
| `unitprefs.txt` | "Cohesion increase by enemy artillery fire" (0.01) | Parsed, never read. |
| `unitprefs.txt` | "Cohesion increase by enemy cavalry shock" (0.15) | Parsed, never read. |
| `battleprefs.txt` | "Maximum Gamespeed" (10) | **No consumer anywhere.** The real ceilings are hardcoded: ×20 in battle, ×50 in the campaign. |
| `aistrategies.dat` | night-retreat trigger (0.275) | Computed and discarded on the two paths that would use it. |
| commander prefs | subordinate command limit | No consumer — and the config is honest about it, marking the pref `[deactivated]`. |
| `battleprefs.txt` | "No micro ai charge if unit is outflanked or receiving flanking fire" — ships **true** | Parsed, never read. A unit under the Charge order will go in while outflanked. |
| `unitprefs.txt` | linear and exponential accuracy-over-distance terms | Parsed, never read. Range does degrade fire, but through aim scatter, not these. |
| `unitprefs.txt` | "Probability of commander dead / wounded if hit on group was detected" (0.005 / 0.02) | Parsed, never read. HQ commander losses use the brigade values scaled by hierarchy depth instead. |
| `campaignprefs.txt` | "Upkeep cost factor of transports for transportation subsidies" (0.5), "Increased IIP transport capacity…" (1.5), "Increased railroad construction speed…" (2.0) | Parsed, never read. **The Transportation subsidy category was removed**; its effect constants were left in the file. Seven more in the same block are dead for the same reason. |
| `campaignprefs.txt` | "Max supply ratio of units retreating or on forced march" (0.25) | Parsed, never read. Forced march cuts resupply to **zero**, not to a quarter. |
| `campaignprefs.txt` | "Readiness improvement factor based on intelligence value" (0.008) | Parsed, never read — and marked `[deactivated]`, unlike its live near-namesake. |
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

## 5. For modders: three label traps

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
