# 20 — The campaign AI

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `AICampaign` (8,374 lines), plus
`GameVars.Alliance.AIPersonality`, `BattleUnits`, `Heatmap`, `FilterMap`,
`Frontline2`, `MainMenu`; `Config/campaignprefs.txt`,
`Campaigns/001/A/aipersonalities.dat`
**Summary:** [summary-ai-campaign](summary-ai-campaign.md)

> **Scope.** This file covers `AICampaign` only — the strategic layer. The
> battlefield AI (`AIBattle`, 5,458 lines, and `Config/aistrategies.dat`,
> which is `AIBattle.StrategiesImportFileName`) is a separate subsystem and
> gets its own file. The feud controller, `aibattle[2]`, is already described
> in [19](19-commanders-and-perks.md) §7.

---

## 1. Shape of the thing

`AICampaign` is a `MonoBehaviour` on `GameController` with **one** `Update`,
six nested classes, and a hundred-odd static methods. It holds no per-unit
state on the units themselves; everything lives in three parallel structures:

| Structure | What it is |
| --- | --- |
| `List<AIArea> aiarea` | The strategic zone partition and its value map |
| `List<AIFaction> aifaction` | One per non-player alliance: unit lists, port lists, operation rosters, heatmaps |
| `Heatmap` ×3 per faction | A 20×20 blurred influence grid for recruiting and transfers |

Plus six operation types that each carry their own little state machine:
`DefensiveOperation`, `DefensiveMovingOrder`, `RaidForce`,
`SeaInvasionForce`, `FortConstructionOrder`, and the implicit
"offensive operation" (just membership of
`AIFaction.unitsinoffensiveoperations`).

### 1.1 The strategic map is an image asset

`InitializeAIAreas()` reads `TextureAIAreas` off the `Frontline2` component of
the `Battlemap/Borders` GameObject, wraps it in a `FilterMap`, and scans every
texel:

```
for each texel with alpha > 0:
    colour = aiareas.GetColorOnPos(worldpos)
    if no AIArea has that colour, create one
    area.UpdateEdges(worldpos); area.averageposition += worldpos; area.UpdateStates(worldpos)
```

> **Every distinct colour in that texture is a strategic zone.** The AI's
> entire partition of the continent is authored as a bitmap, not as config or
> scene data. A mod that wants different zones repaints the texture; a mod that
> accidentally changes its colour quantisation changes the AI's whole map.

Lookups go the same way: `AIArea.GetAIArea(aiareas.GetColorOnPos(pos))` — a
texture sample followed by a **linear scan of all zones comparing `Color`
values for exact equality**. That call appears inside per-unit loops in
`UpdateAreaPoints`, `UpdateImportanceValues`, `CheckStanceSetting` and
`CheckOffensiveMovements`, so it is the campaign AI's hot path.

Zones also carry `supplycapacity`, the `states` and `nearbystates` they touch,
and cached lists of the `iips`, `cbuildings` and `towns` inside them (built
once, during `airuns <= 1`).

### 1.2 Two debug switches worth knowing

- `GameVars.debug_turnoffcampaignai` — early-returns out of `Update`.
- `GameVars.debug_showcampaignaimapvalues` — instantiates a
  `CampaignUI/AIZoneText` label over every zone showing
  `ImpPts / TroopPts / DistPts / Own-Supp`. The single best tool for
  understanding this subsystem at runtime.

Also `debug_skipaimovessecondarynations` (only faction 0 is processed),
`debug_permanentaimovement` (bypasses the aggression roll),
`debug_seainvasionsactive`, and `debug_deactivatefeuds`.

---

## 2. The scheduler: a 36-step job machine per faction

`Update()` does almost nothing itself:

```csharp
int num = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(GameVars.gamespeed, 0.5f)));
for (int k = 0; k < num; k++) UpdateUnitAI();
UpdateCompanyFoundationList();
CBuilding.WorkDownConstructionWishes();
```

> **AI throughput scales with the *square root* of game speed.** At 16× the AI
> gets four passes per frame, not sixteen. So **fast-forwarding gives the AI
> proportionally less thinking time per game-day** — the single most
> consequential performance fact about this subsystem, and a plausible reason
> a fast-forwarded campaign feels passive.

`UpdateUnitAI()` advances `currentfactionupdatejob` for `currentfaction`,
normally executing exactly one job per call:

| Job | Work |
| --- | --- |
| 0 | `SeparateUnits` + `UpdateHeatmaps` |
| 1 | `UpdateAreaPoints` |
| 2 | (pass-through to 3) |
| 3 | `UpdatePorts` |
| 4 | per-zone `CalculateMostValueableAIZones` + `UpdateSupplyCapacity`, one zone per call |
| 5 | `RelieveUnitsInOperations` |
| 6 | `UpdateMicroMovementInOffensive` |
| 7 | `CheckFleetMovements` |
| 8 | `CalculateZoneRecruitingProbs` |
| 9 | `ZoneRecruiting` |
| 10–12 | `UpdatePortBlockRatios`, `RemoveConstructedVessels`, `CheckShipConstruction` |
| 13–14 | `CheckStanceSetting`, `CheckPerkSelection` |
| 15–16 | `CheckForDefensiveOperations`, `CheckForEndOfDefensiveOperations` |
| **17** | **skipped** — 16 jumps straight to 18 |
| 18–20 | `AssignUnitToDefendCapital`, `CheckSupplyDepotConstruction`, `CheckSiegeAssaults` |
| 21 | `CheckFortConstruction` — **only advances if it returns `true`** |
| 22–24 | `CheckForSeaInvasion`, `LinkToFightingUnits`, `CheckAICommanderReplacements` |
| 25–28 | `UpdateCampaignTheaters`, `CheckCombinationOfUnits`, `CheckTransferOfUnits`, `CheckAssignmentOfPoolShips` |
| 29–32 | `CheckOccupationOfLostCities`, `CheckCreationOfRaidForces`, `CheckCombinationOfBrigades`, `CheckArmyGroupManagement` |
| 33–35 | `PickCampaignObjective`, `CheckFinalPositions`, `RollUpEnemyObjectivesInZone` |

Then `currentfactionupdatejob = 0` and `currentfaction++`; when the faction
index wraps, `airuns++` and `AICampaign.weatherrandomoverride` is re-rolled to
`Random.Range(0.8f, 4f)`.

> That private static multiplies **both** weather thresholds in the only two
> places the AI consults the weather —
> `weather.CheckWeatherLine(from, to, minimumsnowlevelforaimovements × w,
> minimumrainlevelforaimovements × w)` in
> `IsUnitAvailableForOffensiveOperations` and in the defensive responder check.
> So **the AI's tolerance for marching through snow and rain is re-rolled once
> per sweep and varies between 0.8× and 4× the configured limit** — a
> deliberate randomisation of its weather caution, and the reason two
> identical-looking situations can produce different AI behaviour a minute
> apart.

Notes on the control flow, because it is not uniform:

- Jobs 8 and 9 run in the **same** call (job 8's block is a plain `if`, not an
  `else if`); every other job runs alone.
- **Job 21 can stall the pipeline** — `CheckFortConstruction` returning `false`
  leaves the job index unchanged, so nothing after it runs that call.
- **Everything except jobs 0–3 and 14 is gated on `!GameVars.tutorialactive`.**
  The tutorial therefore has unit-list bookkeeping, area points, ports and perk
  selection, and no strategy at all.
- With eight alliances and the player taking one, there are normally **seven AI
  factions**, so a full sweep is ~7 × 36 ≈ **250 `UpdateUnitAI` calls**.

Offensive movement is scheduled separately, outside the job machine: one unit
per call, round-robin over `aifaction[currentfactionoffensiverun].ownunits`,
and the faction index only advances after **two** complete job cycles
(`currentfactionupdatecycles == 2`).

Three other staggered round-robins share the same idiom: `UpdateImportanceValues`
walks one IIP, one `CBuilding` and one `Town` per call; job 4 walks one zone per
call; `UpdateCompanyFoundationList` runs every frame.

---

## 3. What the AI knows

`SeparateUnits(allunits)` is fed `BattleUnits.completeunitlist` — **every unit
in the game** — and filters on exactly two things: `activeInHierarchy`, and
`GetUnitVisibilityStateOnCampaignMap(...) != 0`.

That second test is easy to misread. It is **not** fog of war; it is an
echelon/rendering classification (see
[16-enumerations](16-enumerations.md)):

| Value | Meaning |
| --- | --- |
| 0 | not drawn on the campaign map — a subordinate below the top campaign echelon |
| 1 | a movable formation (Corps, a top-level Division, `overridesymbol == 15`) |
| 2 | a garrison division, or an Army whose subordinates all sit under Corps |
| 3 | a fleet |

> **The campaign AI has full information.** It enumerates every top-level
> formation on the map regardless of concealment, and sorts them into
> `ownunits` / `enemyunits` / `neutralunits` / fleets / garrisons by alliance
> and `IsAtWarWith`.

And it goes further. Every position the AI evaluates — for threat points, for
stance, for defensive interception — is read as

```csharp
unit.GetLastTransmittedPathPos(ignoreorderdelay: true)
```

which returns the **final corner of the unit's last ordered path**, with the
order-transmission delay explicitly bypassed.

> So the AI plans against **where enemy formations are going**, not where they
> are, and it sees those destinations **before the orders have been
> transmitted** — a delay that does apply to the player. This is structural,
> not a tuning multiplier, and it is the AI's largest single advantage.

`flag` in `SeparateUnits` (the "at war" result) is assigned inside the loop and
read in the same iteration, but it is declared outside and never reset, so a
unit of an alliance the faction is *not* at war with inherits the previous
iteration's value when `flag2` is true. In practice `flag` is only read in the
`!flag2` branches, where it has just been assigned — so it is a latent hazard
rather than a live bug.

### 3.1 Fog of war is asymmetric in the AI's favour

The detection loop in `Regiment` that fills `unitrange` — the structure the
defensive AI reads — guards its enemies-in-range list like this:

```csharp
if (!unitrange.temp_enemyinrange.Contains(regiment.gameObject)
    && (!regiment.iswithinfog | (regiment.alliance == GameVars.playeralliance) | !GameVars.usefow)
    && num4 <= num2)
```

Read it from each side. When a **player** unit is scanning, the candidate is an
AI unit, and `iswithinfog` hides it. When an **AI** unit is scanning, the
candidate is a player unit, and the
`regiment.alliance == GameVars.playeralliance` clause fires, so it is added
**whether or not it is in fog**.

> **Concealment works against the player and not against the AI.** This is one
> clause, in the shared detection routine, and it applies in battle as well as
> on the campaign map.

`closestenemyunitfartemp` — the nearest-enemy reference that
`CheckForDefensiveOperations`, `CheckStanceSetting` and the raid abort check all
consult — is assigned **outside that guard entirely**, so it ignores fog for
both sides.

---

## 4. The value map

### 4.1 `importancevalues` — economic worth per zone

`UpdateImportanceValues()` accumulates into `importancevaluestemp[faction]` and
commits into `importancevalues[faction]` when all three object lists have been
walked — the same temp/commit double-buffer as the economy's `_tmp` fields.

```
warWeight   = IsAtWarWith(owner) ? 1 : nonwarparticipantsobjectimportanceweighting
importance += IIP.GetTransportCapacity()         × aiimportancetransportcapacityweight × warWeight
importance += CBuilding.volumesold.GetTtmValue() × aiimportancecompanyprofits          × warWeight
importance += Town.representingpopulation        × aiimportancepopulation              × warWeight
importance += aitownweightfactorobjective        (if part of the followed campaign objective)
importance += aiimportancevaluescapital          (capitals of alliance 0 and 1 only)
```

Shipped weights:

| Pref | Value |
| --- | --- |
| `aiimportancetransportcapacityweight` | **1.0** |
| `aiimportancepopulation` | 0.0001 |
| `aiimportancecompanyprofits` | 0.00001 |
| `aiimportancevaluescapital` | 750 (absolute) |
| `nonwarparticipantsobjectimportanceweighting` | 0.35 |

> **The AI's strategic map is purely economic** — transport capacity, corporate
> turnover, population, plus a flat capital bonus and an objective bonus. No
> terrain, no defensibility, no rail connectivity as such, no history.
>
> And the weights are wildly different in scale. A 100,000-person town scores
> 10; the capital scores 750; an IIP scores its raw transport capacity at
> weight 1.0. **Transport capacity dominates** unless capacities are very small
> — worth measuring against real `GetTransportCapacity()` values
> ([18-economy](18-economy.md)) before tuning anything here.

The capital bonus loop is `for (int l = 0; l < 2; l++)`, so only the Union's and
Confederacy's capitals count. Harmless — the foreign powers' capitals are off
the map.

### 4.2 `points` and `distancepoints` — the military picture

`UpdateAreaPoints` fills two more per-zone arrays. `GetUnitPoints` scores a unit
as

```
points = weight × Σ(groupstrengthperunittypcampaigndirect) × groupmorale
       + weight × fortWeaponStrength × weightingfactorfortweaponssiegecombats   (garrisons)
```

with `weight` taken from the unit's bucket:

| Pref | Value |
| --- | --- |
| `aiimportanceownunits` | **−0.01** |
| `aiimportanceenemyunits` | **+0.01** |
| `aiimportanceneutralunits` | **+0.0005** |

> `points[faction]` is therefore **signed**: negative means the zone is
> own-dominated, positive means enemy-dominated. `ownpoints[faction]` is the
> absolute own contribution only. And **a neutral soldier counts as 5% of an
> enemy soldier** — the pre-war posture, before the Border States choose sides.

`distancepoints[faction]` is a decayed threat field over all enemy units:

```
distancepoints = Σ  ( 100 / max(1, d)^0.25 ) × sqrt(groupstrengthactive) / 100
```

where `d` is the distance from the enemy unit to the nearest objective in the
zone (falling back to the nearest zone edge).

> The `^0.25` falloff is **very shallow** — a tenfold increase in distance only
> halves the contribution. Distant armies still register, which is what keeps
> the AI from tunnel-visioning on its immediate front. `sqrt` on strength is
> the same diminishing-returns idiom as `Autocalc`
> ([11-combat-resolution](11-combat-resolution.md)).

### 4.2.1 `CalculateMostValueableAIZones` — the two target slots

Run per zone per faction (job 4, one zone per call), it fills two fields whose
names are both slightly misleading — and the second one's spelling, `…aiarea…`
versus `…albeaiarea…`, is a typo in the game.

```
score         = importancevalues × noEnemyFactor + points + distancepoints
noEnemyFactor = (enemyStrengthClose < ownStrength) ? zoneimportancefactornoenemy : 1
supplyOK      = (ownpoints <= supplycapacity)
searchRadius  = aimaximumdistancetosearchforunitrelocations × rangefactoroffensivetargets
              = 275 × 0.4 = 110
```

**`mostvalueableaiareaclose[faction]`** — the highest-scoring zone within 110,
subject to two further constraints:

- `supplyOK`, i.e. **the zone can already supply the force present in it**. A
  genuine logistics check inside target selection, which is more than most
  strategy AIs do.
- not already securely held:
  `points >= 0 || distancepoints > ours || importancevalues > ours`.

> `zoneimportancefactornoenemy = 0.10`, so **a zone the AI already dominates is
> worth a tenth of its economic value as a target.** That is the mechanism that
> pushes the AI toward contested ground instead of re-garrisoning secure rear
> areas.

**`mostvaluealbeaiareaclosedistanceonly[faction]`** is **not** "the nearest
zone". It is the **friendly, enemy-free zone with the highest
`distancepoints`** — that is, the uncontested zone under the greatest *distant*
threat — searched within 110 and, if nothing qualifies, re-searched at
`275 × 2 = 550`.

> So the two slots are an **attack** target and a **reserve/redeployment**
> target, and the `num2 == 0` branch in `CheckOffensiveMovements` (§5) is not
> "advance toward the nearest value" but "shift toward the friendly zone most
> likely to be hit next".

### 4.3 Heatmaps — a 20×20 influence grid

Only alliances 0 and 1 get them (`InitializeHeatmaps` early-returns above 1):

| Heatmap | Contents |
| --- | --- |
| `recruitmentheatmapown` | own formations, weight = `groupstrengthdirect`, radius 4, blurred; capital-defence groups scaled by `reducedrecruitmentcapital` |
| `recruitmentheatmapenemy` | enemy formations, radius 4; weight multiplied by `aienemyweightfactorobjectiverecruiting` near an objective town; in chapter 0 of campaign "001", neutral Union/CSA units are added at radius 3 |
| `heatmapowntowns` | a plain presence mask of owned towns — this is what gates *where* recruiting is allowed |

So the AI keeps **three different spatial representations at three
resolutions**: the texture-derived zones for strategy, a 20×20 blurred grid for
force distribution, and raw world positions for movement.

---

## 5. Offensive operations

`CheckOffensiveMovements(faction, unit, timediff)` is the largest decision
function in the file (~260 lines) and runs for one unit per `UpdateUnitAI`
call. It early-returns if the unit is already in an offensive or defensive
operation or building a supply depot.

**Step 1 — pick a target zone.** From the unit's own zone take
`mostvalueableaiareaclose[faction]`, *unless* the current zone has no nearby
enemy strength at all, in which case take
`mostvaluealbeaiareaclosedistanceonly[faction]`.

> Per §4.2.1 those two slots are an **attack** target and a
> **reserve/redeployment** target. So with enemies nearby the AI moves toward
> contested value; with none, it shifts toward the friendly uncontested zone
> under the greatest distant threat. The second branch is a redeployment, not
> an advance — which is also why it moves only one formation (step 7).

**Step 2 — estimate the required force.**

```
threat  = target.enemycampaignunitsstrengthclose × target.avgtroopmoraleinzone
        × aiimportanceenemyunits × target.enemycampaignunitsstrengthcloseweapon
threat /= GetNationalMoraleFactor(faction)        // own nationalmorale ÷ enemy's
```

> **Probable slip.** An earlier line in the same method computes the same
> product using `avgenemytroopmoraleinzone` and is immediately overwritten; the
> surviving line uses `avgtroopmoraleinzone[_aifaction]`, i.e. **the AI's own
> average troop morale in the target zone**, to scale its estimate of the
> enemy's strength there. The structurally parallel code in
> `CheckStanceSetting` pairs the arrays correctly (own morale over enemy
> morale), which is what makes this look like a copy-paste error rather than
> intent.

Dividing by `GetNationalMoraleFactor` means **high national morale makes the
AI perceive less threat**, hence attack more readily. `GetNationalMoraleFactor`
itself uses `num = (allianceid == 0) ? 1 : 0`, so every alliance except the
Union compares itself against the **Union** — fine for the Confederacy, a quirk
for foreign powers.

**Step 3 — assemble a force.** Collect units in the same group that pass
`IsUnitAvailableForOffensiveOperations`, sorted by distance, accumulating
`num -= GetUnitPoints(...) × weaponFactor` (own points being negative, so `num`
grows). Then widen the search to any available unit within
`aimaximumdistancetosearchforunitrelocations` of either the lead unit or the
target, until

```
num >= threat × aidominanceforoffensiveoperations × initiativeFactor
initiativeFactor = 1 + (1 − initiative(force C-in-C)) × commanderinfluenceinitiativeforoperations
weaponFactor     = (1 − influenceweaponsonoffensives) + influenceweaponsonoffensives × GetWeaponStrengthFactor(unit)
```

With `aidominanceforoffensiveoperations = 0.8` and
`commanderinfluenceinitiativeforoperations = 0.25`:

> **The required force ratio runs from 0.8 (a bold commander) to 1.0 (a
> timid one).** The AI will attack at a *numerical disadvantage* if its general
> has high initiative. That is a deliberate and quite aggressive setting.

**Step 4 — trim.** A second loop removes the unit **closest to the target**
while the force still exceeds the threshold, so surplus formations are released.
The weapon factor in the removal branch is written differently from the addition
branch —
`w × (influence × w + (1 − influence))` versus `(1 − influence) + influence × w`
— so **adding and removing a unit do not cancel**; the accumulator drifts. A
real accounting inconsistency, small in magnitude.

**Step 5 — roll for aggression.** Only if the nearest objective in the target
zone sits in a state belonging to **another alliance**:

```
threshold = aiagressiveness[Policy.CurrentChapter] × timediff × 10
          × aiagressivenessoffensiveoperationfaction[allianceid]
          × usedcampaignagressiveness
          × winterModifier
threshold += clamp(t³, 0, 1)        // t = years since the force last moved
attack if Random(0,1) <= threshold
```

| Pref | Value |
| --- | --- |
| `aiagressiveness[0..4]` | **0.00**, 0.75, 1.50, 2.00, 2.50 |
| `winterdefensiveoperationsmodifier` | applied when `month <= winterquartersmonthuntil` or `>= winterquartersmonthfrom` |
| `aiagressivenessoffensiveoperationfaction[0..1]` | separate Union and Confederate constants |

> **`aiagressiveness[0] = 0` means the AI launches no offensive operations at
> all in chapter 0.** The "phoney war" is enforced by data, not code.
>
> And the `t³` term is the anti-stall mechanism: **the longer a force sits
> still, the more likely it attacks, cubically**, reaching certainty after
> about a year of immobility. It is the reason an AI army eventually moves even
> when the arithmetic says it should not.

**Step 6 — a final composite gate.**

```
if (threat != 0 && target.points − own.points
                 + (target.distancepoints − own.distancepoints)
                 + forcePoints  <  10f)     return;      // hardcoded 10
```

with a four-term `||` guard in the no-enemy case that declines the move if the
AI's own zone is more threatened, or already more valuable, than the target.

**Step 7 — move.** `num11 = list.Count`, **except that an unopposed advance
moves only the lead unit** (`if (threat <= 0 && num11 > 1) num11 = 1;`). So
contested moves commit the whole assembled force; uncontested ones send one
formation. Each mover is added to `unitsinoffensiveoperations` and the method
sets `recruitingzonesupdated = false`, forcing a recruiting recalculation.

### 5.1 A hardcoded historical script

```csharp
if (GamePrefs.leveltoload == "001" && !CampaignObjective.GetObjectiveAccomplishment(6)
    && aifaction[_aifaction].allianceid == 0
    && GameVars.GetCampaignFloatingDate() > 1861.513f)   flag = true;
```

`flag` bypasses the aggression roll entirely. Date 1861.513 is early July 1861:
**the Union is forced to advance on Manassas** until objective 6 is
accomplished. Scenario id and date both literal.

---

## 6. Defensive operations

`CheckForDefensiveOperations(faction)` runs on an elapsed-time delta
(`lastdefensiveoperationcheck`) rather than per frame.

**Threat priority** is the nice part:

```
priority = min( distance(enemyUnit, its nearest enemy),
                distance(enemyUnit, our capital) / 3 )
```
sorted ascending.

> **Dividing the capital distance by three** makes an enemy formation heading
> for the capital look three times closer than it is. One line, and it gives
> the AI a capital-defence reflex without a dedicated subsystem (there is also
> a dedicated `AssignUnitToDefendCapital`, job 18).

The AI only reacts to an enemy whose **destination** (again
`GetLastTransmittedPathPos(ignoreorderdelay: true)`) lies in a state it owns —
so interception is pre-emptive by construction.

Responders must clear a long conjunction: not already in a defensive operation,
not retreating, not in battle, morale above
`minimummoralelevelcanceldefensivemovments + 0.15`, `groupstrengthactive >
grouplosses`, not building a depot or a fort, not in a sea-invasion force, a
fighting force, and within its operations theater. Range is
`maxdistancedefensiveoperations` (**275**) if the responder has enemies in
range, else `maxdistancedefensiveopnoenemy` (**650**).

Probability is per side: `probdefensiveop[1]` for the Confederacy,
`probdefensiveop[0]` for everyone else, times the same winter modifier.

`DefensiveOperation` accumulates `initialstrength` as
`Σ groupstrengthactive × groupmorale ^ expmoraleweightfordefensiveoperations`
and cancels itself when current strength falls below
`initialstrength × canceldefensiveoperationsratio`, or average morale below
`minimummoralelevelcanceldefensivemovments` (0.40), or it runs out of units.

---

## 7. Recruiting and force structure

`ZoneRecruiting(faction)` is gated on **`allianceid < 2`** (only the Union and
Confederacy recruit), `IsRatingOkForRecruitment()`, a populated heatmap, and —
in campaign "001" — `Policy.CurrentChapter > 0`.

It scans the 20×20 grid over cells where `heatmapowntowns > 0` and finds two
extremes of `own / max(1, enemy)`:

- the **best** ratio → `positionsurplus`
- the **worst** ratio, requiring `enemy > 2500` → `positiondeficit`

with the ratio clamped into roughly `[0.09, 0.11]` and `[9, 11]` **using random
jitter** rather than hard clamps, to break ties between saturated cells. Those
two cells drive both where new units are raised and `CheckTransferOfUnits`.

The recruiting target is

```
target = targetrecruitingratio + usedcampaignagressiveness × targetrecruitingmultiplier
       = 1.25 + difficulty × 0.17
```

measured against `GetStrengthRatio(alliance)`:

```
ratio = (own strength − recruitscontractexpiries + troopsinrecruitment)
      / (enemy strength + every hostile faction's troopsinrecruitment)
```

> `GetStrengthRatio` looks at the **recruitment pipeline on both sides**, not
> just units on the map — so the AI reacts to the player's recruiting before
> those men reach the field. In chapter 0 **neutral Union/CSA units count as
> enemy strength**.

Force-structure limits, all from `campaignprefs.txt`:

| Pref | Value |
| --- | --- |
| new-army composition (inf / cav / art) | 0.65 / 0.10 / 0.25 |
| max corps per army, pre-reform → post | **3 → 10** (the reform is **project 90**, see [22](22-policy-and-project-catalogue.md) §12.4) |
| max divisions per corps | 3 |
| max brigades per division | 4 |
| `minimumgroupstrengthformovement` | 5,000 men |
| `aiminimummoraleformovement` | 0.65 |
| cancel offensive operations below | 0.55 |
| minimum morale for defensive movements | 0.60 |
| cancel defensive operations below | 0.40 |
| `minimumsupplyoffensivetargets` | 0.75 |

---

## 8. AI personalities

`GameVars.Alliance.AIPersonality` is a **named strategic doctrine** loaded from
a per-campaign `aipersonalities.dat`:

| Field | Shape |
| --- | --- |
| `id`, `name`, `description` | |
| `taxfocus[5]` | **only 4 values are read** from the file |
| `subsidyfocus[8]` | one cap per subsidy type |
| `prewarpolicies` | 20 fixed slots, `-1` = empty |
| `policies` | 80 fixed slots |
| `projects` + `projectsprob` | 100 fixed `(id, weight)` pairs |
| `projectspendingratio` | ÷ 100 on load |

> This file uses **fixed-width padding with `-1` sentinels**, not the
> count-prefixed convention documented in
> [06-config-file-format](06-config-file-format.md). 317 lines per record, and
> the shipped `Campaigns/001/A/aipersonalities.dat` is
> `1 + 20 × 317 = 6,341` lines — verified exactly. **A mod cannot give a
> personality a 21st pre-war policy or a 101st project**; the slots must be
> filled in place.

The shipped roster:

| id | Union (alliance 0) | Confederacy (alliance 1) |
| --- | --- | --- |
| 0 | Historical | Historical |
| 1 | Industrial | Industrial |
| 2 | Expansionist | Expansionist |
| 3 | Diplomatic | Diplomatic |
| 4 | Militaristic | Militaristic |
| 5 | **Agricultural** | **King Cotton** |
| 6 | Economic | Economic |
| 7 | Abolitionist | Abolitionist |
| 100 | Recruit Emergency | Recruit Emergency |
| 101 | Credit Emergency | Credit Emergency |

`SetRandomAIPersonality()` picks uniformly among personalities with
**`id < 100`**, so the 100-block is reserved. And the main-menu
`usehistoricaipersonality` checkbox decides whether the pre-war policy set
comes from the scenario's historical list or from the rolled doctrine's
`prewarpolicies` (`Policies.cs` ~line 226).

### 8.1 The personality is re-evaluated on every call

`GetAIPersonality(alliance)` is not a getter over stored state — it is a
decision:

```
if (!IsRatingOkForRecruitment(useemergencylevel: true) && frame > 50)
        return personality[emergencyaipolicycredit];      // id 101

statesWithRecruits = count of states (alliance <= 1) with HasEnoughRecruits(...)
if (statesWithRecruits <= emergencyairecruitingtrigger && frame > 50
    && Policy.CurrentChapter >= 1 && GetStrengthRatio(alliance) < 0.8f)
        return personality[emergencyaipolicyrecruiting];  // id 100

return personality[usedaipersonality];
```

> **Under fiscal or manpower stress the AI swaps its entire agenda** — tax
> focus, subsidy caps, policy list and project lottery — for a crisis doctrine,
> and swaps back when the pressure lifts. The newspaper even says so ("The
> government is in a crisis, as the nation's recruitment base has
> deteriorated"). Credit distress takes precedence over manpower distress.

Because it is recomputed per call, a mod can read the AI's current doctrine
without tracking any state.

### 8.2 The AI's economic brain is the player's autopilot

Three of the economic routines are shared with the player's auto-manage
toggles:

| Method | Runs for the player when |
| --- | --- |
| `UpdateFinancialAI` | `GameVars.automanagefinances` |
| `UpdateProjects` | `GameVars.automanageprojects` |
| `UpdateRailroadConstruction` | never (AI only) |

> **Ticking "auto-manage finances" hands the player's treasury to the AI
> personality**, including its `taxfocus` and `subsidyfocus` preferences. Good
> to know, and a clean seam: a mod that wants to improve the economic AI
> improves the player's autopilot at the same time.

Two parts of `UpdateFinancialAI` run for *every* alliance 0–1 **regardless of
the toggle**:

```
treasury < 0 and rating not already bottom  → issue bonds   (+issuerepaybondsum to both treasury and debt)
treasury >= balancetriggerforredemption and debt > 0 → repay bonds
```

So the player's debt is serviced automatically, with only a message from the
Secretary of the Treasury. That closes the loop with the
debt → rating → interest spiral in [18-economy](18-economy.md): the AI (and
the player) will keep borrowing until the rating hits the floor.

Tax adjustment picks a bracket by walking `taxfocus` and keeping the **last**
index that passes `Random(0,1) < taxfocus[i]` — so it is biased toward
higher-indexed brackets rather than being a proper weighted draw. Subsidies
pick a **uniformly random** slot and clamp to `subsidyfocus[slot]`.

`UseSubsidyForPurpose(alliance, subsidytype)` returns 0 = fund projects,
1 = fund construction: build instead of research when a good IIP site exists
for that subsidy type **and** the project share of cumulative spending has
reached `projectspendingratio`.

---

## 9. Difficulty: two sliders, and ten hardcoded cheats

### 9.1 The sliders

Both are set once, in `MainMenu`, and then saved with the campaign:

```csharp
GameVars.usedcampaignagressiveness = aimode;
GameVars.usedcampaignbonus         = bonus;
GameVars.usedcampaignbonusrunning  = bonus;
GameVars.casualtiesmodifier        = bonus * GamePrefs.casualtiesmodifierbonus;
```

| Slider | Range (from `campaignprefs.txt`) | What it drives |
| --- | --- | --- |
| `usedcampaignagressiveness` | **0.5 – 1.75** | offensive-operation probability, recruiting target, and it **divides** the economy's debt burden ([18](18-economy.md)) |
| `usedcampaignbonus` / `…running` | **0 – 0.5** | the handicap multiplier below |

> **`casualtiesmodifier` is the bonus slider × `casualtiesmodifierbonus`** —
> which answers the provenance question left open in
> [11-combat-resolution](11-combat-resolution.md).

`usedcampaignbonus` is a **one-shot that never takes effect in a campaign.**
It is applied in `BattleUnits.import_battledata`'s `!IsCampaign` branch — the
battle scene — where it adds itself to the AI's `nationalmorale`, multiplies
`volunteersadj`, and zeroes itself. In a campaign battle the statement
immediately after `import_battledata` is
`ImportExportUnitData.OverwriteNationsData(…)`, which re-reads all three
values from the save, so the bonus is applied and discarded at every battle
for the whole campaign. The write is also indexed by **battle seat** rather
than alliance id, and the volunteers line is scaled for a 0–50 slider against
a shipped maximum of 0.5. Full account in
[17](17-politics-and-support.md) §3a.

> **Corrects an earlier reading of this file**, which had the one-shot applying
> at scenario load. The practical consequence is that the difficulty slider's
> only lasting effects are `usedcampaignbonusrunning` and
> `casualtiesmodifier`.

`usedcampaignbonusrunning` is the persistent one and feeds subsidies
(`influenceaibonusonsubsidies`), national-morale loss resistance, finances
(`influenceaibonusonfinances`), weapons production
(`influenceaibonusonweaponsproduction`), military-experience gain from battles
(`aibonus_gainmilexp`), state-support loss reduction (`aibonus_supporthit`),
and base morale recovery (`aibonus_restoringmorale`).

### 9.2 The cheats are in the assembly, not config

Ten constants on `GameVars`, with literal defaults, every one of them
**disabled when `GameVars.ai_vs_ai`**:

| Constant | Default | Effect |
| --- | --- | --- |
| `debug_aicheat_noorderdelayinfrontofenemy` | `true` | **AI units in contact get zero order delay** in battle (`combatstatussubordinates > 0`, `!IsCampaign`) |
| `debug_aicheat_nomorallosswhenregrouping` | `true` | zeroes battle morale slot 2 for a moving AI unit |
| `debug_aicheat_slowermoralelossfallback` | `0.7` | retreating-under-fire morale loss ×0.7 |
| `debug_aicheat_fallbackmoralemultiplier` | `0.5` | the **whole** of morale slot 10 ×0.5, applied *after* the 0.7 — so a retreating AI unit takes **0.35×** the player's morale loss |
| `debug_aicheat_fasterfallbackspeed` | `1.75` | `movementmode == 2` fallback speed |
| `debug_aicheat_fatiguefactor` | `0.55` | **march/terrain** fatigue accrual ×0.55 (it scales `num5`, which multiplies the `terrainfatigue` term — **not** the melee term) |
| `aicheat_shiftedbonusmorale` | `1.65` | base morale increase, via `aibonus_restoringmorale` |
| `aicheat_shiftedbonusmoralelosstrigger` | `1.45` | unbearable-losses trigger, via `aibonus_restoringmorale` |
| `debug_aicheat_weaponpurchases` | `2.0` | **dead** — `_ = GameVars.debug_aicheat_weaponpurchases;` |
| `aicheat_lowerhighmomentarycasualties` | `0.8` | **dead** — `_ = GameVars.aicheat_lowerhighmomentarycasualties;` |

> **Two of the ten are discarded reads** and have no effect at all: the AI's
> weapon-purchase multiplier and the player's momentary-casualty relief.
>
> And one has an **inverted guard**. The two shifted-morale cheats are
> structurally identical code five thousand lines apart, but one reads
> `if (GameVars.ai_vs_ai) num = 0f;` (disabled in AI-vs-AI, as all the others
> are) and the other reads `if (!GameVars.ai_vs_ai) num = 0f;` — so
> `aicheat_shiftedbonusmoralelosstrigger` applies **only** in AI-vs-AI games
> and never against a human. Given the uniformity of every sibling, this looks
> like a sign error rather than intent.

---

## 10. Foreign intervention is a hardcoded script

`UpdateInterventions(alliance)` early-returns unless
`leveltoload == "001" || "002"` and `alliance` is 2, 3 or 4.

Entry requires **two** conditions, not one:

```
GameVars.Alliance.GetInterventionProbability(alliance) >= 1
AND CampaignObjective.winmajorbattleintervention == <the side being joined>
AND !already at war  AND  timesincewar <= 0
```

> That refines [17-politics-and-support](17-politics-and-support.md), which
> described intervention as a policy game. The policy-driven probability is
> **necessary but not sufficient** — a major battle victory must also have been
> registered for the side being joined.

Sides are hardcoded: **4 (Spain) joins the Union** against the Confederacy;
**2 (Great Britain) and 3 (France) join the Confederacy** against the Union.
Each fires a `LaunchHistoricalEventByID` (6, 5 and 4 respectively).

The expeditionary forces are **pre-placed, inactive GameObjects found by exact
English display name**:

```
"French Expeditionary Corps", "Spanish Expeditionary Force", "Army of Canada",
"British Expeditionary Corps", "French Intervention Fleet",
"Spanish Intervention Fleet", "British Intervention Fleet",
"North American Squadron", "Western Coast Fleet"
```

activated on timers `timeofarrivallateexpeditionary` and
`timeofarrivaleuropeanfleets`, with a different four-entry array for scenario
"002".

> **This is the worst data coupling found anywhere in the assembly** — worse
> than the trade-node names in [18-economy](18-economy.md) and the state-name
> strings in [17](17-politics-and-support.md). Nine units by display name, two
> scenario folder names, three alliance indices and their sides, and two event
> ids. A mod cannot add a foreign power, add intervention to a new scenario, or
> rename any of those units.

Withdrawal: when `CheckPullback` returns 1 for **every** one of a power's
units, it makes peace, the opposing alliance's intervention level toward it is
adjusted by **−100** (the "impossible" level from
[17](17-politics-and-support.md)), the units are deleted and a newspaper fires.
The unit set and the count differ per power — scenario `001` checks **France 3**
(`EuropeanUnits[0]`, `[4]`, `[8]` → event **7**), **Britain 4**
(`[2]`, `[7]`, `[3]`, `[6]` → event **8**) and **Spain 2**
(`[1]`, `[5]` → event **9**), with shorter two-unit variants for scenario
`002`. Spain's branch is `leveltoload == "001"` only, so **if Spain intervenes
in scenario `002` it can never withdraw.** `CheckPullback` has a copy-paste slip — the
army branch divides by `EuropeanUnits[2].grouplosses` regardless of which unit
is being tested, so French and Spanish pullback thresholds are computed against
the Army of Canada's losses.

---

## 11. Raids, sea invasions, forts

### 11.1 `RaidForce` — state machine

Created as a **Corps** (`unittyp = 15`, `overridesymbol = 15`) by
`CreateUnitDefault(..., israidgroup: true)`.

| State | Meaning |
| --- | --- |
| 0 | idle / finished |
| 1 | raising — reverts to 1 if strength drops below `minimumgroupstrengthforraids` |
| 2 | ready; `SearchObjective()` picks a `CBuilding` within `raidgrouptargetdistance` |
| 3 | moving to the objective |
| 4 | on the objective — **burns a supply depot immediately** |
| 5 | aborting |
| 6 | returning to a friendly depot or town, then → 0 |

Abort triggers: objective `Condition < waitforconditionofraid`; group morale
below `aiminimummoraleformovement`; **group supply below a hardcoded `0.25f`**;
or an enemy formation whose *destination* is within `buglerange × 1.1` while it
is physically within `commanderrange`.

> The state-4 burn reads
> `currentobjective.Owner == raidgroup.alliance` — it destroys a depot owned by
> the **raider's own** alliance. Plausible if ownership transfers on arrival
> (capture then scorch), but counter-intuitive enough to be worth a runtime
> check before relying on it. `CheckBurningDownSupplyDepot(cbuilding, takenby)`
> is a separate, more general path.

### 11.2 `SeaInvasionForce`

States 1 raising → 2 moving to the embarkation spot → 3 embarked/escorted →
onward. Landing sites come from `SeaInvasionForce.GetSeaInvasionSpots()`, called
once from `InitializeAI`. Gated behind `GameVars.debug_seainvasionsactive` and
`minimumgroupstrengthforseainvasions`, and requires an `assignedescort` fleet
passing `FleetIsReadyForAction`.

### 11.3 `FortConstructionOrder` and `DefensiveMovingOrder`

`FortConstructionOrder` is a thin `(unit, location, fort, constructionstarted)`
record; its driver is job 21, the one job that can stall the pipeline.
`DefensiveMovingOrder` is the AI's generic "go here, then stop" primitive,
carrying `manualdistancetoenemy`, `usestrengthcomparison`, `alwaysblockforts`
and `obstaclesprepared`, and is what raids and interceptions actually issue.

---

## 12. Two determinism problems

### 12.1 Reseeding the global RNG from the wall clock

```csharp
UnityEngine.Random.InitState(DateTime.Now.Millisecond);
```

appears **three times inside `AICampaign`** — in the raid-force raising path
(~line 205), in `SeaInvasionForce.AddUnitToInvasionForce` (~568), and in
`ZoneRecruiting` (~3494) — plus once each in `LoadingScreen` and `SoundEngine`.
`GameVars.SetRandomAIPersonality` and `BattlefieldMarker` reseed from
`Time.frameCount`.

> **The campaign is not reproducible.** Unity's `Random` is a single global
> stream, so reseeding it from the system clock inside the AI tick makes every
> subsequent draw in the whole game — casualties, morale rolls, sickness,
> feuds — depend on real time. Two loads of the same save will diverge.
> Anything built on the hook library that wants deterministic replay or
> regression testing has to neutralise these five call sites first.
>
> (`Regiment`'s `InitState(i)` calls inside sprite loops are a different,
> legitimate use: deterministic per-sprite cosmetic variation.)

### 12.2 A repo-wide off-by-one in random selection

`UnityEngine.Random.Range(int, int)` is **max-exclusive**. The codebase
contains **39** instances of

```csharp
list[UnityEngine.Random.Range(0, list.Count - 1)]
```

against **36** of the correct `Random.Range(0, list.Count)` — the two forms are
mixed, sometimes within a few lines of each other.

> **The last candidate in the list is never chosen.** In `AICampaign` this hits
> `GrabShipType` (ship type selection), `GrabFleet`, `GrabExistingGroup`,
> `RaiseNewCampaignGroup`, raid-force placement and
> `AIPersonality.GetNextProjectRandom` (the weighted project lottery). Impact
> depends on list order, so it is usually a mild bias rather than a visible
> fault — but for a project with weight 1 that happens to land last, it is an
> unreachable option.
>
> It is also the same slip already noted for dummy-commander name selection in
> [19](19-commanders-and-perks.md) §5.4.

---

## 13. Saved state

`AICampaign.Save/Load` use `<folder>/AICampaign.dat`, and `Load` **bails out
entirely for save versions ≤ 0.9102** — older saves resume with no AI state and
rebuild from scratch.

What persists: the defensive-operation roster (as regiment references),
per-faction operation lists, and `initialstrength` per defensive operation.

A dead condition in `Load`:

```csharp
if (defensiveoperations.Count - 1 >= 0 && defensiveoperations.Count < defensiveoperations.Count)
    defensiveoperations[…].initialstrength = initialstrength;
```

`x < x` is always false, so **the saved `initialstrength` is never applied**.
`DefensiveOperation.CheckAdding` has already re-accumulated the field from the
units' *current* strength, so the value is not zero — but it is the wrong
value.

> **Consequence:** after a save/load, every defensive operation's cancellation
> baseline resets to its current strength, so the
> `current < initialstrength × canceldefensiveoperationsratio` check restarts
> from scratch and the operation presses on longer than it should. Reloading
> mid-battle quietly makes the AI more stubborn.

---

## 14. Where the campaign AI touches the rest of the game

| Target | Mechanism |
| --- | --- |
| Commanders | `CheckAICommanderReplacements`, `CheckVerticalReplacement`, `CheckCommanderPromotion` ([19](19-commanders-and-perks.md)) |
| Perks | `CheckPerkSelection`, `CheckSelectionSingleBrigadePerk` → `AIBattle.CheckPerkSelection` |
| Economy | `UpdateFinancialAI`, `UpdateProjects`, `UpdateCompanyFoundations`, `UseSubsidyForPurpose`, `UpdateRailroadConstruction`, `UpdateTransportCapacity` ([18](18-economy.md)) |
| Politics | `UpdateInterventions`, `PickCampaignObjective` ([17](17-politics-and-support.md)) |
| Supply | `CheckSupplyDepotConstruction`, `CheckBurningDownSupplyDepot`, `GetGroupSupplyState` ([14](14-supply-and-logistics.md)) |
| Weapons | `CheckWeaponUpgrades`, `UpgradeWeaponForUnit`, `GetBestWeapon`, `CheckPurchaseWeapons` |
| Naval | `CheckShipConstruction`, `CheckFleetMovements`, `UpdatePortBlockRatios`, `GrabShipType`, `RaiseFleet` |
| Battle | hands off to `AIBattle.CheckCurrentOrderUpdate` for units under a DLC player-commander |

---

## Open questions

1. **`UpdateMicroMovementInOffensive`**, `CheckFinalPositions` and
   `RollUpEnemyObjectivesInZone` (jobs 6, 34, 35) — the tail of an offensive,
   not followed.
2. **Job 17 is skipped.** Job 16 sets the index to 18. Either a removed step or
   an off-by-one; nothing between them references 17.
3. **`CheckTransferOfUnits`** uses `positiondeficit` / `positionsurplus` from
   the heatmap extremes, but the actual transfer rule was not traced.
4. **`UpdateCampaignTheaters` / `IsWithinOperationsTheater`** — a per-unit
   `theaterposition` constrains every operation, and where it is assigned was
   not followed. It is probably the AI's answer to "don't send the Army of the
   Potomac to Missouri".
5. **`CheckArmyGroupManagement`** — pairs with the Army Group gap left open in
   [19](19-commanders-and-perks.md). The *unlock* is now known: the
   `allowarmygroups` flag plus project 88 or 89
   ([22](22-policy-and-project-catalogue.md) §12.4).
6. ~~**`emergencyairecruitingtrigger`**~~ **Resolved** — the config labels it
   *"Trigger for launching low recruiting emergency AI, number of small
   brigades, below"* and it ships **4**; `emergencyaicredittrigger` is **0.4**.
   See [22](22-policy-and-project-catalogue.md) §13.2.
7. ~~**`aipersonalities.dat` content**~~ **Done** — the pre-war picks, policy
   queue heads, tax/subsidy focus and project-spending ratios for all twenty
   doctrines are in [22](22-policy-and-project-catalogue.md) §13.1, along with
   the finding that only the *Historical* doctrine differs between the sides and
   that 17 of the 20 pre-war slots are never read. What is still unread is the
   full 80-entry policy order and the 100 weighted project entries per
   doctrine.
8. **`iswithinfog`'s own derivation.** §3.1 shows the AI is exempt from
    concealment, but what sets `iswithinfog` on the campaign map (as opposed to
    the battle `FogOfWar` / `SpottedEnemy` components) was not traced. It
    determines how much the exemption is actually worth.
