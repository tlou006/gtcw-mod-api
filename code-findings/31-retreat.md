# 31 — The retreat system

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Retreat` (624 lines), `Regiment`
(`SetRetreatToDepot`, `DisintegrateUnit`, `Surrender`), `BattleUnits`,
`AICampaign`, `Tools`; `Config/campaignprefs.txt`; `Config/unitprefs.txt`
**Summary:** [summary-simulation](summary-simulation.md)

`Retreat` is the one campaign subsystem no earlier file had opened.
[30-naval-subsystems](30-naval-subsystems.md) §5 established the entry point —
`Regiment.SetRetreatToDepot` enqueues a `Retreat.RetreatOrder` for land units
and 123 lines of inline alternative behind it are unreachable — and promoted it
to the top of the backlog. This is that file.

It turns out to be two things at once: a **queued destination-finder** that
decides where a beaten unit goes, and the game's **only** system that makes
units physically block movement, which it switches on and off around each
pathfinding call. `AICampaign` borrows the second half for its own campaign
moves, so the two contend for a single global slot.

**Result:** six defects, one of them severe — **a cut-off unit ordered to
withdraw marches toward the nearest enemy** — and one exploit:
**holding the right mouse button suspends every retreat in the game.**

---

## 1. Shape

| Member | Role |
| --- | --- |
| `RetreatOrder` (nested class) | one queued retreat; two constructors, battle and campaign |
| `retreatorder` | the global `List<RetreatOrder>` |
| `currentlyactivated` | **a single global int**: which alliance's units are currently nav-mesh obstacles, or −1 |
| `WorkDownRetreatOrders()` | the per-frame pump, `BattleUnits.Update` ~1488 |
| `GetRetreatDestinationBattle` | three-tier destination search for a battle |
| `GetRetreatDestinationLand` | five-source destination search for the campaign |
| `PrepareAllianceObstacles` / `ActivateNavMeshObstacles` / `DeactivateNavMeshObstacles` | the obstacle machinery |
| `IncreaseAllOrdersFrames(int inc = 2)` | pushes **every** pending order's ready-frame to `GameVars.frame + inc` |

`Start()` and `Update()` on the `MonoBehaviour` are **both empty** — the class
is driven entirely from outside, which is why its cadence is
`BattleUnits.Update`'s and not its own.

### 1.1 Queueing is idempotent per unit

Both constructors begin with `IsAlreadyRetreating(unit)`, a linear scan of the
queue, and return without adding if the unit is already in it. So repeated
retreat triggers on the same unit collapse to one order — which matters because
`BattleUnits`' contact check can fire every frame.

The campaign constructor resolves its destination **at queue time**, not at
execution time:

```csharp
if (campaign) {
    retreatdestination = GetRetreatDestinationLand(unittoretreat, fromposition, minrange, maxrange);
    if (Tools.CompareVector3(retreatdestination, fromposition, 5f, excludey: true))
        markedtodisintegrate = true;
}
```

The battle constructor does the same through `GetRetreatDestinationBattle`, or
takes an `EntryPoint`'s position directly if one was passed, and **abandons the
order entirely** (`return` before `retreatorder.Add(this)`) if the search
returns `default(Vector3)`.

---

## 2. `WorkDownRetreatOrders` — the pump

Called **every frame** from `BattleUnits.Update()` ~1488, outside any pause
gate — one of the three things [10](10-simulation-tick.md) Finding 7 found
still running while the game is paused.

```csharp
if (retreatorder == null || Input.GetMouseButton(1) || GameVars.frame < 50) return;
```

> **Holding the right mouse button suspends every pending retreat.** The same
> guard sits at the top of `PrepareAllianceObstacles`, so neither the queue nor
> the obstacle set advances while button 1 is down. Right-drag is the camera
> control, so this is not an obscure input — a player panning the map is
> freezing every rout and withdrawal on both sides for as long as the drag
> lasts.
>
> It is presumably there to stop the obstacle set churning while the player is
> issuing orders. The cost is that the retreat queue is not a simulation
> process so much as a process the interface can stall.

Then, per order, two conditions must hold or it is skipped:

- **`GameVars.frame - order.frame >= 2`** — a two-frame settling delay;
- **`currentlyactivated == order.unit.alliance`** — the obstacle set must
  currently be prepared for that unit's side.

### 2.1 One alliance per call, and every rebuild pushes the queue back

After draining every order whose alliance matches `currentlyactivated`, the
method looks for a remaining order belonging to a **different** alliance and, if
it finds one:

```csharp
DeactivateNavMeshObstacles();
PrepareAllianceObstacles(thatUnit.alliance, thatUnit.transform.position, 10f);
return;
```

So **only one side's retreats are processed per frame**, and switching sides
costs a frame. Worse, both `ActivateNavMeshObstacles` and
`DeactivateNavMeshObstacles` call `IncreaseAllOrdersFrames()`, which sets
**every** pending order's ready-frame to `GameVars.frame + 2`.

> **Each obstacle rebuild delays every pending retreat by two more frames.**
> With both sides retreating the queue alternates, and each alternation resets
> the clock on everything still in it. `AICampaign` ~1128 claims the same
> `currentlyactivated` slot for its own campaign movement orders and releases
> it after each one, so a long AI movement queue pushes retreats back two
> frames per order it processes.
>
> Nothing is lost — orders are not dropped, only postponed — but the "two-frame
> delay" in §2 is a floor, not the typical case, and on a busy frame a retreat
> can wait considerably longer than its author intended.

Finally, when the queue empties and no `AICampaign.orders` remain, the
obstacles are deactivated and `currentlyactivated` returns to −1.

---

## 3. Where a unit retreats to, in battle

`GetRetreatDestinationBattle(unit, minrange)` is a three-tier fallback, each
tier tried only if the one above it found nothing.

**Tier 1 — retrace your own advance.** Only when `unit.routoutofmelee` is set
and the unit has a `lastpositions` history. It walks that history **backwards**,
accumulating distance until it reaches `minrange`, and takes the furthest point
reached. A unit routing out of a melee therefore falls back **along the ground
it advanced over**, which is both historically right and the one tier that
guarantees passable terrain.

**Tier 2 — rally on a parent HQ.** `GrabNextParentUnit` walks up the hierarchy
and takes the first parent for which
`GetClosestEnemyUnit(mindistancetoofferrallypoints)` returns null. That pref is
**70**, labelled *"Minimum distance to enemy for
group commanders to offer rally points for cracked units"*. So a brigade rallies
on its division, a division on its corps, **provided that headquarters has no
enemy within 70**.

**Tier 3 — just go backwards.** `bfs.GetAngleTerrainPos(position,
unit.unitrange.retreatangle, minrange, …)` — a point `minrange` away along the
unit's own retreat angle, with no check that anything is there.

If all three somehow yield nothing the method returns `default(Vector3)` and
the order is never queued (§1.1), so the unit simply does not retreat.

---

## 4. Where a unit retreats to, in the campaign

`GetRetreatDestinationLand(unit, fromposition, minrange, maxrange)`, with
`minrange` = `retreatingdistancefactor` = **35** and `maxrange` =
`maxretreatingdistancefactor` = **300** at every live call site.

**Step 1 — pick a direction.** If the unit has no `closestenemyunitfar` it
forces a full `UpdateUnitRangeFast`. Then:

```csharp
if (closestenemyunitfar != null) {
    fromposition = closestenemyunitfar.transform.position;      // the parameter is overwritten
    angle = Tools.GetAngle(fromposition, unit.position) + 180f;
} else
    angle = unit.transform.eulerAngles.y + 180f;
```

**Step 2 — two probe points** along that angle, at `minrange` and at
`minrange × 2` (so **35** and **70** map units to the rear).

**Step 3 — collect candidates around the far probe**, each within
`[minrange, maxrange]` of it and clear of blocking by at least
`max(minrange, blockrangecampaign + 0.5)`:

| Source | Call |
| --- | --- |
| Friendly **towns** | `BattleUnits.GetTownsInRange` |
| Friendly **forts** | `GetBuildingsInRange(id_fort, …)` |
| Friendly **supply depots** | `GetBuildingsInRange(id_supplydepot, …)` |
| Friendly **small-town trade nodes** | `GetIIPsInRange(id_smalltowns, …)` |
| The nearest friendly **unit** that is not a fleet and not routed | `GetClosestUnitNearPos(…, excluderouted: true)` |

Trade nodes whose owning alliance is at war with the retreating unit are then
dropped, and so is any candidate closer than `minrange` to the unit itself.

**Step 4 — choose.** Two candidates are computed: the closest one inside a
**90° cone** around the retreat direction, and the closest one in any
direction. The cone candidate wins if it exists and is closer than
`closest + minrange`; otherwise the unqualified closest is used. So the
direction is a strong preference, not a constraint — a unit will retreat
sideways or forwards if the nearest friendly position is much closer that way.

### 4.1 With no candidate, a cut-off unit retreats toward the enemy

**Step 5 — `return fromposition;`** And `fromposition` was overwritten in step
1 with **the nearest enemy's position**.

The caller then decides what that means:

```csharp
retreatdestination = GetRetreatDestinationLand(unit, fromposition, minrange, maxrange);
if (Tools.CompareVector3(retreatdestination, fromposition, 5f, excludey: true))
    markedtodisintegrate = true;
```

The method takes `fromposition` **by value**, so the constructor's copy is
whatever the call site passed. Six land call sites exist, and they do not agree:

| Call site | Passes | No-candidate outcome |
| --- | --- | --- |
| `Regiment` ~6533 | the unit's **own** position | **retreats toward the enemy** |
| `BattleUnits` ~2790, ~2937 | the unit's **own** position | **retreats toward the enemy** |
| `Autocalc` ~1078 (transport interception) | the unit's **own** position | **retreats toward the enemy** |
| `Regiment` ~6559 | the **enemy's** position | disintegrates (§5) — intended |
| `BattleUnits` ~14270 | the **enemy's** position when one is known | disintegrates — intended |

> **Four of the six paths send a cut-off unit at the enemy, at double-quick.**
> When no friendly town, fort, depot, trade node or unrouted friendly unit lies
> within 35–300 map units of a point 70 units to the unit's rear, the
> "destination" returned is the position of the nearest enemy, the
> disintegrate check does not fire because the returned value does not match
> what the caller passed, and `WorkDownRetreatOrders` waypoints the unit there
> with `doublequick: true`.
>
> The search ring is bounded, so this is reachable rather than theoretical: a
> force deep in enemy territory, or on a flank with its own army far behind
> it, is exactly the case with no candidate.
>
> There is a second, quieter branch of the same slip. If the unit has **no
> known enemy at all**, step 1 never overwrites `fromposition`, so the method
> returns the caller's value unchanged, the comparison matches, and the unit is
> marked to disintegrate instead — **a unit with nothing near it and nowhere to
> go dissolves rather than standing still.**
>
> The fix is to return a sentinel rather than `fromposition`, and to test for
> that sentinel.

---

## 5. What execution does

### 5.1 Campaign

If the order is not `markedtodisintegrate`, the unit is waypointed to its
destination:

```csharp
unit.onretreat = false;
bunits.SetWaypoint(unit, destination, newpath: true, doublequick: true, …, useorderdelay: false, …);
unit.onretreat = true;
```

**`onretreat` is toggled off and on around the call** so the waypoint logic
does not see a retreating unit and refuse the order, and the path is set with
**no order delay** and at **double-quick** — a retreat bypasses the
order-transmission delay entirely
([19](19-commanders-and-perks.md) §3) and pays the double-quick condition cost
([15](15-attrition-and-condition.md)).

Then, if the resulting path is empty **or** the order was marked to
disintegrate:

```csharp
Town t = BattleUnits.GetClosestTown(unit.position, unit.alliance, order.minrange, 99999f, …);
if (t != null) unit.DisintegrateUnit(null, t.gameObject);
else           unit.DisintegrateUnit();
```

so the town search for the disintegration fallback is **unbounded above** —
`99999f` — unlike the destination search's 300.

### 5.2 Battle

`RegimentSetPath(destination, newpath: true, skipcover: true, …)` — note
`skipcover`, so a retreating unit does not stop to use cover on the way. Then,
unless the order was flagged `usedforregularmovement`:

- `SetMovementMode(5)` — the retreat movement mode;
- the `EntryPoint` is attached if one was given, so the unit leaves the field
  through it;
- and the last resort:

```csharp
if (unit.regimentpaths <= 0 && unit.unitrange.closestenemyunitfardistance <= unit.width)
    unit.Surrender();
```

> **A unit with no retreat path surrenders only if an enemy is within its own
> frontage.** `width` is the unit's deployed frontage, so this is a very close
> test — a surrounded unit with an enemy a little further off keeps standing
> with no path and no surrender, which is the stalemate state a player sees as
> a unit "stuck" in a pocket.

### 5.3 Surrender costs 40–80 % of the men, or 14–28 % for a veteran

`Regiment.Surrender()` (~13938):

```
pow = clamp(powratiosurrender ± powratiovariation, 0, 1)        // 0.6 ± 0.2
pow *= interpolate(experience,     1 → powmultiplierexp)        // 1.0 → 0.7
pow *= interpolate(fightingspirit, 1 → powmultiplierfightingspirit)   // 1.0 → 0.5
SufferCasualtiesAll(…, casualty type 3 = captured, pow)
```

Values from `unitprefs.txt` 1663–1669 by loader replay:
`powratiosurrender` 0.6, `powratiovariation` 0.2, `powmultiplierexp` **0.7**,
`powmultiplierfightingspirit` 0.5.

| Unit | Prisoners taken |
| --- | --- |
| Green, no fighting spirit | **40–80 %** |
| Full experience only | 28–56 % |
| Full fighting spirit only | 20–40 % |
| Full experience **and** full fighting spirit | **14–28 %** |

> **Fighting spirit halves the prisoners a surrender costs, and experience
> cuts them by 30 %**, so a veteran at full fighting spirit loses about
> **2.9×** fewer men to a surrender than a green unit with none *(calculated)*.
> Fighting spirit is the bigger lever. Neither is mentioned anywhere in the
> interface.
>
> An earlier version of this section read `powmultiplierexp` as 0.5, giving
> 10–20 % for a veteran and a fourfold spread.

### 5.4 Disintegration: relocate and bleed, or be deleted

`Regiment.DisintegrateUnit(IIP preventfleetharbor, GameObject preventarmy)`
(~6567), both defaulting to null. The second argument is the fallback — a town
for a land unit, a harbour for a fleet — and whether it is null decides
everything.

Unconditionally: a dispatch is raised, a *"Unit disintegrates!"* rolling text
appears, and **`readiness` is set to 0 for the unit and every attached unit**
([supply-in-practice](../mechanics-findings/supply-in-practice.md) §8 for what
zero readiness forbids).

**With a fallback town**, for each attached brigade down to level 13:

```
share = Random.Range(
    interpolate(nationalmorale, desertionsdisintegrationnatmorale0[0] → …1[0]),
    interpolate(nationalmorale, desertionsdisintegrationnatmorale0[1] → …1[1]))
SufferCasualties(strength × share, …, casualty type 2 = desertion)
SetTransfer(town position ± disintegrationdeplacement)
```

The shipped pair is *"Desertions min/max when unit is disintegrated, at
national morale 0"* = **0.5 / 0.8** and *"… at national morale 100"* =
**0.3 / 0.5**:

| National morale | Deserters |
| --- | --- |
| 0 % | **50–80 %** |
| 50 % | 40–65 % |
| 100 % | **30–50 %** |

The survivors are then placed at the town. So a disintegrated formation
survives as a gutted shell with zero readiness, somewhere behind the line.

**With no fallback town**: `BattleUnits.DeleteUnit(gameObject)` — the unit is
removed outright, every man gone, no casualty accounting at all.

For a **fleet**, the same split: with a harbour every ship is `SendToHarbor`ed
and the fleet relocates there ([30](30-naval-subsystems.md) §3.1 for what that
costs); without one, `DeleteFleet`.

> One naming oddity: the surviving case raises
> **`msg_unitdisintegratesfleetsurvives`** (id 75) even for a land formation
> ([27](27-dispatches-and-the-message-log.md) §4.2), so a land player gets a
> dispatch whose id is named for fleets.

---

## 6. The obstacle system — the only place units block movement

`ActivateNavMeshObstacles(alliance, adjustsize, excludearea, areasize,
usestrengthcomparison, alwaysblockforts, blockfleets)` enables the
`navmeshobstacle` component on every unit of the **given** alliance, and
`PrepareAllianceObstacles(alliance, …)` calls it for every alliance **except**
the one that is about to path. So retreating alliance 0 turns alliances 1 and
up into walls for the duration of the pathfinding call.

Four filters decide whether a unit blocks:

| Filter | Effect |
| --- | --- |
| `usestrengthcomparison >= 0` and the unit's `ownstrengthinrange` is below it | the unit does **not** block — a weak enemy can be walked past |
| `alwaysblockforts` | a unit with a `garrisonreference` blocks regardless |
| `!blockfleets` and `unittyp == 17` | fleets do **not** block |
| wrong alliance, or inactive in hierarchy | skipped |

Sizing, when `adjustsize` is set:

```
base = IsCampaign ? blockrangecampaign (30) : 75
if the unit has a known nearest enemy: base = min(base, distance to it)
if an excludearea was given:            cap  = distance to it − 1, floored at 0
campaign: obstacle is a cube of min(base, cap) on every axis
battle:   formations (unittyp > 13) use base = 1;
          obstacle is a Box of min(base, width × 3, cap) × 30 × …
```

Three readings:

- **A unit's blocking radius shrinks to its own distance from the nearest
  enemy.** Two forces in contact barely block each other, which is what lets a
  retreat squeeze out of a line rather than being sealed in by the enemy it is
  running from.
- **`blockrangecampaign` is a hardcoded `30f` static with no loader** — not a
  pref, not in any config file. It is the single number that decides how wide
  an enemy army's shadow is on the campaign map, and the only way to change it
  is to set the static.
- **In battle, formations block only 1 unit** while fighting units block up to
  three times their frontage — so the hierarchy's HQ markers do not obstruct a
  retreat.

### 6.1 `debug_navmeshoperationoff` only guards the deactivate path

```csharp
public static bool debug_navmeshoperationoff = false;
…
public static void DeactivateNavMeshObstacles() {
    if (BattleUnits.completeunitlist == null || debug_navmeshoperationoff) return;
```

`ActivateNavMeshObstacles` has **no** such guard. So setting the flag does not
turn the nav-mesh operation off — it turns off the half that **cleans up**,
leaving every obstacle enabled permanently after the first retreat and
`currentlyactivated` stuck on one alliance, which would also freeze the queue
for every other side (§2).

The flag is a bare `false` static with no loader, so it is unreachable from
`Config/debug.txt` and can only be set by a mod. Recorded as a trap rather
than a live defect.

---

## 7. Defects

1. **A cut-off unit retreats toward the enemy.** With no friendly candidate,
   `GetRetreatDestinationLand` returns the nearest enemy's position, and four
   of the six call sites pass an argument that makes the disintegrate check
   miss it (§4.1).
2. **A unit with no known enemy and no candidate disintegrates** instead of
   staying put — the other branch of the same slip (§4.1).
3. **Holding the right mouse button suspends every pending retreat** and the
   obstacle machinery with it (§2).
4. **Every obstacle rebuild delays every pending retreat by two frames**, via
   `IncreaseAllOrdersFrames` in both activate and deactivate, so the nominal
   two-frame delay compounds with AI movement traffic (§2.1).
5. **`debug_navmeshoperationoff` guards only the deactivate path**, so setting
   it leaves obstacles permanently on (§6.1).
6. **The surviving-disintegration dispatch uses the fleet-named id** (75) for
   land formations (§5.4).

Not defects, but easy to misread:

- **`Start()` and `Update()` are empty** — the class never ticks itself (§1).
- **`blockrangecampaign` is a hardcoded static**, not a pref (§6).
- **The disintegration town search is unbounded** (`99999f`) while the
  destination search is capped at 300 (§5.1).
- **The surrender test uses the unit's own frontage** as the enemy-proximity
  threshold, which is why pocketed units often neither escape nor surrender
  (§5.2).

---

## 8. Mod seams

1. **`Retreat.retreatorder` is a public static list** and `RetreatOrder` has
   public fields, so a mod can inspect, retarget or cancel any pending retreat
   — including rewriting `retreatdestination` between the queue and the
   two-frame execution window.
2. **`GetRetreatDestinationLand` and `GetRetreatDestinationBattle` are both
   `public static`** and pure functions of unit state. Postfixing either
   replaces the destination policy without touching the pump, and is the
   cleanest fix for defect 1.
3. **`IsAlreadyRetreating` is public static**, so a mod can test retreat state
   cheaply without walking the list itself.
4. **`blockrangecampaign` and `debug_navmeshoperationoff` are public statics
   with no loader**, so both are set in code or not at all (§6, §6.1).
5. **The five candidate sources in §4 are the natural place to add a new
   retreat anchor** — a depot type, a river crossing, a rally point — since
   they are gathered into one list before being scored.
6. **Do not call `ActivateNavMeshObstacles` without pairing it**, and do not
   assume `currentlyactivated` is yours: `AICampaign` claims the same slot for
   campaign movement and expects to find it free (§2.1).

---

## 9. Open questions

- **Whether defect 1 is observable in play.** The derivation is unambiguous but
  the situation needs a force with no friendly town, fort, depot, trade node or
  unrouted friendly unit within 35–300 of a point 70 behind it. **Untested**:
  the test is a lone brigade deep in enemy territory, ordered to withdraw, with
  the enemy in sight.
- **What `usestrengthcomparison` is set to by `AICampaign`.** The flag that
  lets a weak enemy be walked past is passed from
  `AICampaign.orders[0].usestrengthcomparison`, whose writers were not traced
  (§6).
- **`EntryPoint` retreats.** The battle constructor takes an `EntryPoint` and
  `WorkDownRetreatOrders` attaches it to `retreatthroughentrypoint`, which is
  how a unit leaves the field; the entry-point system itself is undocumented.
- **`Tools.GetClosestPositionAngle`'s cone behaviour** at the 90° boundary, and
  whether the `closest + minrange` tie-break in §4 step 4 can prefer a
  candidate *behind* the enemy.
- **`lastpositions`** — how long a unit's position history is kept, which
  bounds how far tier 1 of the battle search can retrace (§3).
