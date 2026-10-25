# Summary: naval combat

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** covers [26-naval-combat](26-naval-combat.md) and
[30-naval-subsystems](30-naval-subsystems.md)

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

## And four subsystems where the plumbing is worse than the model

[30-naval-subsystems](30-naval-subsystems.md) works through the four the first
pass left open, and the pattern holds: the data and the formulas are sound, and
the code around them is not.

> **Every damaged ship escapes a fleet action.** The escape test is
> `(float)Random.Range(0, 1) <= probability`, and `Random.Range(int, int)` is
> max-exclusive, so the left side is always 0 and the test always passes. Any
> vessel below `minconditionshipescape` = 0.35 sails for harbour on the cycle
> it crosses the line. The speed-weighted chance that should govern it — about
> **1.5 % per 21-minute cycle** at an even speed match — is computed and
> thrown away.
>
> This is why fleet actions are indecisive, and it is also why **ship capture
> is rare**: capture needs a vessel disabled below 0.25, and 0.35 is above
> that, so a ship has to fall from healthy to disabled inside one cycle to be
> taken at all. One character fixes it.

> **Shipyard congestion never slows a ship.** `Mathf.Min(1f, utilisation)` on
> one line and `Mathf.Max(1f, b)` on the next cancel exactly, so the
> congestion factor is always 1. Capacity decides whether a hull may be *laid
> down*; after that it builds at full speed however crowded the yards.
> The third clamp-against-clamp in the naval code, after the rate-of-fire
> distance ratio and the armour display band.

**Transport interception** is a one-cycle affair. A fleet that catches an
embarked army attaches an autocalc, and its retreat order sits **outside** the
hit test — so the convoy turns for home whether or not anything is hit, and the
engine destroys itself as soon as `onretreat` is set. **One fleet anywhere on a
sea lane aborts any amphibious movement it touches**, with at most one casualty
roll at 20 %. That 20 % never rises, because the method omits the ×750,000
scale factor its sibling `UpdateNavalBattle` applies to the closing distance.

**Blockade** rewards spreading out. A fleet's per-node value is
`sqrt(raidingEfficiency − mean(enemy guards)) × 0.004`, and because the
raiding term is constant inside the averaging loop it survives intact while the
**enemy's guards are divided by the number of nodes covered**. So a wider
blockade is free, and one enemy guard squadron among four covered harbours
weakens each of them by a quarter of its strength. Closing to combat range
multiplies the whole thing by **2.5**. Land cavalry on **Guard** orders counts
as a blockade guard, and **Sea Raiders** lets a fleet blockade trade nodes
belonging to powers it is not at war with.

**The European squadrons are not hypothetical.** Five named fleets — French
and Spanish Intervention Fleets, British Intervention Fleet, North American
Squadron, Western Coast Fleet — **25 ships, identical in all six campaign
starts**, including five 92-gun Screw Ship-of-the-Line, four 80-gun Fast Screw
Ship-of-the-Line, the Warrior Class and the *Normandie*. They are scenario
data, present from the first frame; the intervention script only decides
whether their alliance goes to war.

One structural find outside the navy: **`SetRetreatToDepot` carries 123
unreachable lines** — a complete inline depot search behind a second
`if (unittyp != 17)` that the first one's early return makes impossible. The
live land path is `Retreat.RetreatOrder`, now written up as
[31-retreat](31-retreat.md). For a **fleet**, the same method ends in
`DisintegrateUnit()` when no reachable harbour is found.

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
