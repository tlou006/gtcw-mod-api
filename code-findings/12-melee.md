# 12 — Melee

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment` (193 melee references), `GamePrefs`
**Summary:** [summary-combat](summary-combat.md)

Completes [11-combat-resolution](11-combat-resolution.md). Melee lives
**entirely** in `Regiment` — `Autocalc` has no melee concept at all, so melee
exists only in manually fought battles. That is a third combat model, distinct
from both fire models.

## 1. Entry, symmetry and exit

Melee is **block-to-block**, tracked as `blockobj[i].meleecounterpart`, a
`List<Regiment>` per block.

| Method | Role |
| --- | --- |
| `MoveIntoMelee(Regiment opponentreg, GameObject hittedcoy, bool updateposition)` | Entry, on physical contact with a `Coy` (company) object |
| `CheckEnemyForMeleeState(Regiment cp1, Regiment cp2)` | Enforces symmetry |
| `UpdateMelees()` | Per-frame driver |
| `ExitMelee(Regiment opponentreg, GameObject hittedcoy)` | Explicit exit |
| `ClearMeleeCounterpartEntries` / `ClearReceivedMeleeCasualties` | Teardown |

On entry the unit is forced to stand (`LieDown(forcestandup: true)`),
`anglebeforemelee` is recorded from `unitrange.retreatangle` (so a unit
remembers which way it was facing before contact), and a charge target is
assigned — preferring the nearest opponent — via `AssignChargeTarget`, which
stops the regiment and sets movement mode 6 (or 3 for an explicit charge
order).

`CheckEnemyForMeleeState` makes melee **mutual**: if exactly one side has the
other registered (`flag != flag2`), it adds the missing reciprocal entry to a
randomly chosen block. So melee is always two-way once contact registers.

**Exit is by distance**, checked at the end of `UpdateMelees`:

```
if (GetXZDistance(other, this) > max(width, depth, other.width, other.depth))
    → remove from both sides' counterpart lists
```

Melee also does not run at all while `bunits.eodcycle > 0` (end-of-day) or
`bui.deploymentmodeactive`.

### `inmelee` is a fraction, not a flag

```
inmelee = amountinmelee / max(1, blockobjectsused)
```

The share of the unit's blocks currently in contact, `0..1`. This is what
gates firing — `FireBullet` returns early on `inmelee > 0`
([11](11-combat-resolution.md) §3b) — so **any** block in contact stops the
whole unit shooting.

## 2. The casualty formula

`CalculateMeleeCasualties(attacker, defender, meleeratio_att, meleeratio_def)`,
called from exactly one site:

```csharp
CalculateMeleeCasualties(
    blockobj[i].meleecounterpart[j],              // attacker = the enemy
    this,                                        // defender = always this unit
    1f / Mathf.Max(1f, counterpart.blockobjectsused),
    1f / Mathf.Max(1f, blockobjectsused));
```

> **It is always evaluated from the defender's point of view**: each unit
> computes, per block pairing, the casualties *it* suffers. The `meleeratio`
> arguments are each side's **per-block share of its own strength**, so a unit
> split across more blocks brings proportionally less to each pairing.

Returns 0 immediately if either side has `unittyp > 13` (non-combat types).

```
base = [ (att.strength·ratio_att + def.strength·ratio_def) / 2
         × (att.strength·ratio_att / max(1, def.strength) / max(1, ratio_def)) ]
       ^ GamePrefs.meleeinfluence_strength

× 1 + (att.weapon.melee − def.weapon.melee)        · meleeinfluence_weapon
× 1 + (att.Experience() − def.Experience())        · meleeinfluence_experience
× 1 + (def.fatigue − att.fatigue)                  · meleeinfluence_fatigue
× 1 + (att.cohesion − def.cohesion)                · meleeinfluence_cohesion
× 1 + (meleeformationmodifier[def.formation] − meleeformationmodifier[att.formation])
× 1 + meleeformationmodifiercav[def.formation]     if attacker.unittyp == 1 (cavalry)
× (speedShock − 1) · meleeinfluence_speedshock + 1
× GamePrefs.meleeoveralllossfactor × 150
× GamePrefs.dlcwl_incidentsbayonetsmeleemultiplier if DLC incident 14 active
× (GameVars.currenttimefromstart − lastmeleecalculation)      // elapsed game time
```

### A third tuning idiom: differential

Every stat term is `1 + (attackerStat − defenderStat) × influence`.

> **Only the *gap* matters, not the absolute values.** Two veteran units fight
> like two green units. This is a different idiom again from the normalized
> weighted mean (manual accuracy) and the lerp-from-one factor (autocalc
> influences) — see [summary-combat](summary-combat.md). Anyone tuning these
> prefs needs to know which of the three they are editing.

Notes on individual terms:

- **The strength term is the only non-differential part**, and it is
  asymmetric: a ratio of attacker to defender strength multiplied by their
  mean, all raised to `meleeinfluence_strength`. Local numerical superiority
  compounds.
- **The formation term has no pref multiplier** — it is the raw difference of
  two `meleeformationmodifier[]` entries, so those config values set the
  magnitude directly.
- **Speed shock**: when the attacker is running along a path,
  `speedShock = GamePrefs.unitbasicspeed[unittyp, mounted] × runningspeedfact[unittyp]`,
  fed through the lerp-from-one idiom. Charging home matters.
- `× 150` is an unexplained magic constant sitting next to
  `meleeoveralllossfactor`.
- Multiplied by elapsed game time, so the model is **rate-correct** — which
  matters given there is no fixed timestep
  ([10-simulation-tick](10-simulation-tick.md)).

### The shipped numbers, and what they weigh against each other

`unitprefs.txt` carries all of them, with labels. The influence weights:

| Pref | Value |
| --- | --- |
| `meleeinfluence_weapon` | 0.5 |
| `meleeinfluence_experience` | 0.8 |
| `meleeinfluence_fatigue` | 0.25 |
| `meleeinfluence_cohesion` | 0.3 |
| `meleeinfluence_strength` | 0.25 |
| `meleeinfluence_speedshock` | 1 |
| `meleeoveralllossfactor` | 1.5 |

**The formation enumeration falls out of the config labels**, which name the
five array entries in order — closing §8's first open question:

| `formation` | Name in the config | `meleeformationmodifier` | `meleeformationmodifiercav` |
| --- | --- | --- | --- |
| 0 | Line | 1.2 | 1 |
| 1 | Skirmish | 2.5 | 1.8 |
| 2 | Attack Column | 0.7 | 1 |
| 3 | March Column | 2.5 | 1.2 |
| 4 | Square | 0.7 | 0.5 |

Both arrays are *defender*-side multipliers on the casualties the defender
takes, and together they dominate everything else in the formula:

- **Formation is worth far more than the weapon.** The weapon term spans
  `1 ± (1.0 − 0.7) × 0.5`, i.e. **0.85 to 1.15** between a bayonet-equipped
  long arm (`melee` 1.0) and a carbine or knife (0.7) — a 35 % swing end to
  end. The formation term for a line attacking a unit in skirmish order is
  `1 + (2.5 − 1.2) = 2.3`, and `1 + (1.2 − 0.7) = 1.5` for an attack column
  against a line. Catching a unit in march column (2.5) is the same as
  catching it skirmishing.
- **Against cavalry, every formation is worse off, square included.** The term
  is `1 + meleeformationmodifiercav[defender.formation]`, so line becomes
  `×2.0`, skirmish `×2.8`, march column `×2.2` and square `×1.5`. The config
  calls these *"Additional defense factor values against Cavalry charges
  (increases/(reduces losses of defending unit)"* — read as additive the code
  is right, but no entry is negative, so the "reduces" half never happens.
  Square's 0.5 is a **25 % reduction relative to line**, not the halving the
  number suggests at a glance.
- **The cavalry term does not check whether the cavalry is mounted.** The gate
  is `attacker.unittyp == 1` alone, so **dismounted cavalry charging a line
  still collects the full ×2.** `unittyp == 4` (scouts) never collects it.

### When a unit under the Charge order will actually charge

The engagement order "Charge" sets `movementmode = 3`, which arms the
micro-AI's own decision at `Regiment.cs:12425`:

```
own   = strength × (1 + w0) × (1 + Experience()·w1) × (1 + morale·w2) × (1 + (1−fatigue)·w3)
enemy = same, for the target
ratio = own / max(1, enemy)                    × weapon.meleemounted  if mounted > 0
if (ratio > GamePrefs.microaitriggerforcharge) → movementmode = 6, charge home
```

with `weightingfactorsformicroaicharge` = **{1, 0.7, 1.5, 0.6}** (strength,
experience, morale, fatigue) and `microaitriggerforcharge` = **3**.

> **Between two units of equal quality the stat terms cancel, so the test
> reduces to "do I have three times his numbers?"** That is the mechanism
> behind the familiar complaint that a unit given the Charge order stands and
> shoots instead. A maximally lopsided quality matchup — fresh elite at full
> morale against green, broken and exhausted — multiplies the ratio by 6.8, so
> quality can substitute for numbers, but only at the extremes.
>
> **`weapon.meleemounted` = 2 for every land weapon but one**, so a mounted
> unit needs only ~1.5× the numbers. Together with §3 this is the whole truth
> about the mounted melee bonus: it makes cavalry **charge twice as readily**
> and it inflates the displayed melee rating, but it is absent from
> `CalculateMeleeCasualties`, so it adds nothing once the units are in contact.
> The exception is **Rebored Muskets**, which carries `meleemounted = -1`
> — the same sentinel the 34 naval and fort guns use. Its `restriction` is
> infantry-only, so the sentinel should never reach the multiply, but it is
> one `restriction` edit away from a negative melee rating and a unit that
> refuses to charge anything.
>
> `nomicroaichargeifunitisoutflanked` — *"No micro ai charge if unit is
> outflanked or receiving flanking fire (true/false)"*, shipped `true` — is
> **parsed and never read** ([07](07-config-data-model.md)). There is no such
> safeguard.

A manual charge order (ALT + right-click) sets the charge target directly and
never reaches this test, which is what the official manual means when it says
the check "is ignored" for a manual order. It is not a morale check, though:
morale is one of four weighted inputs, and the heaviest of them.


### Routed units: losses become prisoners

In `UpdateMelees`:

```
if (isrouted) overridelosstype = 3;           // 3 = prisoners
if (!counterpart.isrouted) SufferCasualties(num, …, block: i, unitmelee: counterpart, …);
```

- **A routed unit's melee losses are recorded as prisoners**, not killed or
  wounded ([11](11-combat-resolution.md) §6).
- **A routed attacker inflicts nothing** — the casualty call is skipped
  entirely.
- A counterpart with `surrenderstate > 0` is skipped and then removed.

## 3. `meleestrength` is a display value only

`CalculateMeleeStrength()` computes a per-unit stat:

```
norm = 4 + 1 / Mathf.Min(GamePrefs.meleeformationmodifier)   // min element of the array

meleestrength = weapon.melee
              × (1 + Experience())
              × (2 − fatigue)
              × (1 + cohesion)
              × (1 / meleeformationmodifier[formation])
              / norm
  × weapon.meleemounted                            if mounted
  × perk_spiritedchargemeleefactor[level]          if perk 4,  battle only
  × perk_coldsteelmeleeeffectiveness[level]        if perk 9,  battle only
  × perk_closesupportmeleestrength[level]          if perk 14, battle only
  × dlcwl_incidentsbayonetsmeleemultiplier         if DLC incident 14
```

**It has exactly one consumer in the entire assembly:**
`BattleUI.UpdateStats2(… selectedunitreg.meleestrength …)`.

> **The number the player is shown as melee strength does not feed melee
> resolution.** `CalculateMeleeCasualties` reads raw `weapon.melee` and applies
> experience, fatigue, cohesion and formation as *differentials*;
> `meleestrength` applies them as *multipliers* on one unit, plus the mounted
> factor and three perks that the casualty formula does not reference at all.

This matters concretely:

| | In `meleestrength` (displayed) | In `CalculateMeleeCasualties` (actual) |
| --- | --- | --- |
| `weapon.melee` | ✅ multiplier | ✅ as a difference |
| Experience, fatigue, cohesion | ✅ multipliers | ✅ as differences |
| Formation | ✅ `1/modifier` | ✅ as a difference of modifiers |
| `weapon.meleemounted` | ✅ | ❌ |
| Perk 4 / 9 / 14 melee factors | ✅ | ❌ |
| Cavalry-vs-formation bonus | ❌ | ✅ `meleeformationmodifiercav` |
| Speed shock (charging) | ❌ | ✅ |
| Opposing unit at all | ❌ | ✅ everything |

So the two will correlate but not match, and the mounted multiplier and the
three melee perks are **visible in the UI without affecting melee casualties**.

Perks 4 and 9 do still influence melee — just elsewhere, through morale
(§4) and by gaining experience from melee losses
(`perk_spiritedcharge_expinc`, `perk_coldsteelperkexp`, applied in the
casualty path). `perk_closesupportmeleestrength` (perk 14) has **no effect
found outside the display value**; perk 14's real combat effects are
`perk_closesupportweaponstrength` in `Projectile` and
`perk_closesupportreloadspeed` in `Regiment`.

> Whether this is intended or a wiring slip is not determinable from code
> alone. Either way, **the ModApi should not expose `meleestrength` as "the"
> melee strength** — it is a HUD number.

## 4. Melee's morale coupling

Melee feeds the morale system rather than resolving through it. In the
`moraleeffects[]` block (`Regiment` ~line 18290 — the morale breakdown array
with parallel `moraleeffecttext[]`), iterating `meleecounterparts`:

- **Cavalry shock** triggers when an enemy counterpart is cavalry
  (`unittyp == 1`), mounted, **and this unit's `formation != 4`**.

  > **Formation 4 is the anti-cavalry formation** (square). It is the only
  > formation that suppresses cavalry melee shock.

- Enemy perk 9 (cold steel) applies `perk_coldsteelenemymoralemalus[level]`.
- A "determined foe" term aggregates `enemy.cohesion × enemy.strength` across
  all counterparts, with enemy perk 4 at level ≥ 2 adding a flat bonus when
  this unit lacks perk 4, then:

  ```
  resolve = (1 + cohesion·weightingcohesionchargedeterminedfoe)
          / max(0.01, 1 + Σ(enemy.cohesion·strength)·weighting / Σ enemy.strength)
          × strength / max(1, Σ enemy.strength) / 3
          × (1 + GetTraining())
          × (1 + Experience())
  ```

  Zeroed for `unittyp >= 2` (so only infantry and cavalry contest a charge),
  forced to 1 when there are no counterparts, and floored at 1 by perk 7
  level ≥ 2.

Full morale treatment belongs with the next subsystem; this is the melee
boundary of it.

## 5. Three suspected wiring bugs

Reported as observations with evidence, not asserted as intent. Each is the
kind of thing a modder would hit and misdiagnose as their own fault.

1. **`MoveIntoMelee` rejects block 0.**
   ```csharp
   int iDOfBlockObject = GetIDOfBlockObject(hittedcoy);
   if (iDOfBlockObject <= 0) return;
   ```
   `GetIDOfBlockObject` returns a block index. The guard is almost certainly
   meant to be `< 0` (not found); as written, **contact on a unit's first
   block never registers melee through this path.** `CheckEnemyForMeleeState`
   can still add the pairing reciprocally, so the effect is partial rather
   than total.

2. **`CheckEnemyForMeleeState` can never pick the last block.**
   ```csharp
   cp1.blockobj[UnityEngine.Random.Range(0, cp1.blockobjectsused - 1)]
   ```
   Unity's `int` overload of `Random.Range` is max-exclusive, so this draws
   from `[0, blockobjectsused − 2]`. The final block is never selected, and
   with `blockobjectsused == 1` the range is empty.

3. **Speed shock tests the defender's movement, not the attacker's.**
   ```csharp
   if (attacker.regimentpaths > 0 && !isrouted && oldposition != GetPosition()
       && attacker.pathstatus[attacker.regimentpaths - 1] == 2 && attacker.running)
   ```
   `isrouted`, `oldposition` and `GetPosition()` are unqualified — they are
   `this`, i.e. the **defender**, while every other clause reads `attacker`.
   Since the method is always called with `defender == this`, **the charge
   bonus requires the defender to be un-routed and to have moved.** A
   stationary defender appears to negate the attacker's charge shock.

A fourth, in the morale block: `if (HasPerk(8) && GetPerkLevel(9) >= 2)` mixes
perk 8 and perk 9 indices in one condition.

## 6. Perk ids are reused across contexts

Melee exposed this, and it is important well beyond melee. The same numeric
perk id means **different things** depending on unit type and context:

| Perk | Melee / battle meaning | Other meanings, same id |
| --- | --- | --- |
| 4 | `perk_spiritedchargemeleefactor` | `combatefficiencynighttime` (naval, `Autocalc`), `intelligencegatheringskilledcartographers`, `movementspeedskilledcartographers` (campaign) |
| 9 | `perk_coldsteelmeleeeffectiveness`, `perk_coldsteelenemymoralemalus` | `supplyspeedpontoontrain`, `movementcostpontoontrain`, `fightingefficiencyvsfortsbatteryrunner` (naval) |
| 14 | `perk_closesupportmeleestrength` (display only) | `perk_closesupportweaponstrength` (`Projectile`), `perk_closesupportreloadspeed` |

> **`HasPerk(n)` is not a global perk registry.** "Spirited Charge" and
> "Skilled Cartographers" are both perk 4, presumably for different unit
> types. Any ModApi perk API must be keyed on *(unit type, perk id)* or it
> will silently collide — and a perk catalogue is now a prerequisite for
> documenting commanders.

## 7. `GamePrefs` keys

`meleeinfluence_strength`, `meleeinfluence_weapon`,
`meleeinfluence_experience`, `meleeinfluence_fatigue`,
`meleeinfluence_cohesion`, `meleeinfluence_speedshock`,
`meleeoveralllossfactor`, `meleeformationmodifier[]`,
`meleeformationmodifiercav[]`, `unitbasicspeed[,]`, `runningspeedfact[]`,
`perk_spiritedchargemeleefactor[]`, `perk_coldsteelmeleeeffectiveness[]`,
`perk_closesupportmeleestrength[]`, `perk_coldsteelenemymoralemalus[]`,
`perk_spiritedcharge_expinc`, `perk_coldsteelperkexp`,
`weightingcohesionchargedeterminedfoe`,
`dlcwl_incidentsbayonetsmeleemultiplier`.

49 `GamePrefs` references mention melee in total, so this list is close to
complete but not guaranteed exhaustive.

## 8. Open questions

- ~~**Formation enumeration.**~~ **Resolved** in §2 from the `unitprefs.txt`
  labels, which name the five array entries in order: 0 Line, 1 Skirmish,
  2 Attack Column, 3 March Column, 4 Square. Consistent with the two values
  already known from the code (`formation == 1` skirmish,
  [11](11-combat-resolution.md) §3d; `formation == 4` square).
- ~~**`unittyp` enumeration.**~~ **Resolved** —
  [16-enumerations](16-enumerations.md).
- **`ReceivedMeleeCasualties`** (with `opponent`, `timeoffire`,
  `sufferedcasualties`) is maintained and cleared, but its consumer was not
  traced — likely morale or the after-action report.
- **`surrenderstate`** gates melee and is clearly a distinct mechanic
  (mass surrender) that has not been looked at.
- `amountofmelees`, `routoutofmelee`, `inmeleeold`, `timeoflastmelee` are
  tracked; only `timeoflastmelee` and `inmelee` have confirmed consumers here.
