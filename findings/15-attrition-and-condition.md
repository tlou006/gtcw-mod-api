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

1. **`encamped`, `entrenched` and `winterquarters` flags** — read from local
   booleans in the attrition block whose derivation was not traced. `Camp` and
   `Camp.GetModifier(...)` are the obvious leads, and camps are clearly a
   mechanic in their own right.
2. **`Experience()` derivation** — still untraced. Training is now understood;
   experience is not.
3. **`GetMedicalTreatmentValue(pos, alliance, mode, unit)`** — the three modes
   and how hospital range and capacity are computed.
4. **`unittypesize[]`** is the establishment size per unit type and is used as
   the attrition base. Worth confirming it is the same array as the config's
   "Maximum Unit Size".
5. **`cavalryorders` enumeration** — value 3 is guard duty.
6. **Reinforcement and replacement flow** (`latestreinforcements`,
   `reinforcementsneeded`, `ReduceRecruitsPool`) — how strength is restored
   from the manpower pool, which is the other half of attrition.
7. **The `attrition_tmp` accounting bug** in §2c — worth a second look before
   anyone relies on the reported attrition figure.
