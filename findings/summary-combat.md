# Summary — Combat resolution

**Covers:** [11-combat-resolution](11-combat-resolution.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

There are **two independent combat models**. Manual battles simulate real
projectiles — `Regiment.FireBullet` computes a per-shot accuracy, spawns a
`Projectile` that flies, raycasts and can hit things en route, and applies
damage to the specific block it struck. Auto-resolve
(`Autocalc.FightUnit`) never spawns anything: it computes casualties in closed
form from a square root of combined strength, a stance matrix and a stack of
multiplicative stat influences. They share exactly one piece of code —
`Regiment.SufferCasualties` — and they read **different weapon fields**. So a
combat mod has to change both, or auto-resolve and manual battles will
disagree about the same engagement.

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

## Two tuning idioms to read before touching prefs

1. **Normalized weighted mean** (manual accuracy):
   `(weapon.accuracy + wExp·Experience + wMorale·morale) / (1 + wExp + wMorale)`.
   The weights set *relative* importance — raising one dilutes the weapon's own
   accuracy. They are not independent knobs.
2. **Lerp-from-one** (every autocalc stat influence):
   `factor = (pref - 1) · stat + 1`. The pref is the multiplier at `stat = 1`
   and the factor is exactly 1.0 at `stat = 0`. **A pref below 1 inverts the
   stat's sense** — so whether low cohesion helps or hurts is decided by the
   config value, not by the code.

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

## Still open

1. **`firepower[3]` indexing** — ammo type (manual) vs. distance band
   (autocalc). Must be settled before `weapons.txt` can be documented.
2. **Melee is not traced.** `MeleeCounterparts`, `inmelee`, `weapon.melee` /
   `meleemounted` all exist; the resolution formula does not yet. Next piece of
   this subsystem.
3. **Stance** — `rofstancepairs` makes it the core of auto-resolve, so the
   stance enumeration (`CalculateStance`, `usedstance[]`) deserves its own
   pass.
4. **`fightingdistance`** — the band index, its value set, and the real
   distances it maps to.
5. **Cohesion** is an input everywhere but its update rule belongs with morale
   — the next subsystem in the Phase 2 order.
