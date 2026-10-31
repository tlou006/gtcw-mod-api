# 19 — Commanders and perks

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GameVars.Commander`, `BattleUnits`,
`AICampaign`, `AIBattle`, `Regiment`, `PerkRow`, `CampaignStrategy`;
`Config/commanders.txt`, `Config/ranks.txt`, `Config/ranks0.txt`,
`Config/PerkBattleTooltips.txt`, `Config/PerkTooltipsArmyCampaign.txt`,
`Config/PerkFleetsTooltips.txt`, `Config/campaignprefs.txt`,
`Config/battleprefs.txt`, `Config/unitprefs.txt`
**Summary:** [summary-commanders](summary-commanders.md)

---

## 1. The perk namespaces — the gap from [16](16-enumerations.md) is closed

There is **not one perk enumeration but three**, each declared by its own
tooltip file and each with its own id space:

| Namespace | File | Count | Holder |
| --- | --- | --- | --- |
| Battle | `Config/PerkBattleTooltips.txt` | 19 | fighting units and low echelons (`unittyp <= 13`) |
| Army campaign | `Config/PerkTooltipsArmyCampaign.txt` | 18 | formations (`unittyp > 13`, not 17) |
| Fleets | `Config/PerkFleetsTooltips.txt` | 11 | fleets (`unittyp == 17`) |

`PerkRow.LoadTooltips()` reads all three into static arrays once, with
`LevelsPerPerk = 3`. Format: a header line, the perk count, a comment line,
then four lines per level — a label line that is **read and discarded**,
headline, description, and a short autoresolve blurb (`-` = none).

> The discarded label line is inconsistent in the shipped data — the battle
> file numbers perk 0's levels `0/1 0/2 0/3` and everything after it
> `n/0 n/1 n/2`, and the fleet file uses `n/1 n/2 n/3` throughout. **It is
> cosmetic**; the parser is purely positional.

### 1.1 Battle perks (19)

Partitioned by branch, and the partition is **hardcoded in C#**, not config:

```csharp
PerksShownInfantrySingle  = { 0,1,2,3,4,5,6,7 };
PerksShownCavalrySingle   = { 8,9,10,11,12,13 };
PerksShownArtillerySingle = { 14,15,16,17,18 };
```

| Id | Name | Effect prefs seen in code |
| --- | --- | --- |
| 0 | Engineering | `hasengineers`; engineering points ×2/3/4; `perk_engineers_pontoonspeed` 1 / 1.25 / 2 (`Pontoons`); the only perk fed by `IncreasePerksAllUnits` |
| 1 | Sharpshooters | `perk_sharpshooters_accuracyfactor`, `perk_sharpshooters_rangefactor`; in `Autocalc` it also receives **Zouave's** fatigue factor (§1.6) |
| 2 | Zouave | `isabletorun`, `perk_zouaves_fatiguefactor` — manual battles only (§1.6) |
| 3 | Expert skirmishers | `perk_expertskirmishersconcealment` +10/+25/+50 (`FogOfWar`), `perk_expertskirmishersreloadspeed`; **level III** (`GetPerkLevel(3) == 2`) suppresses the low-cohesion morale penalty |
| 4 | Spirited charge | `perk_spiritedchargemeleefactor` (display only — see [12](12-melee.md)); at **level III**, adds to an opponent's "doubt their chances" melee term unless the opponent holds it at II+ (`Regiment` ~18311) |
| 5 | Deadly volley | `perk_deadlyvolleylethalityfactor`; from **level II**, no enemy-in-cover morale penalty (~18132); at **level III**, `perk_deadlyvolley_reductionmoralehit` — **inverted**, see §1.6 |
| 6 | Ace of Spade | `perk_aceofspadereloadbonusfactorbehindcover`, `perk_aceofspademoralebonusbehindcover` |
| 7 | Iron discipline | `perk_irondisciplinecohesionrecovery`; **level III** floors the "doubt their chances" term at 1 (~18329); from **level II**, zeroes the *recovery* of `friendlyroutednear` — see §1.6 |
| 8 | Expert scouts | `perk_expertscoutsconcealment`, `perk_expertscoutsvisibilityrangefactor` (`FogOfWar`), `perk_expertscoutsperkexp`; its level III morale branch is **dead** (see §1.6) |
| 9 | Cold steel | `perk_coldsteelmeleeeffectiveness` (display only), `perk_coldsteelenemymoralemalus` 1 / 2 / 2 |
| 10 | Mounted rifles | `perk_mountedriflesmalusfactor` — **inverted**, see §1.6 |
| 11 | Stormers | `perk_stormersaccuracyfactor`; level III ignores enemy cover morale |
| 12 | Vanguard | `perk_vanguardperkexp`; **level II** zeroes the falling-back-under-fire morale term, **level III** the retreating-under-fire term (~18252, ~18269) |
| 13 | Lightning brigade | `perk_lightningbrigadereloadfactor`, `perk_lightningbrigadecovervalue`, `perk_lightningbrigadecovermorale` |
| 14 | Close support | `perk_closesupportweaponstrength` ×2 (`Projectile`), `perk_closesupportreloadspeed`, `perk_closesupportmeleestrength` (display only) |
| 15 | Skilled laying | `perk_skilledlayingcounterbatteryaccuracy`, `perk_skilledlayingartilleryfiredemoralization` |
| 16 | Grand battery | `perk_grandbatterybombardmentaccuracy`, `perk_grandbatteryartillerymalus` |
| 17 | Flying battery | `perk_flyingbatteryspeed`; the same level II / III retreat-morale gates as Vanguard (~18256, ~18273) |
| 18 | Sapper gunners | `perk_sappergunnersreloadspeed` (no cover test at ~10012, despite its label), `perk_sappergunnerscovermoralefactor` |

**Level gates are 0-based.** `GetPerkLevel` returns 0 for level I, so
`>= 1` opens at level II and `>= 2` at level III. An earlier version of this
table read several of these one level low.

### 1.2 Army campaign perks (18)

| Id | Name | Effect prefs seen in code |
| --- | --- | --- |
| 0 | Siege Train | |
| 1 | Field Telegraph | `fieldtelegraphorderdelays` |
| 2 | Balloon Corps | `orderdelaysballooncorps` |
| 3 | Bureau of Military Information | `perkexpincreasescoutingcampaign` |
| 4 | Skilled cartographers | `intelligencegatheringskilledcartographers`, `movementspeedskilledcartographers` |
| 5 | Flying Column | `movementspeedflyingcolumn`, `perkexpincreaseflyingcolumn` |
| 6 | Foot Cavalry | `forcedmarchspeedfootcavalry`, `perkexpincreasefootcavalrymovement` |
| 7 | Engineers & Mechanics | |
| 8 | Ambulance Corps | `speedreductiondisabledsoldiersambulancecorps`, `perkexpincreasewounded` |
| 9 | Pontoon train | `movementcostpontoontrain`, `perkexpincreasecrossingsmallrivers` |
| 10 | Sappers and Miners | |
| 11 | Land Torpedoes | `movementreductionlandtorpedoes`, `perkexplandtorpedoes` |
| 12 | Partisan Brigades | |
| 13 | Bushwhackers | |
| 14 | Embedded reporters | `fameadjustmentembeddedreporters` (fame multiplier, `unittyp >= 15` only) |
| 15 | Limelights | |
| 16 | Expert Scouts | `intelligencegatheringexpertscouts` |
| 17 | River Expedition | `rivermovementspeedriverexpedition`, `perkexpincreaserivermovement` |

### 1.3 Fleet perks (11)

| Id | Name | Effect prefs seen in code |
| --- | --- | --- |
| 0 | Torpedoes | |
| 1 | Mortar Boats | |
| 2 | Supply Colliers | `speedadjustmentsupplycolliers`, `perkexpincreaseblockingfleetiips` |
| 3 | Balloon Barge | `intelligencegatheringperkbaloonbarge`, `siegeeffectivenessperkballoonbarge`. (An earlier version credited it with the `HasPerk(3)` cohesion-alert test at `Regiment` ~18352; that is battle perk 3 in fighting-unit morale, which fleets never run.) |
| 4 | Limelights | |
| 5 | Blockading Squadron | `perkexpincreaseblockingfleetiips` |
| 6 | Sea Raiders | `searaidersperkexpinc` |
| 7 | Amphibious Attack | |
| 8 | Blockade Busters | **selecting it calls `commander.SetFamous()`** (`ChoosePerk`) |
| 9 | Battery Runner | |
| 10 | Port Defender | |

### 1.4 How a namespace is selected

`Regiment.perks` is **one** `List<Perk>` and `HasPerk(id)` / `GetPerkLevel(id)`
search it blindly. The namespace is decided entirely by **who holds the list**:

```csharp
// BattleUnits.create_regiment
int num = ((component.unittyp <= 13) ? 1 : 4);
```

- **`unittyp <= 13` → 1 slot**, holding a battle perk.
- **`unittyp > 13` → 4 slots**, holding campaign perks (or fleet perks if
  `unittyp == 17`).

Mirrored in serialization: `ImportExportUnitData` writes four
`(perk, perklevel, perkexp)` triples for formations and only `perks[0]` for
fighting units.

> **The id collisions documented in [16](16-enumerations.md) were
> cross-namespace collisions.** They were never ambiguous at runtime. But
> `HasPerk(n)` still is not a global registry, so **a ModApi perk surface must
> key on *(namespace, id)*** — and the namespace is derivable from `unittyp`
> alone.

Guards in the code follow from this: battle-perk reads are written
`HasPerk(n) && !calledfromcampaign`, campaign-perk reads
`calledfromcampaign && …`, fleet-perk reads `unittyp == 17 && …`.

**Inheritance is one level and only for Corps:**

```csharp
public bool HasPerkParent(int perkid)        // and GetPerkLevelParent
{
    if (calledfromcampaign && unittyp == 15) { /* read direct parent's perks */ }
    return false;
}
```

So an Army's or Army Group's campaign perk reaches its **Corps** and no
further — a Division does not inherit from its Corps. Where both are checked
(`HasPerk(4)` then `HasPerkParent(4)`) the multipliers **stack
multiplicatively**.

`HasPerk(-1)` is the "has any perk at all" query.

### 1.5 Perk experience and slot unlocking

`IncreasePerkExp(expincrease, perktoincrease)` walks the slot list:

| Rule | Detail |
| --- | --- |
| **Slots unlock in order** | A slot earns experience only if it is index 0 or its predecessor is already assigned. Formations must fill slot 0 before slot 1 starts progressing. |
| Unassigned slot | Accrues at `perkexplvl0mp[class]` (battle) or `perkexplvl0mpcamp[class]` (campaign), capped at 1.0 — at which point `ChoosePerk` may assign a perk. |
| Assigned slot | Accrues only when `perktoincrease` matches it (or is `-2` = all). |
| Level-up | `perkexp > 1` → `perklevel++`, `perkexp -= 1`; capped at `GameVars.maxperklevels - 1` = **2** (three levels, 0-based). |
| Artillery | `unittyp == 2` multiplies **all** perk experience by `perkexpincreasemultiplierartillery`. |
| Corps propagate upward | `calledfromcampaign && unittyp == 15` also credits the direct parent at `perkexpincreasegroup`. Only Corps do this. |

The unassigned-slot rate class is remapped, not the raw `unittyp`:

- **Battle:** `>= 3 → 2`, then `3 → 0`, `4 → 1`. So skirmishers use the
  infantry rate, scouts the cavalry rate, and couriers fall through to the
  artillery rate.
- **Campaign:** `unittyp == 17 ? 1 : 0` — the campaign table has two entries.

`ChoosePerk(newperk)` refuses to duplicate a perk the unit already holds, and
refuses outright if no slot has reached 1.0. Side effects: battle perk 0 sets
`hasengineers`, battle perk 2 sets `isabletorun`, fleet perk 8 makes the
commander famous.

**Merging two units** strength-weights `perkexp` per slot but leaves
`perk`/`perklevel` at the surviving unit's values. **Detaching** a skirmisher
or scout (`unittyp == 3 || == 4`) copies the parent's perk, level and exp;
other spawned units start blank.

`IncreasePerksAllUnits(perktype, unittyp, alliance, amount)` spreads a fixed
budget: first over units that already hold that perk, otherwise over units with
an **empty** slot, **divided by the number of recipients**. Only ever called
with battle perk 0, from pontoon and obstacle construction in `BattleUI`.

### 1.6 Perk defects

| Where | Defect |
| --- | --- |
| `Regiment.UpdateMorale` (~line 18333) | `if (HasPerk(8) && GetPerkLevel(9) >= 2)` — tests perk 8 but reads **perk 9's** level. The line above it is the correct `HasPerk(7) && GetPerkLevel(7) >= 2`. Since a fighting unit has exactly one perk slot, holding 8 and 9 at once is impossible, so **the Expert Scouts III branch is unreachable**. |
| `PerkRow.IsPerkUnlocked()` | `return true;` and **never called**. The in-game tooltip says "the amount of available perks is limited for each nation, depending on its policies and technologies" — **no such gating exists**. What availability there is comes from the prefab-authored `PerksShownGroup` / `PerksShownFleet` lists. |
| `PerkRow` fields | `PerksShownFleets` is assigned into locals but **never returned**; `GetPerksShown` reaches `PerksShownFleet` (singular) for fleets. Two similarly-named prefab arrays, one dead. |
| `PerkRow.GetPerksShown` | Returns **`null`** for a formation whose `oobunitsymbolid` is not in `{0,1,2,3,4,17}`. Also contains a no-op `if (num == 17) { num = 17; }`. |
| `PerkRow.GetPerkInfos` | Indexes the **campaign** tooltip table for anything that is not a fleet, including `unittyp <= 13`. Only ever called on top-level units, so it does not fire in practice — but it would read out of range for a battle perk above id 17. |
| `UnitPerk.UpdateStatus` | Hardcoded tooltip strings from an older build, applied **after** the id-based lookup, claiming perk 2 is "Sharpshooters". Perk 2 is **Zouave**; perk 1 is Sharpshooters. |
| `PerkRow.PerkSpriteMarkNames` | `{ "", "_h", "" }` — the greyed-out sprite is the same asset as the normal one. |
| `orderdelaysballooncorps` (data) | Ships **1.25 / 1.10 / 1.10** against a multiplier on the order delay, where the adjacent `fieldtelegraphorderdelays` ships 0.9 / 0.8 / 0.7 — so **Balloon Corps lengthens orders** (§1.7). |
| `skirmishingbushwhackers` (data) | Ships **1.25 / 1.20 / 1.10**, so the perk's raiding-casualty multiplier **falls** as it levels up, while its sibling support array rises correctly (§1.7). |
| `SpottedEnemy.UpdateMarks` | `informationdelay` and `informationdelaybureauofmilitaryinformation` ship the **same** 1.0 / 0.75 / 0.5 and are applied with **opposite operators**, so one of the two is inverted — and the one that makes its holder easier to track is the intelligence perk (§1.7). |
| `Regiment` ~4700 | `HasPerk(6)` with **no `unittyp == 17` guard**, so campaign perk 6 (Foot Cavalry) satisfies the fleet-only Sea Raiders clause. The `IIP` copy of the same test is guarded (§1.7). |
| `CBuilding` ~1412 | The own-perk and parent-perk branches both **assign** `constructiontimer`, so Engineers & Mechanics does not stack from Army to Corps as every other paired site does (§1.7). |
| `Regiment` ~9620, ~10004 (data) | **Mounted rifles is inverted.** Both sites compute `factor = (1 − base) × perk_mountedriflesmalusfactor[level] + base`, so the pref is the share of the mounted penalty *recovered* — but it ships **0.75 / 0.5 / 0.2**, labelled as a shrinking "malus factor". With `mountedaccuracyinfluencefactor` 0.6 and `mountedreloadinfluencefactor` 1.25, mounted accuracy is ×0.90 / 0.80 / 0.68 and reload time ×1.06 / 1.13 / 1.20 at levels I / II / III: **best at level I, weaker every level**, always better than no perk. |
| `Regiment` ~18221 | **Deadly volley III is inverted.** The high-momentary-casualties term (`moraleeffectonhighmomentarycasualties` −0.6) is reduced by `term × GetTraining() × drillreductionfactorforhighmomentarycasualties (0.7) × num16`, and the perk sets `num16 = perk_deadlyvolley_reductionmoralehit` (0.5). That halves the **drill reduction**, not the term: no change at training 0, and at training 1 the term goes from −0.18 to **−0.39**. The label ("reduction of morale hit … (factor)") describes multiplying the term itself. |
| `Regiment` ~17953 | **Iron discipline II+ freezes the nearby-rout penalty.** `num8 = 0` multiplies the *recovery* of `friendlyroutednear` (`routedunitrecovery` 0.3/h), not the impact (`impactownunitrouted` −0.075 per rout within `rangeroutedunits` 60). The term is applied per hour and only reset in `Initialize`, so from level II each nearby friendly rout costs −0.075/h for the rest of the battle, floored at −1. Presumably meant to zero the impact. |
| `Autocalc` ~3349 | `HasPerk(1)` then `perk_zouaves_fatiguefactor[GetPerkLevel(1)]` — Zouave's fatigue factor gated and levelled on **perk 1, Sharpshooters**. In auto-resolve, Sharpshooters brigades get the fatigue cut and Zouaves get none. `Autocalc` ~3229 also tests `HasPerk(1)` beside a fatigue threshold and may be the same slip; not traced. |
| `Regiment` ~16130–16138 | Spirited charge, Cold steel and Close support all multiply `meleestrength`, which is display-only ([12](12-melee.md) §3). Only Spirited charge was flagged as such in an earlier version of the mechanics translation. |

### 1.7 Shipped magnitudes — and the six "blank" campaign perks

§1.1 to §1.3 listed the prefs each perk reaches. This is the other half: the
values, read from `unitprefs.txt` (battle) and `campaignprefs.txt` (campaign,
fleet). Every array is `float[3]`, indexed by the **0-based** `GetPerkLevel`,
so index 0 is the tooltip's "level I".

**The six campaign perks §1.2 left with an empty effect column all have real
effects**, reached through `HasPerkParent` sites in `Autocalc` and
`CBuilding`:

| Campaign perk | Pref | I / II / III | Site |
| --- | --- | --- | --- |
| 0 Siege Train | `weaponfactorsiegetrains` | 1.25 / 1.50 / 2.00 | `Autocalc` ~415, siege weapon strength |
| 7 Engineers & Mechanics | `constructionspeedengineersmechanics` | 1.25 / 1.50 / 2.00 | `CBuilding` ~1412, `constructiontimer = 1f / pref` |
| 10 Sappers and Miners | `siegeassaultsappersandminers` | 1.25 / 1.50 / 2.00 | `Autocalc` ~3082, siege assault |
| 12 Partisan Brigades | `skirmishingpartisanbrigades` / `raidingmoralepartisanbrigades` | 1.25 / 1.50 / 2.00 and 1.10 / 1.25 / 1.50 | `Autocalc` ~2404, raiding casualties and morale |
| 13 Bushwhackers | `skirmishingbushwhackers` / `supportfactorraidingbushwhackers` | **1.25 / 1.20 / 1.10** and 1.10 / 1.25 / 1.50 | `Autocalc` ~2415; `Regiment` ~4655 |
| 15 Limelights | `combatefficiencynighttimelimelights` | 1.25 / 1.50 / 2.00 | `Autocalc` ~3116, night sieges and skirmishing |

and the rest of the campaign list:

| Campaign perk | Pref | I / II / III |
| --- | --- | --- |
| 1 Field Telegraph | `fieldtelegraphorderdelays` | **0.9 / 0.8 / 0.7** (a multiplier on the delay) |
| | `informationdelay` | 1.0 / 0.75 / 0.5 |
| 2 Balloon Corps | `orderdelaysballooncorps` | **1.25 / 1.10 / 1.10** — see below |
| | `intelligencefactorballooncorps` | 1.25 / 1.25 / 1.50 |
| | `siegeeffectivenessballooncorps` | 1.05 / 1.10 / 1.15 |
| 11 Land Torpedoes | `skirmishlandtorpedoes` | 1.05 / 1.10 / 1.20 |
| | `movementreductionlandtorpedoes` | 0.9 / 0.8 / 0.7 on enemy movement in range |
| 17 River Expedition | `siegeassaultefficiencyriverexpedition` | 1.1 / 1.25 / 1.5 |
| | `rivermovementspeedriverexpedition` | 1.25 / 1.50 / 2.00 |

#### Two arrays that point the wrong way

**`orderdelaysballooncorps` lengthens order delays.** `Regiment` ~14964
multiplies the order-delay accumulator by it, exactly as the line above
multiplies by `fieldtelegraphorderdelays`. Field Telegraph ships
**0.9 / 0.8 / 0.7** and Balloon Corps ships **1.25 / 1.10 / 1.10**, so the
Balloon Corps perk makes a formation's orders **25 % slower at level I and
10 % slower thereafter** — and levelling it up is the only way to recover part
of the loss. The two arrays sit in adjacent config blocks with identical
labels ("Order delays on campaign map for … perk, level N (factor)"), which
makes this look like a set of "+25 % faster" figures entered against a
multiplier on the delay.

**`skirmishingbushwhackers` declines with level**: 1.25 / 1.20 / 1.10, applied
as a multiplier on raiding casualties in `Autocalc` ~2415. Its sibling
`supportfactorraidingbushwhackers` rises correctly (1.10 / 1.25 / 1.50). The
declining array reads like diminishing *increments* authored as absolute
factors, so **Bushwhackers III inflicts fewer raiding casualties than
Bushwhackers I**.

#### And one pair of identical arrays used with opposite operators

`SpottedEnemy.UpdateMarks` computes `num2`, the probability that **the player**
is given a "last seen here" mark for an enemy formation hidden in fog —
`AddUnit` returns early for `reg.alliance == GameVars.playeralliance`, so the
list holds enemy units only and higher `num2` is better for the viewer. The
perks it reads are the **tracked formation's own**:

```csharp
if (regref.HasPerk(1))  num2 *= GamePrefs.informationdelay[level];          // 1.0 / 0.75 / 0.5
…
if (enemy within regref.commanderrange) {
    if (regref.HasPerk(3))  num2 /= GamePrefs.informationdelaybureauofmilitaryinformation[level];
}                                                                           // 1.0 / 0.75 / 0.5
```

Both arrays ship the same three values and are applied with **opposite
operators**, so one of them is inverted:

| Perk | Operator | Effect on its holder at level III |
| --- | --- | --- |
| 1 Field Telegraph | `*=` | marked **half as often** — a concealment benefit |
| 3 Bureau of Military Information | `/=` | marked **twice as often** — while enemies are inside its own command range |

> So the **intelligence** perk is the one that makes its formation easier for
> the enemy to track, and the **telegraph** perk is the one that conceals it.
> Neither effect is about what the holder sees, which is what both config
> labels ("Information delay for …") imply.

> **And in the shipped build neither effect reaches the player**, because
> `num2` is about a thousand times larger than intended before the perks touch
> it. The term they multiply is
> `(now − lastmarktime) / Mathf.Max(0.001f, num3)`, and `num3` is built from
> **`spottingfactor[1]` and `[0]`** — the per-unit-type *concealment* array
> from `unitprefs.txt`, which ships **−50** and **−60** — where it wants
> `timefactorspottings[0..1]` from `campaignprefs.txt` (0.1 and 0.003 game
> years). A negative `num3` means the clamp always wins, so `num2` reaches its
> `maximumcreationprobability` ceiling of **0.9** after about **eight game
> hours** whatever the perks or the viewer's intelligence do to it. The live
> gate on a new mark is `minimumdistanceforspotmark` = **40**.
> See [29-dead-prefs-audit](29-dead-prefs-audit.md) §4.1.

#### Fleet perk magnitudes

| Fleet perk | Pref | I / II / III |
| --- | --- | --- |
| 0 Torpedoes | `rammingefficiencytorpedoes` | 1.25 / 1.50 / 2.00 |
| 1 Mortar Boats | `siegebombardmentefficiencymortarboats` | 1.05 / 1.10 / 1.25 |
| 2 Supply Colliers | `speedadjustmentsupplycolliers` | **0.7 / 0.8 / 0.9** — a speed *penalty* that shrinks with level |
| | `supplyconsumptionsupplycolliers` | 0.85 / 0.70 / 0.50 |
| 3 Balloon Barge | `intelligencegatheringperkbaloonbarge` | 1.25 / 1.35 / 1.50 |
| | `siegeeffectivenessperkballoonbarge` | 1.00 / 1.00 / 1.05 |
| 4 Limelights | `combatefficiencynighttime` | 1.25 / 1.50 / 2.00 — fort strength and every vessel's firing strength at night |
| 5 Blockading Squadron | `blockadeefficiencyblockadingsquadron` | 1.1 / 1.25 / 1.50 |
| 6 Sea Raiders | `priceincreasessearaiders` | 1.1 / 1.25 / 1.5 on prices at a blockaded **trade node** — plus the unlock itself (the ordinary blockade clause in `IIP` ~660 excludes trade nodes), and `SetFamous()` at level III (~650) |
| 7 Amphibious Attack | `fortoccupationprobamphibiousattacklev` | 1.00 / 1.10 / 1.50 |
| 8 Blockade Busters | `fightingefficiencyblockadingfleetsblockadebusters` | 1.1 / 1.2 / 1.3 |
| | `effectoonblockadewithinradius` | 0.9 / 0.8 / 0.7 on the blockade of **your own** ports in the fleet's radius; and **`ChoosePerk` calls `SetFamous()`** |
| 9 Battery Runner | `fightingefficiencyvsfortsbatteryrunner` | 1.25 / 1.50 / 2.00 |
| 10 Port Defender | `fightingefficiencyportradiusportdefender` | 1.25 / 1.50 / 2.00 |

Three notes.

**Supply Colliers is the only perk with a standing penalty.** Level I costs
30 % of the fleet's speed for a 15 % consumption saving; level III costs 10 %
for a 50 % saving. The perk is only worth taking on a fleet that does not need
to move.

**`combatefficiencynighttime` (fleet 4) and
`combatefficiencynighttimelimelights` (campaign 15) are two different arrays
with the same shipped values**, which is why both perks are called Limelights.

**The Sea Raiders test is the one genuine cross-namespace ambiguity.** The
`IIP` site guards it with `unittyp == 17`, but `Regiment` ~4700 — the raiding
target filter — reads `HasPerk(6)` with no such guard, and campaign perk 6 is
**Foot Cavalry**, so a land formation holding Foot Cavalry also satisfies the
`IsTradeNode` clause. No shipped trade node is reachable by land, so it does
not bite, but it qualifies §1.4's claim that the three namespaces are never
ambiguous at runtime.

**And one `HasPerkParent` site that assigns rather than stacks.**
`CBuilding` ~1412 writes
`constructiontimer = 1f / constructionspeedengineersmechanics[level]` in
both the own-perk and the parent-perk branch, so a Corps that holds
Engineers & Mechanics *and* inherits it from its Army takes the **parent's**
level instead of compounding the two. Every other paired site multiplies.

---

## 2. `GameVars.Commander`

A plain class (not a `MonoBehaviour`) in a flat
`static List<Commander> GameVars.commander`. **The list index is the commander
id**, and `Regiment.commander` is that index. `Commander.currentcommand` is the
back reference.

### 2.1 The five attributes

`fame`, `leadership`, `initiative`, `administration`, `cunning`, plus `exp`.
All `[0,1]`. Raw fields are never read directly by the simulation — everything
goes through getters:

```
value = clamp01( raw
              × (status == 0 ? 1 : reductionfactorcommanderoutofduty)
              × expMultiplier
              × parentInfluence
              + dlcStaffBonus )

expMultiplier   = exp × factorcommanderexpattr + (1 − factorcommanderexpattr)
parentInfluence = GetParentCommanderInfluenceFactor(type)
```

`type` is **0 leadership, 1 initiative, 2 administration, 3 cunning** — a
fourth small enumeration, used only by the two influence functions.

Shipped values: `factorcommanderexpattr = 0.5`, so **the experience multiplier
spans [0.5, 1.0]** — a green commander operates at half his nominal attributes,
and no amount of experience raises him above them.
`reductionfactorcommanderoutofduty = 0.5`.

`GetCommanderFame` is the exception — no experience multiplier, no parent
influence, but a date-window factor instead:

```
fame × outOfDuty
     × commanderfameincreasefactor  (1.4)  if famous and not yet defamed
     × commanderfamedecreasefactor  (0.7)  if defamed
```

Both factors multiply into the same local as the out-of-duty reduction, so a
wounded famous commander is `fame × 0.5 × 1.4`.

### 2.2 Superior influence — and a hole in it

Two nearly identical functions, both returning
`1 + (superior − self) × commanderinfluenceonsubordinates` (shipped **0.33**),
with the gain scaled by `(1 − exp)` when the subordinate is the *better* man —
so an experienced subordinate is dragged down less by a poor superior:

- `GetCommanderInChiefInfluenceFactor(type)` — against
  `alliance.commanderinchief`; returns 1 for the C-in-C himself and when the
  C-in-C is out of duty.
- `GetParentCommanderInfluenceFactor(type)` — falls back to the C-in-C factor
  unless the unit `UnitIsInsideRadius(1)` of its direct parent, in which case
  the parent's attributes are used instead.

> **`GetParentCommanderInfluenceFactor` returns `0f` when the parent unit has
> `commander < 0`.** Because the factor is a *multiplier* on the attribute,
> that collapses leadership, initiative, administration and cunning to the DLC
> staff bonus alone — effectively zero. Any mod that creates a formation
> without assigning a commander silently zeroes its subordinates' attributes.

### 2.3 What actually changes, and what never does

| Attribute | Moves in the base game? |
| --- | --- |
| `exp` | Yes — `IncreaseExperienceThroughService` at `expgaincommanderservice` (0.2/yr), capped at `alliance.militaryexperience`; promotion gains and losses; `IncreaseAttributes` |
| `fame` | Yes — battle results, promotions, scripted events, WIA return, `IncreaseAttributes` |
| `initiative` | Yes — WIA return (loss), `IncreaseAttributes` |
| `leadership` | **Only via `IncreaseAttributes`** |
| `administration` | **Only via `IncreaseAttributes`** |
| `cunning` | **Only via `IncreaseAttributes`** |

And `IncreaseAttributes(from, to)` has **exactly one caller**, in the commander
tick:

```csharp
float inf = GameVars.nation[cmd.stateid].GetInfluenceOfExperienceLossThroughMilitarySchools();
cmd.IncreaseAttributes(militaryschoolattributesincreasefrom * inf * elapsed,
                       militaryschoolattributesincreaseto   * inf * elapsed);
```

> **Military schools in the commander's home state are the only thing that
> raises leadership, administration or cunning.** That closes the
> `militaryschools[]` question left open in
> [17](17-politics-and-support.md). Note it draws **one** random value and adds
> the same amount to all four of administration, leadership, cunning and
> initiative, and it bypasses the `changeof*` trackers, so these gains never
> appear in the attribute-breakdown tooltip.

`Nation.GetInfluenceOfExperienceLossThroughMilitarySchools()` =
`militaryschools[alliance] ^ exp × lin`, zero for `alliance > 1`. It also
divides the experience cost of promotion (§3.2). The sibling
`Alliance.GetMilitaryExpImprovementThroughMilitarySchools()` raises national
military experience, which is itself the cap on commander `exp`.

### 2.4 The attribute-breakdown trackers

`Adjust{Fame,Leadership,Initiative,Administration,Cunning}(byvalue, type,
timediff, changeonly)` each write a slot of a `changeof*` array via
`Tools.AddToAverage(current, byvalue, 0.25f, timediff)` before applying the
change, and clamp the attribute to `[0,1]`. `changeonly: true` records without
applying — used for the display of the parent-influence delta.

The slot meanings, from `GetDetailed*Info` (which maps each slot to a
`Tooltip.GetTooltipText` id) and the call sites:

| Array | Slot | Written by |
| --- | --- | --- |
| `changeoffame[8]` | 0 | **never written** — `GetDetailedFameInfo` deliberately starts at 1 |
| | 1 | promotion / appointment (`GetFameEffectOfPromotion`) |
| | 2 | battle result (`BattleUnits`, §4.1) |
| | 3 | returning from WIA (loss) |
| | 4 | scripted events — first major battle, invading the North, the Lt-Gen policy |
| | 5–7 | DLC only |
| `changeofinitiative[7]` | 0 | parent-influence display (`changeonly`) |
| | 1 | returning from WIA (loss) |
| | 2–6 | DLC only |
| `changeofleadership[6]` | 0 | parent-influence display (`changeonly`) |
| | 1–5 | DLC only |
| `changeofadministration[6]` | 0 | parent-influence display (`changeonly`) |
| | 1–5 | DLC only |
| `changeofcunning[6]` | 0 | parent-influence display (`changeonly`) |
| | 1–5 | DLC only |
| `changeofexp[7]` | — | **never written anywhere**; serialized only |

So in the base game the leadership, administration and cunning breakdowns show
**only** the superior-influence line, and the experience breakdown is always
empty. `AdjustLastAttributes(timediff)` maintains `last*` as a smoothed copy
for the UI's up/down arrows, snapping to the exact value within 0.001.

Two small defects: `AdjustInitiative` calls `Tools.AddToAverage` **twice** and
discards the first result; `AdjustFame` has no `changeonly` parameter, unlike
its four siblings.

### 2.5 Aggregate helpers

| Helper | Formula | Consumers |
| --- | --- | --- |
| `GetAverageAttributeValue(excludeexp)` | mean of admin, cunning, fame, leadership, initiative, exp | AI vertical replacement only |
| `GetStaffSkill()` | `(init + lead + 2.5·admin + cunning) / 5.5`, clamped | **DLC only** |
| `GetMaxShips(includeadmin)` | `floor(interp(administration, [0,1] → [maxshipstocommand[rank], maxshipstocommandfulladmin[rank]]))` | enforced by `Fleet`; shown in two panels |
| `GetMaxAmountOfSubordinateUnits()` | `amountofunitstocommand[…]` by 0.2 steps of `exp` | **no consumers** — the config marks the pref `[deactivated]` |
| `GetSeniority()` | `currenttimefromstart − max(0, dateofpromotion[rank].yearfraction − 1860)` | AI promotion |
| `GetNumberOfFamedSeniorOfficers()` | +1 per famous ancestor, −1 per defamed | UI |

> **`GetSeniority()` depends on a dual-unit global.**
> `GameVars.currenttimefromstart` is **hours since midnight of battle day 0**
> in a battle scene (`uniStormSystem.Hour + battlepasseddays × 24` — not since
> the battle started) and **years since 1860** in the campaign
> (`CampaignController` ~490, which is `GetCampaignFloatingDate() − 1860 −
> debugmonthadjustment`). `GetSeniority` only makes sense in the campaign;
> called from a battle scene it returns nonsense.
> ([23](23-game-state.md) §10.1)

### 2.6 Where each attribute actually lands

§2.1 to §2.5 cover how an attribute's value is produced. This is the other
half: every live consumer of the five getters, found by searching the whole
assembly for each one and discarding the display-only sites (`Statistics`,
`DebugInfo`, `CommandersList`, `CampaignMainPanel`, `Tooltip`). It exists
because the official manual publishes a 6 × 3 matrix of attribute effects, and
checking it needed the map ([manual-vs-code](../mechanics-findings/manual-vs-code.md)
§21).

**Fame** — 6 sites in `Regiment`, 4 in `BattleUnits`, and **none in
`AIBattle`, `AICampaign` or `Autocalc`**:

| Site | Effect | Pref |
| --- | --- | --- |
| `Regiment` ~6076, `BattleUnits` ~8137 | reduces overnight desertions | `commandersfameinfluenceondesertions` = 0.2 |
| `Regiment` ~7339 | feud trigger, as the *difference* of two commanders' fame | `feudtriggerlevel` |
| `Regiment` ~17729 | the unhappiness test (campaign morale slot 12) | `expleverageunhappy` |
| `Regiment` ~18082–18092 | morale slot 4: commander nearby, wounded, or killed | `moralemultipliercommanderfame`, `…_w`, `…_k` |
| `Regiment` ~7469–7476 | `fightingspirit`, against the side or alliance average | `influenceofattributesonfightingspirit[,1]` = 0.33 |
| `BattleUnits` ~3656, ~6638 | the two fame baselines those comparisons use | — |
| `BattleUnits` ~5840, ~6493 | commander ranking and the famous/defamed pick | `famefactorinrankingcommanders` |

**Initiative** — the widest spread of the five:

| Site | Effect | Pref |
| --- | --- | --- |
| `Regiment` ~5909, ~6197 | readiness consumption, **capped by the C-in-C's initiative** via `Mathf.Min` | `readinessadjustmentinitiative` = 0.35 |
| `Regiment` ~15692, ~15702 | **command range and combat range of every campaign formation** | `influenceinitiativeoncommanderrange` = 0.5, `influenceinitiativeoncombatrange` = 0.33 |
| `Regiment` ~7340 | feud trigger, as `abs(difference)` | `feudprobability`, `feudtriggerlevel` |
| `AIBattle` ~3281–3315 | divides the macro stance thresholds — defend, attack, **assault-flanking**, objective chains | `forcebalancetriggerdefend / attack / assaultflanking` |
| `AIBattle` ~3330 | how often the AI re-picks its macro stance | `standardtimetochangemacroai` |
| `Autocalc` ~1971 | naval engagement start range, **widened** | `commanderinitiativeinfluenceonnavalrange` = 1.3 |
| `Autocalc` ~1719 | naval ramming probability | `commanderinitiativeinfluenceonramprob` = 1.5 |
| `Autocalc` ~2800, ~2915 | auto-resolve reserve commitment and stance | — |
| `AICampaign` ~2896, ~4500, ~4903, ~4934 | offensive stance and operation planning | `commanderinfluenceinitiative…` |
| `BattleUnits` ~2507 | fog-of-war accuracy — see below | `factorvariationfullinitiative` = 0.2 |

**Leadership**:

| Site | Effect | Pref |
| --- | --- | --- |
| `Regiment` ~4307 | raises the unbearable-losses trigger | `influenceleadershipunbearablelossestrigger` = 0.75 |
| `Regiment` ~18082–18092 | morale slot 4, weighted **1.7× fame** when present (0.125 vs 0.075) and 2.5× when killed | `moralemultipliercommanderleadership` = 0.125, `…_w` = −0.03, `…_k` = −0.05 |
| `Regiment` ~18394 | damps an amplified morale **drop**, as `(2 − leadership)` | `overallmoraleinfluenceleadership` |
| `Regiment` ~18421, `Autocalc` ~3298 | the first-battle morale cap | `effectofcommanderleadershiponfirsttimemalus` = 0.5 |
| `Regiment` ~7470–7476 | `fightingspirit` | `influenceofattributesonfightingspirit[,2]` = 0.33 |
| `Autocalc` ~1483, ~1858, ~1976 | **naval firepower**, both fleet-vs-fleet and fleet-vs-fort | `commanderleadershipinfluenceonnavalcombat` = 0.3 |
| `Autocalc` ~3255 | damps auto-resolve morale hits | `overallmoraleinfluenceleadership` |
| `BattleUnits` ~5985 | retreat losses: `× (1 + (1 − leadership) × pref)` | `influenceofcommanderleadershiponlosseswhenretreating` = 0.6 |

**Administration**:

| Site | Effect | Pref |
| --- | --- | --- |
| `Regiment` ~6077, `BattleUnits` ~8138 | reduces overnight desertions | `commandersadmininfluenceondesertions` = 0.2 |
| `Regiment` ~6145 | readiness recovery | `influenceadminonreadiness` = 1.33 |
| `Regiment` ~8440 | **battlefield** supply line only | `supplyadminweightingbf` |
| `Regiment` ~21520, ~21523, `Autocalc` ~3343 | damps the fatigue penalty from poor condition of men and horses | `conditionofmenreductionadmin` = 0.25 |
| `Vessel` ~353 | fleet movement, repair and resupply rotation | `commanderadmininfluenceonmovementspeed` = 0.35 |
| `GameVars.GetMaxShips` | the ship-command limit, **doubled** at full administration | `maxshipstocommand[]` / `…fulladmin[]` |

**Cunning**:

| Site | Effect | Pref |
| --- | --- | --- |
| `Regiment` ~10590, ~10736–10742 | raiding and guarding efficiency | `cunninginfluenceonraiding` = 0.5, `…onguarding` = 0.4 |
| `Regiment` ~10595, ~10758 | intelligence gathering | `cunninginfluenceintelligence` = 2.0 |
| `Regiment` ~16978, ~16982 | **concealment** — passed straight into `FogOfWar.UnitIsHidden` | `commandercunninginfluenceonconcealment` = 0.5 |
| `Autocalc` ~2134 | siege effectiveness | `commandercunninginfluencesiegeeffectiveness` = 0.5 |
| `Autocalc` ~2521 | skirmishing casualty reduction | shipped 0.25 at full cunning |
| `Autocalc` ~3142 | own casualties when the unit is cavalry | `cavalrycunninginfluenceowncasautocalc` = 0.8 |
| `Autocalc` ~1972 | naval engagement start range, **shortened** | `commandercunninginfluenceonnavalrange` = 0.7 |
| `Autocalc` ~3723 | garrison slip-away trigger | `cunninginfluenceonslipawaytrigger` = 0.5 |
| `AICampaign` ~7870 | the AI's willingness to burn a captured depot | `commandercunningfinluenceonburningdown` = 0.4 |
| `BattleUnits` ~2510 | fog-of-war over-estimation bias | `overestimatingcunning` = 0.5 |

**Experience** is not in this list because it is not an attribute consumer but
an attribute *multiplier* (§2.1) — plus `commanderexpinfluenceonvesselcombatstrength`
= 0.4 on every vessel's combat strength, and the rally-rate term
`expinfluenceonbasemoraleincrease`.

#### Four things the map changes about §2.1 to §2.5

**Raw fields *are* read directly, in three places.** §2.1 states that nothing
reads the raw attribute fields. Three exceptions:

- `GetMaxShips` interpolates on raw `administration`, so the ship-command limit
  ignores the experience multiplier, the out-of-duty reduction and superior
  influence. **A green admiral commands as many ships as a veteran one of the
  same nominal administration.**
- `Regiment.CalculateFightingSpirit` (~7443) reads raw `fame` and `leadership`
  and applies `commanderattributesreductionifnonactive` by hand instead of
  going through the getters.
- `BattleUnits` ~6638 builds `sideinformation[].averagecommanderfame` and
  `…leadership` from the raw fields too — which is the right choice, because it
  keeps the comparison in `CalculateFightingSpirit` raw-against-raw. The
  alliance-level averages at ~3654 use the **getters**, so the campaign branch
  of that same comparison is getter-against-getter. Both halves are internally
  consistent; they are just computed two different ways.

**`factorcommanderexpattr` is live, and its label is in `campaignprefs.txt`.**
It is read by `GetCommanderExperience` with `usedforstatsmultiplying: true`
(`GameVars.cs:2437`), which every attribute getter calls, so the experience
multiplier really does span `[0.5, 1.0]` as §2.1 says. Its label is
`campaignprefs.txt:427` — *"Weighting factor of commander starting attributes
due to lower exp (due to cap of mil exp in earlier scenarios) [0-1]…"* = 0.5,
with **no** marker; the loader order confirms it, since the next two values it
reads are `strengthtriggertodisband` (0.05) and `expgaincommanderservice` (0.2),
matching lines 429 and 431.

> **Corrected.** An earlier version of this note read the `[deactivated]` label
> at `unitprefs.txt:1583` as belonging to this pref and concluded it was the
> one tag in the config files that lies. It is not. That label feeds
> **`experienceinfluenceoncommanderattributes`**, a separate field that also
> ships 0.5 and has exactly two references in the assembly — its declaration
> and its loader line. The tag is honest and the two prefs are a twin pair
> across two files
> ([29-dead-prefs-audit](29-dead-prefs-audit.md) §3.1). No `[deactivated]`
> label in the config is now known to lie.

**No commander attribute reaches any order delay.** The battle order-processing
delay is `GamePrefs.processingtimegroupstandard` (0.0001 campaign years, about
53 game minutes) multiplied by `additionalprocessingtimefeudsfactor` = 3.0
while a feud is active, plus the courier and bugle geometry and
`readinessorderdelay` = 10 scaled by `(1 − readiness)`. Searching the whole
assembly for any `GetCommander*` call on a line mentioning delay, processing,
courier, bugle or recognition returns **nothing**. Initiative reaches order
delays only indirectly, by making feuds more likely.

**`GetNumberOfFamedSeniorOfficers()` is not UI-only.** §2.5 lists it as UI; it
is also the right-hand side of the unhappiness test at `Regiment` ~17731,
which writes campaign morale slot 12.

#### Fog-of-war accuracy: two defects in one function

`BattleUnits.CalculateApproximation()` sets the multiplier that
`BattleUnits.TransformNumber` applies to **every enemy figure the player is
shown** — 104 call sites in `Statistics` alone, plus the battle panel, the
result summary, the campaign top panel and the unit panels:

```csharp
num3 = maximumvariationfowfigures × (1 − spottedRatio)          // 0.90
     + minimumvariationfowfigures × spottedRatio;               // 0.10
num3 *= factorvariationnoinitiative   × (1 − init)              // 1.0
      + factorvariationfullinitiative × init;                   // 0.2
num3 *= 1f * factorvariationfullintelligence                    // 0.3
      − averageintel * factorvariationfullintelligence;
num5  = overestimatingcunning × (1 − cunning);                  // 0.5
approximatefowstatsvariation = 1f + (randomSeed + num5) × num3;
```

1. **Wrong side.** `commandingOfficerFromSide` and `averageintel` are both
   taken from `sideFromAlliance`, which is the side of
   `GetOppositeAlliance(GameVars.playeralliance)` — **the enemy**. So how
   accurately you see the enemy is decided by the *enemy* commander's
   initiative and cunning and by the *enemy's* intelligence gathering. The
   config labels read the other way: *"when **having** full initiative"*,
   *"if **we** have full intelligence"*. And the direction compounds the
   oddity — `factorvariationfullinitiative` = 0.2 means a bold enemy general
   makes your estimate five times *more* accurate.
2. **The intelligence term is mis-formed.** `X − intel × X` with `X` = 0.3
   spans `[0.3, 0.0]`, so the variation is always cut to at most 30 % and
   reaches **exactly zero** at full intelligence. The label describes
   `1 − intel × (1 − X)`, i.e. a span of `[1.0, 0.3]`. As written, the
   fog-of-war estimate is up to **3.3× tighter than intended** at zero
   intelligence and perfect at full intelligence.

`lastapproximatefowstatsvariation` initialises to `99999f` and is never
assigned, so the ratchet clamp two lines later never fires.

---

## 3. Ranks and promotion

### 3.1 The rank tables

`ranktext[10]`: 0 Volunteer, 1 Lieutenant, 2 Captain, 3 Major,
4 Lieutenant Colonel, 5 Colonel, 6 Brigadier General, 7 Major General,
8 Lieutenant General, 9 General. `GameVars.commanderranks = 10`.

`navyranktext[10]`: 0 Master, 1 Lieutenant, 2 Commander, 3 Captain,
4 Flag Officer, **5–9 empty** — the navy stops at rank 4, which matches the
Fleet row of `ranks.txt`.

Two authored tables sit alongside them and are **completely dead**:

`GameVars.NavyRankChangeToBDatePerAlliance` = `float[10]` with slot 0 =
`1862.6f` and slots 1–9 = `9999f`; `…ToCDatePerAlliance` the same with slot 0 =
`1864.1f`.

Those are the two real US Navy rank reforms (the 1862 grades act and the 1864
vice-admiral act), authored for alliance 0 and sentinelled off for everyone
else — so Confederate naval ranks would never have changed. **Neither array
has a single consumer anywhere in the assembly**, which puts them with
`ranks.txt` rows 0–9 and `changeofexp[7]` on the list of authored
commander data the game never reads ([23](23-game-state.md) §10.2).

`Config/ranks.txt` holds four 19-entry tables — min and max rank per
`unittyp`, pre- and post-organisation-reform — selected by
`alliance.HasOrganizationReform()`, which is **project 90** in a stock build
(see [22-policy-and-project-catalogue](22-policy-and-project-catalogue.md)
§12.4; project 89 is the variant for `commandhierarchyshift == -1` and is
unreachable while the shift ships 0). `MinMaxRanks.ImportMinMaxRanks` loads
`ranks<i>.txt` per alliance and falls back to `ranks.txt`:

> Only `ranks0.txt` ships, so **alliance 0 (Union) has its own table and
> alliances 1–7 all share `ranks.txt`.** The two files differ in exactly one
> value: the Union's minimum rank for an Army Group is **7 (Major General)**
> pre-reform where everyone else needs 8 (Lieutenant General).

Shipped values (`ranks.txt`), pre-reform → post-reform:

| `unittyp` | min | max |
| --- | --- | --- |
| 10 Battery / 11 Battalion | 2 → 2 | 4 → 4 |
| 12 Regiment | 4 → 4 | 5 → 5 |
| 13 Brigade | 5 → 5 | 6 → 6 |
| 14 Division | 5 → 6 | 7 → 7 |
| 15 Corps | 6 → 7 | 9 → 8 |
| 16 Army | 7 → 8 | 9 → 9 |
| 17 Fleet | 1 → 1 | 4 → 4 |
| 18 Army Group | 8 → 8 | 9 → 9 |

**Rows 0–9 of all four tables are dead data.** `GetMinRankForUnittyp` /
`GetMaxRankForUnittyp` remap before looking up:

```
unittyp in {0,1,3,4,5} → 13   // fighting units read the Brigade row
unittyp == 2           → 11   // artillery reads the Battalion row
then, if unittyp != 17 && commandhierarchyshift + unittyp >= 10:
    unittyp += commandhierarchyshift
if the result is 17 and the input was 18: unittyp = 16
```

`GamePrefs.commandhierarchyshift` comes from `campaignprefs.txt` (shipped
**0**), is forced to 0 by `BattleController`, and is saved with the campaign.
The only other shipped value is `-1` — `Config/unithierarchydescr0.txt` and
`unithierarchydescr-1.txt` exist for both, and under `-1` the hierarchy labels
shift down one echelon ("brigades showing up as regiments"). The `18 → 17 → 16`
guard exists because a shift of `-1` would otherwise map Army Group onto the
Fleet row.

> Editing `#Unit type 0` in `ranks.txt` has **no effect**. To change a brigade
> commander's rank window, edit `#Unit type 13`.

And one hardcoded nationality rule:

```csharp
if ((num2 >= 8 && alliance == 0) & checkforpromotion)  num2 = 7;
```

> **The Union cannot be promoted past Major General** through the promotion
> path, whatever `ranks0.txt` says. Historically apt — there was no Union
> lieutenant general until Grant — but it is in code, not data.

### 3.2 `UpdateRankToFit`

The single promotion mechanism. Given a unit, it computes the rank window for
that unit's effective `unittyp` (garrisons → `standardunittypegarrisons = 11`;
`overridesymbol >= 10` wins; navy branch is additionally capped at the Fleet
max) and then:

| Condition | Effect |
| --- | --- |
| `rank < min` | **Promote** to `min`. `exp += explosscommanderbelowminrank(−0.25) × steps × professionalFactor / (1 + militarySchoolInfluence)`; `AdjustFame(famegaincommanderaboverank(0.05) × steps, 1)`; `dateofpromotion[rank]` stamped; a service-history line added |
| `rank > max` | `exp += expgaincommanderabovemaxrank(0.10) × steps × professionalFactor`; `AdjustFame(famelosscommanderbelowrank(−0.20) × steps, 1)`; `latestranktofit` lowered |
| `rank − max >= defamedtriggerbelowlevel(3)` | `SetDefamed()` |
| `exp` leaves `[0,1]` | the overflow is parked in `bonusmalusexp` |

`professionalFactor` = `experiencelosspromotingprofessionals` (0.5) for
`westpoint` commanders — West Pointers pay half the experience cost of
over-promotion. The label says *"loss … when appointed to a higher command"*,
but `GetExpEffectOfPromotion` (`GameVars` ~1595) applies the same factor to
the gain branch, so they also earn half the experience for serving below
their rank. Military schools divide only the loss.

> **Rank is never lowered.** Only the `rank < min` branch writes `rank`. Being
> assigned below your station costs fame and *gains* experience, and
> `latestranktofit` records the lower ceiling, but the man keeps his stars.
> `UpdateToRank(destinationrank)` routes through the same function, so **manual
> demotion is impossible too**.

`latestranktofit` is the hysteresis that stops a commander oscillating as he is
shuffled between commands.

Two cosmetic leftovers in the same method: `_ = 1571;` (a discarded constant)
and `commander.IndexOf(this);` with the result thrown away — an O(n) scan over
~1,900 entries on every promotion.

### 3.3 Historical promotion from data

`commanders.txt` ships **every commander at `rank = -1`** (verified: all 1,906
records). Two functions turn the data into ranks:

- `UpdateCommanderRanks(date)` at campaign start — sets `rank` to the highest
  `dateofpromotion[j]` already passed, writing a service-history line for each,
  and defaults to `1` (Lieutenant) if none has.
- `Commander.CheckActivation(date, minimumrank)` in the commander tick —
  continues the ladder as the campaign runs, but **only for officers with no
  command**, and skips the player's own side entirely unless `rank == 0`.

> So the officer corps starts as lieutenants and rises on the authored
> historical schedule, while officers *in post* are promoted by
> `UpdateRankToFit` instead. `dateofpromotion[]` is therefore the most
> load-bearing field in `commanders.txt`.

Both guard on `dateofpromotion[j].yearfraction > 1000f` to distinguish a real
date from the `(0,0,0)` default.

### 3.4 AI promotion

`AICampaign.CheckCommanderPromotion(id)` promotes when

```
exp × aicommanderpromotion_exp
 + fame × aicommanderpromotion_fame
 + GetSeniority() × aicommanderpromotion_duration  > 1
```

subject to the max rank for his current command (navy branch exempt) — so
**experience, reputation and time in grade are substitutable**.
`AICampaign.CheckVerticalReplacement` separately swaps a worse commander for a
better one when the attribute gap exceeds `expdifferenceforverticalpromotion`,
discounting the challenger's score by `GetExpEffectOfPromotion`.

---

## 4. Fame, defame and battle results

### 4.1 One famous and one defamed man per battle per side

`BattleUnits.CalculateCommanderFame(resulttype, …)` (~5872) scores every
commander on the field:

```
ratio = Σ inflictedcasualtiesperunittype / max(grouplosses, 1)
        → 1 if it is exactly 0, capped at 5
score = ratio + (exp − expbeforebattle) − numberofrouts / max(allattachedunits.Length, 1)
```

— a kill ratio, plus the experience gained in the battle, minus the routed
share — then:

```
score × famefactorleveladjustment[clamp(unittyp,13,16) − 13]
score × woundedinfluencemodifier                                  if score > 0 and status == 3 (WIA)
score × (1 + alliance.GetFameImprovementThroughPrintingWorks())   if score > 0
score × fameadjustmentembeddedreporters[level]                    if unittyp >= 15 and campaign perk 14
→ AdjustFame(score, 2)
```

then the **best** scorer becomes famous if `score > fametrigger[resulttype]`,
and the **worst** becomes defamed if `score < defamedtrigger[resulttype]`.

`resulttype` is `battleResult.victorytypeforplayer` — `GetBattleResultType()`
read from the **player's** side (0 minor victory, 1 major victory, 2 minor
defeat, 3 major defeat) — and the one value is used for **both** sides'
commanders. The worst-score slot starts at 0, so only a **negative** score can
be defamed, which needs a kill ratio below the routed share. Shipped values
and the reading are in open question 9.

> **Every commander's fame moves by his score** (`AdjustFame(score, 2)`), not
> only the two who are named. `woundedinfluencemodifier` ships **1.2** — its
> label reads *"[>1]"* — so a wound earns 20 % more fame from a good battle.

> **`Alliance.printingworksvalue` boosts commander fame** —
> `printingworksvalue ^ exp × lin`. Together with
> `GetIntelligenceImprovementThroughPrintingWorks` and the per-state
> `Nation.printingworks[alliance]` →
> `GetInfluenceOfPrintingWorksOnDraftSupport`, that closes the
> `printingworks[]` question from [17](17-politics-and-support.md). The same
> two-level pattern — a per-alliance scalar and a per-state array — applies to
> military schools.

Defect in the same block: the famous-commander guard reads
`(float)array2[i] != array3[i]`, comparing a **commander id** against a **fame
score**. It was evidently meant to compare the best and worst *ids* so the same
man cannot be both. As written the comparison is meaningless but almost always
true, so the guard does nothing. Nearby, `Mathf.Pow(x, 1f)` appears twice —
identity exponents left over from tuning.

### 4.2 Fame state is two dates

`dateoffame` and `dateofdefame`, not booleans. `IsFamous()` requires
`dateoffame` in the past and not defamed; `IsDefamed()` requires `dateofdefame`
in the past. `SetFamous()` returns `false` if already famous (so the
best-of-battle award only lands once) and clears a *past* defame.
`RemoveDefamedStatus()` and `SetFamous`'s clearing path both write
`(1,1,1880)` — a sentinel far past the campaign, rather than zeroing the field.
`ClearDefamedStartup()` pushes any *future*-dated fame or defame out to the
same sentinel when a scenario loads.

> **The sentinel makes both states once-only.** Nothing ever clears
> `dateoffame`, and `SetFamous()` refuses whenever `dateoffame.yearfraction > 0`
> — so famous is permanent, and if a battle's best scorer is already famous,
> nobody is made famous from that battle. The defame side is worse: both
> `SetDefamed()` call sites that can fire in ordinary play — the battle pick
> (`BattleUnits` ~5933) and the over-rank appointment (`GameVars` ~1714) — are
> gated on `dateofdefame.yearfraction <= 0f`, which the 1880 sentinel fails. So
> **once a commander's defame has been cleared, by rehabilitation or by
> becoming famous, he can never be defamed again.** Only the
> first-major-victory monuments in `ImportExportUnitData` (~3043–3069) call
> `SetDefamed()` without that guard.
>
> **Untested:** the same sentinel is what `ClearDefamedStartup()` writes, and
> the shipped `commanders.txt` authors a fame date for **87** commanders and a
> defame date for **76**. Each one it clears is locked out of the matching
> state for the whole campaign. It runs from `Load(resetexp: true)`, which a
> new campaign uses (`CampaignController` ~72). Which dates it clears depends
> on whether `GetCampaignFloatingDateSep()` already returns the scenario date
> at that point: if so, only historically later fame and defame dates are
> locked out; if it returns `(0,0,0)`, **every** authored date is. A new 1861
> campaign that checks whether Sherman (authored famous 2 Sep 1864) can ever be
> made famous would settle it.

Other `SetFamous()` triggers: fleet perk 8, a naval or raiding action in `IIP`,
and the scripted battle monuments in `ImportExportUnitData`.

### 4.3 Rehabilitation, and a probability bug

In the commander tick:

```csharp
float refpoint = Mathf.Max(commanderehabilitationmin, now − dateofdefame.yearfraction);
float num2     = Tools.Interpolate({min, max}, refpoint, {0f, 1f});
if (Random.Range(0f, 1f) * elapsed < num2)  RemoveDefamedStatus();
```

Shipped `commanderehabilitationmin = 0.4` yr, `max = 1.5` yr. Note the pref is
misspelled in the game — "ehabilitation", no `r` — joining
`choesionbyotherunitspassing` from [13](13-morale-and-cohesion.md).

> **`elapsed` multiplies the random draw instead of the threshold.** Every
> sibling rate check in the same method does `Random(0,1) < prob × elapsed`.
> Because `elapsed` is a tiny fraction of a year, `Random(0,1) × elapsed` is
> almost always below `num2` as soon as `num2` rises above ~0.001. The
> practical effect is that **rehabilitation happens almost immediately once
> `commanderehabilitationmin` has passed, and `commanderehabilitationmax` is
> nearly inert.**

The roll is gated on `status == 0` (`BattleUnits` ~2270), so a wounded or
captured commander stays defamed until he returns to duty.

---

## 5. Casualties and replacement

### 5.1 `Commander.status`

| Value | Meaning | Set by |
| --- | --- | --- |
| 0 | Active | recovery, scenario start |
| 1 | **never assigned anywhere** | |
| 2 | POW / captured | melee rout roll; unit surrender |
| 3 | WIA | firefight and melee rolls; player-commander override |
| 4 | KIA | firefight and melee rolls |
| 5 | DLC only | `DLC_WL` |

`status > 0` is the universal "out of duty" test: it halves every attribute,
blocks appointment (`IsAvailable`), and triggers replacement.

### 5.2 `UpdateCommanderCasualtyState`

Guards: returns unless `status == 0`, and never fires for `unittyp` 3, 4 or 5,
nor for a **detachment** (`parentregiment != null`). Then, per casualty event,
with `p = strengthloss / (strength + strengthloss) × probabilityadjustment`:

| Context | Rolls, in order |
| --- | --- |
| Firefight | KIA at `probabilitybrigadecommanderdeadfirefight × p`, then WIA at `probabilitybrigadecommanderwoundedfirefight × p` |
| Melee | KIA at `…deadmelee × p`, then WIA at `…woundedmelee × p`, then — if routed — POW at `probabilitybrigadecommandersurrendering × p` |

> **The rolls overwrite rather than compete.** WIA is rolled *after* KIA and
> assigns unconditionally on success, so a commander is only killed when the
> KIA roll hits **and** the WIA roll misses. In melee the surrender roll
> overrides both. The effective rates are therefore not the nominal prefs.

Two manual-versus-auto divergences, consistent with the firepower split in
[11](11-combat-resolution.md):

1. In the **autocalc** path `SufferCasualties` calls
   `UpdateCommanderCasualtyState` for the unit **twice** — once early, once in
   the common tail — so the effective probability is roughly doubled. The
   manual path calls it once.
2. The autocalc path additionally walks **up to 10 levels of parent
   formations**, rolling for each superior whose nearest enemy is within 75
   units, at `1 / depth³ / max(1, groupstrengthactive)`. This is the same walk
   as the command-shock propagation. **Superior commanders are at risk only in
   auto-resolved battles.**

### 5.3 Succession

`BattleUnits.ReplaceCommanderOfUnit(unit)`:

1. Scan the unit's **direct subordinates** for the active commander with the
   highest `campaignranking` (skipping anyone whose `historicalbranch != 2`
   when the vacancy is artillery), promote him into the vacancy with
   `AssignCommando` + `DoCommanderPromotion`, and then **recurse** to fill the
   hole he left.
2. If no subordinate qualifies, fall through to
   `GameVars.GetSuitableCommander`.

`DoCommanderPromotion(id)` runs `UpdateRankToFit`, writes a service-history
line, and applies `readiness += readinessreductioncommanderchange` (negative)
to **every attached fighting unit** — so a succession cascade costs readiness
at every level it touches.

`CheckCommanderPromotions(refreshall)` is the batch pass over everyone with
`refreshcommand` set.

`AssignCommando` contains one more hardcoded alliance rule:

```csharp
if (reg.alliance != 1)  flag = false;   // flag gates the rename
```

> **Only Confederate units are renamed after their commander.** Historically
> correct — the CSA named formations after generals while the Union numbered
> them — but a mod adding a side cannot opt in.

### 5.4 `GetSuitableCommander` — a four-tier cascade

1. **Strict:** `CanBePickedForAIReplacement`, `CheckSuitableRank`, home state
   `recruitable`, West Point constraints, branch match → highest
   `campaignranking`.
2. If none and `unittyp >= 14` or a garrison: drop rank suitability, require
   `political || westpoint`.
3. Otherwise: relaxed pass keyed on the unit's home state.
4. Last resort: **invent one** — `new Commander(createdummy: true, …)`, with
   `exp = alliance.militaryexperience × Random(dummycommanderattributesfactorfrom, …to)`,
   randomized attributes (a separate `…europe` range for alliances 2+), a name
   assembled from
   `Config/CommanderDummies/{firstname,secondname,lastname}_<n>.dat`, and
   `usedummyphoto = true`.

Notes:

- `!isgarrisonbasicunit` appears in **every** candidate test, so a basic
  garrison unit can never draw a historical officer and always gets a dummy.
- `unittyp == 18` returns early even when nothing was found, so Army Groups get
  no commander here — `GameVars.CheckArmyGroupPromotion` handles them.
- `forcenowestpointarty` makes the game prefer **non-West-Point** officers for
  artillery, unless the West Pointer's own branch is artillery.
- The final `if (returndummycommander) { … }` block is **duplicated verbatim** —
  dead code.

`CanBePickedForAIReplacement` excludes: wrong alliance, `!IsAvailable()`,
serving under the player, already holding a command (unless asked otherwise),
staff members, army-group commanders, and the commander-in-chief.

`IsAvailable()` also honours `dateofunavailability` — an authored date after
which a commander drops out of the pool (historical death, resignation or
transfer).

---

## 6. `campaignranking` and the commander-in-chief

### 6.1 The ranking key

```
campaignranking = commandLevel × 100000
                + rank         × 10000
                + floor(exp  × 1000)
                + floor(fame × 100)
```

A packed lexicographic sort key. `commandLevel` comes from
`BattleUnits.GetCommandLevel(commander)` = `max(13, currentcommand.unittyp)`,
overridden by `overridesymbol >= 10`, **and forced to 18 for the
commander-in-chief**. Dummy commanders (`usedummyphoto`) get 0.

Then it is clamped, and this clamp is the mechanism behind the Lieutenant
General policy:

```csharp
if (alliance.ltgenpolicy)                   level = min(level, 18);
else if (alliance.HasOrganizationReform())  level = min(level, 16);
else                                        level = min(level, 15);
```

> `HasOrganizationReform()` here is **project 90**, and `ltgenpolicy` is
> **policy 42**, the Lieutenant General Act
> ([22](22-policy-and-project-catalogue.md) §7.2, §12.4).

> Before `ltgenpolicy`, the C-in-C's level-18 advantage is clamped away and he
> competes on rank and experience like everyone else. **After it, he outranks
> every possible rival by 100,000 points** — and the periodic re-election is
> *also* gated off (`flag && !alliance.ltgenpolicy`). Together those two lines
> are what "appointing a Lieutenant General" actually does: it makes the
> appointment permanent.

The same pass maintains `alliance.bestcommanderidperlevel[rank]` and
`worstcommanderidperlevel[rank]`, which the AI uses for vertical replacement.

Because `exp × 1000` contributes up to 1,000 and `fame × 100` up to 100 into
the same band, fame outweighs experience differences finer than 0.1.

**There is a second, incompatible producer.** `BattleUnits.UpdateCommanderRankings`
buckets the commanders present in a battle by `(side, rank)`, sorts each bucket
by `exp + famefactorinrankingcommanders × fame`, and writes the **1-based
position** in the bucket:

```csharp
if (IsCampaign) commander[…].campaignranking = n + 1;
else            commander[…].ranking         = n + 1;
```

`Commander.ranking` — a field the earlier draft of this file wrongly called
uncomputed — is exactly that battle-scene ordinal, read by the
commanders-list sort and the statistics panel. But note the semantics are
**inverted** between the two producers: the packed key is "higher is better",
the ordinal is "1 is best".

In `BattleUnits.Update` the call sits inside `if (!IsCampaign)`, so the
`campaignranking` arm is unreachable from the tick. Its only other caller is
`Statistics`, with `overridelastupdate: true`. See open question 4.

### 6.2 Selection

`BattleUnits` re-elects the C-in-C every `updatetimecommanderinchief` (0.25
game-years) as the available, non-navy commander with the highest
`campaignranking`; `Policies` then grants him `ltgenfame` via
`AdjustFame(…, 4)`.

Defect: `bool flag2` is declared **outside** the per-alliance loop and never
reset, so once one alliance's C-in-C is found unavailable, every alliance later
in the same pass is forced to re-elect too. Low impact — re-election usually
returns the same man — but it is a scope bug.

---

## 7. Feuds — insubordination as a mechanic

Not covered by any earlier findings file, and entirely commander-driven.

`Regiment.CalculateFeud()` runs every `feudupdatecycle` (1 game hour) per
formation:

```
threshold = feudtriggerlevel − (fame(self) − fame(superior))
gap       = |initiative(self) − initiative(superior)|
duration  = gap − threshold
feud if Random(0, feudprobability) < gap − threshold
```

With the shipped `feudtriggerlevel = 0.3` and `feudprobability = 1`, the hourly
probability is simply **`|Δinitiative| + Δfame − 0.3`**.

> Two readings fall straight out. **A subordinate feuds when his initiative
> differs sharply from his superior's — in *either* direction** (it is an
> absolute difference, so the over-cautious subordinate quarrels as readily as
> the over-eager one) — **and when he is the more famous of the two.**

`ActivateFeud(duration)` then:

- sets `ai_feudstance = Random(0, 4)` — one of the four stances,
- moves the formation into **`bunits.aibattle[2]`**, a third AI controller that
  is neither player nor enemy, and
- schedules `feuddeactivationtime = now + durationoffeuds(5) × duration` hours.

On expiry the group is removed from that pool and `RestoreOldAIGroup` puts it
back. Throughout `AIBattle`, the micro-AI guards read
`(aigroup.ai_feudstance == -1) | (isplayeraiorfeud == 2)` — that is, a unit is
micro-managed either because it is not feuding, or because it belongs to the
feud controller.

While feuding, `AIBattle.CheckForFeudGroupActions` may order the formation
straight at the nearest enemy within `neededdistancefeudgroupmovement` (250):

```
p = initiative² × probfeudgroupmovement(1.0)
p × chanceoffeudspoliticalcommanders(2)    if commander.political
p × chanceoffeudsvolunteercommanders(0.5)  if neither political nor westpoint
```

> **A high-initiative political general is the most insubordinate thing on the
> field** — twice as likely to charge off as a West Pointer, and four times as
> likely as a volunteer. West Pointers take the baseline.

`GameVars.debug_deactivatefeuds` disables the system; the tutorial forces it on
for a single named unit ("Stonewall Brigade"). The `battleprefs.txt` comment
for `durationoffeuds` works an example with the value 10, but the shipped value
is **5** — stale documentation in the config.

---

## 8. The president does nothing

`CampaignStrategy.currentpresident` is recomputed by `UpdatePresident()` as the
**last** entry in a prefab-authored `(name, date, title, portrait, appointed?)`
table whose `datefrom` has passed, per alliance. It is then used for exactly
four things: a name label, a title label, an "Inaugurated"/"Appointed" line,
and a portrait.

It is written to the save file, but `UpdatePresident()` is called immediately
before the write, so the stored value is always re-derived rather than
restored. And
`SceneManagement.LoadCampaignScenario(…, int chosenpresident = -2, …)`
**accepts the parameter and never reads it**.

> **The president is cosmetic.** Elections exist as a campaign objective
> (national morale reason code 8, `demoralizevotersunionmoralelevel`) but do
> not change who the president is, and nothing mechanical reads
> `currentpresident`. This answers the open question in
> [17](17-politics-and-support.md).

The president arrays are Unity inspector fields with `[Tooltip]` attributes and
`DDMMYYYY` date strings, so they are scene data — a mod can edit them without
touching config, and cannot edit them without touching the scene.

---

## 9. Political commanders and state support

`BattleUnits.UpdateCommanderStateSupport(commanderid)`:

```
if (commander.political && commander.currentcommand != null)
    gain = basestatesupportgainpolcomm(0.005)
         × interp(fame,  [0,1]       → supportgainmultiplierfame  [1.0, 3.5])
         × interp(level, [0,1,2,3,4] → supportgainhierarchylevel  [1.0, 1.5, 2.0, 3.0, 2.0])
    level = clamp(currentcommand.unittyp − 13, 0, 5)
    nation[commander.stateid].supportthroughappointedcommanderstemp[alliance] += gain
```

committed as a **delta** against the previous total via
`AdjustSupport(alliance, temp − committed, 2)`. So it is a **standing**
contribution, not an accrual: the state's support sits that much higher while
the general holds a command and drops back when he loses it. It reads raw
`fame`, so the ×1.4 famous multiplier does not apply.

> The reason code is **2, "Commander appointments"** — not 38. Code 38,
> "Commander", exists but is **DLC-only** (the player-commander's personal
> standing in his home state). [17](17-politics-and-support.md) guessed 38;
> this corrects it.

Two quirks:

- `level` is clamped to 5 but the interpolation x-axis only runs to 4, so
  **Army Group (18) falls off the end onto the Fleet multiplier (2.0)** — one
  notch *below* Army (3.0). The config comment lists exactly five echelons
  ("brigade, division, corps, army, fleet"), so the Army Group case was simply
  not considered.
- The commit is gated on `num2 == GameVars.commander.Count - 1`, and the
  round-robin caller passes one id at a time, so the commit fires after
  accumulating commanders 0…count−2 and then zeroes the temp. **The last
  commander in the list never contributes state support.** A clean off-by-one.

252 of the 1,906 shipped commanders are `political`.

---

## 10. `Config/commanders.txt` — the data model

**Self-indexing**, like `resources.dat`: a count line, then for each record an
id line followed by the fields. `Commander.Load` reads **65 fields** per
record, so 66 lines per record. Shipped file:
`1 + 1906 × 66 = 125,797` lines — verified exactly.

Attributes are stored as **integers 0–100** and divided by 100 on load.

| Offset | Field | Notes |
| --- | --- | --- |
| 0 | id | selects the slot |
| 1–3 | `name`, `firstname`, `combinedname` | |
| 4 | `alliance` | |
| 5–10 | `exp`, `fame`, `leadership`, `initiative`, `administration`, `cunning` | ÷100 |
| 11–12 | `veteran`, `westpoint` | |
| 13 | `historicalbranch` | 0 infantry, 1 cavalry, 2 artillery, 3 engineer, 4 navy |
| 14–16 | `dateofbirth` | day, month, year |
| 17–43 | `dateofpromotion[1..9]` | nine dates × 3 lines; slot 0 is always `(0,0,0)` |
| 44–46 | `dateoffame` | |
| 47–49 | `dateofdefame` | |
| 50–52 | `dateofunavailability` | |
| 53 | `usedummyphoto` | `1`/`0`, not a bool literal |
| 54 | `stateid` | |
| 55 | `political` | |
| 56 | `servicehistory` | `\n` escapes via `Tools.TransformToLineBreaks` |
| 57 | `bonusmalusexp` | |
| 58 | `battles` | |
| 59 | `rank` | **`-1` in every shipped record** |
| 60 | `status` | version ≥ 0.74 |
| 61 | `latestranktofit` | version ≥ 0.8301 |
| 62–65 | `portraitid`, `portraitfilename`, `frameid`, `activeincampaign` | version ≥ 1.08 |

`usedescription` (version ≥ 1.08) interleaves a label line before each field.
The shipped `Config` file does **not** use it; saves may.

An optional sibling `attributes.dat` carries the `changeof*` breakdown arrays,
each prefixed by its own length — the only self-describing array lengths seen
in this format so far.

Shipped distributions:

| Field | Values |
| --- | --- |
| `alliance` | 0 Union **1146**, 1 Confederacy **723**, 2 Great Britain 14, 3 France 9, 4 Spain 4, 7 Neutral States 10 |
| `historicalbranch` | 0 infantry 1086, 1 cavalry 221, 2 artillery 415, 3 engineer **61**, 4 navy 123 |
| `westpoint` | true 434, false 1472 |
| `veteran` | true 456, false 1450 |
| `political` | true 252, false 1654 |
| `usedummyphoto` | 1 → 400, 0 → 1506 |
| `rank` | **−1 for all 1,906** |

Spot-check of record 0 against history: *Irvin McDowell*, Union, artillery
branch, born 15 Oct 1818, commissioned 1 Jul 1838, promoted at offsets 6 and 7
on 14 May 1861 and 14 Mar 1862 — brigadier general and major general on exactly
those dates. The data is researched, not generated.

### 10.1 There are eight alliances, not five

`Campaigns/001/A/nations.dat` declares 53 states and **8 alliances**:

| # | Name |
| --- | --- |
| 0 | Union |
| 1 | Confederacy |
| 2 | Great Britain |
| 3 | France |
| 4 | Spain |
| 5 | Mexico |
| 6 | Sea Trade |
| 7 | Neutral States |

[17](17-politics-and-support.md) said "2–4 foreign powers"; the array is
`new Alliance[num2]` sized from the file, and slots 5–7 are pseudo-alliances
(Mexico, a trade counterparty, and the states that had not yet chosen a side).
This is why ten commanders carry `alliance == 7` without crashing the
per-commander loops that index `GameVars.alliance[]`.

---

## 11. `historicalbranch` is two different enumerations

| Class | Values |
| --- | --- |
| `Regiment.historicalbranch` | 0 infantry, 1 cavalry, 2 artillery |
| `GameVars.Commander.historicalbranch` | 0 infantry, 1 cavalry, 2 artillery, **3 engineer, 4 navy** |

Labels: `GameVars.branchtext[5] = { infantry, cavalry, artillery, engineer,
navy }`, with a descriptive `branchtextsub[5]` alongside.

`historicalbranch == 4` is the navy discriminator used throughout — excluded
from the C-in-C election, from army rank tables, from army service-history
text, and capped at the Fleet rank ceiling.

The data agrees with the label order. Robert E. Lee, George G. Meade, George
B. McClellan and Gouverneur K. Warren — all pre-war engineers — are branch 3;
David G. Farragut, David D. Porter and Raphael Semmes are branch 4; Thomas J.
Jackson and Henry J. Hunt are branch 2.

**Branch matching has a combat effect in two code paths, plus a third site
written for engineers that tests the navy number.** "Matching" means branch 0
on `unittyp` 0 or 3, branch 1 on 1 or 4, branch 2 on 2, and it is always the
commander of the unit itself, never a superior's. Shipped values from
`unitprefs.txt` 1590–1596, alignment checked against the loader
(`GamePrefs` ~5509–5515):

| Path | Guard | Effect |
| --- | --- | --- |
| `Regiment`, manual battle | `status == 0 && branch matches && commander.westpoint` | Artillery only: accuracy ×`increaseofaccuracywhencorrectbranch` **1.1** (`FireBullet` ~9554) and rate of fire ×`increaseofrofwhencorrectbranch` **1.2** (`UpdateFiringAndReloading` ~9987). All three arms: morale recovery ×`increaseofmoralecapforcorrectbranch` **1.25** (`UpdateMorale` ~17897) |
| `Regiment`, campaign map | same | Morale recovery ×1.25 (`UpdateMoraleCampaign` ~17573) |
| `Autocalc` | `status == 0 && branch matches` — **no `westpoint` requirement** | Artillery rate of fire ×1.2 (`GetROF` ~3027); morale recovery ×1.25 (~3408). No accuracy term |
| Engineering points: `BattleUnits.CalculateAllUnitsOfAlliance` ~9205, `Autocalc.CalculateEngineeringPoints` ~4553 | `status <= 0 && historicalbranch == 4` | +`availableconstructiontimeperengineerunit` × (`factorengineerpointsbranchofarms` **1.5** − 1) = +0.25 h per infantry unit (`unittyp == 0`) |

> **The engineer bonus exists, and it is keyed to the navy.** The pref's label
> is *"Increase factor of engineering points if commander fits to branch of
> arms (engineers)"*, and its only two reads test `historicalbranch == 4`.
> A naval officer can never be seated on an infantry unit:
>
> - `CheckSuitableRank` rejects branch 4 for every non-fleet `unittyp`
>   (§11.1), which covers the player's commanders list and tier 1 of
>   `GetSuitableCommander`;
> - tiers 2 and 3 of `GetSuitableCommander` (§5.4) each test
>   `historicalbranch != 4` explicitly;
> - in authored data, a scan of all 68 shipped `regiments.dat` files
>   (`Campaigns/` and `Battlefields/`; 7,165 units, 6,425 with an authored
>   commander id) finds **7** land units under a naval officer, all of them
>   **artillery**: Thomas J. Page on *Pendleton's Corps* in `Campaigns/001/C`,
>   and William W. Carnes on a battery in six battlefields. None is infantry,
>   so none qualifies.
>
> Meanwhile **114** infantry units are authored under engineer-branch
> commanders: exactly the units the bonus was written for. So engineer
> (branch 3) still has no mechanical effect in the base game, but because the
> one bonus written for it tests `4` where `3` was meant. An earlier version of
> this section said no such bonus existed, and that the only code reading
> branch 3 is the avatar panel, the commanders-list filter and the DLC
> requirement system. The second half is still true.
>
> And note the `westpoint` asymmetry — one more manual-versus-auto divergence
> to add to the list in [summary-combat](summary-combat.md).

`increaseofmoralecapforcorrectbranch` is applied to the morale *recovery rate*,
not to a cap, despite the name.

`branchtextsub` promises more than this for every branch: readiness, marching
and condition for infantry; intelligence, raiding and horse condition for
cavalry; gun condition for artillery. None of those reads `historicalbranch`.
The tooltip built in `Commander` ~761 prints the branch only when `westpoint`
is set, which matches the manual-battle rule and hides the branch that still
counts in auto-resolve.

The only other non-cosmetic reads of a commander's branch are about
**assignment**, not effect: the artillery vacancy rule in succession (§5.3)
and `forcenowestpointarty` in `GetSuitableCommander` (§5.4).

### 11.1 Professional, volunteer, political, veteran

Three flags from `commanders.txt` — `westpoint`, `political`, `veteran` — give
the background classes the commanders list filters by (`CommandersList`
~285–309):

| Filter | Test |
| --- | --- |
| Professional | `westpoint` |
| Volunteer | `!westpoint && !political` |
| Political | `political` |
| Veteran | `veteran` (overlaps all of the above) |

Shipped mix:

| | Union | Confederacy | Europe (alliances 2, 3, 4, 7) |
| --- | --- | --- | --- |
| `westpoint` only | 252 | 159 | 3 |
| `westpoint` and `political` | 17 | 3 | 0 |
| `political` only | 132 | 98 | 2 |
| neither (volunteer) | 745 | 463 | 32 |
| `veteran` (any of the above) | 273 | 177 | 6 |

The 20 with both flags appear under both Professional and Political, and
every rule below that tests `political` applies to them.

**`CheckSuitableRank(unittyp, unit, autoassignment)`** (`GameVars` ~1753) is
where the classes differ for assignment. With `num = unit.state`
(−1 if there is no unit):

| Test | Result |
| --- | --- |
| `unittyp == 17 && historicalbranch != 4 && != -1` | rejected: only naval (or invented, branch −1) officers take fleets |
| any land `unittyp` and `historicalbranch == 4` | rejected: naval officers take nothing on land |
| `!political && !westpoint && num >= 0 && num != stateid` | rejected: **volunteers only lead units from their home state** |
| `political && num >= 0 && num != stateid` | rejected: **political generals likewise** |
| `autoassignment && !political && !westpoint && unittyp > 13` | rejected: auto-assignment never gives a volunteer a division or above |
| `autoassignment && usedummyphoto` | rejected: auto-assignment skips officers with a placeholder portrait |
| rank above the unit's maximum; army-group minimum rank and experience; `status != 0`; `!IsAvailable()` | rejected |

West Pointers are exempt from both home-state tests.

- **The rule binds the player.** `CommandersList` ~465 fills
  `commanderassignable` from `CheckSuitableRank(chosenunit)` (with
  `autoassignment` false), and the Assign button (~669) is disabled for an
  officer who fails it.
- **It mostly binds brigade commands.** `CreateUnitDefault` gives every
  `unittyp <= 13` a state; most formations are created with `state = -1`
  (corps and divisions under a new army, policy-raised formations), so the
  home-state tests pass for them.
- **Automatic picks skip it.** `GetSuitableCommander` calls
  `CheckSuitableRank(unittyp, null, autoassignment)`, so `num` is −1 in tier 1
  and the home-state tests never fire; tier 3 applies its own
  `commander.stateid == stateid` match to everyone.

**What else each flag does:**

- **`westpoint`:** the manual-battle branch bonus above; half the experience
  effect of a rank change in both directions (§3.2); feud-charge baseline ×1
  (§7); exempt from the home-state tests; a "West Point Graduate"
  service-history line (`Commander.Load` ~1286, non-navy only).
- **`political`:** the standing state-support contribution (§9);
  feud-charge chance ×`chanceoffeudspoliticalcommanders` 2 (§7); home-state
  rule.
- **Neither (volunteer):** feud-charge chance
  ×`chanceoffeudsvolunteercommanders` 0.5 (§7); home-state rule; no division
  or above under auto-assignment; no branch bonus in manual battles.
- **`veteran`:** read in exactly one mechanical place,
  `Commander.ResetExperience` (~2037), which clamps `exp` to
  `alliance.militaryexperience + increaseexpcapcommanderifveteran` (0.25)
  for a veteran and to `militaryexperience` for everyone else.
  `ResetExperience` runs from `Commander.Load` only when `resetexp` is true
  — a fresh import for a new campaign (`CampaignController` ~72), a
  standalone battle (`BattleController` ~77) or the main menu (`MainMenu`
  ~772); every save path passes `false` — and from the invented-commander
  constructor. After that, `IncreaseExperienceThroughService` (~388) caps
  service experience at `militaryexperience` with **no** veteran term, and
  battle experience (`Regiment` ~16730) clamps only at 1.0. So the veteran
  margin [29](29-dead-prefs-audit.md) §3.2 describes is a ceiling on the
  **starting** value, not an ongoing one. Also a "Fought in the Mexican War"
  service-history line and a tooltip line.

**Invented commanders** (`new Commander(createdummy: true, …)`, §5.4) set
`westpoint = false` and `veteran = false`, leave `political` at its default
`false`, take `historicalbranch = 4` for a fleet and −1 otherwise, and take a
`stateid` from units near where they are created. They are volunteers with no
branch: never a branch bonus, and subject to the home-state test like any
volunteer.

---

## 12. How commanders are driven: one per call

`BattleUnits.UpdateCommanderHistories()` processes **exactly one** commander
per invocation via a `commanderrunthrough` round-robin index that wraps at
`GameVars.commander.Count`. Per [10](10-simulation-tick.md) it runs ten times
per frame, so a full sweep of 1,906 commanders takes roughly 190 frames.

Correctly, the method derives its own elapsed time:

```csharp
float elapsed = campaignFloatingDate − commander.lastreturnupdate;
commander.lastreturnupdate = campaignFloatingDate;
```

so every rate in it is normalised against the stagger rather than the frame.
The same idiom as the staggered economy updates, and worth copying.

What each pass does, in order:

1. `UpdateServiceHistory()`
2. Replace the commander of his unit if `status > 0` and the unit is not in
   battle (`frame >= 50`)
3. WIA recovery at `commanderwiareturningprob × elapsed` — on return, **lose**
   fame and initiative by `commanderattributesmultiplierwia`
4. POW release at
   `interp(rank, [0, commanderranks] → commanderpowreturningprob) × elapsed` —
   senior officers are exchanged on different odds from junior ones
5. Rehabilitation if defamed (§4.3)
6. `AICampaign.CheckVerticalReplacement`
7. `UpdateCommanderStateSupport` (`frame > 31`)
8. `UpdateCommanderRankingsCampaign`
9. `CheckActivation` and, for AI alliances, `CheckCommanderPromotion`
   (`frame >= 50`)
10. `IncreaseAttributes` from military schools

Step 10 indexes `GameVars.nation[commander.stateid]` without a bounds check,
and the dummy constructor can leave `stateid = -1` when no owned state is
found. `GetExpEffectOfPromotion` has the same unguarded access. Both are latent
`IndexOutOfRange` sites for a mod that creates commanders.

---

## 13. Where commanders reach

Collected from the earlier passes, now with the attribute pipeline understood.
Every entry below reads a **getter**, so all of it is scaled by
`expMultiplier ∈ [0.5, 1]`, halved when out of duty, and skewed by the
superior-influence factor.

| Attribute | Consumers |
| --- | --- |
| **leadership** | `fightingspirit` ([13](13-morale-and-cohesion.md)); the casualty amplifier on morale loss; naval victory balance; `GetStaffSkill` |
| **initiative** | stance thresholds ([11](11-combat-resolution.md)); command and combat range; readiness consumption; **feuds**; offensive-operation gating; naval start range and ram probability |
| **administration** | battlefield supply mitigation and depot throughput ([14](14-supply-and-logistics.md)); desertion offset; fatigue buffering ([15](15-attrition-and-condition.md)); readiness recovery; ship movement; `GetMaxShips` |
| **cunning** | concealment; cavalry casualty reduction; siege effectiveness; raiding and guarding efficiency; intelligence gathering; depot burning; slipping away from battle |
| **fame** | desertion offset; `fightingspirit`; state support from political commanders; `campaignranking`; feuds; newspaper and diary events |
| **exp** | the multiplier on all four attributes; `campaignranking`; naval combat strength per vessel; subordinate-influence damping |
| **`westpoint`** | branch bonuses (manual paths only); halved promotion experience cost; artillery appointment preference; feud multiplier baseline |
| **`political`** | state support generation; doubled feud chance; relaxed appointment tier |
| **`veteran`** | **no mechanical consumer** — display, a list filter, and a DLC item requirement only |

---

## Open questions

1. **`changeofexp[7]` is never written.** Seven slots, serialized both ways,
   with a tooltip path in its neighbours — but no producer. Either a removed
   feature or one that was never wired, like `cohesionbyfire` in
   [13](13-morale-and-cohesion.md).
2. **`Commander.veteran` has no *mechanical* consumer.** It is displayed
   (`AvatarManagement`, `CampaignMainPanel`), filtered on (`CommandersList`),
   and read by the DLC's item-requirement system — but nothing in the base
   simulation branches on it. 456 shipped commanders carry it, so either it is
   decoration or a planned effect that was never wired.
3. **`bonusmalusexp` is written but never read back.** It is the parking slot
   for promotion experience overflow; the config comment says it is "reduced
   over time", and no such decay was found.
4. **Can the Statistics panel clobber `campaignranking`?**
   `UpdateCommanderRankings`' `IsCampaign` arm overwrites the packed key with a
   small 1-based ordinal (§6.1), and `Statistics` is the only caller that can
   reach it. If `commandersparticipating` is populated in a campaign scene, the
   commander-in-chief election — which picks the *highest* `campaignranking` —
   would start picking the *worst* man in a rank bucket until the next
   `UpdateCommanderRankingsCampaign` pass restores the key. But
   `commandersparticipating` is built from battle-setup data (`grp`,
   `reg_goreg`), so it is probably null there and the loop simply does nothing.
   **Needs a runtime check to resolve**; it is the one finding in this file
   that static reading cannot settle.
5. **`armygroupcols.dat` / `ArmyGroup` / `CheckArmyGroupPromotion`** — Army
   Groups bypass `GetSuitableCommander` and have their own promotion path. The
   **gate is now fully known, and it is closed in the base game**:
   `Alliance.AllowsArmyGroups()` needs `GameVars.allowarmygroups` **and**
   project 88 (Command Reform) or 89 (Organization Reform) — not 90. The flag
   is a static with an initialiser of `true` that `DLC_WL` ~3457/~3464
   **assigns** `true` with the DLC and `false` without, and both projects are
   unappointable without it, so **Army Groups are a DLC feature**. See
   [28-project-effect-reachability](28-project-effect-reachability.md) §3.1;
   this replaces the earlier reading of `allowarmygroups` as a per-alliance
   pref. The promotion path itself and `minimumexpcommanderarmygroup` were
   still not followed.
6. **`Config/avatar.dat` and `Config/CommanderDummies/`** — the dummy name
   pools and the avatar/portrait system (`AvatarManagement`, 1,792 lines) were
   only skimmed. `AvatarManagement` also contains a commander *editor* writing
   `showncommander.*` directly, which may be a scenario-authoring tool.
7. **Every third line in `PerkBattleTooltips.txt` is `-`**, so the autoresolve
   panel shows nothing for fighting-unit perks. Deliberate, or the same
   omission as `GetPerkInfos` reaching for the wrong table?
8. **`Tools.AddToAverage(current, value, 0.25f, timediff)`** — the 0.25
   constant appears in every tracker call and in `AdjustLastAttributes`. Its
   semantics (a time constant? a weight?) were not derived.
9. ~~**Which `resulttype` values `fametrigger[]` and `defamedtrigger[]` are
   indexed by.**~~ **Resolved** in [21-ai-battle](21-ai-battle.md) §14.2 from
   the `battleprefs.txt` comments: **[minor victory, major victory, minor
   defeat, major defeat]**, with `fametrigger = {0.025, 0.02, 0.03, 0.04}` and
   `defamedtrigger = {−0.0018, −0.0027, −0.0016, −0.0015}`, and
   `famefactorleveladjustment = {brigade 0.005, division 0.0065, corps 0.009,
   army 0.013}`. The matching victory thresholds are vp ratio ≥ 0.7 / ≥ 0.8 /
   ≤ 0.3 / ≤ 0.2. Since the tests are `score > fametrigger` and
   `score < defamedtrigger` (§4.1), both arrays run the obvious way: fame is
   **easiest after a major victory** (0.02) and hardest after a major defeat
   (0.04), and defame is **hardest after a major victory** (−0.0027) and
   easiest after a major defeat (−0.0015). After the echelon factor, the
   famous bar is a kill ratio of about 1.5 : 1 for an army commander in a
   major victory and 4 : 1 for a brigade commander, and a brigade commander
   cannot clear either defeat bar on kill ratio alone (the cap is 5).
   **Corrected:** this entry first read both arrays backwards.
10. **`PerksShownGroup` and `PerksShownFleet` are prefab arrays**, so the
    campaign and fleet perk availability lists could not be read from the
    assembly. They need an asset dump (or a runtime probe) to confirm all 18
    campaign and 11 fleet perks are actually offered.
