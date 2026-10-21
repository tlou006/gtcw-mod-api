# 11 — Combat resolution

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Regiment`, `Projectile`, `Autocalc`,
`GameVars.Weapon`, `GamePrefs`
**Summary:** [summary-combat](summary-combat.md)

First Phase 2 subsystem. Answers the question that has been open since
[02-type-inventory](02-type-inventory.md): **does auto-resolve share the
combat model with manual battles?**

> **No. There are two independent damage models that converge on one casualty
> sink.** They read overlapping but different weapon fields, so a combat mod
> must change both or the two will disagree.

---

## 1. The two paths

```
MANUAL BATTLE                              AUTO-RESOLVE / OFF-MAP
Regiment.FireBullet(spriteid)              Autocalc.FightUnit(unit0, unit1, timediff, …)
  ├─ computes per-shot accuracy              ├─ computes ROF from stance matrix
  ├─ spawns Projectile (1, or 3 at ±15°)     ├─ closed-form statistical casualties
  │    └─ ballistic flight, raycast hits     │
  └─ Projectile.DestroyBullet()              │
       └─ per-block casualties               └─ aggregate casualties
                    ↓                                      ↓
            Regiment.SufferCasualties(amount, …, calledfromautocalc)
```

`SufferCasualties` is the **only** shared code: 1 call site from `Projectile`,
8 from `Autocalc`, plus retreat/building/debug paths in `BattleUnits`,
`Building` and `Regiment` itself. It branches internally on
`calledfromautocalc`.

### `Autocalc` is much more than auto-resolve

It is a general off-map combat engine with seven entry points, all
`Start*`/`Update*` pairs on one `MonoBehaviour`:

| Entry point | Drives |
| --- | --- |
| `StartLandBattle` / `UpdateLandBattleCycle` | Land battle auto-resolve |
| `StartNavalBattle` / `UpdateNavalBattle` | Fleet actions |
| `StartFortBombardment` / `UpdateFortNavyEngagement` | Naval vs. fort |
| `StartSiegeCombat` / `UpdateSiegeCombat` | Sieges |
| `StartRaiding` / `UpdateRaiding` | Cavalry raids |
| `StartSkirmishing` / `UpdateSkirmishing` | Skirmishes |
| `StartTransportInterception` / `UpdateTransportInterception` | Commerce raiding |

A `type` field selects the mode inside shared helpers (`type == 2` is the
siege/fort branch in `FightUnit`). `StartManualBattleFromAutocalc` lets the
player take over an in-progress autocalc battle — **so both models can apply
to the same engagement in sequence.**

---

## 2. Weapon data: both unit systems are stored, and the layers disagree

`GameVars.Weapon` stores **every dual-unit quantity twice**, as separate
parsed fields:

| Metric field | Imperial field |
| --- | --- |
| `range_m` | `range_yd` |
| `velocity_ms` | `velocity_fts` |
| `projectileweight_kg` | `projectileweight_lb` |
| `weight_powder_kg` | `weight_powder_lb` |
| `tubeweight_kg` | `tubeweight_lb` |
| `weight_kg` | `weight_lb` |
| `length_mtrs` | `length_inch` |
| `penetrationmm` | `penetrationinch` |

> **This resolves the dual-unit open question from
> [07-config-data-model](07-config-data-model.md)**, but not the way it was
> framed. The question was "which unit is serialized?" — the answer is *both*,
> and the real question is **which one the simulation reads.** It is not
> consistent:

| Quantity | Battle layer uses | Campaign / naval / UI uses | Never read |
| --- | --- | --- | --- |
| Range | **`range_m`** — `Regiment` fire range and raycast range | `range_yd` — AI weapon valuation (`AICampaign`), economy weapon value (`Economy`), naval range checks (`Vessel`), `GameVars` fort range, `WeaponList` / `BattleUI` display | |
| Velocity | **`velocity_ms`** — `Projectile.Initialize` | | `velocity_fts` |
| Projectile weight | **`projectileweight_kg`** — `Projectile.Initialize` | | `projectileweight_lb` |
| Penetration | | **`penetrationinch`** — `Vessel`, `GameVars` armour checks | `penetrationmm` |
| Powder / tube / barrel weights, length | | at most UI | mostly unread |

**The battle layer is metric. The campaign, naval and UI layers are
imperial. Nothing keeps the pairs in sync.**

> **The shipped pairs are nevertheless exactly consistent**, which bounds the
> hazard: it is created by editing, not present in the base game. All 87
> records in `Config/weapons.txt` were checked on all eight imperial/metric
> pairs (record stride **53** lines; `range_yd` at offset 6, `range_m` at 7)
> and every pair converts to the last stored digit.
>
> One exception, and it is a naming defect rather than a data one:
> **`weight_powder_kg` holds grams.** Its values are exactly
> `weight_powder_lb × 453.59237` across all 87 weapons. Both powder fields are
> at most UI, so nothing in the game is wrong — but a modder editing the field
> as kilograms is out by a factor of 1,000.

> **Consequence for modders, and it is a trap.** Editing `range_yd` changes
> what the AI thinks a weapon is worth, what the economy prices it at, how far
> ships engage, and what the UI displays — but **does not change how far a unit
> shoots in a land battle.** Editing `range_m` does the opposite. A weapon mod
> that touches one and not the other produces a game whose UI lies about its
> own simulation.
>
> **This is a strong case for the ModApi exposing a single logical `Range`**
> that writes both fields, with unit conversion handled once, correctly.

Battle ranges are divided by `GamePrefs.battlefieldrealdimensionfactor` to
convert real metres into Unity world units — so the battlefield is to scale,
by a config-defined factor.

#### Two defects in the weapon panel's own arithmetic

Both live in the code that renders a weapon's stats, and both are visible to
any player who opens a unit panel.

**The weight is always 2 lb light.** `BattleUI.cs:1956` builds the weapon-icon
tooltip with

```csharp
"Weight: " + (GameVars.weapon[num].weight_lb + GameVars.weapon[num].tubeweight_lb - 1f) + " lb"
```

Exactly one of the two fields is populated in every one of the 87 records —
`tubeweight_lb` for artillery, `weight_lb` for small arms — and the other is
the `-1` sentinel. Cancelling one sentinel needs `+ 1f`; the code subtracts.
So a Springfield rifle-musket (9 lb) is shown as 7, and a 6-pounder field gun
(884 lb tube) as 882.

**The accuracy label ladder has lost a rung and kept its clamp.**
`Weapon.GetAccuracyText()` (`GameVars.cs:5185`) is

```csharp
int num = Mathf.FloorToInt((accuracy - 0.8f) * 10f);
if (num < 0) num = 0;
if (num > 5) num = 5;
return weaponaccuracytext[num];          // string[5]
```

`weaponaccuracytext` holds **five** entries — `{ "Poor", "Mediocre", "Good",
"Very Good", "Excellent" }` — so the clamp at `> 5` admits index 5 and
`accuracy >= 1.4` throws `IndexOutOfRangeException`. The clamp is correct for
a **six**-element array, which is what the official manual's own accuracy
vocabulary implies: it uses a six-grade scale beginning *"Very Poor"*, and its
word for every one of the five shipped accuracy values is exactly what a
six-entry array with "Very Poor" at index 0 would return. The shipped array
appears to have lost that first rung without the clamp being retightened.

Float truncation then collides two of the remaining rungs. In float32,
`(0.9f - 0.8f) * 10f` is `0.99999964`, which floors to **0**, and
`(1.0f - 0.8f) * 10f` is `1.9999999`, which floors to **1**:

| `accuracy` | Index | Label shown |
| --- | --- | --- |
| 0.8 | 0 | Poor |
| 0.9 | 0 | Poor |
| 1.0 | 1 | Mediocre |
| 1.1 | 3 | Very Good |
| 1.2 | 4 | Excellent |
| 1.3 | 4 | Excellent |
| 1.4+ | 5 | **throws** |

So **0.8 and 0.9 share a label, and "Good" — index 2 — is never shown by any
shipped weapon.** Two live consumers display this: `WeaponList.cs:244` (the
campaign weapons panel) and `BattleUI.cs:1956` (the battle tooltip).
A modder raising any weapon's accuracy to 1.4 crashes both.

### Other `Weapon` fields

`firepower[3]`, `accuracy`, `reloadspeed[3]`, `roundsperminute`,
`roundsperminute_pract`, `caliber`, `melee`, `meleemounted`, `bayonet`,
`smoke`, `ammousage`, `weightfactor`, `complexity`, `startfireangle`,
`trajectorypeak`, `isari`, `horseari`, `wclass`, `year`, `restriction`.

### `firepower[3]` is indexed by ammunition type — resolved

| Index | Ammunition | Evidence |
| --- | --- | --- |
| **0** | **Solid shot** | Selected as the fallback, and sets `bouncing = true` — ricochet fire. Also the only index non-artillery ever uses. |
| **1** | **Explosive shell** | Selection is gated on `ammo[1]` and governed by the `prob_explosives_*` prefs; `ammotype == 1` activates `explosivesobj`, explosion smoke and an impact sound; `cancelbombardmentbelowammoexplosives` tests `ammo[1]`. |
| **2** | **Canister** | Selected when `distance / firerange <= GamePrefs.canisterrange`; spawns **three** projectiles at `startfireangle` and ±15°. |

**Only artillery (`unittyp == 2`) ever selects a non-zero index.** Everything
else sets the ammo index to 0 unconditionally, so for infantry and cavalry
**both combat models read the same `firepower[0]`.**

Artillery ammunition selection in `FireBullet`, in order:

1. **Canister** if the target is within `canisterrange × firerange` and
   `ammo[2]` remains.
2. Otherwise **shell**, with probability from the situation:
   `prob_explosives_openfield` by default, `prob_explosives_counterbattery`
   (combat behaviour 8), `prob_explosives_bombardment` (behaviour 9), or
   `prob_explosives_behindcover` if the target has `covervalue > 0`, plus
   `prob_explosives_largedistances` when past half of fire range.
3. Otherwise **solid shot**, with bouncing enabled.

`weapon.smoke == 3` forces solid shot with no bounce and no visible
projectile — a special weapon class worth identifying.

> **The official manual names the three bands in the same order**, in its
> artillery table legend: *"Firepower: The theoretical lethality of one round,
> when fired at a unit. Solid shot (/bolt) / Explosive rounds (shell, case,
> shrapnel) / Canister (/grape)."* An independent confirmation of the index
> mapping above.
>
> The shipped values are also strikingly regular: **49 of the 51 artillery
> records have `firepower[2]` exactly `1.2 × firepower[1]`**. The two
> exceptions are the **6-pounder field gun** (ratio 1.106) and the
> **6-pounder Krupp rifle**, whose canister is *weaker* than its shell
> (17.109 vs 15.406, ratio 0.90) — the only gun in the game for which closing
> to canister range lowers the per-round figure. Both look like unfinished
> edits rather than design, but nothing in the code treats them specially.

> **`weapon.bayonet` is parsed and never read.** Its only occurrence in the
> assembly is the loader line (`GameVars.cs:7002`). Melee comes from
> `weapon.melee` (the strength term, [12](12-melee.md) §2) and the displayed
> hand-weapon category comes from `GetMeleeWeapon()`, which reads
> `infantrymeleeweapon` and `meleeweaponstandardperunittype[]` — never
> `bayonet`. The two fields disagree in the shipped data: **Rebored Muskets**
> carry `bayonet = 0` and `infantrymeleeweapon = 0` (knives) while the manual
> lists them as bayonet-equipped, and three records (Mississippi,
> Fayetteville, Whitworth) carry `infantrymeleeweapon = 2`, the sword bayonet.
>
> `GetMeleeWeapon()` also discards `infantrymeleeweapon` whenever the weapon's
> `restriction` string contains `1`, returning
> `meleeweaponstandardperunittype[1]` = 3 ("sabres and revolvers") instead.
> The Henry Rifle and the Spencer Repeating Rifle have `restriction = "01"`,
> so an **infantry** regiment carrying either is described as armed with
> sabres and revolvers, and the Spencer's own `infantrymeleeweapon = 1`
> (bayonet) is never shown.

#### How auto-resolve lands on the same axis

`Autocalc` indexes the same array with `fightingdistance`, which is **not a
measured distance**: `CalculateStance()` derives it from the two sides' stance
matchup. Stances come from force balance, scaled by commander initiative:

| Stance | Meaning | Trigger pref |
| --- | --- | --- |
| 0 | Assault / flanking | `forcebalancetriggerassaultflanking` (highest) |
| 1 | Attack | `forcebalancetriggerattack` |
| 2 | Defend | `forcebalancetriggerdefend` (lowest) |
| 3 | Retreat | `triggerforretreat`, via `0.5 + enemyPoints − ownPoints` |

In sieges (`type == 2`) the fort holder is forced to stance 2 and an attacker
with `campaignstance == 0` to stance 0.

The resulting `fightingdistance` matrix (rows = side 0, columns = side 1):

| | 0 assault | 1 attack | 2 defend | 3 retreat |
| --- | --- | --- | --- | --- |
| **0 assault** | 2 | 2 | 2 | 1 |
| **1 attack** | 2 | 1 | 1 | 0 |
| **2 defend** | 2 | 1 | 0 | 0 |
| **3 retreat** | 1 | 0 | 0 | 0 |

**If either side assaults, the band is 2; two defenders or anyone retreating
gives 0.** So `fightingdistance` runs **2 = close, 0 = distant** — the same
direction as the ammunition axis, since canister is close-range and solid shot
is long-range ricochet fire.

> **So the array has one meaning, read two ways.** It is ammunition type;
> auto-resolve picks the index that the manual model *would* have selected at
> that engagement closeness. The two paths therefore agree about *which* value
> to read. They still disagree about how to use it — linear in `Projectile`,
> square-rooted in `FightUnit` — which is the real source of divergence.
>
> **For modders:** `firepower[0]` is the only entry that affects infantry at
> all, in either model. Entries 1 and 2 are artillery-only.

`randomcasualtiesfactordistance[fightingdistance]` also varies the casualty
randomness by closeness, on the same 0–2 band.

> **`reloadspeed[3]` is dead data.** It is parsed into the `Weapon` class and
> **never read anywhere** — the only reload-related values in use are the
> `GamePrefs.perk_*reloadspeed` arrays. Same status as `velocity_fts` and
> `projectileweight_lb`.

---

## 3. Manual battle: the firing model

### 3a. Accuracy — a normalized weighted average

`Regiment.FireBullet`, ~line 9542:

```
accuracy = ( weapon.accuracy
           + experienceinfluenceonaccuracy * Experience()
           + moraleinfluenceonaccuracy     * morale )
         / ( 1 + experienceinfluenceonaccuracy + moraleinfluenceonaccuracy )
```

A true weighted mean: the denominator normalizes, so the three `GamePrefs`
weights set *relative* importance rather than magnitude. Raising
`experienceinfluenceonaccuracy` makes experience matter more **and** dilutes
the weapon's own accuracy — they are not independent knobs.

Then a cascade of modifiers, in source order:

| Modifier | `GamePrefs` key | Form |
| --- | --- | --- |
| Artillery zeroing-in on a repeated target | `zeroeingeffectari[numberofshotsonsametarget]` | divisor — array indexed by consecutive shots |
| Gun condition | `conditionofgunsinfluenceonaccuracy` | `/= 1 + (1 - conditionguns) * pref` |
| Commander from the correct branch | `increaseofaccuracywhencorrectbranch` | multiplier |
| DLC effect 29 | `DLC_WL.EffectMultiplier(29, 1, …)` | multiplier |
| Fatigue | `fatigueinfluenceonaccuracy` | `-= accuracy * pref * fatigue` |
| Skirmishing | `skirmishingaccuracyinfluencefactor` | multiplier |
| Perk 15 counter-battery | `perk_skilledlayingcounterbatteryaccuracy[level]` | multiplier |
| Perk 16 grand battery | `perk_grandbatterybombardmentaccuracy[level]` | multiplier |
| Perk 1 sharpshooters | `perk_sharpshooters_accuracyfactor[level]` | multiplier |
| Perk 11 stormers | `perk_stormersaccuracyfactor[level]` | multiplier |

**Perk effects are `GamePrefs` arrays indexed by perk level** — so perk
*magnitudes* are data-tunable even though which perk does what is hardcoded.
That is the same split found for project effects in
[09](09-policy-and-project-effects.md).

### 3b. Firing gates

`FireBullet` returns `false` early — i.e. does not fire — when: the unit is
artillery outside combat behaviours 7/8/9; artillery is in a bombardment
cooldown (`bombardmenttime > GameVars.currenttimefromstart`); the battle is in
an end-of-day cycle (`bunits.eodcycle > 0`); the sprite is in rank 2 or deeper
(`sprite[spriteid].lineposition >= 2`); the unit is in melee (`inmelee > 0`);
or the unit is moving in certain path states.

> **Only the first two ranks fire.** `lineposition >= 2` is a hard gate, which
> is why formation depth matters mechanically and not just visually.

Combat behaviours seen: 0–2 use `GetFireRange(calcusedfirerange: true)`; 7 is
normal artillery fire, 8 is canister-hold (switching to 7 once the target is
within `firerange * GamePrefs.canisterrange`), 9 is bombardment.

### 3c. Projectiles are actually simulated

`Projectile.Initialize(cam, firingUnit, targetUnit, startPos, endPos,
accuracy, velocity_ms, trajectorypeak / firerange, ammotype, startfireangle,
showprojectile, bouncing, projectileweight_kg, firepower[], insideBuilding)`

The projectile then flies: `UpdateBulletPosition`, `SetTargetNoise` (accuracy
becomes aim scatter), `GetTrajectoryHits` and `GetExplosivesHits` (raycasts),
`CheckHitsOnWay`, `AdjustEndPath`. It supports bouncing and can hit things en
route — **so cover and intervening units are real, not modelled abstractly.**

Ammo type 2 spawns **three** projectiles, at `startfireangle` and ±15°.

`Projectile` reads **no** `GameVars.weapon` fields itself; everything arrives
through `Initialize`. That is a clean seam: a Harmony prefix on `Initialize`
can rewrite any ballistic input without touching the weapon table.

### 3d. Impact damage

`Projectile.DestroyBullet`, per hit object:

```
casualties = Random( firepowerminofmaxcasualties * firepower[ammotype],
                     firepower[ammotype] )
           * generalcasualtiesadjustment
           × generalcasualtiesadjustmentartillery   (if firer is artillery)
           × perk_closesupportweaponstrength        (if artillery + canister + perk 14)
           × lossfactorlayndown                     (if target is prone)
           × lossfactorskirmishing                  (if target formation == 1)
→ Tools.TrimToInt → SufferCasualties(block: <hit block>, …)
```

`firepowerminofmaxcasualties` is the **lower bound as a fraction of
firepower**, so each hit rolls uniformly in
`[min·firepower, firepower]`. Damage is applied to the **specific block** that
was hit, not the unit as a whole.

### 3e. The accuracy floor — the cascade is inert at or below 1.0

`num8` — the weighted mean of 3a and its ten modifiers — leaves `FireBullet`
by exactly one route: the `_accuracy` argument of `Projectile.Initialize`
(`Regiment.cs:9693`, and 9697/9699 for the two extra canister projectiles).
Inside `Projectile` the field is referenced **once**, in `SetTargetNoise`:

```csharp
float num = originaldistance
          / Mathf.Max(accuracy / GamePrefs.overallaccuracynoisefactor,
                      1f      / GamePrefs.overallaccuracynoisefactor);
```

`Mathf.Max(a/k, 1/k)` is `max(a, 1)/k`, so the aim-scatter radius is

```
scatter = distance × overallaccuracynoisefactor / max(accuracy, 1)
```

applied as `Random.Range(-scatter, scatter)` on each axis, scaled by
`targetnoisefactorx` = 0.03 (traverse) and `targetnoisefactorz` = 0.05
(range). Scatter therefore grows linearly with range, which is how distance
degrades fire — there is no distance term in the accuracy formula itself.

> **The `Mathf.Max` floors the divisor at accuracy = 1, and the floor sits at
> 1 whatever `overallaccuracynoisefactor` is.** For any shot whose computed
> accuracy is at or below 1.0 the scatter is `distance × k`, and **nothing in
> the cascade changes it** — not the weapon's accuracy rating, not experience,
> morale or fatigue, not gun condition, not artillery zeroing-in, not the
> mounted penalty, not perks 1, 11, 15 or 16.

How often does the cascade clear 1.0? With the shipped
`experienceinfluenceonaccuracy` = 0.66 and `moraleinfluenceonaccuracy` = 0.25,
and both `experience` and `morale` clamped to `[0, 1]` (`Regiment.cs:16724`,
`Regiment.cs:17824`), the 3a mean is bounded above by
`(1.2 + 0.66 + 0.25) / 1.91 = 1.105`. Solving `num8 > 1` for the weapon's own
rating:

| `weapon.accuracy` | Records | Condition to clear the floor |
| --- | --- | --- |
| 1.2 | 6 | `0.66·E + 0.25·M > 0.71` — a veteran at almost any morale |
| 1.1 | 28 | `> 0.81` — near-maximum experience **and** high morale |
| 1.0 | 38 | `> 0.91`; the maximum attainable is exactly 0.91, so **never** |
| 0.9 | 7 | never |
| 0.8 | 8 | never |

**53 of the 87 weapon records can never clear the floor at any crew quality**,
and 34 of the 53 land weapons are in that group — including every standard
rifle-musket, which is rated 1.0. For those weapons the whole accuracy
pipeline is decoration, and in a manually fought battle the only thing
experience still buys in a firefight is rate of fire (3f). The skirmisher
multiplier (`skirmishingaccuracyinfluencefactor` = 1.1) and the West Point
artillery branch bonus can lift a good unit over the line, so the feature is
not dead everywhere — it is dead for the ordinary case.

The config label states the intent plainly: *"Weighting of overall accuracy
factors for targeting error [>0.1] (a factor of 3 means that accuracy is
decreased by 3x between highest and lowest accuracy factors)"*, shipped `3`.
A 3× spread needs `num8` to range over `[1, 3]`; it ranges over roughly
`[0.2, 1.1]`.

### 3f. Rate of fire in a manual battle

`Regiment.cs:9992`:

```
secondsPerRound = 1 / ( (roundsperminute_pract × (1 − t) + roundsperminute × t) × ammoFactor )
                  / 60 × timetoreloadfactor
```

- `t = clamp(distanceToTarget / firerange, 0, 1)` **for artillery only**; every
  other unit type passes `t = 0`. Combat behaviours 8 and 9 (canister-hold and
  bombardment) force `t = 0`.
- So `roundsperminute_pract` is the **point-blank** rate and
  `roundsperminute` the rate at maximum range, interpolated linearly in
  between. For the 6-pounder field gun that is 3 rounds/min muzzle-to-muzzle
  falling to 0.2 rounds/min at 1,523 yards.
- `roundsperminute` is `-1` for every non-artillery record, which is harmless
  only because `t` is always 0 for them.
- Then `timetoreloadfactor` = 3.75, `experienceinfluenceonreloading` = 1.4
  (inexperienced troops take 40 % longer), `loadingspeedreductioncanister` =
  0.3 inside `canisterrange`, `rofonlowammo` = 0.25 and `rofonammoout` = 0.1, and
  `increaseofrofwhencorrectbranch` for a West Point artillerist.

> **Fatigue does not slow reloading.** The line is there —
> `num6 += num6 * fatigue * GamePrefs.fatigueinfluenceonreloading` at
> `Regiment.cs:9998` — but `fatigueinfluenceonreloading` is **declared, read
> once, and never assigned anywhere in the assembly**, so it is permanently
> `0f`. `unitprefs.txt` does ship the value the line wants — *"Fatigue effect
> on reloading time for exhausted troops (linear interpolation), how much
> longer do exhausted troops need to reload? [1-x]"* = 1.25 — but the loader
> parses it into `fatigueforreloading`, which is then **overwritten later in
> the same block** by *"Increase of fatigue when reloading in game hrs"* =
> 1.00 (`GamePrefs.cs:5379` and `GamePrefs.cs:5395`). Two small wiring faults
> compose into one missing mechanic.
>
> See [07](07-config-data-model.md) for the sweep that shows this is the only
> gameplay pref in all 1,972 that is read but never loaded.

---

## 4. Auto-resolve: the statistical model

### 4a. Rate of fire

`Autocalc.GetROF(unit0)`:

```
ROF = GamePrefs.rofstancepairs[ usedstance[0], usedstance[1] ]
    × (rofonlowammo if ammo below ammoalertlevel)
    × (increaseofrofwhencorrectbranch if artillery + commander branch 2 + status 0)
    × GamePrefs.rofadjustmentweather[ currentweather ]
```

> **`rofstancepairs` is a 2D matrix keyed on both sides' stances.** That is the
> whole tactical interaction model in auto-resolve: a single config table
> decides how stance matchups affect volume of fire.

Artillery ammo check sums all three ammo types and divides by 3; everything
else checks `ammo[0]` only.

### 4b. Casualties

`Autocalc.FightUnit`, ~line 3144:

```
casualties = sqrt( unit1.strength + unit0.strength )
           * GamePrefs.casualtiesfactorautocalcgeneral
           * ROF
           * timediff * 365 * 24                                  // game-years → hours
           * ( 1 - Random(0, randomcasualtiesfactordistance[fightingdistance]) )
           * fatigueFactor * cohesionFactor * experienceFactor
           * cavalryCunningFactor * firepowerFactor
           * sharpshooterFactor * engineeringFactor
clamp to [0, unit1.strength]
           * casualtymalusforflankmovement                        (if flanking)
```

**Strength enters as a square root of the *sum of both sides*** — so casualty
volume scales sub-linearly with engagement size, and a unit's own size
increases the casualties it inflicts.

`timediff * 365 * 24` converts the game-time delta (in years, consistent with
`Tools.Date.yearfraction`) into hours. This is a **rate-correct** model, which
matters given there is no fixed timestep
([10-simulation-tick](10-simulation-tick.md)).

The influence factors all use one idiom:

```
factor = (GamePrefs.<influence> - 1) * <stat 0..1> + 1
```

| Factor | Pref | Stat |
| --- | --- | --- |
| Experience | `expinfluenceonenemycasautocalc` | `unit0.Experience()` |
| Cohesion | `cohesioninfluenceonenemycasautocalc` | `1 - unit0.cohesion` |
| Fatigue | `fatigueinfluenceonenemycasautocalc` | `unit0.fatigue` |
| Cavalry cunning (defender is cavalry) | `cavalrycunninginfluenceowncasautocalc` | commander cunning |

> **Read this idiom before changing any of these prefs.** The pref is the
> multiplier *at stat = 1*, and the factor is exactly 1.0 at stat = 0. A pref
> **below** 1 inverts the sense of the stat. So whether low cohesion helps or
> hurts depends on the sign of `pref - 1`, not on the code.

`firepowerFactor` is `sqrt(weapon.firepower[band])` — **square-rooted**, so
firepower has diminishing returns in auto-resolve while it is linear in manual
battles (§3d). That alone guarantees the two paths diverge under any
non-trivial firepower edit.

Siege (`type == 2`) replaces the weapon firepower entirely with
`EnvolvedUnits.GetFortWeaponStrength(unit0)` and multiplies by
`siegestrength`. Embarked units (terrain 3/4/5/8) take
`fightingstrengthembarked`, or `fightingstrengthembarkedriverexp` with perk 10.

Night-time is detected per month:
`uniStormSystem.hourCounter > GamePrefs.dayendhours[month-1]` or
`< GamePrefs.daystarthours[month-1]` — **daylight hours are month-indexed
config arrays**, and perk 15 multiplies combat efficiency at night.

### 4c. Ammo and supply drain

Per `FightUnit` call, for the firer:

```
ammo[t] -= timediff * ammoconsumptionperyear / standardroundsperunittyp[unittyp, t] * 50
supplyinstock[s] -= strength / standardroundsperunittyp[unittyp, t]
                    * timediff * ammoconsumptionperyear * 50
```

Artillery picks `t = Random(0,3)` and drains `supplyinstock[1]`; everything
else uses `t = 0` and `supplyinstock[0]`. The `* 50` is an unexplained magic
factor present in both branches.

The manual path drains ammo differently again — via
`standardroundsperunittyp[unittyp, ammotype] * guns` (or `sprites -
secondarysprites`) divided by `generalcasualtiesadjustment*` and
`ammoconsumptionfactor*`. **Same resource, two unrelated consumption
formulas.**

---

## 5. Morale and the rout threshold

`FightUnit` accumulates a morale hit and passes it to `SufferMoraleHit`:

| Source | Pref |
| --- | --- |
| Casualties taken | `moralehitpercasualty * casualties` |
| Being flanked | `× moralehitflankingautocalc` |
| Ammo below alert level (first time) | `+ moraleeffectlowammo * 0.5` |
| Out of ammo (first time) | `+ moraleeffectoutofammo * 0.5` |
| Fatigue above `fatiguealertlevel`, without perk 1 | `+ moraleeffectonhighfatigue * timediff` |
| Cohesion below `cohesionalertlevel` | `+ moraleeffectcohesionbelowalertlevel * timediff` |

The low-ammo and out-of-ammo hits are **latched** through
`GetAmmoInfo(unit, set)`, which doubles as a per-unit state store (0 → 1 → 2)
so each threshold fires once rather than every tick.

### The rout condition

```
lossRatio = (SumUp(unit.losses) - unit.priorwounded)
          / max(1, that + unit.strength)

if lossRatio > unit.GetUnbearableLossesTrigger():
    unit.isrouted = true
    unit.morale   = 0
```

`GetUnbearableLossesTrigger()` is per-unit, and the check is applied twice —
once on the unit alone, then again including a `detachedregiment`'s strength,
so a detachment can save its parent from routing. `priorwounded` is a snapshot
of `losses[0]` taken when the engagement starts, which **excludes
pre-existing wounded from the rout calculation** — units don't rout on arrival
because they came in damaged.

---

## 6. The shared sink: `Regiment.SufferCasualties`

```csharp
public int SufferCasualties(
    float amount, bool userelativevalues = false, int block = -1,
    GameObject unitfired = null, GameObject unitmelee = null,
    Vector3 sourcepositionoffire = default, int usedammo = -1,
    int overridelosstype = -1, Autocalc calledfromautocalc = null,
    bool showmessage = true)
```

Applied before the branch, so it affects **both** paths:

- **Perk 5 (deadly volley):** `amount *= perk_deadlyvolleylethalityfactor[level]`
  — but only when `!calledfromcampaign`.
- **Difficulty:** `if (GameVars.playeralliance != alliance) amount /= 1 + GameVars.casualtiesmodifier;`

  > The modifier applies **only to the non-player side**, as a divisor. A
  > positive `casualtiesmodifier` means the enemy takes *fewer* casualties.
  > Note it keys on `playeralliance`, so in any context where that is not the
  > human's side the asymmetry silently flips.

Then it diverges:

- **Autocalc branch:** aggregate `strengthloss`, then walk up the command
  hierarchy (≤10 levels) calling `UpdateCommanderCasualtyState` with a
  `1 / depth³` falloff, and only for ancestors with an enemy within 75 world
  units. So **command-level shock propagates upward with distance-gated
  cubic decay.**
- **Manual branch:** resolves which `block` took the hit, falling back to
  `GetBlockWithHighestSprites()` when the index is invalid, and refuses to
  apply anything to a unit that is `markedforrout && !isrouted`.

### Loss types

`losses` is `new int[6]`. Confirmed from consumption sites:

| Index | Meaning | Evidence |
| --- | --- | --- |
| 0 | **Wounded** (recoverable) | `groupwounded += losses[0]`; counted into establishment strength as `strength + losses[0] + sick`; `sufferedwoundedperunittype`; `priorwounded` snapshots it |
| 1 | **Killed** | `inflcasualtiesonstatesupport * losses[1]` — KIA drives state support loss |
| 2 | **Deserters / missing** | `GameVars.nation[state].deserters += losses[2]` |
| 3 | **Prisoners** | consistently excluded from own-casualty sums (`SumUp(losses) - losses[3]`) and tallied separately |
| 4, 5 | **unknown** | declared but no consumption site found |

`BattleUnits` uses `overridelosstype` 2 and 3 with
`GamePrefs.relationmiavspowwhenretreating` to split retreat losses between
missing and captured.

> Wounded counting toward establishment strength is why reinforcement maths
> uses `strength + losses[0] + sick` throughout — a unit's "paper" strength
> includes its hospital cases.

### Commander casualties ride on the same call

`SufferCasualties` also decides whether the unit's commander is hit, via
`UpdateCommanderCasualtyState(strengthloss, unitmelee, …)`. The roll is

```
P = strengthloss / max(1, strength + strengthloss) × pref × probabilityadjustment
```

with `pref` one of `probabilitybrigadecommanderdeadfirefight` = 0.04,
`...woundedfirefight` = 0.2, `...deadmelee` = 0.04, `...woundedmelee` = 0.2,
`probabilitybrigadecommandersurrendering` = 0.275 (melee while routed). The
loss ratio makes the model rate-correct, like the casualty formulas.

Three things worth knowing:

- **The wounded roll runs after the dead roll and overwrites it.** Both are
  independent `Random.Range` draws; if both succeed the commander ends up
  `status = 3` (wounded), not `4` (dead). Effective death probability is
  therefore `P_dead × (1 − P_wounded)`, and a commander can never be killed on
  a tick where he is also rolled wounded.
- **Superior commanders are only exposed in auto-resolve.** The loop that
  walks `GetComponentInParent<Regiment>()` up to ten levels and rolls for each
  HQ within 75 units of its nearest known enemy sits inside
  `if ((bool)calledfromautocalc)` (`Regiment.cs:16257-16270`), with
  `probabilityadjustment = 1 / level³ / max(1, groupstrengthactive)`. In a
  battle the player fights himself, **only the commander of the unit taking
  the casualties can be hit** — an army or corps commander cannot become a
  casualty at all.
- **In auto-resolve the unit's own commander is rolled twice** per casualty
  application: once at `Regiment.cs:16255` inside the autocalc branch and
  again at `Regiment.cs:16457` after it, which is reached on both paths.
  Roughly double the per-loss risk of a manually fought battle.

`probabilitycommanderdeadfirefight` = 0.005 and
`probabilitycommanderwoundedfirefight` = 0.02 — the two prefs labelled
*"Probability of commander dead/wounded if hit on group was detected"* — are
**parsed and never read**; the group path above uses the brigade prefs scaled
by the cube law instead. `unittyp` 3, 4 and 5 (skirmishers, scouts, couriers)
and any unit with a `parentregiment` return early and never lose a commander.

---

## 7. Where the constants live

Every combat constant found is in `GamePrefs`, i.e. in `Config/` or a scenario
override ([05-config-paths-and-overrides](05-config-paths-and-overrides.md)) —
**no combat magic numbers were found hardcoded in the formulas**, with the
exception of the `* 50` ammo factor, the `±15°` canister spread, the `75`-unit
command-shock radius and the `sqrt`/`cubic` shapes themselves.

Manual path: `experienceinfluenceonaccuracy`, `moraleinfluenceonaccuracy`,
`fatigueinfluenceonaccuracy`, `conditionofgunsinfluenceonaccuracy`,
`zeroeingeffectari[]`, `increaseofaccuracywhencorrectbranch`,
`skirmishingaccuracyinfluencefactor`, `canisterrange`,
`firepowerminofmaxcasualties`, `generalcasualtiesadjustment`,
`generalcasualtiesadjustmentartillery`, `lossfactorlayndown`,
`lossfactorskirmishing`, `ammoconsumptionfactor`, `ammoconsumptionfactorart`,
`cancelbombardmentbelowammoexplosives`, `battlefieldrealdimensionfactor`,
`perk_*`.

Autocalc path: `rofstancepairs[,]`, `rofonlowammo`,
`rofadjustmentweather[]`, `increaseofrofwhencorrectbranch`,
`casualtiesfactorautocalcgeneral`, `randomcasualtiesfactordistance[]`,
`expinfluenceonenemycasautocalc`, `cohesioninfluenceonenemycasautocalc`,
`fatigueinfluenceonenemycasautocalc`,
`cavalrycunninginfluenceowncasautocalc`, `casualtymalusforflankmovement`,
`sharpshooterperkinfluenceautocalc[]`, `siegeassaultsappersandminers[]`,
`siegeassaultefficiencyriverexpedition[]`, `skirmishlandtorpedoes[]`,
`combatefficiencynighttimelimelights[]`, `fightingstrengthembarked`,
`fightingstrengthembarkedriverexp`, `fortdamageattackersize[]`,
`aiperkexpincreaseautocalc`, `daystarthours[]`, `dayendhours[]`.

Shared: `ammoalertlevel`, `fatiguealertlevel`, `cohesionalertlevel`,
`moralehitpercasualty`, `moraleeffectlowammo`, `moraleeffectoutofammo`,
`moraleeffectonhighfatigue`, `moraleeffectcohesionbelowalertlevel`,
`standardroundsperunittyp[,]`, `ammoconsumptionperyear`,
`relationmiavspowwhenretreating`, `perk_deadlyvolleylethalityfactor[]`.

> **Almost everything is tunable without code.** That is good news for
> balance mods and it sharpens what the hook library is actually *for*: not
> changing numbers, but changing **structure** — new weapon behaviours, new
> perks, effects that no pref expresses.

---

## 8. Open questions

1. **`losses[4]` and `losses[5]`** — declared, never consumed as far as found.
   Possibly sick-to-dead transitions or a save-format remnant.
2. ~~**`GameVars.casualtiesmodifier` provenance**~~ — **answered.** It is set
   once in `MainMenu` from the campaign-start **bonus** slider:
   `casualtiesmodifier = bonus × GamePrefs.casualtiesmodifierbonus`, where
   `bonus` ranges over `[minstartbonus, maxstartbonus]` = **[0, 0.5]** from
   `campaignprefs.txt`. The same slider sets `usedcampaignbonus` and
   `usedcampaignbonusrunning`, the AI handicap multipliers. See
   [20-ai-campaign](20-ai-campaign.md) §9.
3. **Does `Autocalc` ever run during a manual battle?**
   `StartManualBattleFromAutocalc` goes one way; whether the reverse happens
   (autocalc resolving a detached skirmish while the player fights elsewhere)
   would mean both models are live simultaneously.
4. ~~**Cohesion's update rule.**~~ **Traced** —
   [13-morale-and-cohesion](13-morale-and-cohesion.md) §8, including the
   finding that fire never disrupts it.
5. **`weapon.smoke == 3`** forces solid shot with no visible projectile.
   Identifying that weapon class would explain the special case.
6. **`forcebalance[]` and `points[]`** feed `CalculateStance` but their own
   derivation is untraced — they decide every auto-resolve stance and
   therefore the whole ROF matrix lookup.
7. **Whether the accuracy floor (3e) is intentional.** The `Mathf.Max` reads
   like a divide-by-zero guard that was given the wrong bound: a guard needs
   only a small epsilon, while `1f / k` sets the floor at accuracy 1.0 —
   above almost every value the cascade can produce. The config label argues
   for a 1-to-3 working range that the data never reaches. Settling it needs
   the developers, or a build where the floor is lowered and the difference
   observed.
8. **Which shipped weapons can actually clear the floor in play.** 3e bounds
   it analytically; a logged battle would show how often real units cross 1.0,
   and therefore whether the accuracy stat is worth anything to a player.

### Resolved since first writing

- ~~`firepower[3]` indexing — ammo type vs. distance band.~~ **Resolved** in
  §2: it is ammunition type (0 solid shot, 1 shell, 2 canister), and
  auto-resolve's `fightingdistance` selects the index the manual model would
  have chosen at that closeness. Non-artillery always uses index 0 in both
  models.
- ~~`fightingdistance` value set.~~ **Resolved** — 0–2, derived from the
  stance matchup, with 2 = close.
- ~~Stance enumeration.~~ **Resolved** — 0 assault, 1 attack, 2 defend,
  3 retreat, from force-balance thresholds scaled by commander initiative.
- ~~Melee.~~ **Done** — see [12-melee](12-melee.md).
