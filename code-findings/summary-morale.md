# Summary — Morale and cohesion

**Covers:** [13-morale-and-cohesion](13-morale-and-cohesion.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Morale is a single `[0, 1]` value driven by **two systems** — 18 effect slots
in battle, 14 in the campaign — where every effect is a **rate** applied inline
and scaled by elapsed game time. Underneath, morale continuously regenerates
toward a rally value, and **net losses (never gains) are multiplied by
`5 − experience − leadership`, i.e. by three to five**, which makes every
per-hour number in the config an understatement. Three caps bound the result,
and the most important of them, `fightingspirit`, is derived from **state
political support** — so the campaign's politics system reaches directly into
tactical combat. Cohesion is a separate, much simpler system: an exponential
approach to a target set by terrain, movement and other units passing through,
and its terrain table is a direct statement of what each water obstacle costs
a formation. Four features in the subsystem are unwired or mis-wired,
including **fire not disrupting cohesion at all** and **artillery's
cover discount applying in the open instead of under cover**.

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
  to `slowmoraleimprovementfactor` = **0.15** once `morale > battlestartmorale`,
  and `battlestartmorale` is `max(entry morale, 0.5)`. Units rally to their
  starting morale quickly, then crawl. Below that threshold the rate is
  `0.30/hr × [1 − (1 − experience) × 0.66]`, so **0.102/hr green and 0.30/hr
  veteran — 2.94×**. `Rout()` sets morale to **0**, so the climb back to the
  0.25 rally threshold is 2 h 27 m green and 50 minutes veteran.
- **The amplifier fires on losses only**, and collecting its two terms gives
  `net × (5 − experience − leadership)`: **×5 for a green brigade under a poor
  general, ×3 for an elite one under the best**. `UnitPanel` sums the slot
  values and prints *"Overall change per hour"* **without** it, so the panel
  understates a losing unit's real rate by that factor. Experience and
  leadership narrow the band; they never close it.
- **`startedwithlowmorale`** is set only for a battle launched from the
  campaign (`currentoperation == 3`) with the unit under 25 % morale. It raises
  the unit to **0.30** and caps it there all battle — above the 0.25 rally
  threshold, so it can still come back from a rout, but **below the 0.50 melee
  crack level**, so it breaks on any contact, every time.

## The cross-layer coupling worth knowing

`fightingspirit` is a hard morale ceiling, applied in **both** combat models
(`UpdateMorale` and `Autocalc`). It is not a battle quantity — it is built in
four steps from the campaign, and the raw-support line earlier revisions of
this summary quoted is only the one-shot **seed**:

1. **State support becomes a cap through a lerp**, not directly:
   `cap = 1 − (1 − support) × influence + bountiesPenalty`, with `influence`
   **0.20** for volunteers and **0.40** for drafts. So 0 % support caps a
   volunteer brigade at **0.80** and a drafted one at **0.60**; Bounties
   subtracts a further 0.05 or 0.10. The floor and the ×2 leverage the config
   labels describe are **both unwired** and marked `-> inactive`.
2. **`min(own cap, parent formation's mean cap)`** — a brigade is dragged down
   to its formation's average and never lifted above it, so homogeneous
   formations by recruiting state are strictly better than mixed ones.
3. **Three commander terms, as differentials against the side's own average**
   — experience weighted 0.33 at every hierarchy level, fame rising with it
   (0.05 → 0.20) and leadership falling (0.20 → 0.05). **Famous generals
   belong at the top of the chain and good leaders at the bottom.** Fame and
   leadership are halved when the commander is wounded or killed.
4. **Enemy soil**: `−0.30` at national military experience 0, easing to
   `−0.10` at 1. Then clamped to `[0.01, 1]`.

> **Political support in a unit's home state caps how well it can fight.** Any
> mod touching state support is a combat mod whether it intends to be or not.
> The UI says so outright: *"Morale is capped by the fighting spirit."* The
> worst reachable cap — a drafted brigade from a zero-support state under
> Bounties 2 — is **0.50**, exactly the melee crack level.

**In a standalone historical battle none of it applies.**
`fightingspirit = Mathf.Min(sideinformation[side].fightingspirit)` — a
`Mathf.Min` with **one argument**, so an assignment rather than a cap. Every
unit on a side takes the scenario file's value and steps 1–4 are discarded.
`currentoperation == 3` excludes campaign battles, so only historical
scenarios are affected.

## Rout and rally

- **Two crack thresholds**, chosen by melee state: **0.25** out of contact,
  **0.50** in it. **Contact doubles the break threshold** — a unit at 40 %
  morale is steady under fire and routs the instant one block is touched. The
  morale icon starts blinking at 0.5, so the blink means precisely *"this unit
  will break if anything reaches it"*.
- **A melee rout rallies on distance; a fire rout rallies on morale.** A unit
  that broke in contact clears the rout as soon as no enemy is within 40,
  whatever its morale, and is set to 0.26. A unit shelled out of position must
  climb from 0 past 0.25 unaided. So **hand-to-hand routs come back quickly
  and bombardment routs do not.**
- **Campaign units never rout** — `Rout()` early-returns on
  `calledfromcampaign`. Routing cavalry is **remounted first**, and a unit in
  square is reformed into line.
- On rally the unit stops, plays a bugle, and **rotates to
  `anglebeforemelee + 180°`** — turning back the way it came, which is what
  that latched field is for.
- **The commander's rally order is additive and serial**: +0.20/hr onto one
  subordinate at a time, capped at 0.9, **cancelled the moment that unit is
  given a move order**, and then re-targeted at the next-worst non-moving
  non-skirmisher unit in the command.
- Unit symbols track three bands: normal, `fragmentedsymbol` below 0.5,
  `brokensymbol` at or below 0.25.
- First-battle morale is capped at a shipped **0.6**, with drill and commander
  leadership each buying back up to 0.2 of the remaining 0.4.

## Cohesion in one block

```
disruption = cohesionbyfire + terrainPenalty + movingPenalty
target = 1 − disruption × (1 − Training × traininginfluenceoncohesionfactor)
           − cohesioncausedbyotherunitspassing
target = min(target, 1 − terrainPenalty)          // ceiling training can't buy back
cohesion += (target − cohesion) × recoveryRate × elapsed
```

- **Terrain only penalizes a *moving* unit** — it is zeroed when the unit has
  no path. A stationary unit in rough ground takes none, and recovery is fast
  (`cohesionrecoveryperhour` = **2**/hr).
- **Terrain is also a training-proof ceiling.** `battleprefs.txt` names all 14
  terrain meshes, which decodes `currentterrain`:

  | Terrain | Ceiling while moving through it |
  | --- | --- |
  | Grassland, Road | 1.00 |
  | Bridge, Pontoon 0–4 | 0.90 |
  | Fences / Walls | 0.60 |
  | Creek | 0.55 |
  | River Crossing (ford) | 0.40 |
  | **River** | **0.00** |

  So **moving through a River mesh forces cohesion to zero**, below the 0.3
  alert level, which costs morale (slot 14) and loses the charge comparison
  (slot 13). Cross at a bridge, ford or pontoon — and if you take the water,
  halt and re-form before contact.
- **Training reduces disruption, not recovery.** The config label states it:
  *"1 means 100% drill would lead to no cohesion any more."* It does not touch
  the terrain ceiling.
- Marching in the open is nearly free: `cohesionbymoving` = 0.15, so the worst
  a drilled unit reaches on grass is 0.91. The expensive things are water
  obstacles and **having your own troops walk through you** (0.25 absolute,
  decaying at 5/hr).
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

## Four unwired or mis-wired features

The first two were verified by exhaustive reference search across all 211 game
types.

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

### The fighting-spirit floor and leverage are not wired

`minimumsupportlevelfightingspirit` (0.20) and
`leveragefightingspiritsupportcap` (2.0) are parsed and never read — and the
config is **honest about it**, marking both `-> inactive`. Their absence is why
the shipped state-support cap is a mild `1 − (1 − support) × 0.20` lerp instead
of the leveraged, floored curve the labels describe.

### Artillery's cover discount applies in the open, not under cover

Slot 8 subtracts `0.75 × (1 − covervalue)` from the incoming-artillery morale
penalty. `covervalue` **rises** with cover — it is the same field slot 5 uses
to *add* morale for being in cover — so the subtraction is zero at full cover
and 0.75 in the open. **A unit in the open keeps a quarter of the penalty and a
unit behind a stone wall pays all of it**, the opposite of the pref's label.
Live, reachable, and visible in the panel as *"Under artillery fire"*.

### `GetTraining()` writes to the field it reads

`training += DLC_WL.EffectMultiplier(20, 0, …)` sits inside the getter, and
`EffectMultiplier(…, 0, …)` returns `cumulativeeffectsonce[20]` — a bonus the
array name says is meant to apply once. `GetTraining()` is called many times
per unit per cycle, so **in a DLC scenario with that effect active a player
unit's training ratchets to its 1.0 clamp within a few frames** and is saved
that way. `Experience()` next door does the same job correctly into a local.
Base-game-safe, since `EffectMultiplier` returns 0 outside a DLC scenario.

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

1. **Fatigue** feeds morale (slot 16, triggered above 0.7 and only while
   morale is above 0.3, suppressed entirely by perk 2), accuracy, autocalc
   casualties and melee — but its own update rule is untraced. The obvious
   companion to this subsystem.
2. **Where `training` comes from.** `Experience()` is traced: a `Regiment`
   field raised at **0.0002 per enemy casualty the unit inflicts**, so a
   brigade must inflict **5,000 casualties** to go from green to elite. That
   is why the 2.94× recovery and ×5→×3 loss bands move so slowly. `training`
   has no equivalent accumulator here.
3. **`movementmode` enumeration.** Mode 5 zeroes cohesion, mode 6 is charging;
   the full set is unknown and load-bearing across the codebase.
4. **Two terrain readings genuinely conflict.** `currentterrain` is decoded
   above from the `battleprefs.txt` mesh names, where index 8 is *Fences
   Walls* — but `BattleUnits.EstablishCampaignBattle` selects the
   water-movement combat restrictions on `currentterrain == 3 | 4 | 8`, reading
   the same field. Either the scene mesh is not what the label says or the
   water set is wrong; an asset dump of the terrain meshes would settle it.
5. **Whether the unwired features are bugs or deliberate removals** cannot be
   settled from code alone — except the fighting-spirit pair, which the config
   marks `-> inactive` and is therefore deliberate.

`rallyvalue`, `currentterrain` and the fourth `moraleincreaseeffects` element
are now resolved — see [13](13-morale-and-cohesion.md) §2, §8 and §6.
