# 28 — Project effect reachability audit

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` → `Projects`, `GameVars` (`Alliance`,
`Nation`, `Weapon`, `ShipType`), `Economy`, `IIP`, `CBuilding`, `Regiment`,
`BattleUnits`, `Policies`, `AICampaign`, `BuildingList`, `CBuildingIcon`,
`ImportExportUnitData`, `DLC_WL`; `Config/projects.dat`;
`Config/campaignprefs.txt`; `Config/shiptypes.dat`
**Summary:** [summary-effects](summary-effects.md)

[22-policy-and-project-catalogue](22-policy-and-project-catalogue.md) §12
catalogued what each project *claims* to do and closed with the note that *"a
reachability audit of the remaining ~120 project effects is still
outstanding."* This is that audit, prompted by three consecutive passes each
turning up an effect the previous sweep had missed.

**Result:** of the **69 real projects**, **four cannot be appointed at all** in
a base-game install, and **twelve more have an effect that does not reach its
target, reaches the wrong subject, or reaches far more than it was meant to**.
The remaining **53 are live and correct**. Six of the twelve failures are new
here; the other six confirm and sharpen earlier readings.

Closes open question 3 of
[09-policy-and-project-effects](09-policy-and-project-effects.md) and the
outstanding audit in [22](22-policy-and-project-catalogue.md) §12.3.

---

## 1. Scope

`projects.dat` holds **125 records**, ids 0–124, of which **56 are
`PLACEHOLDER`** (20–29 and 42–87). The 69 real ones divide cleanly:

| ids | Count | Kind |
| --- | --- | --- |
| 0–19 | 20 | Weapon unlocks |
| 30–41 | 12 | Ship-type unlocks |
| 88–124 | 37 | Reforms and modifiers |

Record shape is **18 lines**
([06-config-file-format](06-config-file-format.md)), and two of its fields
matter here that no earlier file used:

| Line | Field | Why it matters |
| --- | --- | --- |
| 7 | `repeating` | Decides whether `GetProjectLevel` can exceed 1 |
| 17 | `applicablescenarios` | `001;`, `002;` or both — and `002` ships with the DLC |

`GetProjectLevel(id)` (`GameVars` ~3772) is a **count of occurrences in the
alliance's `projects` list**, so a repeating project's level is how many times
it has been bought. `IsAppointable` (~795) blocks a non-repeating project above
level 0 and every project at `levelgraphics.Length`, so the `1 + level × pref`
shape is safe for non-repeating projects by construction.

One cosmetic note on the data: `repeating` is authored inconsistently as
`True`/`False` for some records and `true`/`false` for others (113, 114).
`bool.Parse` is case-insensitive, so nothing breaks.

---

## 2. Method

Four passes, each catching a class the previous one cannot:

1. **Pref-level sweep.** All **33** `project_*` prefs in `GamePrefs`, every
   reference outside `GamePrefs.cs`, classified as a live read, a discarded
   statement, or the panel description generator
   (`Projects.GetProjectEffect`).
2. **Target-level sweep.** For every effect that writes a field rather than a
   local — `transportupkeepadj`, `railroadspeedadj`,
   `weaponproductionadjustment`, `recruitmentprojectfactor(target)`,
   `groupintelligencegathering`, `interventionlevels`, `weaponstock`,
   `availableforalliance` / `techavailableforalliance` /
   `importavailableforalliance` — whether that field has a live reader.
3. **Local-liveness check.** For every effect that writes a local, whether the
   local is used downstream. This is what caught project 98.
4. **Condition reading.** For every effect inside an `if`, whether the
   condition can be true and whether it reads the right subject. This is the
   expensive pass and the only one that catches 113, 114, 122 and the ship
   import market; no mechanical sweep finds them.

Pass 4 is why the earlier sweeps kept missing things. Passes 1 and 3 are
mechanical and between them find only the two discarded-statement failures
(projects 98 and 107); every other failure in §6 needs a human reading a
condition, and the six classes fail in six different ways.

---

## 3. Four projects cannot be appointed at all

`IsAppointable` (~795) rejects a project whose `applicablescenarios` does not
contain `GamePrefs.leveltoload`, or whose `FulfillsDLC()` is false. The
`datefrom` grant path, `CheckProjectUnlocks` (~1034), applies **both** of the
same tests, so there is no back door.

The install ships **one** campaign directory, `Campaigns/001`, and
`DLC_WL.Initialize` requires `Directory.Exists("Campaigns/002")` among
seventeen conditions before setting `dlc_active`. **So scenario `002` is the
DLC campaign**, and `MainMenu` ~1053 hides any campaign with `mapid == "002"`
when the DLC is absent.

| id | Name | Why it is unreachable here |
| --- | --- | --- |
| **88** | Command Reform | `requireddlcids = 01;` → `FulfillsDLC()` false |
| **89** | Organization Reform | `applicablescenarios = 002;` |
| **123** | Horse Artillery | `applicablescenarios = 002;` |
| **124** | 6-gun Batteries | `applicablescenarios = 002;` |

Each has a scenario-`001` twin, which is what the pairs in
[22](22-policy-and-project-catalogue.md) §12.4 were: **90** Organization Reform
is the `001` twin of 89, **111** Cavalry Reform II of 123, and **112**
Artillery Reform of 124. The design is a parallel reform set per campaign, not
duplicated content.

That resolves the hedge in §12.4. `Alliance.HasOrganizationReform()` returns
`projects.Contains(90)` when `commandhierarchyshift == 0` and
`projects.Contains(89)` when it is `−1`; the shipped value is **0**
([19](19-commanders-and-perks.md) §4), so in scenario `001` the live project is
**90** — and 89 could not be bought there even if the pref were `−1`.

### 3.1 Army Groups are a DLC feature, and the base game cannot unlock them

`Alliance.AllowsArmyGroups()` (`GameVars` ~3733) is:

```csharp
if (!allowarmygroups) return false;
if (projects.Contains(89) | projects.Contains(88)) return true;
return false;
```

It checks **88 or 89 — not 90**. In a base-game install both are unappointable
(§3), so the second test can never pass. And the first cannot either:
`GameVars.allowarmygroups` has a field initialiser of `true`, but
`DLC_WL` ~3457/~3464 **assigns it** — `true` with the DLC, **`false` without**,
alongside `allowgarrisondeployment`.

> **Army Groups cannot be unlocked in a base-game install, for two independent
> reasons.** The flag is switched off by the DLC check, and the two projects
> that would satisfy the project test are both unreachable without the DLC.
>
> With the DLC present the design closes up neatly, because project 88 is
> `requireddlcids = 01;` **and** `applicablescenarios = 001;` — it is the
> base campaign's unlock, available only once the DLC is installed — while 89
> is the DLC campaign's. So Army Groups work in either scenario with the DLC
> and in neither without it.
>
> This closes the `allowarmygroups` open question in
> [19](19-commanders-and-perks.md) and corrects
> [22](22-policy-and-project-catalogue.md) §12.4, which read the flag as a pref
> that is "necessary but not sufficient". It is neither a pref nor a
> prerequisite — it is the DLC switch.

Four other base-game consequences of the scenario split, all correct rather
than defective:

- **The cavalry size penalty disappears in scenario `002`.**
  `BattleUnits.GetMaxUnitSize` ~15104 applies
  `project_nocavalryreformsizefactor` (**0.2**) only when
  `!projects.Contains(110) && Projects.IsProjectAvailableInScenario(110)`, and
  110 is `001`-only — so in `002` cavalry brigades are full size for free.
- **The artillery penalty has no such guard.** ~15108 applies
  `project_noartilleryreformsizefactor` (**0.375**) whenever neither 112 nor
  **124** is held. 112 is `001`-only and 124 is `002`-only, so exactly one of
  the two is buyable in either scenario and the penalty is always clearable —
  but only because the pair covers both scenarios. A mod that removes 124 from
  `002` would leave artillery permanently at 37.5 % size with no remedy.
- `Alliance.IsHorseArtilleryAvailable()` reads **123 in `002` and 111
  elsewhere**, matching the split exactly.
- Project **18** Springfield Rifles (CSA, policy 177) is `001`-only while
  **19** (Union, from 1 June 1861) is in both — so the two routes to weapon 14
  are not symmetric across scenarios.

The split cuts the other way too. Twelve real projects are `001`-only — **18,
40, 41, 88, 90, 93, 94, 103, 110, 111, 112, 119** — so a scenario-`002`
campaign has no Occupation Administration, no Suppress Population, no Send
Envoys and no Railroad Construction, and only one of the two routes to weapon
14. Everything else is available in both.

---

## 4. The five mechanisms

Which one a project uses is not visible from the data file, and the five do not
reach their targets in the same way.

### 4.1 Weapon unlocks — ids 0–19

`UpdateProjectEffects()` (~495), re-run for every alliance and every held
project. Two calls, with different meanings:

- **`Weapon.SetAsImported(alliance)`** adds the alliance to
  `importavailableforalliance` — foreign purchase. Projects **0** (weapons 17,
  83), **1** (15, 16, 23), **2** (7, 8, 11, 13), **3** (46, 1), **4** (47, 84).
- **`Weapon.SetResearched(alliance, w)`** (static, `GameVars` ~5218) adds to
  `weapon[w].techavailableforalliance` — domestic production. Projects **6**
  (21, 22, 85), **7** (1, 5), **8** (4, 9), **9** (3, 6, 39), **10** (37, 38),
  **11** (2, 12), **12** (81), **13** (18, 45), **14** (26, 27, 32, 33), **15**
  (28, 30, 31), **16** (24, 25), **17** (34, 35, 44), **18** (14), **19** (14).

Both are read back through `Weapon.IsResearched(alliance)` and
`Weapon.IsImported(alliance)`, whose consumers are the AI's weapon-order
chooser (`AICampaign` ~6871, ~6888) and the player's weapon list
(`CampaignMainPanel` ~860), which **removes an unavailable weapon from the
list** rather than greying it. So the gate is list membership, and it is real
for both sides. **All 20 live.**

Note that `Weapon` carries **only** the two availability lists;
`importonmarketforalliance`, the metered stock of purchasable hulls, exists on
`ShipType` alone (§4.2). So a weapon import, once bought, is permanent and
unlimited.

One trap in the reader, latent today. `Weapon.GetAvailability(int alliance)`
(`GameVars` ~5143) **ignores its parameter**: all three tests inside it read
`IsResearched(playeralliance)` and `IsImported(playeralliance)`. Its four call
sites all pass `GameVars.playeralliance`, so nothing is wrong in the shipped
game — but it returns one of five strings (*"Not produced"*,
*"Production only"*, *"Import only"*, *"Production & Import"*, and a localised
fifth from `Tooltip.GetTooltipText(1831, 0)`), and `Weapon.AbleToOrder` — the
real gate on the order button at `WeaponList` ~522 — decides by **string
equality** against two of them. A hook that asks whether a weapon is available
to the *other* side gets the player's answer, and a localisation that changes
*"Not produced"* makes every weapon orderable.

### 4.2 Ship unlocks — ids 30–41

Eight projects add directly to `shiptypes[t].techavailableforalliance` —
**30** (types 19, 27), **31** (25, 26), **32** (8, 13), **33** (14), **34**
(28), **35** (15, 16), **36** (24, 38), **37** (22, 23) — read back through
`ShipType.IsResearched`, whose consumer is `ShipRecruitingList` ~433/~437.
**All eight live.**

Four call `ShipType.SetAsImported(alliance)` instead, and these are the only
callers anywhere: **38** → type 17, **39** → 18, **40** → 30, **41** → 36.
Those four types are the only ones in `shiptypes.dat` with a non-`−1`
`importfromallianceid`:

| Project | Ship type | Supplier | Tonnage |
| --- | --- | --- | --- |
| 38 British Warships | 17 Ironclad Turret Ship | Britain (2) | 2,800 |
| 39 French Warships | 18 Ocean Ironclad Ram | France (3) | 1,350 |
| 40 Gloire Class | 30 Ironclad Ship-of-the-Line | France (3) | 5,600 |
| 41 Warrior Class | 36 Warrior Class | Britain (2) | 9,150 |

`ShipType.SetAsImported` (`GameVars` ~2865) does two things:

```csharp
if (!importavailableforalliance.Contains(alliance) && importv) {
    importavailableforalliance.Add(alliance);
    for (int i = 0; i < Random.Range(1, 5); i++)
        importonmarketforalliance.Add(alliance);
}
```

`importavailableforalliance` is the permanent flag.
**`importonmarketforalliance` is a stock of slots**, one entry per available
hull, and the initial grant is `Random.Range(1, 5)` — the int overload, so
**1 to 4** slots, decided by a dice roll at the moment of purchase.

A slot is spent in `BattleUnits` ~4853, when a ship of that type is laid down:

```csharp
if (shiptypes[shiptype].importonmarketforalliance.Contains(num2)) {
    AdjustTreasury(-GetResourcesCost(creationresources, …));
    shiptypes[shiptype].importonmarketforalliance.Remove(num2);
    vessel.constructiontimer = 0f;          // instant
}
else  Economy.Consume(creationresources, …);  // normal build
```

So an imported hull is **paid for out of the treasury, consumes no goods from
the economy, and is finished immediately**. That is a materially different
transaction from building the same ship, and nothing in the catalogue says so.
`ShipRecruitingList` ~534/~557 is what shows the player an import is on offer.

#### 4.2a The import market restocks daily, and closes permanently

`ShipType.UpdateMarket(timer)` (~2814), driven once per campaign frame through
`GameVars.UpdateShipImports()` (`BattleUnits` ~1558):

```csharp
if (timer - lastmarketupdate < GamePrefs.shiptypeupdatecycle) return;   // 0.002 yr
lastmarketupdate = timer;
for (int i = 0; i < 2; i++) {
    if (HasImport(i)) {
        if (Random.Range(0f, 1f)
            < 1f / GamePrefs.probabilityofshiponeuropeanmarkets * timer / tonnage * 2000f)
            importonmarketforalliance.Add(i);
    }
    else Tools.RemoveAllEntriesFromList(importonmarketforalliance, i);
}
```

Two defects compound here, and together they invert the mechanic.

**The probability argument is the absolute campaign date, not the elapsed
cycle.** `GetCampaignFloatingDate()` (`GameVars` ~7792) returns
`year + month/12 + day/365.25 + …` — about **1862** mid-campaign. The config
label reads *"appears every xth year (weighted with tonnage afterwards…)"* with
`probabilityofshiponeuropeanmarkets` = **0.25**, so the intended expression is
plainly `cycle / 0.25 × 2000 / tonnage`. Shipped, it is
`1862 / 0.25 × 2000 / tonnage`:

| Ship type | Intended per cycle | Shipped per cycle |
| --- | --- | --- |
| 18 Ocean Ironclad Ram | 0.0119 | **11,000** |
| 17 Ironclad Turret Ship | 0.0057 | **5,300** |
| 30 Ironclad Ship-of-the-Line | 0.0029 | **2,700** |
| 36 Warrior Class | 0.0017 | **1,600** |

Every one is enormously greater than 1, so **the roll always succeeds**. The
cycle is `shiptypeupdatecycle` = 0.002 game years ≈ **0.73 days**.

**The restock is gated on there already being stock.** `HasImport(i)` (~2912)
is `importavailableforalliance.Contains(i)` **and**
`importonmarketforalliance.Contains(i)` — it requires a slot to exist. So the
moment the last slot is spent the condition goes false, the `else` branch runs
(a no-op on an already-empty list), and the branch that would restock can never
be reached again. The test the author wanted is the first clause alone.

> **Hold one import slot back and you get a free, instantly-completed ironclad
> roughly every three-quarters of a day, for the rest of the campaign. Spend
> your last one and that ship type is closed to you forever.**
>
> The slots also round-trip through the save — `ImportExportUnitData` ~583/~609
> writes a count and one line per entry — so an unspent stock inflates every
> save from then on.
>
> **Untested** in play: the arithmetic is unambiguous but the behaviour has not
> been observed. The test is a campaign with project 38 appointed, building
> that ship type repeatedly while always leaving one on offer, and watching
> whether the offer replenishes.

---

### 4.3 Push, continuous — two projects

Also inside `UpdateProjectEffects()`, which runs at **frame 37** of a campaign
(`CampaignController` ~439) and again inside `AppointProject` (~872):

| id | Writes | Pref | Shipped | Reader |
| --- | --- | --- | --- | --- |
| 101 Military Railroad | `alliance.transportupkeepadj` | `project_militaryrailroadupkeep` | **−0.5** | `Alliance.GetTransportUpkeep` ~4163 |
| 101 | `alliance.railroadspeedadj` | `project_militaryrailroadtransportspeed` | **0.5** | `Regiment` ~7182, ~22092 |
| 102 Weapon Production | `alliance.weaponproductionadjustment` | `project_weaponsproduction` | **0.05** | `Alliance.GetProductionFigures` ~5281, ~5324 |

All three live. Project 101 is non-repeating, so its level is 0 or 1 and
`transportupkeepadj` is 1.0 or **0.5** — transport upkeep halves and railroad
speed rises 50 %. The `1 + level × (−0.5)` form would reach zero at level 2 and
go negative at level 3, which only the non-repeating flag prevents.

### 4.4 Push, once — twelve projects

`UpdateOneTimeProjectsEffects(id, alliance)` (~392), called from
`AppointProject` and from `CheckProjectUnlocks`. Twelve arms, all live, but
three with conditions worth stating:

| id | Effect | Live? |
| --- | --- | --- |
| 5 Hall's Carbines | `weaponstock[36] += 3000`, `[86] += 2500` — **literals, not prefs** | yes |
| 10 Machineguns | `weaponstock[37] += 32`, `[38] += 32` | yes |
| 11 Confederate Guns | `weaponstock[12] += 48` | yes |
| 91 Propaganda | `+0.01` support in every state of your own alliance, reason **34** | yes |
| 92 Counter-propaganda | `−0.01` to the enemy's own support in every state of an alliance at war with you, reason **35** | yes |
| 93 Occupation Administration | `+0.01` your support in states where `occupiedby == alliance`, reason **36** | **conditional — §6.5** |
| 94 Suppress Population | `−0.01` the enemy's support in the same states, reason **37** | **conditional — §6.5** |
| 103 Send Envoys | `AdjustInterventionLevel(+0.05)` | yes |
| 110 Cavalry Reform | every own `unittyp == 1` with `overridesymbol == 12` → **13**, then `CheckCommanderPromotions()` | yes |
| 112 Artillery Reform | every own `unittyp == 2` with `overridesymbol == 10` → **11**, then `CheckCommanderPromotions()` | yes |
| 116 Cotton is King | `AdjustInterventionLevel(+0.025)` | yes |
| 117 Corn is King | `AdjustInterventionLevel(+0.025)` | yes |

`Alliance.AdjustInterventionLevel(value, clearvalues, policyid)` (~4084) loops
`i = 2 … alliance.Length` and adds `value` to `interventionlevels[i]` for
**every** foreign power — so Send Envoys, Cotton is King and Corn is King move
Britain, France and Spain by the same amount at once. The `policyid = −2`
argument routes the same number into `interventionpointsprojects[…]`, which is
the player's breakdown display; that accumulator is **added for the player's
own alliance and subtracted for anyone else's**, the same signed-bookkeeping
pattern [17](17-politics-and-support.md) §4 found on the policy side.

Projects 91–94, 103, 116 and 117 are all **repeating**, and this is a one-shot
arm, so each appointment is a **step**, not a rate. Buying Propaganda four
times is +0.04 support, once.

### 4.5 Pull — level factors read at the point of use

Nineteen projects, all of the form `1 + level × pref` unless noted. Every one
of these was checked for the local actually being consumed:

| id | Pref | Shipped | Read in | Reaches |
| --- | --- | --- | --- | --- |
| 95 Administration Reform | `…policyspeed` | 0.05 | `Policies` ~331 (rate), ~267 (tooltip) | ✅ |
| 96 Subsidize Banks | `project_subsidizebanks` | 0.05 | `Alliance.GetFundingImprovementThroughBanks` ~3839; `Economy` ~1606 capital paint | ✅ |
| 97 Improve Credit Rating | `project_creditrating` | **−1** | `Economy` ~2181 — `FloorToInt(level × −1)` added to the **rating notch index** | ✅ |
| 98 Market Reform | `project_marketeffects` | 0.05 | `Economy` ~1613 | ❌ **§6.1** |
| 99 Infrastructure Reform | `project_infrastructure` | 0.05 | `IIP` ~1203 transport capacity | ✅ |
| 100 Logistics Reforms | `project_logisticsreform` | **−0.05** | `CBuilding` ~1941, `Alliance.GetTransportUpkeep` ~4162, `Regiment` ~5322 | ✅ |
| 104 Subsidize Industry | `project_subsidizeindustry` | 0.05 | `CBuilding` ~2326 | ⚠️ **§6.4** |
| 105 Subsidize Agriculture | `project_subsidizeagriculture` | 0.05 | `CBuilding` ~2330 | ✅ |
| 106 Trade Warfare | `project_blockadeefficiency` | 0.05 | `IIP` ~664 — scales the **blockader's** efficiency up | ✅ |
| 106 | `project_blockaderunnerefficiency` | 0.05 | `IIP` ~680 — `blocked /= max(1, …)` for the **node owner** | ✅ |
| 107 Civil Order | `project_civilordercasualties` | **−0.05** | `ImportExportUnitData` ~3642, ~3648 | ⚠️ **§6.6** |
| 107 | `project_civilorderraids` | **−0.05** | `Regiment` ~4661, ~4663 | ❌ **§6.1** |
| 108 Recruit Agents | `project_recruitagents` | 0.05 | `Regiment` ~10967 → `groupintelligencegathering`, read at ~5902/~6191 | ✅ |
| 109 Recruitment Offices | `project_recruitment` | 0.05 | `Alliance.UpdateSubsidyRecruitment` ~4437 → a **target**, see below | ✅ |
| 113 Farm Mechanization | `…cropsproduction` | 0.1 | `CBuilding` ~2345 | ❌ **§6.2** |
| 113 | `…recruitment` | 0.1 | `GameVars` ~4852 | ✅ |
| 114 Plantation Mechanization | `project_cottonmachinery` | 0.10 | `CBuilding` ~2349 | ❌ **§6.2** |
| 118 Training Manuals | `project_trainingmanuals` | 0.05 | `Regiment` ~6125 drill rate | ✅ |
| 119 Railroad Construction | `project_railroadconstruction` | 0.05 | `BattleUnits.GetConstructionSpeed` ~260 | ✅ |
| 121 Trade Deals | `project_sendenvoystradevolume` | 0.05 | `IIP` ~889 — divides the counterpart's relative price | ✅ |

Project **109** is the only one that does not apply immediately. It writes
`recruitmentprojectfactortarget = 1 + level × 0.05`, and
`recruitmentprojectfactor` — the figure the volunteer pool actually multiplies
by (`GameVars` ~4880) — converges toward it at
`additionalrecruitssubsidies × timediff / timetoreachrecruitstarget` =
**2 × timediff / 2**, i.e. **1.0 per campaign year**. So a level-4 Recruitment
Offices reaches its +20 % about **73 days** after purchase.

> A label trap falls out of the same two lines.
> `additionalrecruitssubsidies` is labelled *"Additional recruits (volunteers)
> available for full recruitment subsidies (1 = no change)"* and ships **2** —
> a target multiplier. It is used **only** as the numerator of that convergence
> rate, and `recruitmentprojectfactortarget` has exactly two writers: the
> constructor's `1f` and project 109. **No subsidy slider contributes to it.**
> Both that pref and `timetoreachrecruitstarget` have one consumer each, this
> expression, so the doubling the label promises is not applied anywhere.

---

### 4.6 Pull — boolean and count gates

Nine projects carry no pref at all; the project's presence or level **is** the
effect.

| id | Gate | Where | Reaches |
| --- | --- | --- | --- |
| 88 / 89 | `projects.Contains(89) \| projects.Contains(88)` | `Alliance.AllowsArmyGroups` ~3739 | **§3.1** |
| 89 / 90 | `commandhierarchyshift == 0 → Contains(90)`, `== −1 → Contains(89)` | `Alliance.HasOrganizationReform` ~3746 | ✅ (90 in `001`) |
| 90 | `project_organisationreformlowerconsumption` **0.9** on all four supply consumption lines | `Regiment` ~5080 → ~5101/5110/5120/5124 | ✅ |
| 110 | `!Contains(110) && IsProjectAvailableInScenario(110)` → cavalry max size × **0.2** | `BattleUnits` ~15104 | ✅ |
| 112 / 124 | `!Contains(112) && !Contains(124)` → artillery max size × **0.375** | `BattleUnits` ~15108 | ✅ |
| 111 / 123 | `Contains(123)` in `002`, `Contains(111)` elsewhere | `Alliance.IsHorseArtilleryAvailable` ~3823/~3829 | ✅ |
| 115 Supply Reform | `!Contains(115)` → supply-depot upgrade button disabled | `CBuilding` ~3070 → ~3134 | ⚠️ UI-only |
| 120 Improvised Shipyards | `GetProjectLevel(120) > 0` → ship type **28** exempt from the tonnage-under-construction total | `GameVars` ~7862 | ✅ |
| 122 Military Education | a **count**, not a factor: military schools allowed = `GetProjectLevel(122)` | `BuildingList` ~111 (player), `AICampaign` ~5785 (AI) | ⚠️ **§6.3** |

**Project 115's gate is in the button, not in the simulation.**
`CBuilding` ~3070 sets a flag when the project is absent, and ~3134 turns it
into *"Upgrade of supply depots require the project supply reform to be
appointed!"* plus a disabled button. The method that performs the upgrade,
`CBuildingIcon.Upgrade(alliance)`, has **no project-115 test**; its three
callers are the two player buttons (`PanelHandler` ~5127, `Production` ~567)
and the AI (`AICampaign` ~5661). The AI's path only considers building types
with `subsidytype < 0` — privately funded companies — so it never reaches a
supply depot, and the gate holds in practice. It would not hold for a mod that
calls `Upgrade` directly, and the `Owner == GameVars.playeralliance` clause in
the gate confirms it was written as presentation rather than rule.

Projects **110** and **112** also appear in §4.4: appointing them converts
existing units' `overridesymbol`, and holding them removes the size penalty.
Two mechanisms, one project each.

---

## 5. Failure classes, ordered by how hard they are to find

Twelve projects fail, in six distinct ways. The order below matters for anyone
auditing a future build: the first class falls out of a text search, the last
needs somebody tracing a field's lifetime across two files.

| Class | Projects | Found by |
| --- | --- | --- |
| §6.1 Discarded statement | 98, 107 (raids) | text search |
| §6.2 Condition that cannot be true | 113 (crops), 114 | reading the loop |
| §6.3 Gate read from the wrong alliance | 122 | reading the condition's subject |
| §6.4 Guard too narrow | 104 | comparing against sibling guards |
| §6.5 State that never clears | 93, 94 | tracing a field's writers |
| §6.6 Accumulator carried across sides | 107 (casualties) | reading a ternary's two branches |
| §4.2a Guard excludes its own purpose | 38, 39, 40, 41 | reading the guard against intent |

---

## 6. The failures

### 6.1 Discarded statements — projects 98 and 107's raid half

Both already in [22](22-policy-and-project-catalogue.md) §12.3, restated here
for completeness because this audit confirms they are the **only** two of this
class among all 33 prefs.

**Project 98 Market Reform.** `Economy.UpdateFilterMaps` ~1610: the market
branch computes `_ = 1f;`, calls `GetProjectLevel(98)` as a bare expression and
reads `project_marketeffects` into a discard, then paints
`marketinfluenceoninfastructure[level]` with none of it applied.
`GetProjectLevel(98)` has exactly one occurrence in the assembly — that one.
**The project does nothing.**

**Project 107 Civil Order, raid half.** `Regiment` ~4661 and ~4663 each
evaluate `Mathf.Max(0.01f, 1f + GetProjectLevel(107) * project_civilorderraids)`
as a bare expression immediately before the two `AdjustSupport` calls it was
plainly meant to scale. The casualties half works (§6.6); the raid half does
not, though the panel advertises both.

### 6.2 Conditions that cannot be true — projects 113 and 114

`CBuilding.UpdateProductionEfficiency` ~2343–2351 compares the **loop counter**
against a resource id:

```csharp
for (int i = 0; i < producedgoods.Count; i++) {
    if (BuildingType == id_farm && i == Economy.resid_crops)          // 9
        num3 = 1f + GetProjectLevel(113) * project_farmmechanizationcropsproduction;
    if (BuildingType == id_plantation && i == Economy.resid_cotton)   // 8
        num3 *= 1f + GetProjectLevel(114) * project_cottonmachinery;
    …
```

`i` indexes `producedgoods`; everywhere else in the same loop the code writes
`producedgoods[i]`. A farm's list is `{9, 13, 16, 21, 37}` (5 entries) and a
plantation's `{8, 26, 30, 31}` (4), so `i` never reaches 9 or 8 and `num3`
stays 1. In both cases the intended good is at index **0** of its own list, so
the fix is one token.

`GetProjectLevel(114)` has exactly one occurrence in the assembly, so **project
114 does nothing at all**; project 113 keeps its volunteer half. First recorded
in [18-economy](18-economy.md) §3; this audit confirms no third instance of the
pattern exists among the project effects.

### 6.3 A gate read from the wrong alliance — project 122

The player's gate, `BuildingList` ~111, is correct: a military school may be
built while `CountBuildings(id_militaryschool, playeralliance) <
GetProjectLevel(122)`, and the tooltip says *"You need to increase Military
Education project to level …"*. So level N permits N schools.

The AI's gate, `AICampaign` ~5785, is not:

```csharp
int num9 = GetAIOpponentAllianceID(iips[run].allianceowner);
if (iips[run].allianceowner == 0) num9 = 1;
float num10 = (alliance[num9].militaryexperience − alliance[owner].militaryexperience) / …;
if (num10 > num4 && alliance[num9].militaryschools >= alliance[num9].GetProjectLevel(122))
```

`num9` is the **opponent**, deliberately so — `num10` is the enemy's
military-experience lead, which is what makes the AI want a school. But the
gate then reads the **opponent's** school count against the **opponent's**
project level, and the comparison is `>=` where the player's is `<`. Two
independent errors in one condition.

> What this produces, concretely. Both sides start at 0 schools and project
> level 0, so `0 >= 0` passes and **the AI can build military schools without
> ever buying Military Education**. Its own project level never constrains it.
>
> And the player can switch the AI off. Buy Military Education to level 1 and
> build no school, and for the AI the test becomes *"has the player built at
> least one school"* — which is false. **Holding the project while declining to
> use it locks the AI out of military schools entirely**, until the player
> builds one.
>
> The condition the author wanted is
> `alliance[owner].militaryschools < alliance[owner].GetProjectLevel(122)`.

`militaryschools` is a genuine count — `Economy` ~1672 does
`militaryschools_temp++` per completed school and ~1754 publishes it — so the
units match and only the subject and the direction are wrong.

### 6.4 A guard too narrow — project 104

`CBuilding.UpdateProductionEfficiency` ~2324:

```csharp
if ((BuildingType == id_factory) | (BuildingType == id_ironworks) | (BuildingType == id_foundry))
    num2 = 1f + GetProjectLevel(104) * project_subsidizeindustry;
```

`id_factory` = 5 ("Factory") has an **empty `producedgoods` list**
([18-economy](18-economy.md) §3), and the building that actually makes
Provisions and Uniforms is `id_manufactory` = 12. So Subsidize Industry reaches
**Iron Works and Foundries only**. The three other sites that group industrial
buildings — `CBuilding` ~502, `Policies` ~1270 and ~1668 — all include
`id_manufactory`, which is how the omission is identifiable as one rather than
a design choice. Project 105 Subsidize Agriculture correctly covers both Farms
and Plantations.

The effect is live; its scope is half what the name promises.

### 6.5 State that never clears — projects 93 and 94

Both require `GameVars.nation[j].occupiedby == alliance`. That field is written
in exactly one place, `GameVars.UpdateNationsData` ~7947:

```csharp
int num = -1;
for (k …) if (townsinstate[k].Owner != nation[j].alliance && num == -1) num = townsinstate[k].Owner;
if (num < 0) continue;
bool flag = true;
for (l …) if (townsinstate[l].Owner != num) flag = false;
if (flag) nation[j].occupiedby = num;
```

Two consequences, and the second is the interesting one.

**It requires total occupation.** `flag` survives only if **every** town in the
state is held by the same other alliance. A state half taken is not
`occupiedby` anybody, so Occupation Administration and Suppress Population do
nothing for it. Buying either with no fully-occupied enemy state spends the
subsidy and changes no number — and since both are repeating one-shot effects
(§4.4), the money is simply gone.

**It never resets.** There is no `occupiedby = -1` anywhere outside the
constructor and the save loader. The `num < 0` path `continue`s rather than
clearing, and the only assignment is the one above. So once a state has been
completely overrun, the flag stays for the rest of the campaign:

- a state you occupied and then **lost** still counts for *your* Occupation
  Administration and Suppress Population;
- a state the enemy occupied and you then **liberated** still reads
  `occupiedby == enemy`, so your own 93/94 never apply there and the enemy's
  always do;
- the same latch reaches `Regiment.IsOnEnemySoil` ~27160, which returns false
  for a state whose `occupiedby` matches the unit's alliance — so a liberated
  state permanently stops counting as enemy soil for the side that once held
  it, which also suppresses the enemy-soil alert and the
  `supportinfluenceenemysoil` weighting.

The one-line fix is an `else nation[j].occupiedby = -1;` on the `num < 0` path
and on `!flag`.

### 6.6 An accumulator carried across sides — project 107's casualties half

`ImportExportUnitData` ~3640–3651, the battle-result support hit:

```csharp
float num67 = 1f;
num67 = ((alliance[0] != playeralliance) ? (num67 / Max(1, bonus)) : 1f);
num67 *= Max(0.01f, 1f + alliance[0].GetProjectLevel(107) * project_civilordercasualties);
for (…) nation[n].AdjustSupport(alliance[0], array2[n] * num65 * num67, 1);

num67 = ((alliance[1] != playeralliance) ? (num67 / Max(1, bonus)) : 1f);
num67 *= Max(0.01f, 1f + alliance[1].GetProjectLevel(107) * project_civilordercasualties);
for (…) nation[n].AdjustSupport(alliance[1], array3[n] * num66 * num67, 1);
```

The second ternary's **false** branch resets `num67` to `1f`; its **true**
branch divides the value already accumulated for side 0. So the reset happens
only when side 1 *is* the player.

| Who is side 0 | Who is side 1 | Side 1's multiplier |
| --- | --- | --- |
| player | AI | `0.95^level(side 0)` × `0.95^level(side 1)` ÷ handicap |
| AI | player | `0.95^level(side 1)` — clean |
| AI | AI | side 0's factor carried in |

> **When the player's side is stored first on the battle monument, the player's
> Civil Order project also protects the enemy.** At level 1 the AI's
> casualty-driven support loss is multiplied by an extra 0.95 that belongs to
> the player's nation, compounding with the AI's own level.
>
> [20-ai-campaign](20-ai-campaign.md) §9.2 recorded this as the difficulty
> handicap leaking; the leak is in fact of **both** factors that touch `num67`,
> and the Civil Order half is the larger of the two at any project level above
> zero. Declared `float num67 = 1f;` once and reset by only one of the two
> branches that should reset it.

---

## 7. The 33 project prefs, and their standing

Every `project_*` pref in `GamePrefs`, its shipped value, and whether it has a
live consumer outside `Projects.GetProjectEffect` — the panel description
generator, which reads **all** of them and therefore proves nothing.

| Pref | Shipped | Live consumer |
| --- | --- | --- |
| `project_propagandainfl` | 0.01 | yes |
| `project_counterpropaganda` | −0.01 | yes |
| `project_occupationadministration` | 0.01 | conditional (§6.5) |
| `project_suppresspopulation` | −0.01 | conditional (§6.5) |
| `project_administrationreformpolicyspeed` | 0.05 | yes |
| `project_subsidizebanks` | 0.05 | yes, two sites |
| `project_creditrating` | **−1** | yes — notches, not a factor |
| `project_marketeffects` | 0.05 | **no — discarded** |
| `project_infrastructure` | 0.05 | yes |
| `project_logisticsreform` | −0.05 | yes, three sites |
| `project_militaryrailroadupkeep` | **−0.5** | yes |
| `project_militaryrailroadtransportspeed` | **0.5** | yes |
| `project_weaponsproduction` | 0.05 | yes |
| `project_sendenvoystradevolume` | 0.05 | yes |
| `project_sendenvoysinterventionlevel` | 0.05 | yes |
| `project_subsidizeindustry` | 0.05 | narrow (§6.4) |
| `project_subsidizeagriculture` | 0.05 | yes |
| `project_blockadeefficiency` | 0.05 | yes |
| `project_blockaderunnerefficiency` | 0.05 | yes |
| `project_civilordercasualties` | −0.05 | leaks (§6.6) |
| `project_civilorderraids` | −0.05 | **no — discarded** |
| `project_recruitagents` | 0.05 | yes |
| `project_recruitment` | 0.05 | yes, via a 1.0/yr convergence |
| `project_farmmechanizationcropsproduction` | 0.1 | **no — unreachable condition** |
| `project_farmmechanizationrecruitment` | 0.1 | yes |
| `project_cottonmachinery` | 0.10 | **no — unreachable condition** |
| `project_cottonisking` | 0.025 | yes |
| `project_cornisking` | 0.025 | yes |
| `project_trainingmanuals` | 0.05 | yes |
| `project_railroadconstruction` | 0.05 | yes |
| `project_nocavalryreformsizefactor` | 0.2 | yes |
| `project_noartilleryreformsizefactor` | 0.375 | yes |
| `project_organisationreformlowerconsumption` | 0.9 | yes |

Plus `machinegunprojectstock` = **{32, 32, 48}**, the only project magnitude
held in an array, consumed by §4.4's arms 10 and 11.

**Four of the 33 prefs are dead and two are impaired** — 12 % and 6 % — which
sets a useful expectation for the still-unaudited `~137` candidates from the
[07-config-data-model](07-config-data-model.md) sweep.

Two further observations from the same pass:

- **Project 97 is the only effect whose pref is not a multiplier.**
  `project_creditrating = −1` is added to a **rating notch index**, and
  `Projects.GetProjectEffect` ~744 formats it as
  `GetAbrevStrValue(−pref × level × 100) + "%"` — so the panel prints "+100 %"
  per level for a quantity that counts notches
  ([22](22-policy-and-project-catalogue.md) §12.5).
- **Only one project effect uses literals rather than prefs**: project 5's
  `weaponstock[36] += 3000f` and `[86] += 2500f`. Everything else is a pref or
  structural, which answers [09](09-policy-and-project-effects.md)'s question
  3 — the workload for rebalancing projects is almost entirely config, not
  code.

---

## 8. Defect list

1. **Project 98 Market Reform has no effect** — level and pref both read into
   discards (§6.1).
2. **Project 107's raid half has no effect** — two bare expressions beside the
   calls they should scale (§6.1).
3. **Project 114 Plantation Mechanization has no effect** — loop counter
   compared against a resource id (§6.2).
4. **Project 113's crops half has no effect** — the same comparison (§6.2).
5. **The AI's Military Education gate reads the opponent's school count against
   the opponent's project level, in the permissive direction.** The AI is
   unconstrained by its own project, and a player who buys the project without
   building a school blocks the AI entirely (§6.3).
6. **Project 104 Subsidize Industry omits the manufactory**, reaching Iron
   Works and Foundries only (§6.4).
7. **`nation[].occupiedby` never resets**, so projects 93 and 94 keep applying
   to states their owner has lost and never apply to states it has liberated;
   the same latch skews `Regiment.IsOnEnemySoil` (§6.5).
8. **Projects 93 and 94 need a state totally occupied**, so buying either
   before any enemy state is fully overrun spends the subsidy for nothing
   (§6.5).
9. **`num67` is reset by only one of the two branches that should reset it**, so
   the player's Civil Order factor is applied to the enemy's support hit too
   (§6.6).
10. **The ship-import restock probability uses the absolute campaign date**
    rather than the elapsed cycle, inflating it by about a million and making
    the roll certain (§4.2a).
11. **The ship-import restock is gated on stock already existing**, so spending
    the last slot closes that ship type permanently (§4.2a).
12. **`additionalrecruitssubsidies` is used as a convergence rate, not as the
    target its label describes**, and nothing but project 109 writes
    `recruitmentprojectfactortarget` (§4.5).
13. **Project 115's gate is in the button, not the simulation** —
    `CBuildingIcon.Upgrade` has no project test (§4.6).
14. **Project 101's `1 + level × (−0.5)` form is safe only because the project
    is non-repeating**; flipping `repeating` to `true` makes transport upkeep
    zero at level 2 and negative at level 3 (§4.3).
15. **`Weapon.GetAvailability(int alliance)` ignores its alliance argument**
    and reads the player's availability instead, and `Weapon.AbleToOrder`
    gates the order button by string equality against two of its return
    values. Latent — every call site passes `playeralliance` — but a hazard
    for a hook and for localisation (§4.1).
16. **`repeating` is authored with inconsistent casing** in `projects.dat`
    (`True` for most records, `true` for 113 and 114). Harmless under
    `bool.Parse`; a hand-rolled parser will trip (§1).

---

## 9. Mod seams

1. **Four of those defects are one-token fixes.** 113 and 114 need
   `producedgoods[i]` where they have `i`; 104 needs `id_manufactory` in its
   guard; 115's gate needs moving into `CBuildingIcon.Upgrade`.
2. **Projects 98 and 107's raid half need their discarded expressions
   assigned** and multiplied into the neighbouring call — the smallest possible
   patch for two advertised effects that do nothing.
3. **The ship-import market is the richest seam in the system.** Changing
   `HasImport(i)` to `importavailableforalliance.Contains(i)` in
   `ShipType.UpdateMarket`, and passing `GamePrefs.shiptypeupdatecycle` instead
   of `timer` into the probability, turns projects 38–41 from a
   one-shot-plus-exploit into the slow trickle the config label describes.
4. **The 56 `PLACEHOLDER` ids are free**, and a project needs no prefab —
   unlike a policy ([22](22-policy-and-project-catalogue.md) §1.1). Add the
   18-line record to `projects.dat`, give its `applicablescenarios` the
   scenario you ship, and postfix `UpdateProjectEffects` or
   `UpdateOneTimeProjectsEffects`; neither switch has a `default:` arm
   ([09](09-policy-and-project-effects.md)).
5. **`Projects.GetProjectEffect(id, effect, secondeffect)` is the id → meaning
   map** and the only description generator, so a new project's panel text is
   one postfix.
6. **Scenario availability is a data field, not code.** Moving 89, 123 or 124
   into `001;` makes them appointable in the base campaign, and the effects
   they gate are all implemented and live — which for 89 means Army Groups,
   provided `GameVars.allowarmygroups` is also set back to `true` after
   `DLC_WL` clears it (§3.1).

---

## 10. Open questions

- **`levelgraphics.Length`** caps every repeating project's level
  (`IsAppointable` ~797) and is prefab data — still the binding unknown on what
  a repeating project's real ceiling is, and already on the asset-dump list
  ([22](22-policy-and-project-catalogue.md) §16).
- **Whether the ship-import restock behaves as derived.** §4.2a is arithmetic,
  not observation; the test is stated there.
- **Whether the AI ever buys project 122.** `CheckAIProjectChange`
  ([22](22-policy-and-project-catalogue.md) §13.3) picks projects by
  personality agenda. If Military Education is in no agenda then §6.3's first
  consequence is moot for the AI and only the player-side lockout matters.
- **Why weapon imports are unmetered while ship imports are not.** `ShipType`
  carries an `importonmarketforalliance` stock and a market cycle; `Weapon` has
  only the availability flag, so a weapon import is permanent and unlimited
  once bought. Whether the ship mechanic is the newer design or the weapon one
  the simplification is not decidable from the code.
- **The `[deactivated]` pref beside the ship-import block** — *"Minimum
  relations to European nations to allow imports [0-1]"*, shipped **0.25** —
  means import projects keep working regardless of how relations with Britain
  or France stand. Consistent with `[deactivated]` elsewhere
  ([07](07-config-data-model.md)), and worth re-checking against a future
  build.
