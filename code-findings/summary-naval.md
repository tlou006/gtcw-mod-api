# Summary: naval combat

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** covers [26-naval-combat](26-naval-combat.md)

Written for the manual's sixth cross-reference pass, which needed Appendix E
checked against `shiptypes.dat` and the naval combat chapter against the model
that runs. No earlier file in this folder had touched the navy;
[11-combat-resolution](11-combat-resolution.md) is land-only.

---

## There is no tactical naval battle

Fleet actions, fort bombardments and transport interceptions are all resolved
in `Autocalc`, on the campaign clock, at `cycleupdateforautocalcs = 0.00004`
campaign years per cycle — about **21 game minutes**. The model shares nothing
with either land path of findings/11 §1: it is a **third damage model**, with
its own hit, penetration and damage rolls and its own rate-of-fire term.

One consequence for a modder: a combat mod that changes both land paths still
leaves naval combat untouched.

## The data is clean; three of its consumers are not

`Config/shiptypes.dat` holds **41 ship types** in variable-length records, and
it is the most internally consistent data file checked in this project: for
all 41, the ship-level `guns` figure equals the sum of its weapon systems'
guns, and `turrets` equals the sum of their `withinturret` values. Appendix E
describes 34 of them and gets all 34 armament lines and 29 of 34 stat blocks
exactly right ([manual-vs-code](../mechanics-findings/manual-vs-code.md) §34).

What goes wrong is downstream:

- **Rate of fire is a constant.** `GetFiringStrength` blends two prefs on
  `Mathf.Max(1f, distance / averageRangeOfInRangeGuns)`. Every gun counted in
  that average passed `range >= distance`, so the ratio can never exceed 1 and
  the `Max` pins it at exactly 1 — which always selects
  `navalbattlerofmaxdistance = 0.25` and never the close-combat value of 1.00.
  **Closing the range buys no rate of fire**, and one shipped pref is
  unreachable.
- **Three turret ships get no turret bonus.** The ×1.4 rate-of-fire bonus
  tests `withinturret == 1`, but the field is a turret *count*: the Ocean
  Casemate Ironclad, the Ironclad Turret Ship and the River Monitor each spread
  a weapon system over **two** turrets and so score zero turret guns. The ship
  panel, which counts `withinturret > 0`, still reports their guns as
  turret-mounted.
- **The displayed Firepower is a point-blank figure.** `Fleet.UpdateFleetData`
  calls `GetFiringStrength(0f, …)`, and `EnvolvedFleets.GetCombatValue(float
  _fleetdistance)` never reads its argument. So the number the panel shows, and
  the number that sets the victory balance, are range-blind — while the
  per-vessel damage rolls are not.

## What the combat model actually rewards

```
hitProb = mean over in-range guns of
            accuracy × (0.006 × relativeDistance + 0.075 × (1 − relativeDistance))
          × Random.Range(0.9, 1.1) × 10
pen     = min(1, (avgPenetration / max(0.1, targetArmor)) ^ 0.8 / 10)
damage  = firingStrength / (targetSize × 50) × 3        // or 1.0 on a 2.5 % magazine roll
```

- **Closing the range is the dominant lever**, through hit probability: **6 %
  at the opening range to 75 % at contact**, a factor of 12.5.
- **Gun accuracy is live**, unlike land fire, where the aim-scatter floor
  throws away everything at or below 1.0 (findings/11 §3e).
- **Armour gates penetration; it does not reduce damage.** A hit that fails the
  penetration roll does nothing at all; one that passes does the same damage
  whatever the armour was.
- **Size is not in the hit probability.** It enters as `ceil(size)` entries in
  the target lottery and as `size × 50` hull strength, which roughly cancel.
- **2.5 % of penetrating hits sink the target outright**, regardless of size,
  armour or remaining condition.
- **Only seven ships a side fire per tick at sea**, three on a river
  (`combatwidthsea`, `combatwidthrivers`), while the firepower comparison sums
  the whole fleet.

## Two mechanics the manual never mentions

**A raiding fleet forces a blockading fleet to even numbers.** When one side is
on Raid orders and the other on Blockade, the blockader has vessels removed
from the engagement at random until it has no more than the raider (floor of
two). They are not sunk; they simply take no part. Three raiders can fight a
twenty-ship blockade squadron three against three.

**Ship capture is unrestricted by type.** At the end of a fleet action every
vessel of the losing side below `conditionfordisabledships = 0.25` and no
longer active is moved into the winner's fleet. Eight ship types — including
the 92-gun Screw Ship-of-the-Line — are granted by no project to either
belligerent and belong to the European powers; beating an intervention
squadron is the only way to own one.

## Hook seams

| Seam | What a postfix gets you |
| --- | --- |
| `Vessel.GetFiringStrength` | the single chokepoint for every ship's firepower, in battle and on the panel — and where to fix the rate-of-fire clamp and the turret test |
| `Vessel.GetAverageHitProbability` / `GetAveragePenetration` | gunnery, independent of the damage roll |
| `Fleet.UpdateFleetData` | fleet aggregation; also where to correct the point-blank Firepower figure |
| `Autocalc.UpdateNavalBattle` | one call per 21 game minutes per battle, with both sides in scope |
| `Autocalc.PickTargetVessel` | target selection, including the `Count − 1` draw |
| `GameVars.import_shiptypes` | the data layer, for adding or re-statting ship types |

Open questions are listed in findings/26 §10: the prefab tooltip strings,
transport interception, ship escape, blockade efficiency, the construction
pipeline, and whether the European-only types appear in any shipped
intervention force.
