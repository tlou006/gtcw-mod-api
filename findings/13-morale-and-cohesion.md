# 13 — Morale and cohesion

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment.UpdateMorale`,
`Regiment.UpdateMoraleCampaign`, `Regiment.CalculateCohesion`, `Autocalc`,
`BattleUnits`, `GamePrefs`; pref values quoted from the shipped
`Config/unitprefs.txt`
**Summary:** [summary-morale](summary-morale.md)

Second Phase 2 subsystem. Morale is the decisive mechanic in this genre, and
it is also the best-documented code in the assembly — the game maintains its
own effect breakdown with player-facing text, so the semantics come free.

> **The documentation shortcut `plan.md` predicted works here better than
> anywhere.** `Regiment` keeps `moraleeffects[18]` alongside
> `moraleeffecttext[18]`, so every contributing factor is enumerated in one
> method *with its own explanation attached*. No inference needed.

---

## 1. Two morale systems, one value

| | Battle | Campaign |
| --- | --- | --- |
| Method | `UpdateMorale()` | `UpdateMoraleCampaign(bool _inbattle, Regiment parentcampaignunit)` |
| Breakdown array | `moraleeffects[18]` + `moraleeffecttext[18]` | `moraleeffectscampaign[14]` (12 for `unittyp == 17`) |
| UI | `BattleUI` | `CampaignArmyPanel`, via `moraleeffectsaverage[]` |

Both write the same `Regiment.morale` field, in `[0, 1]`. Both skip
`unittyp > 13` and `unittyp == 5`.

Every effect is a **rate**: each is multiplied by elapsed game time and added
immediately, inline, rather than summed at the end —

```
morale += moraleeffects[i] * num [* num4];
```

where `num = GameVars.currenttimefromstart - lastmoraleupdate` and `num4` is a
DLC incident multiplier. The pref values are per game hour (the config labels
say so explicitly, e.g. *"per hour"*).

> `num4` is applied to the negative-leaning slots (2, 3, 4, 6, 7, 8, 9, 10, 12,
> 13, 14, 15, 16) but **not** to slots 1, 5 and 11 — supportive units, own
> cover and friendly cavalry nearby. So the DLC incident multipliers scale
> fear but not confidence. Whether deliberate is unclear.

## 2. Base recovery: the rally value

Before any effect is applied, morale regenerates:

```
morale += num × rallyValue × branchBonus × slowFactor
```

| Term | Source |
| --- | --- |
| `rallyValue` | `rallyvalue`, then `×= 1 - (1 - Experience()) × expinfluenceonbasemoraleincrease`, then AI bonus (`aibonus_restoringmorale`, non-player side only) and DLC effect 16 |
| `branchBonus` | `increaseofmoralecapforcorrectbranch` when the commander is West Point *and* his `historicalbranch` matches the unit type |
| `slowFactor` | `slowmoraleimprovementfactor` **when `morale > battlestartmorale`** |

`battlestartmorale` is latched on the first update as
`max(morale, abovemoraleslowimprovement)`.

> **Recovery above where the unit started the battle is deliberately slowed.**
> A unit can rally back to its starting morale at full rate, then crawls above
> it.

### `historicalbranch` decoded

From the three branch/unittyp pairings in the bonus check:

| `historicalbranch` | Branch | Matching `unittyp` |
| --- | --- | --- |
| 0 | Infantry | 0, 3 |
| 1 | Cavalry | 1, 4 |
| 2 | Artillery | 2 |

This also explains `increaseofrofwhencorrectbranch` and
`increaseofaccuracywhencorrectbranch` from
[11-combat-resolution](11-combat-resolution.md), both of which test
`historicalbranch == 2` for artillery.

Shipped rally values: base rally without a commander `0.3`/hr, rally morale
increase per hour `0.20`, experience multiplier on rally `0.66`, rally ceiling
`0.9`.

## 3. The 18 battle morale slots

Taken from the method together with its own `moraleeffecttext`, which is the
authoritative statement of intent.

| # | Factor | Sign | `GamePrefs` key |
| --- | --- | --- | --- |
| 0 | **First battle — a cap, not an addend** (§5) | cap | `firsttimeinbattlecap`, `effectofdrillonfirsttimemalus`, `effectofcommanderleadershiponfirsttimemalus` |
| 1 | Friendly units protecting flanks / rear (one flank → both → both + rear) | + | `moraleeffectsupportiveunits × n` |
| 2 | Enemy **positioned** on flank / rear, up to surrounded | − | `moraleeffectenemyflanking`, `moraleeffectenemyinrear` |
| 3 | Enemy **firing** from flank / rear | − | `moraleeffectenemyflankingfire`, `moraleeffectenemyinrearfire` |
| 4 | Commanders nearby (leadership + fame, per hierarchy level); penalty if wounded or killed | ± | `basemoraleincreasecommandernearby`, `moralemultipliercommanderleadership_*` |
| 5 | Own unit behind cover | + | `moralefactorbehindcoverown × covervalue` |
| 6 | Enemy behind cover | − | `moralefactorbehindcoverenemy` |
| 7 | Low / out of ammunition | − | `moraleeffectlowammo`, `moraleeffectoutofammo` |
| 8 | Under artillery or machine-gun fire | − | `moraleeffectofeachincomingarifire × n` |
| 9 | Recent severe casualties; plus army-wide casualty ratio without objectives taken | − | `moraleeffectonhighmomentarycasualties`, `armylosstriggerinfluenceonmorale` |
| 10 | Falling back / retreating under fire | − | `moraleeffectfallingback`, `moraleeffectretreating` |
| 11 | Friendly cavalry nearby | + | `moraleeffectfriendlycavalrynearby` |
| 12 | Engaged by enemy cavalry | − | `moraleeffectenemycavalryhits × n` |
| 13 | Charging a foe with better cohesion or numbers | − | `moraleeffectchargecohesionlower × max(0, 1 − resolve)` |
| 14 | Own cohesion below the alert level ("formation disrupted") | − | `moraleeffectcohesionbelowalertlevel` |
| 15 | Outside the commander's influence (cut off) | − | `moraleeffectofbeingcutoff` |
| 16 | High fatigue | − | `moraleeffectonhighfatigue` |
| 17 | Routed units nearby — enemy routed motivates, own routed demoralizes | ± | `routedunitrecovery` |

### Slot 13 — the "determined foe" resolve

The charge-doubt term reuses the melee `resolve` value computed over all melee
counterparts ([12-melee](12-melee.md) §4):

```
resolve = (1 + cohesion·weightingcohesionchargedeterminedfoe)
        / max(0.01, 1 + Σ(enemy.cohesion·enemy.strength)·weighting / Σ enemy.strength)
        × strength / max(1, Σ enemy.strength) / 3
        × (1 + GetTraining())
        × (1 + Experience())
```

Zeroed for `unittyp >= 2`, forced to 1 with no counterparts, floored at 1 by
perk 7 level ≥ 2. Shipped weighting `0.4`, morale effect `−1`/hr.

### Slot 17 — routed-nearby accumulators

`enemyroutednear` decays toward 0 and `friendlyroutednear` rises toward 0
(it is negative), both at `routedunitrecovery × num`, each clamped to its own
half-range. Perk 7 level ≥ 1 zeroes the friendly-rout penalty entirely.

Both are gated on `morale > minimumlevelforfatigueinfluenceonmorale` — the
same gate as slot 16, despite the name mentioning only fatigue.

## 4. The experience / leadership amplifier

After every slot has been applied:

```
num22 = morale - moraleAtEntry;          // net change this tick
if (num22 < 0) {
    morale += num22 × (2 - Experience())                  × overallmoraleinfluenceexp;
    morale += num22 × (2 - commanderLeadership)           × overallmoraleinfluenceleadership;
}
```

> **This fires on losses only** — gains are never amplified. And it is
> *additional* loss on top of what was already applied.

`(2 - stat)` ranges over `[1, 2]`, so experience and leadership **halve the
amplification but never remove it**: a veteran under a brilliant commander
still takes the extra, just at half the rate of a green unit under a poor one.
That asymmetry is the main reason veteran units feel steadier than their raw
stats suggest.

## 5. Three caps, in order

1. **`fightingspirit`** — if `>= 0.01`, a hard ceiling; also sets
   `maxmoralevalue`. `Autocalc` applies the identical cap, so it holds in both
   combat models.
2. **`[0, 1]`** clamp.
3. **First battle** — when `firstbattle`:

   ```
   cap = min(1, firsttimeinbattlecap
              + (1 - firsttimeinbattlecap) × effectofdrillonfirsttimemalus × GetTraining()
              + (1 - firsttimeinbattlecap) × effectofcommanderleadershiponfirsttimemalus × leadership)
   ```

   Shipped `firsttimeinbattlecap` = **0.6**, and drill and leadership buy back
   part of the remaining 0.4. Stored into `moraleeffects[0]` so the UI can
   explain it.

### `fightingspirit` is the campaign→battle link

It is **not** a battle quantity. It is seeded from **state political support**:

```
regiment.fightingspirit = GameVars.nation[regiment.state].support[alliance]   // BattleUnits
```

then shaped by commander attributes (experience, fame, leadership, weighted
`0.33` each, at four hierarchy levels from brigade upward), halved for an
inactive commander (wounded/killed), and reduced by `−0.30` when fighting on
enemy soil at low military experience (`fightingspiritenemysoil`). It is also
serialized per side and per group in the battle setup files, divided by 100 on
load, and scaled by `GameVars.usedbonusbattles`.

> **Political support in a unit's home state caps how well it can fight.**
> That is the single most important cross-layer coupling found so far: the
> campaign's politics system reaches directly into tactical combat as a morale
> ceiling, through `GameVars.nation[state].support[]`. Any mod touching state
> support is a combat mod whether it means to be or not.
>
> `CampaignArmyPanel` surfaces it as *"Morale is capped by the fighting
> spirit"*.

## 6. Rout and rally

**Two crack thresholds**, selected by melee state:

```
if ((morale < moralecracklevel      && inmelee <= 0)
 || (morale < moralecracklevelmelee && inmelee >  0))
    → Rout(...)
```

So a unit in contact breaks at a different threshold than one under fire —
`inmelee` being the fraction of blocks engaged ([12-melee](12-melee.md) §1).

**Rally**, at the end of `UpdateMorale`:

```
if ((morale > moralecracklevel || (routoutofmelee && no enemy within minretreatdistanceoutofmelee))
    && isrouted)
    → isrouted = false; StopRegiment(); play rally bugle;
      RotateRegiment(anglebeforemelee + 180°);
```

The unit turns to face back the way it came — `anglebeforemelee` was latched
on melee entry, which is what that field is for.

`startedwithlowmorale` caps a unit at `moralecracklevel + 0.05` for the rest of
the battle: **a unit that entered the battle already shaken can never recover
past the break point.**

A DLC incident (2) floors morale at `moralecracklevel + 0.01`, preventing rout
outright.

The unit symbol texture tracks three bands — normal above `moralealertlevel`,
`fragmentedsymbol` between the alert and crack levels, `brokensymbol` at or
below `moralecracklevel` (skipped for `unittyp` 3 and 4). Shipped
`cohesionalertlevel` = 0.3.

There is also a diagnostic: a drop to `morale <= 0` from above `0.4` in one
tick logs every one of the 18 effects — **useful as a debugging hook, and it
means the developers hit the same confusion.**

## 7. The 14 campaign morale slots

| # | Factor | Sign | `GamePrefs` key |
| --- | --- | --- | --- |
| 0 | Commander within camp radius | + | `moraleimprovementcommanderradiuscamp` |
| 1 | Telegraph connection | + | `moraleimprovementtelegraphconnection` |
| 2 | — **never assigned** | | |
| 3 | Out of food | − | `moralelossoutoffood` |
| 4 | Out of ammunition | − | `moralelossoutofammo` |
| 5 | Low condition — men | − | `moralelosslowconditionmen` |
| 6 | Low condition — horses | − | `moralelosslowconditionhorses` |
| 7 | Low condition — guns | − | `moralelosslowconditionguns` |
| 8 | High attrition | − | `moralelosshighattrition` — **computed but never applied, see §9** |
| 9 | Commander lost (`status >= 2`) | − | `moralelosscommanderlost` |
| 10 | Bad weather | − | `moralelosswithinbadweather` |
| 11 | — **never assigned** | | |
| 12 | Unhappy with commander | − | `moralelossunhappywithcommander` |
| 13 | Hospital in range | + | `moralehospitalrange × CBuilding.GetMedicalTreatmentValue(...)` |

### Slot 12 — the unhappiness test

```
if (fame/2 + leadership×2 < Experience()×expleverageunhappy
                          + expleverageunhappyseniorofficers × famedSeniorOfficers
    || commander.IsDefamed())
```

**Veteran units resent mediocre commanders.** Experience is on the *demand*
side of the inequality, so the better the troops, the more fame and leadership
they require. A `unhappytooltip` string is built explaining which cause
applied — low fame/leadership, defamed, or senior officers.

### UI labels are addressed by numeric id

`CampaignArmyPanel` renders the breakdown with
`GetTooltipHeadline(panel, num3 + i)` — the label for campaign morale slot `i`
is fetched by **index offset into the panel's tooltip table**, not from a
string in code. Effects are only shown when their magnitude exceeds
`triggertoshowcampaignmoraleeffects`.

> This is supporting evidence for the long-standing question about numeric
> header cells being localization ids
> ([07-config-data-model](07-config-data-model.md)): at least in this panel,
> UI text is unambiguously addressed by number. **A mod adding a morale effect
> would need a tooltip id, not just a string** — worth confirming before the
> ModApi offers custom morale effects.

---

## 8. Cohesion

A clean, self-contained system: `CalculateCohesion()`, gated on
`GameVars.frame >= 50` (so it does not run during load) and skipping
`unittyp == 5` and `unittyp > 13`.

**Exponential approach to a target**, not an accumulator:

```
disruption = cohesionbyfire + terrainPenalty + movingPenalty

target = 1 - disruption × (1 - GetTraining() × traininginfluenceoncohesionfactor)
           - cohesioncausedbyotherunitspassing
target = min(target, 1 - terrainPenalty)        // terrain is a hard ceiling
target = clamp(target, 0, 1)

rate = cohesionrecoveryperhour × perk_irondisciplinecohesionrecovery[level]   // perk 7

cohesion += (target - cohesion) × rate × elapsed
```

Four things worth knowing:

- **Terrain only penalizes a moving unit.** `terrainPenalty =
  terraininfluenceoncohesion[currentterrain]` is zeroed in the `else` branch
  when the unit has no transmitted path. A stationary unit in rough ground
  takes no terrain disruption at all.
- **Terrain is also a ceiling**, applied twice: inside the disruption sum and
  again as `min(target, 1 - terrainPenalty)` — which training cannot buy back,
  since the second application bypasses the training factor.
- **Training reduces disruption, not recovery.** Shipped
  `traininginfluenceoncohesionfactor` = 0.4, and the config label states the
  intent plainly: *"1 means 100% drill would lead to no cohesion any more"*.
- **`movementmode == 5` sets `cohesion = 0` outright**, bypassing the approach
  entirely.

Recovery is fast: shipped `cohesionrecoveryperhour` = 2, and the config notes
*"values > 1 mean recovery takes less than 1 hour"*.

### Cohesion is visible

When `|cohesion - lastcohesion| >= cohesionspriteupdatetrigger`, sprite
positions are re-laid out (`SetRegimentSpritePos`), spreading up to
`cohesionmaximumspritespread` (shipped `2`), scaled by `(1 - cohesion)`.
**Disorder is literally rendered as the formation loosening.**

### A naming inversion to watch

In code, `cohesion` is **order**: 1 = perfect formation. In the config labels,
"cohesion" means **disorder** — *"Cohesion increase by enemy artillery fire"*
describes a penalty. The disruption variables are named for the damage, then
subtracted from 1.

> Reading `unitprefs.txt` with the code's sense of the word will invert every
> conclusion.

### Cohesion's consumers

| Consumer | Use |
| --- | --- |
| Morale slot 14 | Below `cohesionalertlevel` → morale penalty |
| Morale slot 13 | Compared against the enemy's, as charge doubt |
| `Autocalc.FightUnit` | `cohesioninfluenceonenemycasautocalc`, shipped 0.3 |
| `CalculateMeleeCasualties` | `meleeinfluence_cohesion`, as a differential |
| `CalculateMeleeStrength` | `(1 + cohesion)` multiplier — display only |
| Sprite layout | Formation spread |

## 9. Two unwired features

Both verified by exhaustive reference search across all 211 game types. Each
is a config knob the player can set that does nothing.

### 9a. Fire does not disrupt cohesion

`unitprefs.txt` ships three values under *Cohesion Preferences*:

| Config label | Value | Field | Status |
| --- | --- | --- | --- |
| Cohesion increase by enemy infantry fire | 0.001 | `cohesioninfantryfire` | **parsed, never read** |
| Cohesion increase by enemy artillery fire | 0.01 | `cohesionartilleryfire` | **parsed, never read** |
| Cohesion increase by enemy cavalry shock | 0.15 | `cohesioncavalryshock` | **parsed, never read** |
| Cohesion due to fire: decrease per hour | −3 | `firecohesiondecreasebyhour` | used |

And `cohesionbyfire` — the accumulator those three would feed — has **no
increment path anywhere in the assembly.** Its complete reference set is:
declaration, zeroing at init, the decay `cohesionbyfire += -3 × elapsed`
(clamped at 0), the read in the target formula, and save/load.

> **So `cohesionbyfire` is permanently 0, and fire has no effect on cohesion in
> this build.** Only the decay half of the feature is wired. The config labels
> show clear design intent — infantry fire, artillery fire and cavalry shock
> were each meant to disrupt formations — and the three prefs are even parsed
> consecutively, immediately before the decay value that *is* used.
>
> Cavalry shock does still hit **morale** (slot 12) and melee; it just never
> touches cohesion.
>
> **A modder tuning these three numbers would see no effect and reasonably
> conclude they had the wrong file.** This is exactly the class of finding that
> justifies the project.

### 9b. Campaign high-attrition morale loss is computed but not applied

`moraleeffectscampaign[8]` is assigned `moralelosshighattrition` (or 0), and
then — unlike slots 0, 1, 3–7, 9, 10, 12 and 13 — there is **no
`morale += moraleeffectscampaign[8] * num;`** line. Each slot is applied
individually inline; there is no bulk loop that would pick it up.

The value still reaches `moraleeffectsaverage[]` and therefore the campaign UI,
so **the player can be shown an attrition morale penalty that is not being
applied.**

Campaign slots 2 and 11 are never assigned at all — dead slots in a
fixed-size array, probably removed effects.

---

## 10. `GamePrefs` keys

**Battle morale:** `moraleeffectsupportiveunits`, `moraleeffectenemyflanking`,
`moraleeffectenemyinrear`, `moraleeffectenemyflankingfire`,
`moraleeffectenemyinrearfire`, `basemoraleincreasecommandernearby`,
`moralemultipliercommanderleadership_w`,
`moralemultipliercommanderleadership_k`, `moralefactorbehindcoverown`,
`moralefactorbehindcoverenemy`, `moraleeffectlowammo`,
`moraleeffectoutofammo`, `moraleeffectofeachincomingarifire`,
`moraleeffectonhighmomentarycasualties`, `armylosstriggerinfluenceonmorale`,
`moraleeffectfallingback`, `moraleeffectretreating`,
`moraleeffectfriendlycavalrynearby`, `moraleeffectenemycavalryhits`,
`moraleeffectchargecohesionlower`, `moraleeffectcohesionbelowalertlevel`,
`moraleeffectofbeingcutoff`, `moraleeffectonhighfatigue`,
`routedunitrecovery`, `weightingcohesionchargedeterminedfoe`.

**Thresholds and caps:** `moralecracklevel`, `moralecracklevelmelee`,
`moralealertlevel`, `firsttimeinbattlecap`, `effectofdrillonfirsttimemalus`,
`effectofcommanderleadershiponfirsttimemalus`, `abovemoraleslowimprovement`,
`slowmoraleimprovementfactor`, `minimumlevelforfatigueinfluenceonmorale`,
`minretreatdistanceoutofmelee`.

**Recovery:** `rallyvalue` (field), `expinfluenceonbasemoraleincrease`,
`increaseofmoralecapforcorrectbranch`, `aibonus_restoringmorale`,
`overallmoraleinfluenceexp`, `overallmoraleinfluenceleadership`.

**Campaign morale:** `moraleimprovementcommanderradiuscamp`,
`moraleimprovementtelegraphconnection`, `moralelossoutoffood`,
`moralelossoutofammo`, `moralelosslowconditionmen`,
`moralelosslowconditionhorses`, `moralelosslowconditionguns`,
`moralelosshighattrition`, `moralelosscommanderlost`,
`moralelosswithinbadweather`, `moralelossunhappywithcommander`,
`moralehospitalrange`, `expleverageunhappy`,
`expleverageunhappyseniorofficers`, `triggertoshowcampaignmoraleeffects`.

**Cohesion:** `cohesionrecoveryperhour`, `cohesionbymoving`,
`terraininfluenceoncohesion[]`, `traininginfluenceoncohesionfactor`,
`choesionbyotherunitspassing` *(sic — the game misspells it)*,
`cohesionbyotherunitsreductionperhour`, `cohesionmaximumspritespread`,
`cohesionspriteupdatetrigger`, `cohesionalertlevel`,
`firecohesiondecreasebyhour`, `perk_irondisciplinecohesionrecovery[]`, and the
three dead keys in §9a.

**DLC multipliers:** `dlcwl_incidentsholycommunionmoralemultiplier`,
`dlcwl_incidentsfearofdeathmoralemultiplier`,
`dlcwl_incidentsnellyblymultiplier`,
`dlcwl_incidentsourgutshisglorybaserallyvalue`.

> Note the typo `choesionbyotherunitspassing` in `GamePrefs`. Any ModApi
> binding must match the misspelling, and it is a trap for anyone
> pattern-matching on `cohesion*`.

## 11. Open questions

1. **`movementmode` enumeration.** Mode 5 zeroes cohesion outright and mode 6
   is set by `AssignChargeTarget` ([12-melee](12-melee.md) §1); the full set is
   unknown and clearly load-bearing.
2. **`currentterrain` enumeration.** Indexes `terraininfluenceoncohesion[]`,
   and values 3/4/5/8 mean "embarked" in
   [11-combat-resolution](11-combat-resolution.md) §4b. Worth a dedicated map.
3. **`rallyvalue` derivation.** It is a `Regiment` field, not a pref, and
   `UpdateRallyZone` plus the commander rally-command prefs suggest commanders
   actively raise it. Not traced.
4. **Fatigue** is an input to morale, accuracy, autocalc casualties and melee,
   but its own update rule has not been traced. It is the obvious companion to
   this subsystem and arguably belongs with supply.
5. **`GetTraining()` vs `Experience()`** are distinct and both widely used;
   neither derivation is traced. Training is drill (settable?), experience is
   earned — but that is inference.
6. **`moraleincreaseeffects[4]`** holds the rally/branch/slow factors for
   display; the fourth element's use was not found.
7. **Whether 9a and 9b are bugs or deliberate removals** cannot be settled from
   code. Checking whether the three cohesion prefs changed value across patches
   would be suggestive.
