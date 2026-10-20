# Summary — Combat resolution

**Covers:** [11-combat-resolution](11-combat-resolution.md),
[12-melee](12-melee.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

There are **three combat models**, not one. Manual battles simulate real
projectiles — `Regiment.FireBullet` computes a per-shot accuracy, spawns a
`Projectile` that flies, raycasts and can hit things en route, and applies
damage to the specific block it struck. Auto-resolve (`Autocalc.FightUnit`)
never spawns anything: it computes casualties in closed form from a square root
of combined strength, a stance matrix and multiplicative stat influences.
**Melee is a third model again** — block-to-block, purely differential, and
absent from `Autocalc` entirely, so it only exists in manually fought battles.
All three converge on one casualty sink, `Regiment.SufferCasualties`, and each
uses a **different tuning idiom**, so a combat mod has to touch all three or
they will disagree about the same engagement.

## The answer to the long-standing question

`Autocalc` does **not** share the combat model. It is also much more than
auto-resolve: one `MonoBehaviour` with seven `Start*`/`Update*` entry points
covering land auto-resolve, naval battles, fort bombardment, sieges, raids,
skirmishes and transport interception.

| | Manual battle | Auto-resolve |
| --- | --- | --- |
| Damage generation | Simulated projectiles, per block | Closed-form statistics, per unit |
| Weapon range | `range_m` | — |
| Velocity, projectile weight | `velocity_ms`, `projectileweight_kg` | — |
| Firepower | `firepower[ammotype]`, **linear** | `sqrt(firepower[band])`, **diminishing** |
| Rate of fire | implicit in reload/sprites | `rofstancepairs[s0, s1]` matrix |
| Accuracy | normalized weighted mean + 10 modifiers | not modelled |
| Ammo drain | `standardroundsperunittyp × guns / adjustments` | `ammoconsumptionperyear × 50` |
| Casualty sink | `Regiment.SufferCasualties` | `Regiment.SufferCasualties` |

> **Firepower is linear in one model and square-rooted in the other.** That
> alone guarantees divergence under any non-trivial weapon edit, independent of
> everything else.

## Melee: the third model

Melee lives entirely in `Regiment` — **`Autocalc` has no melee concept**, so an
auto-resolved battle never has one. It is block-to-block, tracked as
`blockobj[i].meleecounterpart`, entered on physical contact, made mutual by
`CheckEnemyForMeleeState`, and exited on distance.

`CalculateMeleeCasualties(attacker, defender, …)` is called from exactly one
site, always with `defender == this`: **each unit computes the casualties it
suffers**, per block pairing, with each side's strength scaled by its
per-block share.

Every stat term is **differential** — `1 + (attackerStat − defenderStat) ×
influence` — for weapon melee value, experience, fatigue and cohesion.

> **Only the gap matters.** Two veteran units fight like two green ones.

The exceptions are the strength term (asymmetric, raised to
`meleeinfluence_strength`, so local superiority compounds), the formation term
(a raw difference of `meleeformationmodifier[]` with no pref multiplier), a
cavalry-versus-formation bonus, and **speed shock** — a charging attacker's
actual movement speed becomes a multiplier.

Two mechanics worth knowing:

- **`inmelee` is a fraction, not a flag** — the share of blocks in contact. Any
  block in contact stops the whole unit firing.
- **A routed unit's melee losses become prisoners** (`overridelosstype = 3`),
  and a routed attacker inflicts nothing at all.

### `meleestrength` is a HUD number, not the model

`CalculateMeleeStrength()` has exactly **one consumer in the assembly**:
`BattleUI.UpdateStats2`. It is not read by melee resolution.

It applies experience, fatigue and cohesion as *multipliers on one unit* where
the casualty formula uses *differences*, and it includes
`weapon.meleemounted` plus three perk factors (4 spirited charge, 9 cold steel,
14 close support) that **the casualty formula never references**. Perks 4 and 9
do affect melee, but through morale and perk XP rather than casualties; perk
14's melee factor has no effect found outside the display.

> **The ModApi must not expose `meleestrength` as "the" melee strength.**

## The trap worth knowing before anything else

`GameVars.Weapon` stores **every dual-unit quantity twice** — `range_m` *and*
`range_yd`, `velocity_ms` *and* `velocity_fts`, and so on. This resolves the
dual-unit open question, but not as asked: both are serialized, and the layers
read different ones.

- **Battle layer is metric:** `range_m`, `velocity_ms`, `projectileweight_kg`.
- **Campaign, naval and UI layers are imperial:** `range_yd` (AI weapon
  valuation, economy pricing, naval engagement range, weapon list display),
  `penetrationinch`.
- **Several imperial fields are parsed and never read at all:**
  `velocity_fts`, `projectileweight_lb`, the powder and barrel weights.

**Nothing keeps the pairs in sync.** Edit `range_yd` and you change what the AI
thinks a weapon is worth, what it costs, how far ships engage and what the UI
says — but not how far a unit shoots in a land battle. Edit `range_m` and you
get the opposite.

> **Strong case for the ModApi exposing one logical `Range`** that writes both
> fields with the conversion handled once.

## Mechanics worth remembering

- **Only the first two ranks fire.** `sprite[spriteid].lineposition >= 2` is a
  hard gate in `FireBullet`, so formation depth is mechanical, not cosmetic.
- **Rout is a loss-ratio threshold**, not a morale check:
  `(SumUp(losses) - priorwounded) / (that + strength) > GetUnbearableLossesTrigger()`
  sets `isrouted` and zeroes morale. `priorwounded` snapshots pre-existing
  wounded at engagement start, so units don't rout on arrival for old damage.
  The check runs twice — a `detachedregiment`'s strength can save its parent.
- **Loss types** (`losses[6]`): 0 wounded (recoverable, counts toward
  establishment strength), 1 killed (drives state support loss), 2 deserters,
  3 prisoners (excluded from own-casualty sums). 4 and 5 are declared but
  unused.
- **Command shock propagates upward** in auto-resolve: up to 10 hierarchy
  levels with `1/depth³` falloff, gated on an enemy within 75 world units.
- **Difficulty is one-sided.** `SufferCasualties` divides by
  `1 + GameVars.casualtiesmodifier` only when `playeralliance != alliance`, so
  a positive modifier means the enemy takes *fewer* casualties.
- **Daylight is month-indexed** (`daystarthours[]` / `dayendhours[]`), and
  night has its own perk multiplier.
- Canister fires **three** projectiles at `startfireangle` and ±15°.

## Three tuning idioms, one per model

Knowing which one you are editing is the difference between a balance change
and a mess.

1. **Normalized weighted mean** — manual accuracy:
   `(weapon.accuracy + wExp·Experience + wMorale·morale) / (1 + wExp + wMorale)`.
   The weights set *relative* importance, so raising one dilutes the weapon's
   own accuracy. They are not independent knobs.
2. **Lerp-from-one** — every autocalc stat influence:
   `factor = (pref − 1) · stat + 1`. The pref is the multiplier at `stat = 1`
   and the factor is exactly 1.0 at `stat = 0`. **A pref below 1 inverts the
   stat's sense** — so whether low cohesion helps or hurts is decided by the
   config value, not by the code.
3. **Differential** — every melee stat term:
   `factor = 1 + (attackerStat − defenderStat) · influence`. Absolute values
   cancel; only the gap matters.

## Perk ids are reused across contexts

Melee exposed this, and it reaches well beyond combat. The same numeric perk id
means different things by unit type and context: perk 4 is both
`perk_spiritedchargemeleefactor` and `intelligencegatheringskilledcartographers`;
perk 9 is both `perk_coldsteelmeleeeffectiveness` and
`supplyspeedpontoontrain`; perk 14 covers melee strength, weapon strength and
reload speed.

> **`HasPerk(n)` is not a global perk registry.** Any ModApi perk API must key
> on *(unit type, perk id)* or it will silently collide, and a perk catalogue
> is now a prerequisite for documenting commanders.

## Four suspected wiring bugs

Recorded in [12-melee](12-melee.md) §5 with evidence, as observations rather
than assertions — each is the kind of thing a modder would misdiagnose as their
own fault:

- `MoveIntoMelee` guards `if (blockId <= 0) return;` where `< 0` was almost
  certainly meant, so contact on a unit's **first block** never registers melee
  through that path.
- `CheckEnemyForMeleeState` uses `Random.Range(0, blockobjectsused - 1)`, whose
  int overload is max-exclusive, so the **last block is never chosen** and a
  single-block unit yields an empty range.
- Melee **speed shock tests the defender's** `isrouted` and movement while
  every other clause reads the attacker — so a stationary defender appears to
  negate the attacker's charge bonus.
- The morale block tests `HasPerk(8) && GetPerkLevel(9) >= 2`, mixing two perk
  indices in one condition.

## What this means for the hook library

**Almost every combat constant is already in `GamePrefs`**, hence in `Config/`
or a scenario override. No combat magic numbers were found inline except the
`×50` ammo factor, the ±15° canister spread, the 75-unit shock radius, and the
`sqrt` / cubic curve shapes themselves. Perk magnitudes are `GamePrefs` arrays
indexed by perk level.

> So balance is already a data-modding problem. **What the hook library adds is
> structural change** — new weapon behaviours, new perks, effects no pref
> expresses — plus the safety rail of keeping the two combat models and the two
> unit systems consistent.

Two clean hook seams found:

- **`Projectile.Initialize`** takes every ballistic input as an argument and
  reads no weapon table itself. A prefix there can rewrite ballistics without
  touching `weapons.txt`.
- **`Regiment.SufferCasualties`** is the single casualty chokepoint for both
  models, with a `calledfromautocalc` discriminator already in the signature.
  One postfix observes all damage in the game.

## `firepower[3]` and stance — resolved

**`firepower[3]` is indexed by ammunition type:** 0 solid shot (the fallback,
and it sets `bouncing`), 1 explosive shell (gated on `ammo[1]`, chosen by the
`prob_explosives_*` prefs, triggers the explosion effects), 2 canister (chosen
inside `canisterrange`, fires three projectiles at ±15°).

**Only artillery ever selects a non-zero index.** Infantry and cavalry set it
to 0 unconditionally — so for them *both* models read the identical
`firepower[0]`.

Auto-resolve indexes the same array with `fightingdistance`, which is **not a
measured distance**: `CalculateStance` derives a 0–2 closeness band from the
two sides' stance matchup, where **2 = close**. Stances are 0 assault,
1 attack, 2 defend, 3 retreat, set by force-balance thresholds scaled by
commander initiative. If either side assaults the band is 2; two defenders or
anyone retreating gives 0.

> So the array has **one meaning read two ways** — auto-resolve picks the index
> the manual model would have chosen at that closeness. The paths agree about
> *which* value to read and still disagree about how to use it (linear vs.
> square-rooted). For modders: `firepower[0]` is the only entry that touches
> infantry at all, and 1 and 2 are artillery-only.

`reloadspeed[3]` turns out to be **dead data** — parsed and never read
anywhere, like `velocity_fts` and `projectileweight_lb`.

## Still open

1. **Cohesion** is an input to all three models but its own update rule is
   untraced — it belongs with morale, the next subsystem.
2. **`forcebalance[]` and `points[]`** decide every auto-resolve stance, and
   therefore the ROF matrix and the firepower index, but their derivation is
   untraced.
3. **Formation and `unittyp` enumerations.** Formation 1 = skirmish,
   4 = anti-cavalry square, and `unittyp` 1 = cavalry, 2 = artillery, `<= 13` =
   land combat unit are confirmed piecemeal. Both deserve a proper map —
   `meleeformationmodifier[]` is indexed by formation and dozens of guards key
   on `unittyp`.
4. **`surrenderstate`** gates melee and is plainly a distinct mass-surrender
   mechanic that has not been examined.
5. **`losses[4]` / `losses[5]`** — declared, no consumer found.
