# 26 — Naval combat and the ship data model

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GameVars.ShipType`, `Vessel`, `Fleet`,
`Autocalc`; `Config/shiptypes.dat`, `Config/weapons.txt`
**Summary:** [summary-naval](summary-naval.md)
**Translates into:**
[manual-vs-code](../mechanics-findings/manual-vs-code.md) Part VI

Written for the manual's sixth cross-reference pass, which needed Appendix E
checked against the shipped ship table and the naval combat chapter against
the model that actually runs. Nothing in this folder had covered naval combat
before; [11](11-combat-resolution.md) is land-only.

> **There is no tactical naval battle.** Fleet actions, fort bombardments and
> transport interceptions are all resolved inside `Autocalc`, on the campaign
> clock, with a model that shares nothing with either land path of
> [11](11-combat-resolution.md) §1. It is a third damage model.

---

## 1. `shiptypes.dat` — 41 types, variable-length records

`GameVars.import_shiptypes` (`GameVars` ~6858) reads `Config/shiptypes.dat`
with `Encoding.Default`. The count line is **41**. Each record is **not** a
fixed length: it carries a resource-count line followed by that many id/amount
pairs, so a positional editor has to track it.

Field order, in full:

| Lines | Field | Notes |
| --- | --- | --- |
| 1 | `id` | matches the record index in the shipped file |
| 2 | `name` | |
| 3 | `importfromallianceid` | −1 for all but 17 (GB), 18 and 30 (France), 36 (GB) |
| 4–5 | `rivermovement`, `seamovement` | booleans; see §3 |
| 6 | `engineonly` | 1 = steam only, 0 = sail or steam-and-sail |
| 7 | `engines` | 0 = pure sail |
| 8 | `turrets` | **parsed and read nowhere** |
| 9 | `armor` | abstract, not inches — see §2 |
| 10–12 | `lengthmeters`, `beammeters`, `tonnage` | |
| 13 | `guns` | equals the sum of the weapon systems' guns in all 41 records |
| 14–28 | five weapon systems | `weaponid`, `guns`, `withinturret`; `weaponid < 0` skips the slot |
| 29 | resource count *n* | |
| 30…29+2n | *n* × (`resourceid`, `amount`) | becomes `creationresources` |
| +3 | `requiredtechids` | three slots, **all −1 in every record** |
| +1 | `requiredportlevel` | |
| +3 | `upgradetoshipid` | three slots, ids ≥ 0 kept |
| +1…+13 | `crew`, `coalcapacity`, `provisions`, `ammo`, `size`, `speedkn`, `daysatsea`, `isram`, `flagshipvalue`, `coalconsumption`, `coalconsumptionstationary`, `modelid`, `isironclad` | |
| +1 | availability string | the alliances for which the type is **not** researched |

**The availability string is inverted.** The loader splits it two characters
at a time and then:

```csharp
for (num5 = 0; num5 < alliance.Length; num5++)
    if (list.Contains(num5)) shiptypes[i].SetAsResearched(num5, setvar: false);
    else                     shiptypes[i].SetAsResearched(num5);
```

So listing an alliance **removes** the type from it. The shipped values are
`""` (15 types, available to everyone at once), `"0;1;"` (25 types, neither
belligerent has it) and `"1;"` (the Gunboat, Union only). Every alliance from
2 upward therefore starts with all 26 of the gated types.

---

## 2. `armor` is an abstract rating, and its display ladder has a dead rung

`ShipType.UpdateProsaInfo()` (`GameVars` ~2931) turns the numbers into the
words the ship panel shows. Propulsion and movement are clean:

```csharp
if (engineonly == 1f)                      propulsiontext = text(662, 0);   // steam
if (engineonly == 0f && engines == 0)      propulsiontext = text(662);      // sail
if (engineonly == 0f && engines  > 0)      propulsiontext = text(663, 0);   // steam and sail
```

The armour ladder is not:

```csharp
armortext = text(667);                                   // armor == 0
if (armor > 0f && armor < 1f) armortext = text(668, 0);
if (armor == 1f)              armortext = text(668);
if (armor >= 1f)              armortext = text(669, 0);  // overwrites the line above
```

**The `armor == 1f` band is unreachable**: the next test is `>= 1f` and fires
for the same value. Exactly one shipped type has `armor == 1` — the Cottonclad
Ram, which Appendix E describes as *"Cotton Bales"* — so that ship is
described by whatever string 669 holds instead. *Untested:* the tooltip table
is prefab data and is not in `Config/`, so the strings themselves are not
readable without an asset dump.

The shipped values are 0, 0.5, 1, 1.5, 2, 2.5, 3, 4, 4.5, 4.7, 6, 7 and 8.
Appendix E reads them as inches, which works from 2 upward; below that it uses
material names (0.5 → *Timber*, 1 → *Cotton Bales*) and calls the Tinclad
Gunboat's **1.5** *"0,5-inch iron plating"*. So `armor` is a rating, not a
thickness, and only the upper two thirds of the scale happens to line up with
the manual's inches.

---

## 3. `Fleet.UpdateFleetData` — aggregation, and two defects

Called on the fleet update rhythm or forced. It resets thirteen fields and
walks `attachedships[]`. A ship is counted only if
`constructiontimer <= 0 && timeofarrival <= 0.01 && ammo > 0`; otherwise it is
counted as disabled.

```csharp
if (condition < GamePrefs.conditionfordisabledships) disabledships++;
else {
    guns += CeilToInt(shiptype.guns * condition);
    num2  = ship.GetFiringStrength(0f, reduceammo: false, isfortengagement: false, inbattle: false);
    if (condition <= GamePrefs.strengthrelationtriggerdefeatnavybattles)
        combatvalue = 0f;                      // assign, not subtract
    combatvalue += num2;
    …
    if (!regref.inbattle) ship.GetFiringStrength(1f, …);   // result discarded
}
```

**Defect 1 — `combatvalue = 0f` assigns.** If it ever fires it discards the
running total and the fleet's combat value becomes the sum of the ships *after*
the offender in the array. As shipped the branch is **dead**: it needs
`condition <= 0.10` inside a block that only runs for `condition >= 0.25`.
Raising `strengthrelationtriggerdefeatnavybattles` above
`conditionfordisabledships` — the config labels it `[0-1]` — activates it. Same
assign-for-accumulate shape as `Alliance.powratio`
([23](23-game-state.md) §6.2).

**Defect 2 — the displayed Firepower is a point-blank figure.** The call passes
distance **0**, so every gun counts as in range and §4's rate-of-fire term
takes its best value. `Autocalc`'s
`EnvolvedFleets.GetCombatValue(float _fleetdistance)` then **never reads its
argument** and returns the sum of these stored values, so the fleet-level
firepower that sets the victory balance is range-blind while the per-vessel
damage rolls are not.

`UpdateLowestShipSpeed` is the fleet's movement and capability aggregator:
`lowestshipspeed` is the minimum over ships, and `abletomoveonriver` /
`abletomoveonsea` are **ANDs** over `rivermovement` / `seamovement`, so one
sea-only hull costs the fleet its rivers. Vessel speed is itself
condition-scaled —
`speed = speedkn × condition + speedkn × shipspeedfactorconditionzero (0.3) × (1 − condition)`
— so a damaged ship slows the whole squadron.

---

## 4. `Vessel.GetFiringStrength` — the per-ship firepower

`Vessel.cs:627`. Returns 0 outright for `ammo <= 0`,
`condition < minshipconditionfighting (0.25)`, `constructiontimer > 0` or
`timeofarrival > 0`.

```
for each weapon system:
    if weapon.range_yd >= distance:
        group.AddWeapon(weaponid, guns)
        inRange   += guns
        if withinturret == 1: inTurret += guns
        rangeSum  += range_yd × guns
    total += guns

turretFrac = inTurret / max(1, inRange)
avgRange   = rangeSum / max(1, inRange)
rangeFrac  = inRange  / max(1, total)            // strengthdata[0], zeroed out of battle

ratio = Mathf.Max(1f, distance / Mathf.Max(1f, avgRange))
rof   = navalbattlerofmaxdistance × ratio + navalbattlerofmindistance × (1 − ratio)
rof  *= (rofimprovementturrets − 1) × turretFrac + 1
if reduceammo: ammo -= 1/averagesalvopervessel × rangeFrac × rof × ammoconsumptionfactor
if ammo <= lowonammotriggerfleets (0.20): rof = navalbattleroflowammo (0.05)

cmdr  = 1 + commanderExperience × commanderexpinfluenceonvesselcombatstrength (0.4)
return group.GetWeaponStrength() × condition × rof × cmdr
```

**`ratio` is always exactly 1.** Every gun in `avgRange` passed
`range_yd >= distance`, so `distance / avgRange <= 1`, and `Mathf.Max(1f, …)`
raises it to 1. The blend therefore always yields
`navalbattlerofmaxdistance = 0.25`:

> **Naval rate of fire is a constant.** `navalbattlerofmindistance = 1.00`,
> labelled *"ROF factor min distance (close combat)"*, is never reached, and
> closing the range buys no extra rate of fire. The clamp wanted to be
> `Mathf.Min`, or the ratio wanted to be inverted.

The two live modifiers on `rof` are the turret factor and the low-ammo cliff.
The turret test is **`withinturret == 1`**, not `> 0`, while the panel's
`ShipType.gunsinturrets` counts `> 0` — so the three types whose guns are
spread across **two** turrets (15 Ocean Casemate Ironclad, 17 Ironclad Turret
Ship, 27 River Monitor) get no bonus at all, while 16, 18 and 19 get the full
×1.4. `withinturret` is a turret *count*; the consumer reads it as a flag.

`strengthdata[0..5]` is the UI breakdown: in-range fraction, average range,
turret guns, rate of fire, commander factor, final strength. The fleet divides
slots 0, 1, 3 and 4 by the ship count — including disabled ships — when it
aggregates them.

---

## 5. `Autocalc.UpdateNavalBattle` — the fleet action

Runs on the autocalc cycle, `cycleupdateforautocalcs = 0.00004` campaign years
≈ **21 game minutes**; `num` is the elapsed time since the last cycle.

```
fleetdistance -= fleetdistancereduction (1.5) × num × 750000      // floored at 0
relativedistance = fleetdistance / max(1, startfleetdistance)      // 1 → 0
seabattle = both fleets' abletomoveonsea
```

Then, per side, per vessel, in list order:

```
if (Random.Range(0f,1f) >= 0.1f / navalbattlelengthfactor (0.6)) continue;   // ≈ 1 tick in 6
target = PickTargetVessel(side)
fp     = GetFiringStrength(fleetdistance, reduceammo: true)
        × night perk 4 × blockade-buster perk 8 × port-defender perk 10
pen     = min(1, (GetAveragePenetration(fleetdistance) / max(0.1, target.armor))
                 ^ (1 / penetrationfactor 1.25) / 10)
hull    = target.size × hullstrength (50)
hitProb = GetAverageHitProbability(relativedistance, fleetdistance) × 10
if (rand < hitProb) {
    if (rand < pen) {
        if (rand <= extraordinaryeventhit 0.025) damage = 1.0;   // magazine explodes
        else                                     damage = fp / max(hull, 0.01) × 3;
        target.AdjustCondition(−damage)
    } else "no effect!"
}
if (engaged++ > (seabattle ? combatwidthsea 7 : combatwidthrivers 3)) break;
```

Four things to take from that.

- **Armour gates penetration; it does not reduce damage.** On a penetrating
  hit the damage is `fp / hull × 3` whatever the armour was. The *"no effect!"*
  branch is the whole of armour's contribution.
- **Target size is not in the hit probability.** `GetAverageHitProbability` is
  the gun-count-weighted mean of
  `accuracy × (generalhitprobabilitynavalfight[0] (0.006) × relativedistance +
  [1] (0.075) × (1 − relativedistance)) × Random.Range(0.9f, 1.1f)`, times 10
  at the call site — so **6 % at the opening range rising to 75 % at contact**,
  and nothing about the target. Size enters twice elsewhere: `PickTargetVessel`
  enters each active enemy `ceil(size)` times in the draw, and `hull = size ×
  50` divides the damage. The two roughly cancel.
- **Gun `accuracy` is live here**, unlike land fire, where the aim-scatter
  floor discards everything at or below 1.0
  ([11](11-combat-resolution.md) §3e). A 1.1-rated naval rifle really is 10 %
  more likely to hit.
- **Only seven ships a side fire per tick at sea, three on a river.** The
  `break` is on the count of vessels that actually engaged a target.
  `GetCombatValue`, which sets the victory balance, sums *all* of them.

`PickTargetVessel` draws with `Random.Range(0, list.Count - 1)`, the
max-exclusive slip catalogued in [20](20-ai-campaign.md) §12.2, so the last
active enemy vessel is never drawn.

`GetAveragePenetration` is the matching mean of
`penetrationinch × guns / max(10, distance) ^ penetrationdistancefactor (0.1)`
over the in-range systems — a very flat distance curve: penetration at
3,900 yd is **55 %** of its point-blank value, and most of that fall happens
inside the first 500 yd (67 %).

---

## 6. Ramming

Same loop, after the gunnery, gated on three conditions:

```csharp
if (fleetdistance <= 0f && shiptype.isram > 0
    && vessel.IsActive() && vessel.condition >= GamePrefs.minshipconditionforram)
```

`fleetdistance <= 0f` is literal contact — the distance has to have been ground
all the way down by §5's reduction — and `minshipconditionforram = 0.60`.

```
k        = ramprobability (0.4) × num × 3650
k       *= 1 + (commanderinitiativeinfluenceonramprob (1.5) − 1) × initiative
toTarget = sqrt(ownTonnage / targetTonnage × isram) × k
toSelf   = sqrt(targetTonnage / ownTonnage) × ramdamageownvessel (0.15) × k
toTarget *= rammingefficiencytorpedoes[perk 0 level]            // if the rammer has perk 0
if (target.isironclad) toTarget *= ramdamagefactorironclads (0.3)
if (own.isironclad)    toSelf   *= ramdamagefactorironclads (0.3)
```

Three notes.

- **Nothing rolls.** `ramprobability` is named a probability and used as a
  damage scale, so a qualifying ram ship rams on **every** tick it is in
  contact. On a 21-minute cycle `k` ≈ 0.058, so an even-tonnage rating-1 ram
  does about 5.8 % condition a tick and takes 0.88 %.
- **`isram` is a rating, not a flag**, and it sits under the square root, so
  rating 3 does **1.73×** the damage of rating 1 at equal tonnage. The shipped
  values are 1 (24 Cottonclad Ram, 15 Ocean Casemate Ironclad, 17 Ironclad
  Turret Ship), 2 (8 Broadside Ironclad) and 3 (14 Casemate Ironclad Ram,
  18 Ocean Ironclad Ram, 28 Ironclad Gunboat Ram).
- **Ironclad armour cuts ram damage 70 % in both directions**, so an ironclad
  ram against a wooden hull is the best case in the model and ironclad against
  ironclad the worst.

---

## 7. The other three naval paths

### 7.1 A raiding fleet forces a blockader to even numbers

At the top of `UpdateNavalBattle`, when one side's `fleetorders` is **3**
(Raid) and the other's is **1** (Blockade):

```csharp
isnavalraid = true;
while (envolvedfleets[blockader].singlevessels.Count > Mathf.Max(2, smaller) && guard < 100)
    envolvedfleets[blockader].RemoveVessel(random vessel);
```

`smaller` is `min(count0, count1)`. The removed vessels are **not** sunk — they
simply take no part. So a three-ship raider engages a twenty-ship blockade
squadron three against three, with a floor of two.

### 7.2 Fort bombardment

`UpdateFortBombardment` (~1180) mirrors §5 with the fort as one side:
`GetFortWeaponStrength(isnavalengagement: true)` scales the fort's guns by
`weightingfactorfortweaponsnavalengagement = 0.2`, hit probability comes from
`generalhitprobabilitybombardment = {0.2, 1.0}` with a wider
`Random.Range(0.8f, 1.2f)`, and damage to the fort is
`fortWeaponStrength × num × 365 × damagefactorfortnavalengagements (0.01)`.
The victory comparison weights the fort by `fortnavyvictoryrelation = 1.5`.
Perk 9 (Battery Runner) multiplies a ship's strength against forts.

### 7.3 Capture

`FinishNavyBattle` (~3486), after the winner is known:

```csharp
for each vessel of the losing side
    if (condition < conditionfordisabledships (0.25) && !IsActive())
        BattleUnits.MoveShip(vessel, envolvedfleets[winningside].regrefs[0]);
```

Every disabled, inactive enemy vessel changes hands, with no check on ship
type. That is how a player can acquire the eight types no project grants —
including the 92-gun Screw Ship-of-the-Line — by beating a European
intervention squadron
([manual-vs-code](../mechanics-findings/manual-vs-code.md) §35). The battle log
reports them as *"ships captured"* for the winner and *"ships abandoned"* for
the loser.

---

## 8. `GamePrefs` keys

**Engagement:** `navalbattlefightingdistancefactor` (65 — map distance 50
becomes 3,250 yd), `fleetdistancereduction` (1.5), `navalbattlelengthfactor`
(0.6), `cycleupdateforautocalcs` (0.00004), `combatwidthsea` (7),
`combatwidthrivers` (3), `fleetupdaterhythm`.

**Gunnery:** `navalbattlerofmaxdistance` (0.25),
`navalbattlerofmindistance` (**1.00, unreachable — §4**),
`navalbattleroflowammo` (0.05), `lowonammotriggerfleets` (0.20),
`rofimprovementturrets` (1.4), `averagesalvopervessel` (50),
`generalhitprobabilitynavalfight` ({0.006, 0.075}),
`generalhitprobabilitybombardment` ({0.2, 1.0}), `penetrationfactor` (1.25),
`penetrationdistancefactor` (0.1), `hullstrength` (50),
`extraordinaryeventhit` (0.025).

**Condition thresholds:** `minshipconditionfighting` (0.25),
`conditionfordisabledships` (0.25), `minconditionshipescape` (0.35),
`minshipconditionforram` (0.60), `shipspeedfactorconditionzero` (0.3),
`escapeprobabilityfactor` (0.3),
`strengthrelationtriggerdefeatnavybattles` (0.10 — and read for three
different quantities, §9).

**Ramming:** `ramprobability` (0.4, used as a damage scale),
`ramdamageownvessel` (0.15), `ramdamagefactorironclads` (0.3),
`commanderinitiativeinfluenceonramprob` (1.5).

**Commanders:** `commanderexpinfluenceonvesselcombatstrength` (0.4),
`commanderleadershipinfluenceonnavalcombat` (0.3),
`commanderinitiativeinfluenceonnavalrange` (1.3),
`commandercunninginfluenceonnavalrange` (0.7),
`expinccommanderhitshipstrength` (0.0005).

**Forts:** `weightingfactorfortweaponsnavalengagement` (0.2),
`damagefactorfortnavalengagements` (0.01), `fortnavyvictoryrelation` (1.5).

**Outcome:** `moraleinfluencenavaldefeats` (−0.00035 per lost combat
value), `commanderfamenavalbattlestrigger` /
`commanderdefamednavalbattlestrigger` (150 combat value either way),
`minshipsfornewspapernavalengagement` (8 — no newspaper below that many
participants).

---

## 9. Defects and oddities

1. **`navalbattlerofmindistance` is unreachable** (§4). Naval rate of fire is
   the constant 0.25 before the turret and ammo modifiers; closing the range
   does nothing for it.
2. **The turret bonus tests `withinturret == 1`** (§4), so the three ship
   types with guns across two turrets get none, while the panel still reports
   their guns as turret-mounted.
3. **`Fleet.combatvalue = 0f` assigns inside an accumulator** (§3) — dead as
   shipped, live if a modder raises one threshold above another.
4. **`GetCombatValue(float _fleetdistance)` ignores its argument** (§3), and
   the value it returns was computed at distance 0.
5. **`ramprobability` is a probability by name and a damage scale by use**
   (§6).
6. **`ShipType.turrets` is parsed and read nowhere** (§1); the live turret
   figures are `gunsinturrets` (display) and `withinturret` (model).
7. **`requiredtechids` is empty in all 41 records** (§1); availability is
   carried entirely by the inverted last line and by the twelve ship-granting
   projects.
8. **The `armor == 1f` display band is overwritten** by the `>= 1f` test that
   follows it (§2), so the Cottonclad Ram's armour string is unreachable.
9. **`PickTargetVessel` uses `Random.Range(0, list.Count - 1)`** (§5), the
   repo-wide max-exclusive slip ([20](20-ai-campaign.md) §12.2).
10. **`strengthrelationtriggerdefeatnavybattles` is read for three unrelated
    quantities**: a fleet firepower ratio (`Autocalc` 1382, 2020, 1782, 5488),
    a ship's condition (`Fleet` 162) and a **garrison's** condition when
    deciding bugle range (`BattleUnits` 2848).
11. **The second `GetFiringStrength(1f, …)` call in `UpdateFleetData` discards
    its result** (§3); it is there for the `strengthdata[]` side effect.

---

## 10. Open questions

1. **The tooltip string table.** §2's armour bands, the propulsion and
   movement words and the size bands all resolve through
   `Tooltip.GetTooltipText(id, entry)`, whose table is prefab data. An asset
   dump would turn the band structure into readable text.
2. **Transport interception.** `transportinterceptionlossfactor` (10) and
   `moralelosstransportinterception` (0.05) are shipped and the path
   was not traced here.
3. **Ship escape.** `minconditionshipescape` (0.35) and
   `escapeprobabilityfactor` (0.3) point at a speed-weighted escape roll that
   this pass did not follow.
4. **Blockade efficiency.** `Regiment` ~10591 builds
   `groupraidingefficiency` and a two-slot `blockadeefficiencyfactors` from
   `lowestshipspeed` and `combatvalue`; the blockade model belongs with
   [14](14-supply-and-logistics.md) §9 rather than here, and is unwritten.
5. **Shipyard capacity and construction.** `requiredportlevel`,
   `creationresources` and `upgradetoshipid` are read here only as data; the
   construction pipeline is unexamined.
6. **Whether the seven European-only types ever appear in a shipped
   intervention force.** Their availability flags allow it; the scenario
   `ships.dat` files were not parsed.
