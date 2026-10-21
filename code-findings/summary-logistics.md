# Summary — Supply, attrition and logistics

**Covers:** [14-supply-and-logistics](14-supply-and-logistics.md),
[15-attrition-and-condition](15-attrition-and-condition.md),
[16-enumerations](16-enumerations.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Supply is **two models again** — a battlefield one and a campaign one — sharing
a four-slot stock array (small-arms ammo, artillery ammo, provisions, forage).
On the battlefield, supply is a **multiplicative chain up the command hierarchy
to a map-edge entry point**, and any enemy unit standing on the line between a
unit and its parent cuts it. In the campaign, supply state is the **worst**
ratio of the four types — Liebig's law of the minimum — fed by depots that hold
actual economy goods, so **local prices throttle resupply**. Shortage does not
cause damage directly: it degrades `condition`, which raises fatigue, lowers
accuracy and reload speed, and slows drill. And it bites on a **5% cliff**, not
a gradient.

## The chain, end to end

```
economy prices & depot stock ──► depot throughput
                                      │
map-edge entry point ──► supply chain ─┴──► supplyinstock[4]
                                              │
                        5% cliff ─────────────┤
                                              ▼
                                          condition ──┬──► fatigue ──► morale, accuracy, melee
                                                      ├──► accuracy, reload (guns)
                                                      └──► drill rate (men)
weather, forced march ──► sickness ──► strength
low morale, low state support ──► desertion ──► strength
```

## Five findings worth remembering

| | Finding | Why it matters |
| --- | --- | --- |
| 1 | **Campaign supply state is the minimum across the four types**, not the average. | An army full of ammunition is "out of supply" if forage is short. The binding constraint is invisible unless you check all four. (`GetAvgSupplyState()` exists separately for the places that want the mean — including, confusingly, the overnight morale hit.) |
| 2 | **Battlefield supply is a multiplicative chain**, each link multiplying a distance-decay factor by its parent's state. | Penalties compound with **hierarchy depth**, not just distance. And an enemy unit physically on the line interrupts it — traced with a real geometric raycast. |
| 3 | **Depots hold economy resources, and local price throttles throughput** (`max(0.1, 1 − price) ^ buysupplygoodspriceratio`). | A shortage that raises prices *further reduces* the rate supply moves forward. Economy and logistics are one system. |
| 4 | **Shortage is a 5% cliff.** Condition only degrades once stock falls to `≤ 0.05` of (strength + sick). | At 20% provisions a unit is completely fine. There is no graduated penalty. The `0.05` is a **hardcoded literal**, not a pref. |
| 5 | **Supply never damages units directly.** It works entirely through `condition`. | So `condition` is the hinge of the whole logistics model, and it reaches combat only via fatigue, accuracy, reload and drill. |

## `EntryPoint` was more important than it looked

[02-type-inventory](02-type-inventory.md) flagged `EntryPoint` as a "false
friend" — correctly, in that it is not a program entry point. But it is not
just a reinforcement marker either: **it is where battlefield supply
originates.** Threatening or taking entry points cuts an army's supply at the
root.

## Two cross-layer couplings

**Building hospitals makes your army fight better.** Treating wounded calls
`nation[state].AdjustSupport(alliance, …, 32)`, and state support caps
`fightingspirit`, which is a hard morale ceiling in *both* combat models
([summary-morale](summary-morale.md)). Medicine → politics → battle morale.

**The AI raids your logistics using your own lookup.**
`BattleUnits.GetClosestDepotsAndTowns(...)` is what units use to find supply
and what `AICampaign` uses to pick raid targets.

## Attrition: three pools, three idioms

- **Wounded** recover or die at rates set by encampment, hospitals (three
  separate `GetMedicalTreatmentValue` modes) and perk 8. Rates scale with
  `unittypesize[unittyp]` — the **establishment** size, not current strength —
  so a half-strength brigade loses wounded as fast as a full one.
- **Sickness is target-seeking**, the same exponential-approach idiom as
  cohesion: a unit converges on a sickness *level* set by weather, marching and
  encampment, and drains when conditions improve. **Winter campaigning has a
  steady-state cost, not a spiral.**
- **Desertion is morale-driven and politically amplified.** Commander fame and
  administration are subtracted straight from the drive, and low state support
  multiplies the result — the same political variable that caps
  `fightingspirit` also bleeds manpower.

## Two questions from the morale pass, now closed

**Fatigue**: unconditional recovery, plus accrual from melee (scaled by
`inmelee`, the fraction of blocks engaged) and from marching
(`terrainfatigue[currentterrain]` × a march-mode factor). Low `condition`
multiplies accrual, buffered by both drill and commander administration. No
accrual at all while `eodcycle > 0`.

**Training** is accumulated **drill**, distinct from earned `Experience()`:

```
rate = base | encamped | winterquarters | entrenched     // overwriting if-chain, last wins
rate *= 1 + GetProjectLevel(118) × project_trainingmanuals
training += rate × elapsed × conditionmen
```

> **Project 118 ("training manuals") boosts drill** — a verified example of a
> project effect reaching the unit simulation, and a useful reference case for
> [summary-effects](summary-effects.md).
>
> Training also **scales with `conditionmen`**, so a worn-out army cannot drill
> its way back. It is the most broadly-used derived stat found so far: cohesion
> resistance, condition wear, fatigue, the first-battle morale cap, the melee
> resolve, and the overnight casualty shock all read it.

## `unittyp` is finally complete

`Config/unitprefs.txt` declares **19 unit types by name**, which retroactively
explains every guard seen across three subsystems:

| # | | # | | # | |
| --- | --- | --- | --- | --- | --- |
| 0 | Infantry | 6–9 | *Not used* | 14 | Division |
| 1 | Cavalry | 10 | Battery | 15 | Corps |
| 2 | Artillery | 11 | Battalion | 16 | Army |
| 3 | Skirmishers | 12 | Regiment | 17 | **Fleet** |
| 4 | Scouts | 13 | Brigade | 18 | Army Group |
| 5 | Courier | | | | |

So `unittyp <= 13` means "a fighting unit or a low echelon" — the only things
that fight, tire, lose cohesion or hold condition. `> 13` is Division and above
plus Fleet, which early-return from most per-unit simulation. The horse-using
set `{1, 2, 4}` is Cavalry, Artillery and Scouts, which is why exactly those
draw forage.

**The names are data; the indices are hardcoded in guards.** They can be
renamed but not reordered, and the four unused slots at 6–9 must be filled in
place.

[16-enumerations](16-enumerations.md) now collects this alongside
`historicalbranch`, `currentoperation`, the batch-operation ids, loss types,
stance, ammunition, supply slots, and the partially-known `formation`,
`movementmode`, `combatbehavior` and `currentterrain` sets.

## Two suspected bugs

- **Drill buffers men and horses against forced-march wear, but not guns** —
  `conditioninfluencefastmarch[2]` is applied without the drill buffer that the
  other two get.
- **Desertions are mis-accounted in the attrition statistic.** The campaign
  block adds a stale `num9` (the recovered-sick count) to `attrition_tmp` after
  the desertion calculation instead of the deserter count. Affects a reported
  figure, not the simulation.

## Still open

1. ~~**The depot/local split**~~ — **resolved** in
   [14](14-supply-and-logistics.md) §3a, together with the node-selection rule,
   the shipped rates in days, the transport-capacity model (§8), raiding as a
   third supply source (§9) and **readiness** (§10), which this summary had not
   covered at all.
2. **`IIP`** — the economy/logistics junction. Only `id_harbor` is known.
   Belongs with the economy pass.
3. **`encamped` / `entrenched` / `winterquarters`** derivation — `Camp` and
   `Camp.GetModifier(...)` are the leads, and camps are a mechanic in their own
   right.
4. **`Experience()` derivation** — training is now understood, experience is
   not.
5. **Reinforcement and replacement flow** — the other half of attrition.
6. **Fleet supply** takes a separate harbour path that was not followed.
7. **`eodcycle`'s full state machine** — it gates firing, melee, cohesion,
   fatigue and the whole overnight batch.
