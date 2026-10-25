# 30 — The four naval subsystems

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Autocalc`
(`UpdateTransportInterception`, `UpdateFortNavyEngagement`,
`UpdateNavalBattle`, `StartTransportInterception`, `PickUnit`), `Vessel`
(`SendToHarbor`, construction), `Regiment` (`UpdateBlockadeEfficiency`,
`SetRetreatToDepot`), `GameVars` (`ShipType`, `Alliance`,
`UpdateShipProductionCapacity`), `BattleUnits`, `IIP`;
`Config/campaignprefs.txt`; `Campaigns/001/*/ships.dat`;
`Campaigns/001/*/groups.dat`
**Summary:** [summary-naval](summary-naval.md)

[26-naval-combat](26-naval-combat.md) §10 left six questions open. This file
closes five of them — transport interception, ship escape, blockade
efficiency, shipyard capacity and construction, and whether the European-only
ship types ever reach a shipped scenario. The sixth needs an asset dump.

**Result:** four live defects, two of them decisive. **Damaged ships always
escape a fleet action**, because the roll that should let them is
`Random.Range(0, 1)` on the int overload. **Shipyard congestion never slows
construction**, because two clamps point in opposite directions and cancel. And
the European intervention squadrons are not hypothetical: **five named fleets,
25 ships, identical in all six campaign starts**, including five of the 92-gun
Screw Ship-of-the-Line.

---

## 1. Transport interception

A fleet catching an enemy army at sea. `BattleUnits` ~2835, inside the
contact check, attaches a fresh `Autocalc` of `type_transportinterception = 5`
when all of:

- the caller is a fleet (`unittyp == 17`) and not `inbattle`;
- the found enemy is **not** a fleet, and its `currentmodeltype` is 1 or 2 —
  that is, it is being drawn as a ship, so it is embarked;
- `embarkmentstarttime == 0f`, so loading has finished;
- neither side is `onretreat`, and the fleet has no interception running.

The fleet then rotates to face the convoy.

### 1.1 Setup, and what the admiral's attributes do

`StartTransportInterception` (~2034) destroys the engine immediately if the
fleet is in battle, if the army has no `shownmodel`, if the army is already
retreating, or if the two are **further apart than the fleet's `buglerange`**.
Otherwise:

```
fleetdistance  = mapDistance × navalbattlefightingdistancefactor      // 65
fleetdistance *= 1 + initiative × (commanderinitiativeinfluenceonnavalrange − 1)   // 1.3
fleetdistance *= 1 + cunning    × (commandercunninginfluenceonnavalrange    − 1)   // 0.7
startfleetdistance = fleetdistance
```

Both attributes are the **intercepting fleet commander's**, and they pull in
opposite directions: at an attribute of 1.0, initiative **lengthens** the
engagement range by 30 % and cunning **shortens** it by 30 %. Since the hit
probability rises as the distance closes (§1.2), a cunning admiral opens the
action at better odds and an aggressive one at worse. The label on each pref
reads *"Influence of commander … on naval combat start range (factor,
1=none)"*, so this is the intent rather than a slip.

### 1.2 Per cycle — and the distance that never closes

`UpdateTransportInterception` runs on the shared autocalc cadence,
`cycleupdateforautocalcs` = **0.00004** game years ≈ **21 minutes** of game
time. Each cycle:

```
num   = elapsed game years since the last cycle
fleetdistance -= fleetdistancereduction × num                    // 1.5
num2  = fleetdistance / max(1, startfleetdistance)               // 1 → 0
num3  = generalhitprobabilitybombardment[0] × num2
      + generalhitprobabilitybombardment[1] × (1 − num2)         // 0.2 … 1.0
num4  = Σ GetFiringStrength(fleetdistance, reduceammo: true, inbattle: true)
if (Random(0,1) < num3)  →  one unit of type 0, 1 or 2 takes
     num5 = transportinterceptionlossfactor × num × num4 × 1000  // 10
     and a morale hit of moralelosstransportinterception / max(1, strength) × num5 × 100   // 0.05
```

The second line is the defect. Compare the three naval paths:

| Method | Closing term |
| --- | --- |
| `UpdateNavalBattle` ~1547 | `fleetdistancereduction × num × **750000f**` |
| `UpdateTransportInterception` ~1047 | `fleetdistancereduction × num` |
| `UpdateFortNavyEngagement` ~1169 | `fleetdistancereduction × num` — but harmless, see §2 |

`1.5 × 0.00004` is **0.00006** per cycle against a `startfleetdistance` of
`mapDistance × 65`, which for any real separation is in the thousands. So
`num2` stays at 1.0 for the life of the engagement.

> **The hit probability is pinned at `generalhitprobabilitybombardment[0]` =
> 0.2 per cycle and never rises.** The designed behaviour is a ramp from 20 %
> at maximum range to 100 % at contact — which is what the naval-battle path
> gets, because it carries a ×750,000 scale factor this one is missing. The
> firing strength is likewise always evaluated at opening range.

### 1.3 The interception always turns the convoy around, and lasts one cycle

The retreat order sits **outside** the hit test:

```csharp
if (Random.Range(0f, 1f) < num3) { … casualties … }
for each top unit of the transported force
    SetRetreatToDepot(its own position, interceptingFleet.buglerange);
```

So every cycle, hit or miss, the whole embarked force is ordered to retreat.
`SetRetreatToDepot` sets `onretreat = true` for a land unit on its first
statement (§5), and the tail of `UpdateTransportInterception` destroys the
engine when `envolvedunits[0].topunits[0].onretreat`.

> **One fleet anywhere on a sea lane aborts any amphibious movement it
> touches**, and the engagement resolves in a single 21-minute cycle with at
> most **one** casualty roll at 20 %. The intercepted army also moves at
> `fleetmovementfactorwhileunderfire` = **0.1** — a tenth speed — for as long
> as the engine is attached (`Regiment` ~22645).
>
> The engine also destroys itself when the two separate beyond the fleet's
> bugle range, when the army's ship model disappears, or when the fleet enters
> a battle.

### 1.4 And the last unit in the convoy is never the casualty

`Autocalc.PickUnit` (~1138) collects every single unit whose `unittyp` is in
the caller's list — `{0, 1, 2}` here, Infantry, Cavalry and Artillery — and
returns

```csharp
return list[Random.Range(0, list.Count - 1)];
```

`Random.Range(int, int)` is max-exclusive, so with *n* candidates it draws from
`[0, n − 2]`. **The last unit in the list can never be picked.** With one
candidate `Random.Range(0, 0)` returns 0 and the single unit is hit, so the
defect only bites from two units upward — which is every real convoy. It is the
same idiom [21](21-ai-battle.md) found in the battle AI's wing selection.

---

## 2. A correction: the method is `UpdateFortNavyEngagement`

[26](26-naval-combat.md) §7.2 describes fort bombardment under the name
`UpdateFortBombardment`. **There is no such method.** The real one is
`Autocalc.UpdateFortNavyEngagement` (~1157); everything else §7.2 says about it
holds.

It carries the same un-scaled `fleetdistance -= fleetdistancereduction × num`
line, but there the line is **dead rather than harmful**, because a few
statements later the method recomputes the distance from live map positions:

```csharp
fleetdistance = Tools.GetXZDistance(fort.position, fleet.position)
              * GamePrefs.navalbattlefightingdistancefactor;
```

Note the order: `num2` and the hit probability are computed *before* that
refresh, from the previous cycle's value. So the fort engagement's hit
probability does track the real geometry — one cycle late — and does ramp from
0.2 to 1.0 as the fleet closes on the fort. The decrement above it changes
nothing.

---

## 3. Ship escape — every damaged ship gets away

Two paths send a damaged vessel home, and both are unconditional in practice.

**Fort engagement** (`UpdateFortNavyEngagement` ~1372). No roll at all: every
vessel that `IsActive(forfighting: false)` and whose `condition` is below
`minconditionshipescape` = **0.35** is logged *"is leaving the fleet and
heading for repairs to the next harbor"* and sent. That is deliberate.

**Fleet battle** (`UpdateNavalBattle` ~1764). Here there is a roll, and it does
nothing:

```csharp
if (vessel.IsActive(forfighting: false) && vessel.condition < minconditionshipescape) {
    float num14 = Mathf.Pow(vessel.speed / max(1, enemySide.GetAverageSideFleetsSpeed()),
                            GamePrefs.escapeprobabilityfactor) * num * 365f;    // 0.3
    if ((float)Random.Range(0, 1) <= num14) {
        … SendToHarbor();
    }
}
```

`Random.Range(0, 1)` resolves to the **int** overload, which is max-exclusive
and therefore returns **0** every time. The test is `0f <= num14`, and `num14`
is a non-negative power of a non-negative ratio, so **the condition is always
true**.

> **Every ship below 35 % condition leaves a fleet action on the cycle it
> crosses the threshold.** The speed-weighted escape chance is computed and
> thrown away. Its intended magnitude was small: at an even speed match,
> `1^0.3 × 0.00004 × 365` ≈ **1.5 % per 21-minute cycle**, a slow trickle of
> crippled ships leaving over hours. What ships instead is an immediate,
> certain departure.
>
> **This is why fleet actions are indecisive, and it undercuts ship capture.**
> `FinishNavyBattle` transfers a losing vessel only if
> `condition < conditionfordisabledships` (**0.25**) **and** `!IsActive()`
> ([26](26-naval-combat.md) §7.3) — and the escape threshold of 0.35 sits
> *above* that, so a ship must fall from above 0.35 to disabled inside one
> cycle to be capturable at all. The eight ship types no project grants are
> still obtainable, but far more rarely than §7.3 implies.
>
> The fix is one character: `Random.Range(0f, 1f)`.

### 3.1 Repairs are paid for the moment the ship detaches

`Vessel.SendToHarbor(IIP newharbortoreturn = null)` (~728) returns early if the
ship is already under way (`timeofarrival > 0f`), already has a
`harbortoreturn`, or is still under construction. Otherwise it picks the
fleet's `closestharbor` or, failing that, `GetClosestHarbor` for its alliance,
and then **immediately charges the economy**:

- `repairtimer = 1 − condition`, clamped to `[0, 1]`;
- `Economy.Consume(shiptype.creationresources, …, repairtimer, harbour)` — that
  fraction of the ship's **build cost**, plus an `AddAODemand` of the same;
- a top-up of coal, provisions and artillery ammunition to full, with coal
  skipped for a ship whose `coalconsumptionstationary` is zero.

So a vessel reduced to 10 % condition costs 90 % of a new hull the instant it
turns for port, and the bill lands at the harbour it is heading for rather than
where it is. If no harbour can be found the method returns before any of this,
and the ship stays with the fleet.

---

## 4. Blockade efficiency

Three quantities, built in two places.

### 4.1 A fleet's raiding efficiency

`Regiment.UpdateGroupStrength` ~10590, for `unittyp == 17`:

```
cunningBonus = 1 + cunninginfluenceonraiding × cunning                  // 0.5
groupraidingefficiency += lowestshipspeed × combatvalue
                        × fleetblockadefactor × cunningBonus            // 5.0
blockadeefficiencyfactors[0] = lowestshipspeed / (lowestshipspeed + combatvalue)
blockadeefficiencyfactors[1] = combatvalue     / (lowestshipspeed + combatvalue)
blockadeefficiencyfactors[2] = cunningBonus − 1
```

`lowestshipspeed` is the **slowest ship in the fleet**, so one laggard caps the
whole squadron's raiding and blockading value — the same aggregation
[26](26-naval-combat.md) §3 found for fleet speed. `combatvalue` is the fleet's
flagship value per the config label. `groupraidingefficiency` is also what the
raiding and skirmishing autocalcs read (`Autocalc` ~2351, ~2355, ~2382, ~2509,
~2558).

`blockadeefficiencyfactors` is a `float[4]` and all four slots are **display
only** — `Tooltip` ~1007/~1017/~1022/~1027 are their sole consumers. Slot 3 is
set elsewhere, in §4.2, to the number of nodes the fleet is covering.
[26](26-naval-combat.md) §10 called it a two-slot array; it is four.

### 4.2 The per-node value, and why spreading a fleet wide is free

`Regiment.UpdateBlockadeEfficiency` (~4692) builds the number `IIP` actually
reads. It first collects the nodes in range that count — a node whose owner is
at war with the fleet's alliance, **or** any trade node at all if the fleet has
perk 6 (**Sea Raiders**) — filtered to harbours for a fleet. Then:

```
for each node j in range:
    guard_j = Σ over units in range of node j, of other alliances:
                  land units with cavalryorders == 3 (Guard)
                + fleets    with fleetorders   == 1 (Blockade)
              → their groupguardefficiency
    num2 += groupraidingefficiency − guard_j
num2 /= nodeCount
blockadeefficiencyfactors[3] = nodeCount
blockadeefficencyvalue = (num2 < 0) ? 0
                       : sqrt(num2) × blockadeefficiencyfactor            // 0.004
```

Note the arithmetic. `groupraidingefficiency` is constant inside the loop, so
summing it *n* times and dividing by *n* gives it back untouched, while the
**enemy guard terms are averaged**.

> **A blockading fleet applies its full raiding efficiency to every node it
> covers, and the enemy's guards are divided across all of them.** Covering
> four harbours instead of one does not weaken the blockade of any single
> harbour — and a single enemy guard squadron stationed at one of those four
> reduces the blockade at *each* of them by only a quarter of its strength.
>
> Two consequences worth stating plainly. **Spread a blockading fleet as wide
> as its range allows.** And **land cavalry on Guard orders defends a trade
> node against a naval blockade** — the guard sum does not distinguish
> `unittyp`, it only separates the land test (`cavalryorders == 3`) from the
> fleet test (`fleetorders == 1`), and both add the same
> `groupguardefficiency`.
>
> A third: **Sea Raiders (perk 6) lets a fleet blockade trade nodes belonging
> to powers it is not at war with**, since `IsTradeNode && HasPerk(6)` is an
> alternative to the at-war test. That is also the perk whose level check
> [19](19-commanders-and-perks.md) found is missing its `unittyp == 17` guard.

`IIP` ~665 then consumes it:

```
blocked += blockadeefficencyvalue
         × (within buglerange ? blockadeefficiencyfactorcombatrange : 1)   // 2.5
         × (1 + GetProjectLevel(106) × project_blockadeefficiency)         // 0.05
```

so closing to combat range **multiplies a blockade by 2.5**, and Trade Warfare
adds 5 % a level ([28](28-project-effect-reachability.md) §4.5). The field is
spelled `blockadeefficencyvalue` in the assembly — one `i` short — which is
worth knowing before grepping for it.

---

## 5. `SetRetreatToDepot` — one method, two systems, and 123 dead lines

The campaign-wide retreat entry point, called from **nine** sites: campaign
withdrawal (`BattleUnits` ~2790, ~2937, ~14270), the fleet autocalc paths
(`Autocalc` ~1388, ~1428), transport interception (~1078), a regiment's own
retreat triggers (`Regiment` ~6533, ~6559) and a debug panel. Its structure
matters because two thirds of it cannot run.

```
SetRetreatToDepot(fromposition, minrange = 0, maxrange = 9999)
  6672  if (garrisonreference != null) return;        // garrisons never retreat
  6676  lastretreatiswithdrawal = false;
  6685  if (unittyp != 17) { new Retreat.RetreatOrder(…); onretreat = true; return; }
  6691  if (debug_useretreatheatmaps) { _ = GameVars.frame; _ = 37; }   // two discards
  6696  if (unittyp != 17) { … 123 lines … }          // UNREACHABLE
  6819  …the fleet path…
```

**Every land unit returns at 6689**, so the second `if (unittyp != 17)` —
lines 6696 to 6818 — can never be entered. What it contains is a complete
alternative retreat implementation: a `retreatheatmap.EvaluatePath` call, a
search for a Town, a `CBuilding`, an `IIP` or a friendly `Regiment` to fall
back on, and `DisintegrateUnit` fallbacks when none is found.

> That is **superseded rather than lost**. The live land path is
> `Retreat.RetreatOrder`, which enqueues the retreat for
> `Retreat.WorkDownRetreatOrders()` — one of the three things
> [10](10-simulation-tick.md) Finding 7 found still runs while the game is
> paused — so the inline search was replaced by a queued one and left behind
> the same guard. The replacement keeps the same five candidate sources, which
> is how [31-retreat](31-retreat.md) §4 could confirm the intent. Worth
> knowing before patching: a mod that hooks `SetRetreatToDepot` expecting the
> depot search to run will find it does not.

### 5.1 For a fleet, retreating means finding a harbour or ceasing to exist

Lines 6819 onward are the fleet branch, and they are live:

1. if the fleet is within `buglerange` of `startpointforpatrols`,
   `SetFleetOrders(0)` — it stands down;
2. if it is **raiding** (`fleetorders == 3`) and `fleetref.averagecondition`
   is above `minconditionforraid` = **0.70**, it is waypointed back to
   `startpointforpatrols` at double-quick and returns — a raider breaks off
   rather than running home;
3. otherwise a terrain probe sets `flag3` for open sea or `includeriverports`
   for river water, and `GetClosestHarbor` is called within a range of
   `max(buglerange × 1.5, nearestEnemyDistance × 1.5)`, raised to
   `commanderrange` if that is larger;
4. the fleet is waypointed to that harbour at double-quick, with
   `useorderdelay: false`.

Three paths end in **`DisintegrateUnit()`**: no harbour found at all, a harbour
whose position is not on water terrain with no water field within 3, and a
waypoint path whose points are not all on water.

> **A fleet ordered to retreat with no reachable friendly harbour is
> destroyed.** Not damaged, not stranded — removed. That is the fleet
> equivalent of the land `DisintegrateUnit` fallback, and it is the one real
> risk in sending a squadron far from its own coast.

---

## 6. Shipyard capacity and construction

Three gates decide whether a ship type can be laid down, and one number
decides how fast — except that it does not.

### 6.1 The three gates

`GameVars.UpdateShipProductionCapacity()` (~7819) recomputes, per alliance,
from `BattleUnits.harbors`:

```
shipconstructioncapacity += porttonnagelevels[harbour.BuildingLevel]
                          × harbour.condition
                          × (1 − harbour.blocked)
hasseaport  |= harbour.isseaport
hasriverport|= harbour.isriverport
if (harbour.BuildingLevel > highestportlevel) highestportlevel = harbour.BuildingLevel
```

`porttonnagelevels` = **1500 / 2000 / 3000 / 5000**, so a level-3 port is worth
3.3 level-0 ports. Both a **damaged** and a **blockaded** harbour contribute
less: condition scales linearly and `blocked` subtracts directly, so a harbour
at half condition under a 40 % blockade yields 30 % of its rated tonnage.

The three gates a type must pass, all on `ShipType`:

| Method | Test |
| --- | --- |
| `HasAvailablePort(alliance)` | a sea port for a `seamovement` type, or a river port for a `rivermovement` one |
| `HasPortLevel(alliance)` | `highestportlevel + 1 >= requiredportlevel` |
| `HasCapacity(alliance)` | `shipconstructioncapacity − shiptonnageunderconstruction >= tonnage` |

### 6.2 `highestportlevel` is a latch

The reset loop at ~7825 clears `shipconstructioncapacity`,
`shiptonnageunderconstruction`, `shipsunderconstruction`, `shipsunderrepair`,
`hasseaport` and `hasriverport` — and **not `highestportlevel`**. The harbour
loop below only ever raises it.

> **Losing your best harbour does not lose you the ship types it unlocked.**
> Once an alliance has held a level-3 port for one evaluation, `HasPortLevel`
> keeps returning true for the rest of the campaign however the port is
> captured, blockaded or levelled. The capacity *total* does fall, so the
> `HasCapacity` gate still bites; only the level requirement latches.
>
> The same pattern as `nation[].occupiedby`
> ([28](28-project-effect-reachability.md) §6.5): a field reset nowhere and
> raised monotonically.

### 6.3 Shipyard congestion never slows a ship that has been laid down

`Vessel`'s per-tick construction progress (~507):

```csharp
float b = Mathf.Min(1f, shiptonnageunderconstruction / Mathf.Max(1f, shipconstructioncapacity));
float num2 = timediff / Mathf.Max(1E-05f, constructionDuration * Mathf.Max(1f, b));
```

`b` is the yard utilisation ratio, and the intent of `Mathf.Max(1f, b)` is
plainly to stretch the build time once utilisation passes 1. But the line above
has already clamped `b` to **at most** 1 with `Mathf.Min(1f, …)`, so
`Mathf.Max(1f, b)` is **always exactly 1**.

> **Two clamps pointing in opposite directions cancel, and congestion has no
> effect on build speed.** Capacity decides whether you may *start* a hull;
> once started, it builds at `constructionDuration` regardless of how many
> others share the yards. Remove the `Mathf.Min` and the designed behaviour
> returns.
>
> It is the third instance of this shape in the naval code, after the
> rate-of-fire distance ratio that can never exceed 1
> ([26](26-naval-combat.md) §4) and the armour display band that can never be
> reached ([26](26-naval-combat.md) §2).

`CampaignTopPanel` ~1137 and `LogisticsPanel` ~341 show the player the
utilisation and the total, so the figure is visible and honest — it simply does
not feed the simulation.

---

## 7. The European intervention fleets are real, and identical in every start

[26](26-naval-combat.md) §10 asked whether the European-only ship types ever
appear in a shipped intervention force. They do.

`Campaigns/001/<letter>/ships.dat` is a count header followed by **23-line**
records, whose first five fields are an index, the ship's name,
`attachedtofleetgroupid`, `attachedtopool` and `shiptype` (`BattleUnits` ~4709).
`groups.dat` is a count header and **32-line** records beginning
`groupid`, `name`, `tempgroupref`, `alliance` (`BattleUnits` ~10219).

Joining the two gives five pre-placed fleets, **25 ships**, byte-identical in
all six campaign starts — A, B, C, D, E and F:

| Group | Alliance | Name | Composition |
| --- | --- | --- | --- |
| 20 | **3 France** | French Intervention Fleet | 3× Sail Spardeck Corvette (29), 2× Converted Screw Frigate (31), 2× Screw Spardeck Corvette (32) |
| 21 | **4 Spain** | Spanish Intervention Fleet | 3× Screw Schooner (34) |
| 22 | **2 Britain** | British Intervention Fleet | 3× Screw Ship-of-the-Line (35) |
| 23 | **2 Britain** | North American Squadron | 2× Screw Ship-of-the-Line (35), 1× Warrior Class (36), 4× Steam Corvette (37) |
| 24 | **3 France** | Western Coast Fleet | 1× Ironclad Ship-of-the-Line (30), 4× Fast Screw Ship-of-the-Line (33) |

So the heaviest ships in the game are on the map from day one, in neutral
hands:

- **five Screw Ship-of-the-Line (type 35)** at 9,150 t and **92 guns** — the
  largest and most heavily armed hull in `shiptypes.dat`;
- **four Fast Screw Ship-of-the-Line (33)** at 80 guns;
- the **Warrior Class (36)**, which project 41 otherwise sells for $20 M;
- and the *Normandie* — the French ironclad the newspaper catalogue names in
  events 4 and 16 ([25](25-newspaper-and-events.md) §2.6) — as the type-30
  hull in group 24.

That settles two earlier notes. [26](26-naval-combat.md) §7.3's observation
that capture *"is how a player can acquire the eight types no project grants"*
has identifiable targets rather than a theoretical path; and §3 above explains
why it is still hard, since a damaged ship leaves at 35 % condition and capture
needs it disabled below 25 %.

> **The fleets exist whether or not the intervention script ever fires.** They
> are scenario data, not a reward for the intervention track
> ([17](17-politics-and-support.md) §4), so they are on the map from the first
> frame of every start. What the intervention script controls is whether their
> alliance goes to war ([25](25-newspaper-and-events.md) §2.8), which is what
> brings them within reach of a player fleet. Their starting positions live in
> each scenario's `regiments.dat` and were not read here.

---

## 8. Defects

1. **Transport interception's closing distance never closes.** It omits the
   `× 750000f` scale factor its sibling `UpdateNavalBattle` carries, so the hit
   probability stays at `generalhitprobabilitybombardment[0]` = 0.2 per cycle
   instead of ramping to 1.0, and firing strength is always evaluated at
   opening range (§1.2).
2. **Every damaged ship escapes a fleet action.**
   `(float)Random.Range(0, 1) <= num14` uses the max-exclusive int overload, so
   the left side is always 0 and the test always passes. The speed-weighted
   escape chance — about 1.5 % per cycle at an even speed match — is computed
   and discarded (§3).
3. **Shipyard congestion never slows construction.**
   `Mathf.Min(1f, utilisation)` followed by `Mathf.Max(1f, b)` always yields 1
   (§6.3).
4. **`highestportlevel` is never reset**, so the port-level requirement on a
   ship type latches permanently once met (§6.2).
5. **`Autocalc.PickUnit` cannot pick the last candidate** —
   `Random.Range(0, list.Count - 1)` (§1.4).
6. **The transport-interception retreat order is outside the hit test**, so an
   interception aborts the convoy whether or not it inflicts anything (§1.3).
   Whether that is a defect or the design is a judgement call; it is recorded
   here because nothing in the interface says so.
7. **123 lines of `SetRetreatToDepot` are unreachable** behind a second
   `if (unittyp != 17)` that the first one's early return makes impossible
   (§5).
8. **`UpdateFortNavyEngagement`'s `fleetdistance` decrement is dead**,
   overwritten by a recompute from map positions a few statements later (§2).
9. **Two discarded statements** in `SetRetreatToDepot`'s
   `debug_useretreatheatmaps` block — `_ = GameVars.frame; _ = 37;` — the same
   pattern catalogued in [23](23-game-state.md) §7.5.

Naming note: the field is spelled **`blockadeefficencyvalue`** in the
assembly, and [26](26-naval-combat.md) §7.2's `UpdateFortBombardment` should
read `UpdateFortNavyEngagement`.

---

## 9. Mod seams

1. **Four of the nine defects are one-token fixes**: add `× 750000f` to the
   interception's closing term, change `Random.Range(0, 1)` to
   `Random.Range(0f, 1f)`, drop the `Mathf.Min(1f, …)` from the construction
   congestion factor, and reset `highestportlevel` in
   `UpdateShipProductionCapacity`'s clearing loop.
2. **`Autocalc.PickUnit` is private and shared** by the interception, raiding
   and skirmishing paths, so fixing its off-by-one fixes all of them at once.
3. **The escape fix is the highest-leverage change in the naval code.** With it
   in place, `minconditionshipescape` (0.35) and `escapeprobabilityfactor`
   (0.3) become real knobs, damaged ships start being lost, and ship capture
   ([26](26-naval-combat.md) §7.3) becomes reachable rather than incidental.
4. **Blockade is tuned entirely from config**: `fleetblockadefactor` (5.0),
   `blockadeefficiencyfactor` (0.004), `blockadeefficiencyfactorcombatrange`
   (2.5), `cunninginfluenceonraiding` (0.5) and `minconditionforraid` (0.70).
   Only the node-averaging in §4.2 needs code.
5. **The five European fleets are scenario data.** Editing
   `Campaigns/001/<letter>/ships.dat` and `groups.dat` changes what an
   intervention brings, with no code involved — 23-line and 32-line records
   respectively, field orders in §7.
6. **Do not hook `SetRetreatToDepot` expecting the depot search to run.** For
   land units it enqueues a `Retreat.RetreatOrder` and returns; the inline
   search is dead (§5). The live surface is `Retreat`, 624 lines, still
   unwritten.

---

## 10. Open questions

- **The tooltip string table** — [26](26-naval-combat.md) §10's first item,
  still the only one of the six that needs an asset dump.
- ~~**`Retreat`** (624 lines).~~ **Written up** as
  [31-retreat](31-retreat.md), which found that a cut-off unit with no friendly
  fallback is sent **toward the nearest enemy**, and that holding the right
  mouse button suspends every pending retreat in the game.
- **Whether the interception's one casualty roll matters.** At 0.2 probability
  and `10 × 0.00004 × firingStrength × 1000` casualties, the magnitude depends
  entirely on `GetFiringStrength`, whose scale [26](26-naval-combat.md) §4
  gives but which was not evaluated against a real squadron here.
- **Where the five European fleets start.** Their positions are in each
  scenario's `regiments.dat`, unparsed.
- **Whether a fleet can actually be destroyed by `SetRetreatToDepot`'s
  `DisintegrateUnit` fallbacks in normal play**, or whether a reachable harbour
  always exists on the shipped map. **Untested**: the derivation is clear, the
  situation may be unreachable in practice.
