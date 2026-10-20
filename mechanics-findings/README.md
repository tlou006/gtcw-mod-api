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
| [What the game shows you, and what it actually does](ui-vs-simulation.md) | The numbers and words the interface gets wrong: melee strength as a HUD-only stat, the attrition morale penalty shown but never applied, the permanently empty support-loss list, the strategic assessment computed from its own sliders, the perk tooltip's phantom gating, the newspaper's constant morale verdict, cavalry status labels, the cosmetic president — plus the config knobs that do nothing, and a verified list of what the interface gets right |
| [The war economy](the-war-economy.md) | Why every place has its own prices and what the price curve actually looks like; how goods move, and why broad shortages travel worse than sharp ones; who may trade with whom and what crosses the Union/Confederate line; production per building, the specialisation bonus and the harvest calendar; what a blockade does and what the loss figure really measures; the five taxes and the administration trap; debt as an automatic process, and why late-war borrowing is cheap; the boom/bust curve that nothing you do can bend |
| [Production and workforce](production-and-workforce.md) | The supply side of the economy: the 32×32 labour map that limits every building and what fills and drains it; why slave labour is exempt from it entirely; how recruitment permanently costs five workers a man, casualties included; fertility applied squared; the full output formula; where new industry comes from and the gates it must pass, including the three systems that all switch off at one credit rating; and why abundant labour makes your citizens poorer |
| [The manual against the code](manual-vs-code.md) | The official manual cross-checked against the shipped data: what it confirms, four recipes and one producer that do not match, two warnings it gives that the code does not implement, one threshold where it is a notch pessimistic, and what it leaves out |

## Planned

Ordered by how much the code already decided, which is roughly how much value a
translation adds. Each is backed by a `code-findings` file that already exists.

| Topic | Translates | The gap worth closing |
| --- | --- | --- |
| `time-and-speed.md` | [10](../code-findings/10-simulation-tick.md) | What a game day costs in real minutes at each speed, what pausing actually stops, and which systems tick on the clock rather than the frame. |
| `why-units-break.md` | [13](../code-findings/13-morale-and-cohesion.md), [12](../code-findings/12-melee.md) | Morale vs. cohesion as two different things the player confuses; the politics→battle cap; what rallying can and cannot recover. |
| `what-kills-your-men.md` | [11](../code-findings/11-combat-resolution.md), [15](../code-findings/15-attrition-and-condition.md) | Combat is often not the largest column. Sickness, desertion and the wounded-recovery pipeline, in men per week. |
| `supply-in-practice.md` | [14](../code-findings/14-supply-and-logistics.md) | What a depot is *for*, the 5% shortage cliff, and why an army can starve inside friendly territory. |
| `the-manpower-tap.md` | [17](../code-findings/17-politics-and-support.md), [23](../code-findings/23-game-state.md) | State support as the thing that actually limits your army, the 42 reasons it moves, and which of them you control. |
| `how-you-lose.md` | [17](../code-findings/17-politics-and-support.md), [24](../code-findings/24-campaign-objectives.md) | National morale as the real loss condition, the 1862 and 1864 elections, and the one loss condition that is simply a date. |
| `policy-order.md` | [22](../code-findings/22-policy-and-project-catalogue.md), [9](../code-findings/09-policy-and-project-effects.md) | Which policies and projects are worth their cost, which ladders are abandoned dead ends, and the prerequisites the UI does not surface. |
| `commanders-worth-having.md` | [19](../code-findings/19-commanders-and-perks.md) | Which perks touch the simulation and which are flavour; how promotion, fame and feuds actually resolve. |
| `fighting-the-ai.md` | [20](../code-findings/20-ai-campaign.md), [21](../code-findings/21-ai-battle.md) | What the difficulty sliders change, the ten hardcoded cheats, and what an enemy stance commits the AI to — including what your *own* stance does to your units. |
| `events-and-the-press.md` | [25](../code-findings/25-newspaper-and-events.md) | Which historical events can still fire from your start date, what gates the rest, and why eleven of them never will. |
