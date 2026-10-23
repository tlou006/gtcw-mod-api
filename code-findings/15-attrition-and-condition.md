# 15 — Attrition, condition, fatigue and training

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment.UpdateCondition`,
`Regiment.UpdateFatigue`, the campaign attrition block (`Regiment` ~5975–6140),
`BattleUnits`, `CBuilding`, `GamePrefs`
**Summary:** [summary-logistics](summary-logistics.md)

The consequences of logistics. These four quantities sit between supply and
everything else — they are how a shortage becomes a combat penalty, and they
close two questions left open by
[13-morale-and-cohesion](13-morale-and-cohesion.md).

```
supply shortage ──► condition ──┬──► fatigue ──► morale, accuracy, melee, autocalc
                                ├──► accuracy, reload       (guns)
                                └──► training rate          (men)
weather, marching ──► sickness ──► strength loss
low morale, low state support ──► desertion ──► strength loss
```

---

## 1. Condition: three separate pools

`Regiment.UpdateCondition(bool running, bool doublequick)`, skipping
`unittyp > 13`.

`conditionmen`, `conditionhorses`, `conditionguns`, each in `[0, 1]`, each
recovering continuously and degraded by three things:

```
drillBuffer = 1 − Interpolate(GetTraining() → [0, conditiontrainingmodifier])

// constant recovery
conditionmen    += conditionimprovement[0] × elapsed × dlcEffect30
conditionhorses += conditionimprovement[1] × elapsed × dlcEffect31
conditionguns   += conditionimprovement[2] × elapsed × dlcEffect32

// forced marching
if (running || doublequick):
    conditionmen    += conditioninfluencefastmarch[0] × elapsed × drillBuffer
    conditionhorses += conditioninfluencefastmarch[1] × elapsed × drillBuffer
    conditionguns   += conditioninfluencefastmarch[2] × elapsed      // NOT buffered

// supply — see the 5% cliff in 14-supply-and-logistics §5
if (supplyinstock[2] / max(1, strength + sick) <= 0.05):
    conditionmen    += conditionoutofsupply[0] × elapsed × drillBuffer
if (supplyinstock[3] / max(1, strength + sick) <= 0.05):
    conditionhorses += conditionoutofsupply[1] × elapsed × drillBuffer

clamp each to [0, 1]
```

The degradation prefs are negative values added, hence `+=` throughout.

> **Drill buffers men and horses against wear, but not guns.**
> `conditioninfluencefastmarch[2]` is applied without `drillBuffer`, so
> gun condition degrades on forced marches at a rate training cannot reduce.
> Whether deliberate or an omission is not determinable from code.

### The shipped numbers

All per game year, from `campaignprefs.txt`:

| | Men | Horses | Guns |
| --- | --- | --- | --- |
| `conditionimprovement` | **+13** | +11 | +9 |
| `conditioninfluencefastmarch` | −20 | −16 | **−12** |
| `conditionoutofsupply` | **−40** | −30 | — |

`conditiontrainingmodifier` = **0.6**, so
`drillBuffer = 1 − 0.6 × GetTraining()` runs from 1.0 undrilled to **0.4**
fully drilled.

> **The recovery line's label is wrong about when it applies.** It reads
> *"Regular improvement of condition men/horses/guns when not moving, per
> year"*, and the code applies it **unconditionally** at the top of
> `UpdateCondition`, before any movement test. Only `running || doublequick`
> costs condition; **ordinary marching costs none at all.**

Netting the three terms for men:

| Situation | Net per year | Full → empty |
| --- | --- | --- |
| In supply, normal march or halted | **+13** | — |
| Forced march, undrilled | −7 | 52 days |
| **Forced march, fully drilled** | **+5** | never |
| Out of provisions, undrilled | −27 | 13 days |
| Out of provisions, fully drilled | −3 | 122 days |
| Out of provisions **and** forced march, undrilled | −47 | **8 days** |
| Guns on forced march, any drill | −3 | 122 days |

So the drill asymmetry is not academic: **a fully drilled brigade's men gain
condition on a forced march while its guns always lose it.**

### 1a. The exhausted-reload penalty is wired to a field nothing writes

Three prefs in `unitprefs.txt` govern what exhaustion does in combat. One of
them is live, one is parsed into the wrong field, and one is read but never
parsed:

| Config label | Value | Field the loader writes | Consumer | Status |
| --- | --- | --- | --- | --- |
| *"Reduction of accuracy when unit is exhausted…"* | 0.33 | `fatigueinfluenceonaccuracy` | `Regiment` ~9609, `num8 -= num8 × pref × fatigue` | **live** — up to −33 % accuracy |
| *"Fatigue effect on reloading time for exhausted troops…[1-x]"* | 1.25 | `fatigueforreloading` (`GamePrefs` ~5379) | — | **overwritten 16 lines later** |
| *"Increase of fatigue when reloading in game hrs"* | 1.00 | `fatigueforreloading` (`GamePrefs` ~5395) | `Regiment` ~9797, adds fatigue while reloading | live, and it is the value that survives |
| — | — | `fatigueinfluenceonreloading` | `Regiment` ~9998, `num6 += num6 × fatigue × pref` | **never parsed from any file — permanently 0** |

> **Exhausted troops do not reload more slowly in this build.** The reload
> penalty's formula exists and runs; its coefficient is a static float that no
> loader ever assigns, so it is 0. The config line written for it lands in
> `fatigueforreloading` and is destroyed by the next pref in the same method.
> Two slips in sixteen lines of loader, and the only visible symptom is that a
> documented penalty does nothing.
>
> Note the value conventions differ too: the label's `[1-x]` form (1.25 meaning
> "25 % longer") does not match the formula's `+= num6 × fatigue × pref` form,
> which would want 0.25. So even assigning it to the right field would need the
> value changed.

### What condition does

| Pool | Consumers |
| --- | --- |
| `conditionguns` | Manual accuracy: `num8 /= 1 + (1 − conditionguns) × conditionofgunsinfluenceonaccuracy`. Reload: `+= num6 × conditionofgunsinfluenceonreloading × (1 − conditionguns)`. Campaign morale slot 7. |
| `conditionmen` | Fatigue accrual rate (§3). Training rate (§4): `training += rate × elapsed × conditionmen`. Campaign morale slot 5. |
| `conditionhorses` | Fatigue accrual for horse-using types. Campaign morale slot 6. |

So **condition is the hinge**: supply reaches combat only through it, and it
reaches combat through accuracy, reload speed, fatigue and drill — never
directly as damage.

---

## 2. Attrition: wounded, sick, deserters

A single campaign block (`Regiment` ~5975–6140), running only when the parent
group is **not** in battle for the wounded portion.

### 2a. Wounded recover or die

```
encamped ? (mortalityratewoundedencamped, recoveryratewoundedencamped)
         : (mortalityratewoundednotencamped, recoveryratewoundednotencamped)

mortality /= 1 − CBuilding.GetMedicalTreatmentValue(pos, alliance, 1, unit)
recovery  *= 1 + CBuilding.GetMedicalTreatmentValue(pos, alliance, 2, unit)
mortality *= woundedtokilledambulancecorps[perk 8 level]     // self and parent
mortality /= DLC effect 15 ;  recovery *= DLC effect 15

died      = TrimToInt(unittypesize[unittyp] × mortality × elapsed × (1 ± variationforattrition))
recovered = TrimToInt(unittypesize[unittyp] × recovery  × elapsed × (1 ± variationforattrition))

losses[0] -= died;                                  // wounded pool shrinks
strength  += recovered;  losses[0] -= recovered;    // and some return to duty
```

Three points:

- **Rates scale with `unittypesize[unittyp]`, the establishment size, not
  current strength.** Attrition is therefore absolute per unit type: a
  half-strength brigade loses wounded at the same rate as a full one.
- **Hospitals use three different "modes"** of
  `GetMedicalTreatmentValue(pos, alliance, mode, unit)` — mode 0 for the
  campaign morale bonus ([13](13-morale-and-cohesion.md) slot 13), mode 1 for
  mortality, mode 2 for recovery.
- **Treating wounded raises state support**:
  `GameVars.nation[state].AdjustSupport(alliance, inflwoundedtreatedstatesupport × elapsed × treatment, 32)`.
  Reason code **32**. Since state support caps `fightingspirit`
  ([13](13-morale-and-cohesion.md) §5), **building hospitals makes your army
  fight better in battle**, via politics.

#### The shipped rates, and what they decide

| State | Mortality/yr | Recovery/yr | **Share of wounded who die** |
| --- | --- | --- | --- |
| Not encamped | **0.66** | 0.50 | **56.9 %** |
| Encamped | 0.33 | **1.00** | **24.8 %** |

Both are absolute rates against `unittypesize[unittyp]`, so the pool drains at
a fixed number of men per year and the **split** is what the two states change,
not the speed. `unittypesize` is the config's *"Maximum Unit Size"* per unit
type — **closing §6's fourth open question**:

| `unittyp` | Type | Establishment |
| --- | --- | --- |
| 0 | Infantry | 3000 |
| 1 | Cavalry | 2500 |
| 2 | Artillery | 240 |
| 3 | Skirmishers | 200 |
| 4 | Scouts | 150 |

> **Encamping converts one wounded man in three from a death into a returned
> soldier.** For a brigade, the pool drains at 3,480 men/yr in the field and
> 3,990 in camp, so a 300-man wounded pool resolves in about four weeks either
> way — but 171 of those 300 die in the field against 74 in camp. The decision
> is taken entirely after the battle.

`encamped` and `entrenched` resolve to simple null tests at the top of the
block — **closing §6's first open question**:

```csharp
bool entrenched     = (groupunit.entrenchment != null);
bool encamped       = (groupunit.campbuilding != null);
bool winterquarters = (groupunit.campbuilding != null
                    && groupunit.campbuilding.BuildingType == CBuilding.id_winterquarters);
```

so **winter quarters are a kind of camp building and therefore also encamped**,
and entrenchment is independent of both.

#### Hospitals, and the pref that is outside its own range

`GetMedicalTreatmentValue(pos, alliance, rettype, unit)` — **closing §6's third
open question** — reads a hospital filter map at the position, requires the
map's colour to match the alliance's symbol colour, takes the maximum against
`unitforrangesearch.GetHospitalValueInRange()`, and then switches on `rettype`:

| `rettype` | Returns | Pref | Value |
| --- | --- | --- | --- |
| 0 | the raw treatment value | — | 0.4 / 0.65 / 1.0 per hospital level |
| 1 | `value × hospitalinfluenceonmortality` | *"influence of hospitals on mortality rate of wounded `[-1 to 0]`"* | **−3** |
| 2 | `value × hospitalinfluenceonrecovery` | *"influence of hospitals on recovery `[>0]`"* | 1.5 |

Applied as `mortality /= 1 − (value × −3)` and `recovery *= 1 + value × 1.5`,
so at a level-3 hospital mortality is divided by **4** and recovery multiplied
by **2.5**:

| State | Effective mortality | Effective recovery | Share who die |
| --- | --- | --- | --- |
| Encamped, level-3 hospital | 0.0825 | 2.50 | **3.2 %** |
| Not encamped, level-3 hospital | 0.165 | 1.25 | 11.7 % |

> **`hospitalinfluenceonmortality` ships at −3 against a documented range of
> `[-1 to 0]`.** At −1 the divisor would be 2 and a level-3 hospital would
> halve mortality; at the shipped −3 it quarters it. Whether the range comment
> is stale or the value is a typo is not decidable from code, but the effect is
> that hospitals are twice as strong as their own label allows — and it is the
> highest-return building in the game for an army that fights.

### 2b. Sickness is a target-seeking model

Same exponential-approach idiom as cohesion:

```
target = sicknesstargetrate × strength × (1 ± variationforattrition)
       × DLC effect 22
       × sicknessadjustmentforcedmarch     if doublequick
                                           (reduced by attritionfootcavalry[perk 6], self + parent)
       × sicknessadjustmentwinter          if temperature <= coldweathertemperature
       × ((sicknessadjustmentbadweather − 1) × badWeather + 1)
       × sicknessadjustmentencamped        if encamped
capped at strength × sicknesscapfactor

delta = (target − sick) × elapsed / speedtoreachattritiontargets
sick += delta;  strength -= delta

died      = sick × mortalityratesick   × elapsed × (1 ± variation)   → sick -= died
recovered = sick × recoveryratesick    × elapsed × (1 ± variation)   → sick -= recovered; strength += recovered
```

Weather comes from `weather.GetClimateData(pos, 100)` for temperature and
`GetClimateData(pos, 105) / 100` for a bad-weather measure — **UniStorm's
climate feeding attrition**, consistent with it owning the game clock
([10-simulation-tick](10-simulation-tick.md) §5).

> Because it is target-seeking rather than cumulative, a unit in bad weather
> converges on a sickness *level* and stays there. Moving to better weather
> lowers the target and the pool drains. Winter campaigning has a steady-state
> cost, not a spiral.

#### The shipped rates, and the steady state they produce

| Pref | Value |
| --- | --- |
| `sicknesstargetrate` | **0.10** of current strength |
| `sicknesscapfactor` | 0.50 of strength |
| `sicknessadjustmentforcedmarch` | **×1.66** |
| `sicknessadjustmentwinter` (at `coldweathertemperature` ≤ **50 °F**) | **×1.80** |
| `sicknessadjustmentbadweather` | ×1.00 to **×1.40**, linear in the climate measure |
| `sicknessadjustmentencamped` | **×0.50** |
| `speedtoreachattritiontargets` | 0.10 yr — i.e. the approach runs at 10/yr |
| `mortalityratesick` / `recoveryratesick` | **2.5** / **5.0** per year |
| `variationforattrition` | ±0.15 on every draw |

> **The two sick-pool rates are proportional to the pool, not to the
> establishment size** — `sick × 2.5 × elapsed`, not
> `unittypesize × 2.5 × elapsed` — even though both config labels say
> *"calculated from the standard unit size"* like the wounded pair above. The
> labels are describing the wounded model. At 7.5/yr combined drain a sick case
> lasts about **seven weeks**, and **one sick man in three dies** (2.5 of 7.5).

Solving the two rates together gives the equilibrium, which is what a player
actually experiences — inflow `10 × (target − sick)` against outflow
`7.5 × sick` settles at `sick = 0.571 × target`, and deaths run at
`2.5 × sick`:

| Situation | Target | Sick at equilibrium | Deaths/yr | …as a share of a 3,000-man brigade |
| --- | --- | --- | --- | --- |
| Good weather, encamped | 150 | 86 | 214 | **7 %** |
| Good weather, in the field | 300 | 171 | 428 | **14 %** |
| Winter, encamped | 270 | 154 | 386 | 13 % |
| Winter, in the field | 540 | 309 | 771 | 26 % |
| Winter + bad weather, in the field | 756 | 432 | 1 080 | 36 % |
| **Winter + bad weather + forced march** | 1 255 | 717 | 1 794 | **60 %** |

> So **winter marching costs nearly five times the disease deaths of winter
> camping**, and the worst case loses more of a brigade in a year than most
> brigades lose to the enemy. The shipped `averagetemperatures` table (27, 36,
> 46, 70, 80, 85, 86, 85, 72, 59, 48, 34 °F) puts **five months at or below the
> 50 °F threshold** — January, February, March, November, December — though
> `GetClimateData` is position-dependent, so the real boundary moves with
> latitude.

### 2c. Desertion is morale-driven and politically amplified

```
drive = clamp(1 − morale
                 − fame  × commandersfameinfluenceondesertions
                 − admin × commandersadmininfluenceondesertions, 0, 1)

stateSupport = GameVars.nation[state].support[alliance]      // 0.5 if no valid state
supportFactor = (desertionfactorstatesupport − 1) / (1 + stateSupport) + 1

guardBonus = (cavalryorders == 3) ? desertioninfluenceguard × groupguardefficiency : 0

deserters = deserterstargetrate
          × drive ^ moraleinfluenceondesertionsexp
          × moraleinfluenceondesertionslvg
          × (strength + guardBonus)
          × supportFactor
          × elapsed
          × DLC effect 22 × dlcwl_incidentgodswrathdeserterstargetrate

strength -= deserters;   losses[2] += deserters
```

> **Commander fame and administration are subtracted directly from the morale
> drive**, so a famous administrator can hold a demoralised army together.
> And **low state support multiplies desertion** — the same political variable
> that caps `fightingspirit` also bleeds manpower.

#### The shipped rates, in men per week

| Pref | Value |
| --- | --- |
| `deserterstargetrate` | **0.5** |
| `moraleinfluenceondesertionsexp` | **2** (the drive is squared) |
| `moraleinfluenceondesertionslvg` | **0.2** |
| `commandersfameinfluenceondesertions` | 0.2 |
| `commandersadmininfluenceondesertions` | 0.2 |
| `desertionfactorstatesupport` | **5.0** |
| `desertioninfluenceguard` | −0.01 × `groupguardefficiency` |

Collecting the constants gives `rate = 0.1 × drive² × strength ×
supportFactor` men per year. An **average commander** (fame and administration
both 0.5) subtracts 0.2 from the drive outright, so a unit above **80 %
morale** under an average general loses nobody at all. For a 3,000-man brigade
from a state at 50 % support:

| Morale | Average general | Poor general (fame 0, admin 0) |
| --- | --- | --- |
| 0.8 | **0 / week** | 0.8 / week |
| 0.6 | 0.8 | 3.4 |
| 0.4 | 3.4 | 7.6 |
| 0.2 | 7.6 | 13.5 |

> **`supportFactor` cannot reach 1.** It is
> `(desertionfactorstatesupport − 1) / (1 + support) + 1`, which gives **×5.0
> at zero support and ×3.0 at full support** — never the ×1 the label
> *"Additional desertion factor if state support … is 0"* implies. Full
> political loyalty reduces desertion by 40 %; it never switches it off. The
> shape that would reach 1 is `(f − 1) × (1 − s) + 1`.
>
> Compared with disease (§2b) desertion is the smaller column until morale
> breaks, at which point it roughly doubles the weekly bill.

This confirms `losses[2]` = deserters from
[11-combat-resolution](11-combat-resolution.md) §6.

There is a **second, overnight desertion pass** in `BattleUnits`
(`desertionslastnight`, `desertionscumulative`) using the same formula shape
but applied through `SufferCasualties(..., overridelosstype: 2)`. Battle and
campaign desertion are separate code paths with the same prefs.

> **Suspected accounting bug.** In the campaign block,
> `GameVars.alliance[alliance].attrition_tmp += num9;` is executed after the
> desertion calculation, but `num9` still holds the **recovered-sick count**
> from the preceding block, not the deserter count (`num20`). So desertions
> appear to be omitted from the alliance attrition statistic while recovered
> sick are counted into it twice. Recorded as an observation — it affects a
> reported figure, not the simulation.

**Three more places desertion goes missing from the reported numbers**, all
confirmed:

- `Regiment` ~10658: in the campaign branch,
  `grouplosses += losses[0] + losses[1] + losses[3]` — **wounded, killed and
  prisoners, but not deserters.** The battle branch uses
  `Tools.SumUp(losses)` and does include them. So a formation's total-losses
  figure omits desertion on the campaign map and counts it in a battle.
- `Regiment` ~10667: `if (missingavg > losses[2]) losses[2] = (int)missingavg;`
  — the **display aggregation writes back into the loss ledger**. `missingavg`
  is a 0.25-year trailing average of the cumulative `losses[2]`, so on a
  monotonically rising counter the test should not fire; it is a guard rather
  than a live mutation, but it is a display path assigning to simulation state.
- `groupmissing += losses[2] − missingavg` — the formation's *Missing* figure
  is therefore **desertions above their own three-month trailing average**, a
  recent-rate measure rather than a cumulative count. `CampaignArmyPanel` is
  honest about this for a brigade, printing *"(*trailing 3 months)"*; the
  formation-level line carries no such note.

---

## 3. Fatigue

`Regiment.UpdateFatigue()`, gated on `GameVars.frame >= 50`. This closes an
open question from [13-morale-and-cohesion](13-morale-and-cohesion.md).

```
expBuffer = 1 − fatiguereductionfactorexp × Experience()

conditionMult = 1 + (1 − conditionmen) × conditionofmeninfluenceonfatigue
                    × (1 − GetTraining() × conditionofmenreductiondrill)
                    × (1 − admin × conditionofmenreductioncommanderadmin)
if unittyp in {1, 2, 4}:
    conditionMult *= same expression with conditionhorses

elapsed = 0 if first update, immediateunitplacement, or gap > 24h

fatigue -= fatiguereduction × elapsed                    // unconditional recovery
if (bunits.eodcycle > 0) return;                         // no accrual overnight

fatigue += meleefatiguefactor × elapsed × expBuffer × inmelee × conditionMult

if moving along a path:
    marchFactor = runningfatiguefactor      if running
                : doublequickfatiguefactor  if doublequick
                : basicfatiguefactor
    fatigue += terrainfatigue[currentterrain] × elapsed × marchFactor
               × expBuffer × conditionMult × perk_zouaves_fatiguefactor[perk 2]
               × debug_aicheat_fatiguefactor      // non-player side only
clamp [0, 1]
```

Also `fatigue += fatigueforreloading × reloadInterval / …` in the firing path,
so sustained fire tires a unit.

Notes:

- **Melee fatigue scales with `inmelee`**, the fraction of blocks engaged
  ([12-melee](12-melee.md) §1) — a partially engaged unit tires
  proportionally.
- **Low condition multiplies fatigue accrual**, buffered by both drill and
  commander administration. This is the main mechanical route from supply to
  combat performance.
- **Terrain-indexed** via `terrainfatigue[currentterrain]`, like
  `terraininfluenceoncohesion[]`.
- The `> 24h` elapsed clamp prevents a fatigue spike after a long campaign gap
  or a save reload.
- `debug_aicheat_fatiguefactor` applies to the non-player alliance — one of
  several AI handicap/bonus multipliers scattered through the simulation
  (`aicheat_shiftedbonusmorale`, `aibonus_restoringmorale`,
  `debug_aicheat_fallbackmoralemultiplier`).

**Perk 2 is both Zouaves (fatigue) here and a sharpshooter bonus in
`Autocalc.FightUnit`** — more of the perk-id reuse documented in
[12-melee](12-melee.md) §6.

---

### The shipped fatigue numbers

| Pref | Value |
| --- | --- |
| `fatiguereduction` (recovery, unconditional) | **0.6**/hr |
| `basicfatiguefactor` | ×2.5 |
| `doublequickfatiguefactor` | ×5 |
| `runningfatiguefactor` | ×7 |
| `meleefatiguefactor` | **12**/hr, scaled by `inmelee`, no terrain factor |
| `fatiguereductionfactorexp` | 0.4 — experience buffers accrual by up to 40 % |
| `conditionofmeninfluenceonfatigue` | 0.5, reduced by drill (0.35) and commander admin (0.25) |
| `fatiguefactorspeed` | 0.2 |
| `fatigueinfluenceonaccuracy` | **0.33** |
| `fatiguealertlevel` | 0.7 |

`terrainfatigue[]` uses the same 14-entry terrain index decoded in
[13-morale-and-cohesion](13-morale-and-cohesion.md) §8:

| Terrain | per hour | ×2.5 normal | ×5 double-quick | ×7 running |
| --- | --- | --- | --- | --- |
| Road, Fences/Walls | 0.20 | 0.50 | 1.00 | 1.40 |
| Grassland | 0.25 | 0.625 | 1.25 | 1.75 |
| Bridge, Pontoon 0–4 | 0.30 | 0.75 | 1.50 | 2.10 |
| Ford | 0.40 | 1.00 | 2.00 | 2.80 |
| Creek | 1.00 | 2.50 | 5.00 | 7.00 |
| **River** | **1.50** | 3.75 | 7.50 | 10.50 |

> **Set against the flat 0.6/hr recovery, a normal-pace march on a road makes a
> unit fresher** — 0.50 accrual against 0.60 recovery — and on grass it is a
> standing draw at 0.625. **Ordinary marching is free; the pace and the water
> are what tire troops.** Time from fresh to exhausted: never on a road at
> normal pace, 1 h 32 m at double-quick on grass, 52 m running on grass, 14 m
> double-quick through a creek, 6 m running through a river, and **5 m in
> melee** — against **1 h 40 m** of standing still to recover.

And `fatiguefactorspeed` = 0.2 makes the speed penalty almost flat:
`max(1 − fatigue, 0.01)^0.2` gives **87 % speed at half fatigue and 40 % at
full exhaustion**. Fatigue is a combat penalty (−33 % accuracy, the morale slot
and the melee differential) much more than a movement one.

## 4. Training (drill)

Closes the other question from [13](13-morale-and-cohesion.md): `GetTraining()`
is **accumulated drill**, distinct from earned `Experience()`.

```
rate = drillincreasebase
rate = drillincreaseencamped        if encamped
rate = drillincreasewinterquarters  if in winter quarters
rate = drillincreaseentrenched      if entrenched

rate *= 1 + GameVars.alliance[alliance].GetProjectLevel(118) × project_trainingmanuals
rate *= DLC effect 20 × Camp.GetModifier(6, dividebycommandedunits: true)

training += rate × elapsed × conditionmen      // clamp [0, 1]
```

> The four states are an **overwriting if-chain, not multipliers** — the last
> matching condition wins, in the order base → encamped → winter quarters →
> entrenched. So an entrenched unit drills at the entrenched rate regardless of
> also being encamped.

Shipped rates, per game year, with the time a unit needs to go from undrilled
to fully drilled at `conditionmen == 1`:

| State | Rate | 0 → fully drilled |
| --- | --- | --- |
| In the field | **0.20** | **5 years** |
| Encamped (ordinary camp) | **0.60** | 1 yr 8 mo |
| Winter quarters | 0.30 | 3 yr 4 mo |
| Entrenched (overrides both) | 0.40 | 2 yr 6 mo |

> **A brigade cannot be drilled to full in the field — five years is longer
> than the war.** And because winter quarters are a camp building (§2a) and
> entrenchment overrides everything, the if-chain produces two results no
> player would guess: **entrenching a camp cuts its drill rate from 0.60 to
> 0.40**, and **winter quarters are the worst place to drill** at 0.30, below
> an ordinary camp.

Two couplings worth noting:

- **Training scales with `conditionmen`**, so a worn-out army cannot drill its
  way back.
- **Project 118 ("training manuals") boosts the drill rate** via
  `GetProjectLevel(118) × project_trainingmanuals`. A concrete, verified
  example of a project effect reaching the unit simulation — useful as a
  reference case for [09-policy-and-project-effects](09-policy-and-project-effects.md).

Training feeds: cohesion disruption resistance, condition wear resistance,
fatigue resistance, the first-battle morale cap, the melee "determined foe"
resolve, and the overnight casualty-shock morale term. It is the single most
broadly-used derived stat found so far.

---

## 5. `GamePrefs` keys

**Condition:** `conditionimprovement[]`, `conditioninfluencefastmarch[]`,
`conditionoutofsupply[]`, `conditiontrainingmodifier`,
`influenceonconditionifprovisionsnegative`,
`influenceonconditionifforagenegative`, `lowconditionalertlevel`,
`conditionofgunsinfluenceonaccuracy`, `conditionofgunsinfluenceonreloading`,
`conditionofmeninfluenceonfatigue`, `conditionofmenreductiondrill`,
`conditionofmenreductioncommanderadmin`.

**Wounded / sick:** `mortalityratewoundedencamped`,
`mortalityratewoundednotencamped`, `recoveryratewoundedencamped`,
`recoveryratewoundednotencamped`, `mortalityratesick`, `recoveryratesick`,
`sicknesstargetrate`, `sicknesscapfactor`, `sicknessadjustmentwinter`,
`sicknessadjustmentbadweather`, `sicknessadjustmentencamped`,
`sicknessadjustmentforcedmarch`, `coldweathertemperature`,
`speedtoreachattritiontargets`, `variationforattrition`,
`woundedtokilledambulancecorps[]`, `attritionfootcavalry[]`,
`perkexpincreasewounded`, `inflwoundedtreatedstatesupport`,
`levelofhighattrition`.

**Desertion:** `deserterstargetrate`, `moraleinfluenceondesertionsexp`,
`moraleinfluenceondesertionslvg`, `commandersfameinfluenceondesertions`,
`commandersadmininfluenceondesertions`, `desertionfactorstatesupport`,
`desertioninfluenceguard`, `dlcwl_incidentgodswrathdeserterstargetrate`.

**Fatigue:** `basicfatiguefactor`, `doublequickfatiguefactor`,
`runningfatiguefactor`, `fatiguereduction`, `fatiguereductionfactorexp`,
`meleefatiguefactor`, `terrainfatigue[]`, `fatigueforreloading`,
`fatiguealertlevel`, `perk_zouaves_fatiguefactor[]`,
`debug_aicheat_fatiguefactor`.

**Training:** `drillincreasebase`, `drillincreaseencamped`,
`drillincreasewinterquarters`, `drillincreaseentrenched`,
`project_trainingmanuals`.

---

## 6. Open questions

1. **Reinforcement and replacement flow** (`latestreinforcements`,
   `reinforcementsneeded`, `ReduceRecruitsPool`) — how strength is restored
   from the manpower pool, and how long a request takes to arrive. This is the
   other half of attrition and the largest remaining gap in this subsystem.
2. **What a hospital's range actually covers.** `GetMedicalTreatmentValue` is
   decoded (§2a), including the own-side colour test on the filter map and the
   0.4 / 0.65 / 1.0 level values, but `GetHospitalValueInRange()` and the
   filter map's painting radius have not been traced.
3. **`cavalryorders` enumeration** — value 3 is guard duty, which is the only
   value this file needs; the rest belongs with campaign orders.
4. **`Camp` and `Camp.GetModifier(...)`.** The `encamped` / `entrenched` /
   `winterquarters` flags are now resolved (§2a) as null tests on
   `campbuilding` and `entrenchment`, but camps are a mechanic in their own
   right — what builds one, what it costs, and what the eight `GetModifier`
   ids do.
5. **Whether the sickness equilibria of §2b hold in play.** They are
   steady-state solutions of two rates against a position-dependent weather
   input, not observations. A season in camp against a season on the march with
   the same brigade would settle it.
6. **Whether the mis-wired reload penalty of §1a and the out-of-range
   `hospitalinfluenceonmortality` are intentional.** Not decidable from code;
   comparing the values across patches would be suggestive.

The `attrition_tmp` accounting slip, `unittypesize[]`, the three camp flags and
`GetMedicalTreatmentValue` are now all answered above; `GetTraining()` and
`Experience()` are resolved in §4 and
[13-morale-and-cohesion](13-morale-and-cohesion.md) §11.
