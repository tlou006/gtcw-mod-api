# Summary — Morale and cohesion

**Covers:** [13-morale-and-cohesion](13-morale-and-cohesion.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Morale is a single `[0, 1]` value driven by **two systems** — 18 effect slots
in battle, 14 in the campaign — where every effect is a **rate** applied inline
and scaled by elapsed game time. Underneath, morale continuously regenerates
toward a rally value, and losses (never gains) get amplified by a factor that
experience and leadership can halve but not remove. Three caps bound the
result, and the most important of them, `fightingspirit`, is seeded from
**state political support** — so the campaign's politics system reaches
directly into tactical combat. Cohesion is a separate, much simpler system: an
exponential approach to a target set by terrain, movement and other units
passing through. And **the fire-disrupts-cohesion feature is unwired** — three
shipped config values do nothing at all.

## The documentation shortcut paid off

`Regiment` keeps `moraleeffects[18]` alongside `moraleeffecttext[18]`, so every
contributing factor is enumerated in one method **with its own player-facing
explanation attached**. This is exactly the `Get*Tooltip` technique `plan.md`
predicted, and morale is where it works best — the semantics came free rather
than being inferred.

## The pipeline

```
morale += elapsed × rallyValue × branchBonus × slowFactor      // base recovery
morale += moraleeffects[i] × elapsed × dlcMultiplier           // each of 18, inline
if (netChange < 0)                                             // losses only
    morale += netChange × (2 − Experience())  × overallmoraleinfluenceexp
    morale += netChange × (2 − leadership)    × overallmoraleinfluenceleadership
cap by fightingspirit → clamp [0,1] → cap by first-battle ceiling
```

Three details that matter more than the slot list:

- **Recovery above where the unit started the battle is deliberately slowed**
  (`slowmoraleimprovementfactor` once `morale > battlestartmorale`). Units
  rally to their starting morale quickly, then crawl.
- **The amplifier fires on losses only**, and `(2 − stat)` spans `[1, 2]`, so
  experience and leadership **halve the extra loss but never eliminate it**.
  That asymmetry is why veterans feel steadier than their raw stats suggest.
- **`startedwithlowmorale` caps a unit at `moralecracklevel + 0.05` for the
  whole battle.** A unit that arrived shaken can never recover past the break
  point.

## The cross-layer coupling worth knowing

`fightingspirit` is a hard morale ceiling, applied in **both** combat models
(`UpdateMorale` and `Autocalc`). It is not a battle quantity:

```
regiment.fightingspirit = GameVars.nation[regiment.state].support[alliance]
```

then shaped by commander experience, fame and leadership (weighted 0.33 each
across four hierarchy levels), halved if the commander is wounded or killed,
and reduced by −0.30 for fighting on enemy soil at low military experience.

> **Political support in a unit's home state caps how well it can fight.** Any
> mod touching state support is a combat mod whether it intends to be or not.
> The UI says so outright: *"Morale is capped by the fighting spirit."*

## Rout and rally

- **Two crack thresholds**, chosen by melee state: `moralecracklevel` out of
  contact, `moralecracklevelmelee` in it.
- On rally the unit stops, plays a bugle, and **rotates to
  `anglebeforemelee + 180°`** — turning back the way it came, which is what
  that latched field is for.
- Unit symbols track three bands: normal, `fragmentedsymbol` below
  `moralealertlevel`, `brokensymbol` at or below `moralecracklevel`.
- First-battle morale is capped at a shipped **0.6**, with drill and commander
  leadership buying back part of the remaining 0.4.

## Cohesion in one block

```
disruption = cohesionbyfire + terrainPenalty + movingPenalty
target = 1 − disruption × (1 − Training × traininginfluenceoncohesionfactor)
           − cohesioncausedbyotherunitspassing
target = min(target, 1 − terrainPenalty)          // ceiling training can't buy back
cohesion += (target − cohesion) × recoveryRate × elapsed
```

- **Terrain only penalizes a *moving* unit** — it is zeroed when the unit has
  no path. A stationary unit in rough ground takes none.
- **Training reduces disruption, not recovery.** The config label states it:
  *"1 means 100% drill would lead to no cohesion any more."*
- **`movementmode == 5` sets cohesion to 0 outright**, bypassing the approach.
- **Disorder is rendered** — sprite positions re-lay out as cohesion drops,
  spreading by `(1 − cohesion) × cohesionmaximumspritespread`.

### A naming inversion that will bite

In **code**, `cohesion` means *order* — 1 is a perfect formation. In the
**config labels**, "cohesion" means *disorder* — *"Cohesion increase by enemy
artillery fire"* is a penalty. Read `unitprefs.txt` with the code's sense of
the word and every conclusion inverts.

Also note `GamePrefs.choesionbyotherunitspassing` is **misspelled in the
game**. Any binding must match the typo.

## Two unwired features

Both verified by exhaustive reference search across all 211 game types.

### Fire does not disrupt cohesion

`unitprefs.txt` ships three values — *"Cohesion increase by enemy infantry
fire"* (0.001), *"...artillery fire"* (0.01), *"...cavalry shock"* (0.15) —
and **all three are parsed and never read.** The accumulator they would feed,
`cohesionbyfire`, has **no increment path anywhere**: it is declared, zeroed,
decayed at −3/hr with a clamp at 0, read, and serialized. Nothing adds to it.

> **So `cohesionbyfire` is permanently 0 and fire has no effect on cohesion in
> this build.** Only the decay half of the feature is wired, while the config
> labels show unmistakable design intent. Cavalry shock still hits *morale* and
> *melee*; it just never touches cohesion.
>
> A modder tuning those three numbers would see nothing happen and reasonably
> conclude they had the wrong file. **This is the class of finding that
> justifies the project.**

### Campaign attrition morale loss is computed but not applied

`moraleeffectscampaign[8]` (high attrition) is assigned, but unlike every other
live slot there is no `morale += ...` line for it, and there is no bulk loop to
catch it. The value still reaches the campaign UI — so **the player can be
shown an attrition penalty that is not being applied.** Campaign slots 2 and 11
are never assigned at all.

## Smaller decodings

- **`historicalbranch`**: 0 = infantry (`unittyp` 0, 3), 1 = cavalry (1, 4),
  2 = artillery (2). This also explains `increaseofrofwhencorrectbranch` and
  `increaseofaccuracywhencorrectbranch` from
  [summary-combat](summary-combat.md).
- **Veteran units resent mediocre commanders.** The campaign unhappiness test
  puts `Experience()` on the *demand* side: the better the troops, the more
  fame and leadership they require before they stop losing morale.
- **Campaign morale UI labels are addressed by numeric id**
  (`GetTooltipHeadline(panel, base + i)`), not by string. Supporting evidence
  for the old localization-id question — and it means a mod adding a morale
  effect needs a tooltip id, not just text.

## Still open

1. **Fatigue** feeds morale, accuracy, autocalc casualties and melee, but its
   own update rule is untraced. The obvious companion to this subsystem.
2. **`GetTraining()` vs `Experience()`** — both widely used, neither
   derivation traced.
3. **`movementmode` and `currentterrain` enumerations.** Mode 5 zeroes
   cohesion, mode 6 is charging; terrain indexes
   `terraininfluenceoncohesion[]` and 3/4/5/8 mean embarked. Both are
   load-bearing across the codebase and deserve a dedicated pass.
4. **`rallyvalue` derivation** — a `Regiment` field, not a pref, and commanders
   appear to raise it actively via `UpdateRallyZone`.
5. **Whether the two unwired features are bugs or deliberate removals** cannot
   be settled from code alone.
