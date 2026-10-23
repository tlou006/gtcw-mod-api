# Mechanics findings

How the code's behaviour translates into **gameplay**. This is the player- and
designer-facing layer over [`code-findings/`](../code-findings/README.md), which
is the code-facing one.

The split exists because the two answer different questions:

| Folder | Question it answers | Written for |
| --- | --- | --- |
| [`code-findings/`](../code-findings/README.md) | *What does the code do?* | Someone about to patch a method |
| `mechanics-findings/` | *What does that mean at the table?* | Someone about to play a turn, or decide what is worth changing |

A `code-findings` file says `fightingspirit` is seeded from
`nation[state].support[alliance]`, shaped by commander attributes, and applied
as a hard ceiling on `morale`. A `mechanics-findings` file says: **political
support in a unit's home state sets the highest morale that unit can ever
reach, and no commander, drill or terrain lifts it past that** — then points
back at the code file for the pipeline.

## Conventions

**Every claim is sourced.** Each one cites the `code-findings` file and section
it comes from, so a reader can always descend to the mechanism, and so nothing
here drifts into folklore. A mechanics file is a *translation*, never an
independent claim.

**Tag the standing of every non-obvious claim:**

| Tag | Meaning |
| --- | --- |
| **Derived** | Follows from the code. The default; cite the section. |
| **Observed** | Seen in play. Say which build, scenario and date. |
| **Untested** | Follows from the code but has a plausible failure mode a real game would expose — a prefab value, an unverified sprite, a path no shipped scenario reaches. Say what test would settle it. |

Untagged prose is **Derived**. Keep **Untested** visible rather than rounding
it up: a confident wrong number is worse here than in `code-findings`, because
a player will act on it.

**Speak in the player's units, not the engine's.** Real minutes, not frames.
Men, not `strength` floats. Percentage points of support, not `[0, 1]`. Where
the game's own UI shows a different number than the simulation uses, that gap
is itself a finding.

**Give the decision, not just the model.** The useful output of a mechanics
write-up is what a player should *do* differently, and what a modder should
consider *worth* changing. State it.

**No decompiled source.** `code-findings` already holds the line at signatures
and short snippets; this layer is prose, tables and numbers, so it has no
reason to go near source at all.

**Naming:** `<topic>.md`, kebab-case, named after the player's concern rather
than the code's subsystem — `why-units-break.md`, not `13-morale.md`. These
files are not a sequence, so they are not numbered; the code files are, and
cross-folder links should name the file, not a number.

## Index

| File | Contents |
| --- | --- |
| [What the game shows you, and what it actually does](ui-vs-simulation.md) | The numbers and words the interface gets wrong: melee strength as a HUD-only stat, the attrition morale penalty shown but never applied, the permanently empty support-loss list, the strategic assessment computed from its own sliders, the weapon weight that is always 2 lb light, the loss-resilience bar that cannot be read as a percentage, **every enemy figure distorted by the enemy's own generals**, the perk tooltip's phantom gating, the newspaper's constant morale verdict, cavalry status labels, the cosmetic president, two accuracy grades sharing a word, rail transport that saves readiness rather than costing it, the Confiscation Act's inverted supply bonus, **three Strategy-panel rows that measure something else**, the monthly intelligence report naming the wrong European powers, three Acts promising a subsidy category that was cut, a fleet's Firepower measured at point-blank range, turret guns that are not, four naval-combat sentences pointing at the wrong mechanism — plus **a speed panel that cannot show the speed you are running at**, **a morale breakdown whose own total omits a 3-to-5× multiplier**, artillery's cover discount applying in the open, **a "Disabled Soldiers" count that includes the dead and a breakdown that does not sum to it**, a hospital tooltip claiming a 300 % mortality reduction, the config knobs that do nothing, eight label traps, and a verified list of what the interface gets right |
| [The war economy](the-war-economy.md) | Why every place has its own prices and what the price curve actually looks like; how goods move, and why broad shortages travel worse than sharp ones; who may trade with whom and what crosses the Union/Confederate line; production per building, the specialisation bonus and the harvest calendar; what a blockade does and what the loss figure really measures; the five taxes and the administration trap; debt as an automatic process, and why late-war borrowing is cheap; the boom/bust curve that nothing you do can bend |
| [Production and workforce](production-and-workforce.md) | The supply side of the economy: the 32×32 labour map that limits every building and what fills and drains it; why slave labour is exempt from it entirely; how recruitment permanently costs five workers a man, casualties included; fertility applied squared; the full output formula; where new industry comes from and the gates it must pass, including the three systems that all switch off at one credit rating; and why abundant labour makes your citizens poorer |
| [The manual against the code](manual-vs-code.md) | The official manual cross-checked against the shipped data, in six passes — every chapter now covered. **Economy:** four recipes and one producer that do not match, and project 98 Market Reform, which does nothing at all. **Combat:** the weapons tables number by number, the accuracy ladder that lost a rung, and **why the accuracy stat does nothing for every weapon rated 1.0 or below**. **Supply:** the railroad factor that is eleven rather than ten, why an army attaches to the best node rather than the nearest, the Confiscation Act's inverted sign, and **forced march cutting resupply to zero**. **Commanders:** the attribute matrix cell by cell — order delays, flanking and rallying attributed to the wrong things, and **every enemy figure you are shown distorted by the enemy's own generals**. **Politics:** thirty-one Appendix A numbers checked, European relations that cannot go negative, a Strategy-panel row that reads 0 for the whole of Chapter I, the hostile-territory movement penalty shipped off, and **the 1864 election charging its penalty twice, which makes it an instant loss**. **Naval:** Appendix E number by number — 29 of 34 stat blocks exact and all 34 armament lines — seven ship types it never lists and you can capture, **naval rate of fire that is a constant because a clamp points the wrong way**, three turret ships that get no turret bonus, and armour that gates penetration rather than reducing damage |
| [Time and speed](time-and-speed.md) | What a game day costs in real minutes — **four hours in a battle and two and a half minutes in the campaign, because the two scenes read the same config field from two different files**; the July fighting day that is nearly twice the January one; the night that fast-forwards at 1000×; what the speed buttons do and the speeds they cannot show; why fast-forwarding does not change the war but does starve the AI, delay enemy detection by six times and privilege the unit you have selected; **the late-war newspapers that can go missing below 40 fps**; what pausing stops and the panels that keep converging while it is stopped; and the full list of how often each number is recalculated, in game time |
| [Why units break](why-units-break.md) | The three separate ways a unit leaves the fight, and why cohesion is not one of them. **Contact doubles the break threshold** — 50 % morale in melee against 25 % under fire, which is exactly where the morale icon starts blinking; the eighteen morale factors ranked by what they are worth; **every morale loss multiplied by three to five, a step the unit panel's own total leaves out**; being outflanked costing almost nothing until they shoot; **artillery's cover discount applying in the open instead of under cover**; what rallying recovers and the 2 h 27 m a green brigade needs to climb out of a rout; why melee routs come back fast and bombardment routs do not; the four-step campaign cap on battle morale, including the formation average that drags your best brigade down; the 20 %-to-66 % casualty cliff that nothing recovers; and the cohesion table that prices every water crossing |
| [What kills your men](what-kills-your-men.md) | Where a brigade's men actually go, in men per week. The three dice every combat casualty is rolled on, and how **canister kills four times the share musketry does**; **the wounded pool, where 57 % die in the field, 25 % in camp and 3 % with a hospital in range** — so of 100 men hit by rifle fire, 54 end up dead if you march on and 31 if you camp; disease as a steady state costing 7 % of a brigade a year in camp and **60 % on a forced march through a bad winter**; desertion that is zero above 80 % morale and whose political multiplier never falls below ×3; condition and fatigue with every shipped rate, including the road march that makes a unit fresher and the melee that exhausts it in five minutes; **why a brigade cannot be drilled to full in the field, and why entrenching a camp slows its drill**; the state support your dead cost in their home state; and five attrition readouts that are wrong |
| [Supply in practice](supply-in-practice.md) | Two supply systems that share nothing but a number. What a depot is for — **a level-3 depot resupplies four times faster than the best economic node** — and why Supply Reform unlocks it; the five reasons an army starves in friendly territory, starting with **forced march cutting supply to zero rather than to a quarter**; the 5 % cliff that makes every percentage above it a countdown; the overnight batch, where **a tenth-full army loses 34 morale points in one night** and **a battle night costs two thousand times the desertion of a campaign night**; battlefield supply rooted at corps, where distance barely matters but **interdiction is binary and judged at nightfall**; the corps wagon train that holds two and a half nights of food; and readiness, the stat that decides what orders you are allowed to give |

## Planned

Ordered by how much the code already decided, which is roughly how much value a
translation adds. Each is backed by a `code-findings` file that already exists.

| Topic | Translates | The gap worth closing |
| --- | --- | --- |
| `the-manpower-tap.md` | [17](../code-findings/17-politics-and-support.md), [23](../code-findings/23-game-state.md) | State support as the thing that actually limits your army, the 42 reasons it moves, and which of them you control. |
| `how-you-lose.md` | [17](../code-findings/17-politics-and-support.md), [24](../code-findings/24-campaign-objectives.md) | National morale as the real loss condition, the 1862 and 1864 elections, and the one loss condition that is simply a date. |
| `policy-order.md` | [22](../code-findings/22-policy-and-project-catalogue.md), [9](../code-findings/09-policy-and-project-effects.md) | Which policies and projects are worth their cost, which ladders are abandoned dead ends, and the prerequisites the UI does not surface. |
| `commanders-worth-having.md` | [19](../code-findings/19-commanders-and-perks.md) | Which perks touch the simulation and which are flavour; how promotion, fame and feuds actually resolve. |
| `fighting-the-ai.md` | [20](../code-findings/20-ai-campaign.md), [21](../code-findings/21-ai-battle.md) | What the difficulty sliders change, the ten hardcoded cheats, and what an enemy stance commits the AI to — including what your *own* stance does to your units. |
| `events-and-the-press.md` | [25](../code-findings/25-newspaper-and-events.md) | Which historical events can still fire from your start date, what gates the rest, and why eleven of them never will. |
