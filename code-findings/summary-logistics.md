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
a gradient. Attrition is the other half: disease is a target-seeking steady
state costing a brigade 7 % of its strength a year in camp and up to 60 % on a
forced march through a bad winter, and **the wounded pool is where most of the
dying happens** — 57 % of your wounded die in the field, 25 % in camp and 3 %
with a hospital in range.

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

## Seven findings worth remembering

| | Finding | Why it matters |
| --- | --- | --- |
| 1 | **Campaign supply state is the minimum across the four types**, not the average. | An army full of ammunition is "out of supply" if forage is short. The binding constraint is invisible unless you check all four. (`GetAvgSupplyState()` exists separately for the places that want the mean — including, confusingly, the overnight morale hit.) |
| 2 | **Battlefield supply is a multiplicative chain rooted at corps** (`supplytoplevelbf` = 15, and its config label names the ladder). | Penalties compound with **hierarchy depth**, not distance: the decay is `distance^0.1` weighted 0.15, so even 2,000 away costs only 8 %. A brigade two echelons down under a mediocre commander draws **72 %**; under a poor one, 62 %; detached, half of that again. |
| 3 | **An enemy on the line sets supply state to 0, and the chain is only recomputed at nightfall.** `CalculateSupplyLine` runs under `eodcycle > 0 \|\| supplyiconactive`. | **Interdiction is judged at dusk.** A line cut at midday and cleared by evening costs nothing; a line cut at dusk costs that brigade its whole night's resupply — and the overnight handout is scaled by each brigade's own state, so a cut line means *no* resupply rather than slow resupply. |
| 4 | **A corps carries 1 / 1 / 2.5 / 2.5 man-days** and every brigade eats one man-day of provisions a night. | **Two and a half nights of food, with no other source on a battlefield.** The third night puts the army at the 5 % cliff — which is what ends long battles, rather than any explicit limit. |
| 5 | **Depots hold economy resources, and local price throttles throughput** (`max(0.1, 1 − price) ^ buysupplygoodspriceratio`). | A shortage that raises prices *further reduces* the rate supply moves forward. Economy and logistics are one system. A level-3 depot is worth about **four adjacent economic nodes**, and twenty-seven distant ones. |
| 6 | **Shortage is a 5% cliff.** Condition only degrades once stock falls to `≤ 0.05` of (strength + sick). | At 20% provisions a unit is completely fine. There is no graduated penalty. The `0.05` is a **hardcoded literal**, not a pref. |
| 7 | **Supply never damages units directly.** It works entirely through `condition`. | So `condition` is the hinge of the whole logistics model, and it reaches combat only via fatigue, accuracy, reload and drill. |

## The overnight batch is where a battle's real bill lands

`CheckDesertionsAndMorale()` runs **once per night**, and all three of its
terms are flat per-night hits with no `elapsed` scaling
([14](14-supply-and-logistics.md) §5a):

- **Casualty shock** on a fifth-power curve, worth −0.23 morale at 30 % losses
  for a green brigade and **exactly zero** for one that is both elite and fully
  drilled, since both terms carry `(1 − stat)`.
- **The supply hit** on a seventh-power curve: nothing above 50 % average
  supply, −0.15 at 20 % and −0.70 at empty. **Half-empty wagons cost nothing
  and a tenth-full army collapses overnight.**
- **Desertion** at `drive² × 0.2 × strength` men, which drops the campaign
  formula's base rate, its `supportFactor` **and its `× elapsed`**. A 3,000-man
  brigade at 50 % morale loses **54 men in one battle night** against about
  0.03 on the campaign map — **roughly two thousand times the rate.** This is
  why multi-day battles dissolve units rather than merely bleeding them.

## Replacements come down the supply pipe

The largest gap in this subsystem is now closed:
**[14](14-supply-and-logistics.md) §3b**. There is no separate replacement
system — the same method that resupplies a formation in the campaign fills its
brigades back up to establishment from their home states' recruiting pools, on
the slow frame-paced cycle, with no player order.

| Term | Shipped value |
| --- | --- |
| Time to fill a whole unit, at its supply node | **1.5** campaign years |
| …at map distance 2,000 | 4.0 campaign years |
| Priority steps (low / medium / high) | **{0, 0.5, 1.0}** |
| Readiness multiplier at zero readiness | ×0.25 |
| Full snow or rain | ×0.25 |
| Reforming (one month, at home) | **×10**, and supply treated as full |
| Draft Act I / II | ×1.5 / ×2.0, **not cumulative** |
| Supply state | multiplies directly |

So a 2,000-man brigade at the best case gets **26 men a week**, and a brigade
left on *low* priority gets **none** — `prioritysteps[0]` is zero, so the
lowest setting is an off switch rather than a slower rate.

Three defects and a label trap:

- **`refpoint` is the distance to the chosen supply node**, not to the home
  state as both config labels claim, and it is initialised to the maximum, so a
  brigade with no node in range refills at the 4.0-year rate.
- **The winter penalty ignores winter quarters.** `GetWeatherAdjustment()`'s
  winter-quarters branch resets four of its five values and leaves the
  reinforcement multiplier weathered, although the config label says *"if unit
  is not encamped"*. A second routine, `GetReinforcementspeedWinter()`,
  implements the label — at **1 %** outside winter quarters — and has **no
  callers anywhere in the assembly.**
- **Replacement equipment comes out of depot stock first**, so filling units up
  competes with daily supply for the same goods, and the purchase enters the
  alliance's army-order demand and therefore national prices
  ([23](23-game-state.md) §13).
- **Drafted replacements charge their home state the drafting support penalty**
  exactly as a new unit would ([17](17-politics-and-support.md) §5).

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

## Attrition: three pools, three idioms, and the numbers

- **Wounded** recover or die at two competing absolute rates against
  `unittypesize[unittyp]` — the **establishment** size (infantry 3000, cavalry
  2500, artillery 240, skirmishers 200, scouts 150), not current strength:

  | State | Mortality/yr | Recovery/yr | **Share of wounded who die** |
  | --- | --- | --- | --- |
  | Not encamped | 0.66 | 0.50 | **57 %** |
  | **Encamped** | 0.33 | 1.00 | **25 %** |
  | Encamped, level-3 hospital | 0.08 | 2.50 | **3 %** |

  > **Encamping converts one wounded man in three from a death into a returned
  > soldier**, and a level-3 hospital converts almost all of them. Of 100 men
  > hit by musket fire (13 killed, 72 wounded, 15 missing) **54 end up dead if
  > the army keeps marching and 31 if it camps**. `hospitalinfluenceonmortality`
  > ships at **−3** against a documented range of `[-1 to 0]`, which makes
  > hospitals twice as strong as their own label allows.

- **Sickness is target-seeking**, the same exponential-approach idiom as
  cohesion: the target is **10 % of strength**, ×1.66 on forced march, ×1.80
  below 50 °F, up to ×1.40 in bad weather, **×0.50 encamped**, capped at 50 %.
  The pool turns over at 2.5/yr mortality against 5.0/yr recovery, so **one
  sick man in three dies** and a case lasts about seven weeks. Solving the two
  rates gives the standing bill for a 3,000-man brigade: **4 deaths a week in
  camp in good weather, 8 in the field, and 35 on a forced march through a bad
  winter** — 7 %, 14 % and 60 % of the brigade per year. **Winter campaigning
  has a steady-state cost, not a spiral**, and a week in camp recovers most of
  it.
- **Desertion is morale-driven and politically amplified**:
  `0.1 × drive² × strength × supportFactor` men/year, where
  `drive = clamp(1 − morale − 0.2 × fame − 0.2 × admin, 0, 1)`. An average
  commander subtracts 0.2 outright, so **a unit above 80 % morale under an
  average general loses nobody at all**; at 20 % morale it loses 8 men a week.
  `supportFactor` runs **×5.0 at zero state support to ×3.0 at full** — it
  never reaches ×1, so full loyalty reduces desertion by 40 % rather than
  ending it.

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

## Six suspected bugs

- **Drill buffers men and horses against forced-march wear, but not guns** —
  `conditioninfluencefastmarch[2]` is applied without the drill buffer that the
  other two get. The practical result is that **a fully drilled brigade's men
  gain condition on a forced march (+5/yr) while its guns always lose it
  (−3/yr)**.
- **Exhausted troops do not reload more slowly.** The field the reload penalty
  reads, `fatigueinfluenceonreloading`, is **never parsed from any config
  file** and is permanently 0. The config line written for it is parsed into
  `fatigueforreloading`, which the next pref in the same loader method
  overwrites sixteen lines later
  ([15](15-attrition-and-condition.md) §1a). The accuracy penalty for
  exhaustion (−33 %) is live; the reload penalty is not.
- **Desertions are mis-accounted in the attrition statistic.** The campaign
  block adds a stale `num9` (the recovered-sick count) to `attrition_tmp` after
  the desertion calculation instead of the deserter count, so recovered sick
  are counted twice and desertion not at all. The campaign branch of
  `grouplosses` drops deserters too, while the battle branch keeps them — so
  **men who desert leave the army without appearing in any readable loss
  total**.
- **A unit on low reinforcement priority receives no replacements at all** —
  `prioritysteps[0]` is 0, and the UI presents it as the first of three
  settings ([14](14-supply-and-logistics.md) §3b).
- **The winter reinforcement penalty applies in winter quarters**, although its
  config label exempts encamped units — and the routine that does implement the
  exemption has no callers (§3b).
- **The hospital tooltip prints a 300 % mortality reduction** at a level-3
  hospital, by printing the raw −3 coefficient as a percentage. The real
  reduction is **75 %**, since the coefficient is applied as a divisor. The
  recovery half of the same sentence is correct.

## Still open

1. ~~**The depot/local split**~~ — **resolved** in
   [14](14-supply-and-logistics.md) §3a, together with the node-selection rule,
   the shipped rates in days, the transport-capacity model (§8), raiding as a
   third supply source (§9) and **readiness** (§10), which this summary had not
   covered at all.
2. **`IIP`** — the economy/logistics junction. Only `id_harbor` is known.
   Belongs with the economy pass.
3. **`Camp` as a mechanic.** The three flags are resolved — `encamped` and
   `entrenched` are null tests on `campbuilding` and `entrenchment`, and
   **winter quarters are a camp building**, which is why they also carry the
   encamped wounded rates and the ×0.5 sickness factor. What *builds* a camp
   and what the eight `Camp.GetModifier` ids do is still untraced.
4. ~~**Reinforcement and replacement flow**~~ — **resolved** in
   [14](14-supply-and-logistics.md) §3b and summarised above. What is left is
   how reinforcement priority propagates to units attached after it was set,
   and whether the recruiting panel's offered figure is the pool or the pool
   clamped at the maximum unit size.
5. **Fleet supply** takes a separate harbour path that was not followed.
6. **`eodcycle`'s full state machine** — ~~gates firing, melee, cohesion,
   fatigue and the whole overnight batch~~ — **resolved** in
   [10](10-simulation-tick.md) §8.
7. **What a hospital's range covers.** `GetMedicalTreatmentValue` is decoded,
   but `GetHospitalValueInRange()` and the filter map's painting radius are
   not.
8. **What campaign-map distance means in miles.**
   `FilterMap.GetFieldRealWorldFieldSize()` is
   `terrain.terrainData.size.x × mapfactor` — both **scene values**, so this
   needs an asset dump rather than more reading. Until it is pinned, the
   foraging distance figures are relative, not absolute.
9. **Whether a detached brigade acting as its own supply root can resupply at
   all.** It becomes `istopsupplyunit`, and `ResupplyAllUnits` excludes the top
   unit from its own handout ([14](14-supply-and-logistics.md) §11).

`unittypesize[]`, `GetMedicalTreatmentValue`'s three modes,
`Experience()`'s derivation, `overridesymbol` and `groupsupplystate[]` are all
now answered — see [15](15-attrition-and-condition.md) §2a,
[13](13-morale-and-cohesion.md) §11 and
[14](14-supply-and-logistics.md) §2, §2a.
