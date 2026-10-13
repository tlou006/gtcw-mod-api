# 01 — Assembly architecture map

First pass over the decompiled tree. Describes what is where, how the game is
wired, and which methods matter for the two project goals. No decompiled
bodies are reproduced here — signatures, names, line references and
paraphrased idioms only.

Export command (output is gitignored):

```
ilspycmd "game/The Civil War (1861-1865)_Data/Managed/Assembly-CSharp.dll" \
  -p -o decompiled/Assembly-CSharp
```

Line references below are into that tree and will shift if the game updates or
a different ILSpy version is used. Treat them as "roughly here", not as
addresses.

---

## 1. Scale and partition

| | Types | Lines |
| --- | --- | --- |
| `Assembly-CSharp`, global namespace | 251 | 216,659 |
| `Assembly-CSharp`, namespaced (third-party) | 225 | 31,354 |
| `Assembly-CSharp-firstpass` | 108 | ~15,000 |

**Game code is ~207k lines**, after subtracting ~9.4k lines of third-party
code that also sits in the global namespace (see below).

### The partition rule — corrected

Phase 1 planned to separate game code from engine and asset-store code using
the `C`-prefix convention suggested by `CBuilding`. **That convention does not
generalise** — only `CBuilding` and `CBuildingIcon` carry it.

The real rule is simpler and cleaner:

> **Global namespace = game code. Namespaced = third-party.**

Every asset-store package the developers imported kept its own namespace
(`Gaia`, `FSG.MeshAnimator`, `Jobberwocky.GeometryAlgorithms`,
`OrbCreationExtensions`, `mattatz.Triangulation2DSystem`, `Unitycoding`,
`UnityEngine.UI`). The game's own code was written without a namespace at all.

**Exception — third-party code that leaked into the global namespace.** These
must be excluded by name, not by namespace. ~9.4k lines across ~40 types:

- **UniStorm** (weather/time-of-day asset): `UniStormWeatherSystem_C` (3,590
  lines), `UniStorm_Clock_C`, `UniStormEventSystem`, `UniStormGUIMenu`,
  `UniStormWeatherForecast`, `UniStormDemoUI`, `UniStormTimeEventExample`,
  `WeatherZoneExample_C`, `GetUniStormComponents_C`, `DamageFromUniStorm`,
  `SavePlayerData_C`, `PlayerVitalsExample`, `DynamicSnow_C`,
  `DynamicSnow_Height_C`, `Climate_Zone_C`, `RandomWeather`,
  `RandomWeatherOnStart`
- **EasyRoads3D**: `RoadObjectScript`, `sideObjectScript`, `runtimeScript`,
  `EasyRoads3DTerrainID`, `ERVegetationStudio`
- **SimpleLOD**: `SimpleLODClickObject`, `SimpleLODDemo`, `SimpleLODDemoCamera`,
  `SimpleLODShowInfo`, `SimpleLODTredMill`, `LODMaker` (1,762), `LODSwitcher`,
  `MeshCombiner`
- **Misc samples/utilities**: `ExampleInteractiveVoronoi2D`,
  `MouseOrbitImproved`, `MouseLookDesktop`, `Triangulator`,
  `PlantGrowthSystem`, `HideFlagsUtility`, `DrawSolidCube`, `SurfaceScript`,
  `MarkAnimatedScript`, `SpawnPlayer`

> **Caveat on the `_C` suffix:** it marks UniStorm's own C# ports of its
> original scripts — it is *not* a game convention. `*_C` means third-party,
> which is the inverse of what a reader might assume from `CBuilding`.

`Weather.cs` and `WeatherFunctions.cs` are the game's own wrappers *around*
UniStorm, so they are game code. Do not confuse them with the asset.

### `Assembly-CSharp-firstpass` is irrelevant

108 types, entirely Unity Standard Assets (`UnityStandardAssets.*`:
FirstPerson/ThirdPerson characters, CrossPlatformInput, ImageEffects, Water,
Vehicles.Ball) plus two UI helpers. **Nothing game-specific.** It was exported
for completeness and can be ignored from here on; no further note will
reference it.

---

## 2. The god classes

Sorted by line count. The top four hold most of the game's behaviour:

| Type | Lines | Role |
| --- | --- | --- |
| `Regiment` | 28,569 | The single most important type. A unit on the battlefield *and* in the campaign: state, movement, morale, firing, melee, supply, orders. |
| `BattleUnits` | 15,493 | Battle-side unit management, spawning, the per-tick unit loop. Lives on `GameController`. |
| `GamePrefs` | 10,471 | The global tuning table. Every `Config/*.txt` pref and its parser. |
| `GameVars` | 8,406 | Global mutable game state: `alliance[]`, `nation[]`, dates, flags. |
| `AICampaign` | 8,374 | Strategic AI. |
| `DLC_WL` | 7,932 | "Whiskey & Lead"(?) DLC — commanders, actions, biographies, rankings, items. Largely self-contained. |
| `ImportExportUnitData` | 7,552 | The save/load and scenario-data layer. 29 reader sites. |
| `PanelHandler` | 7,162 | UI panel orchestration. |
| `Autocalc` | 5,612 | Auto-resolve combat model. |
| `BattleUI` | 5,680 | Battle HUD. Lives on `GameController`. |
| `AIBattle` | 5,458 | Tactical AI. Lives on `GameController`. |
| `CampaignMainPanel` | 4,032 | Campaign map UI. |
| `Tools` | 4,375 | Static utility grab-bag: geometry, `Tools.Date`, string partitioning, PNG/texture IO. |
| `BattlefieldSetup` | 3,061 | Terrain/battlefield construction. Lives on `GameController`. |
| `CBuilding` | 3,271 | Campaign buildings, incl. the nested `AIPlacement` geometry. |
| `Economy` | 2,564 | Production, resources, trade. Lives on `GameController`. |
| `Policies` | 1,926 | **Policy effect dispatch — see §5.** |
| `Projects` | 1,042 | **Project effect dispatch and the `projects.dat` parser — see §5.** |

Other notable game types: `Statistics`, `Messages`, `Saving`, `Finances`,
`Production`, `Trade`, `Town`, `Ship`, `Fleet`, `Vessel`, `Camp`, `OOB`,
`Objectives`, `CampaignObjective`, `CampaignStrategy`, `CampaignController`,
`BattleController`, `SceneManagement`, `FogOfWar`, `Frontline` / `Frontline2`,
`WeaponList`, `UnitPerk`, `Injuries`, `AvatarManagement`, `Tooltip`.

> **`EntryPoint` is a false friend.** It is a *battlefield* entry point — a
> `MonoBehaviour` marking where reinforcements walk onto the map. It is not a
> program entry point. There is no `Main`; the game starts from Unity scene
> load like any Unity game.

---

## 3. Scene wiring — where to attach

The codebase locates its own singletons by `GameObject.Find` on well-known
names rather than by a service locator or static instance property. 202 call
sites target one object:

```
GameObject.Find("GameController").GetComponent<T>()
```

The components a plugin can expect to find on `GameController`, by call-site
count:

| Component | Sites |
| --- | --- |
| `BattleUnits` | 68 |
| `BattlefieldSetup` | 52 |
| `BattleUI` | 35 |
| `Economy` | 7 |
| `SpottedEnemy` | 4 |
| `Smoke`, `FogOfWar`, `AIBattle` | 1 each |

Other scene anchors worth knowing: `UI` (84 sites), `UICampaign`,
`Cam_Pivot` (30), `WeatherObj`, `ProceduralObjects`, `SoundEngine`, `Units`,
`PreloadedModels`, `UniStormSystemEditor` (53 — third-party).

**Implication for the hook library.** `GameObject.Find` is cheap to mimic and
needs no reflection, so the Bindings layer can expose typed accessors to the
live game singletons without touching private state. It is also fragile:
`Find` returns `null` in scenes where the object does not exist, and much of
the game guards on that. The ModApi must do the same, and must not assume
`GameController` exists at plugin-load time — it is scene-scoped.

### Lifecycle hooks

`SceneManagement` is the lifecycle hub and the natural event surface for
Phase 4. Static methods, all good Harmony targets:

- `LoadCampaignScenario(...)`
- `LoadCampaignBattleScenario(bool loadfromsave)`
- `LoadBattleScenario(...)`
- `PrepareLaunchingBattleFromCampaign(...)`
- `LoadBattleScenarioFromCampaign()`
- `SaveAll(...)`
- `ExitBattleScenario(...)`, `ExitCampaignScenario(...)`,
  `ExitCampaignBattleAndRebuildCampaign()`
- `RebuildScenes()`, `UpdateBatchOperations()`

It also carries a nested `BatchOperation` type with an operation id and up to
eight boxed `object` arguments — the game's own deferred-work queue. Worth
understanding before adding deferred work of our own.

`SceneManagement.currentoperation` is an int used as a mode flag, and it
changes config loading behaviour (see §4). Values 3 and 8 appear in pref-load
guards; the full enumeration is still unknown.

---

## 4. The config data layer — verified

This section confirms, from code, what `notes/02` inferred from the
ModdingTool workbook and the Quick Guide.

### 4a. Paths are relative string literals

There is no path-resolution service. Config files are opened by literal
relative path, resolved against the process working directory (the game
install root):

- `Config/aistrategies.dat` (`AIBattle.StrategiesImportFileName`)
- `Config/actions.dat`, `Config/items.dat`, `Config/dlcwl_rankings.dat`
  (`DLC_WL.ConfigFileName*`)
- `Config/resources.dat`, `Config/armynames{i}.dat`, `Config/avatar.dat`,
  `Config/armygroupcols.dat`, `Config/Filters.dat`, `Config/ui.txt`,
  `Config/ui_battle.txt`, `Config/unitprefs.txt`, `Config/battleprefs.txt`,
  `Config/campaignprefs.txt`, `Config/debug.txt`, `Config/questionnaire.dat`,
  `Config/tooltips_dlcwl.txt`, `Config/dlcwl_config.dat`

Only a handful of sites touch `Application.dataPath` at all
(`BattlefieldSetup`, `VideoScreen`). **Good news for modding:** the data layer
is plain `System.IO` against relative paths, so it is interceptable with
ordinary file-system or Harmony hooks and needs no Unity-specific machinery.

### 4b. The override mechanism is whole-file replacement — confirmed

The guide's "is then loaded instead of" wording is literal. The idiom
throughout `GamePrefs` is: build the scenario-local path, and if that file
does not exist, reassign the path to the `Config/` default. One path wins
entirely; **there is no field-level merge, and no third layer.** Examples:

- `GamePrefs.loadcampaigncontrols(string folder)` — falls back to `Config`
  when `{folder}/campaignprefs.txt` is absent (~line 6784).
- `GamePrefs.loadunitprefs(string folder = "")` — same shape for
  `unitprefs.txt` (~line 4694).
- `GamePrefs.loadcontrols()` — builds
  `Battlefields/{leveltoload}/{subleveltoload}/battleprefs.txt`, and falls
  back to `Config/battleprefs.txt` if it is missing **or** if
  `SceneManagement.currentoperation` is 3 or 8 (~line 5987).

That last one is the mechanism behind the "scale-dependent prefs" finding in
`notes/02`: the same pref file is loaded from a different place depending on
scenario scale and current operation.

> **This settles the design question from `plan.md` Phase 4.** Additive,
> field-level config overrides are *new capability*, not a reimplementation of
> something the game already does. Two mods that each want to change one field
> of `unitprefs.txt` cannot both win today; with the ModApi they can.

### 4c. The file format — exact, from the parsers

Confirmed positional and line-oriented, with a **count header**. Two dialects:

- **`.dat` files: one value per line.** A leading line gives the record count,
  then each record's fields follow in fixed order, one per line.
- **`.txt` pref files: alternating label/value lines.** The parsers read a
  line and discard it, then read the value. The labels are documentation for
  humans and are never parsed — which means *a label can be edited freely, but
  a line can never be inserted or removed*.

**Line endings are CRLF**, and the files are read with `Encoding.Default` in
the pref loaders (not UTF-8). Any tool that rewrites a config file must
preserve both, or risk changing field widths and breaking non-ASCII text.

### List-valued fields: fixed-width, semicolon-separated

Three `Tools` helpers encode the entire convention:

```csharp
public static List<string> GetStringPartitions(string strvar, int partlen = 3, bool usecomma = true)
public static List<int>    GetIntPartitions   (string strvar, int partlen = 3, bool usecomma = true)
public static string       TransformToLineBreaks(string textbeforebreaks)
```

- A list field is **`partlen` characters per entry plus a one-character
  separator**, repeated, with a *trailing* separator. The separator in the
  shipped files is **`;`** (`usecomma` is a misnomer — the helpers only skip
  one character, they never check which). So the default `partlen = 3`
  produces `001;002;`.
- Entries are **zero-padded to the full width**. An entry that does not fill
  `partlen` exactly is **silently dropped**, not an error.
- `GetIntPartitions` uses `int.TryParse` and **silently skips** anything
  unparseable. This is the one place the data layer does *not* hard-fail —
  which makes a typo'd id in a list field invisible rather than loud. Worth
  knowing when debugging a mod that "does nothing".
- `TransformToLineBreaks` decodes `\n` and `\t` escapes inside a single-line
  string field. That is how multi-line prose survives a line-oriented format.
- An empty line is a legitimate empty list.

From `Projects.LoadedProjects.Import(string filename)` (~line 42), the exact
`projects.dat` schema — **a count header, then 16 lines per record**:

| Field | Encoding |
| --- | --- |
| # | Field | Encoding | Example (project 0) |
| --- | --- | --- | --- |
| 1 | `projectid` | int | `0` |
| 2 | `projectname` | string | `Austrian Rifles` |
| 3 | `alliances` | width 1 + `;` | `0;1;` |
| 4 | `requiredpolicyids` | width 3 + `;` | *(empty)* |
| 5 | `subsidytype` | int | `5` |
| 6 | `subsidycostfirstlevel` | float | `2500000` |
| 7 | `repeating` | `bool.Parse` | `false` |
| 8 | `usedgroup` | int | `0` |
| 9–10 | `imagepath_big` | **two** lines, joined with `/` | `UI/Weapons/Rifles` + `17` |
| 11–12 | `imagepath_small` | **two** lines, joined with `/` | `UI/ProjectImages` + `0_s` |
| 13 | `prosadescr` | string, escapes decoded | *(flavour text)* |
| 14 | `effectsdescr` | string, escapes decoded | *(effect summary)* |
| 15 | `datefrom[0]` | `dd-mm-yyyy`, offsets (0,2)(3,2)(6,4) | `01-01-1862` |
| 16 | `datefrom[1]` | same; **skipped if < 10 chars** | *(empty)* |
| 17 | `applicablescenarios` | width 3 + `;` | `001;002;` |
| 18 | `requireddlcids` | width 2 + `;` | *(empty)* |

Verified line-by-line against the shipped `Config/projects.dat` (125 records).
Note that `imagepath_*` each consume **two** lines, so a record is 18 lines
even though it has 16 fields — an easy off-by-two when hand-editing.

> **Date order is `dd-mm-yyyy`.** `Tools.Date(int _day, int _month, int _year,
> float _hourfraction = -1f)` takes day first, and the parser feeds it the
> substrings in file order. The constructor clamps day > 31 and month > 12
> (logging a warning) and zeroes the whole date if any component is ≤ 0 — so a
> malformed date degrades to "no date" rather than throwing. `Date` also
> carries a precomputed `yearfraction`, which is what most date comparisons
> actually use.

> **The format is brittle by construction.** Field count, field order, line
> count and character offsets are all load-bearing, and `float.Parse` /
> `int.Parse` / `bool.Parse` are unguarded. A missing line does not degrade —
> it throws, or silently shifts every subsequent field. This is the "hard-fail
> data layer" from the guide, now explained.

### 4d. The save/load surface

`ImportExportUnitData` is the data layer, organised as paired
`Save*`/`Load*` (or `Create*`/`Overwrite*`) methods, one per subsystem, almost
all taking a single `folder` string:

`ShipTypeData`, `WeaponsData`, `TechnologyData`, `NationsData`
(`Save`/`Overwrite`), `ScenarioFile` (`Save`/`Load`), `UnitFiles`
(`Create`/`Overwrite`), `AdditionalUnitData`, `AdditionalCommanderInfo`,
`GarrisonReferences`, `Buildings`, `Commanders`, `Spottings`, `RailroadData`,
`IIPAndTownData`, `ChartDatabases`, `BattleObjectives`, `BattleEntryPoints`,
`Pontoons`, `BattleBuildings`, `FrontlineData`, `Obstacles`, `BattleUnitData`,
`Artillery`, `BattleSideInfo`, `LastGamePlayed`.

Also present, and directly useful:

- `CheckFileIntegrity(string loadname)` (~1719) — the game's own validator.
  Read this before writing ours.
- `CreateVersionFile(string folder)` / `GetVersionFromFile(string folder)`
  (~5785) — save-data versioning as a float.
- `ImportWeaponStock(string path)` (~619) — confirms `weaponstock.dat` is read
  through an explicit path argument, consistent with it being a
  campaign-scenario file rather than a `Config/` file (the retiering recorded
  in `notes/02`).
- `LoadAdditionalUnitData(string folder, int mode = 0, bool skipworkdownstoredpaths = false)`
  — a `mode` parameter worth decoding.

Individual `MonoBehaviour`s implement their own `Load(StreamReader)` /
`Save(StreamWriter)` pair and are driven by the above (`EntryPoint` is a small,
readable example: three fields, three lines). `Tools.Date` follows the same
pattern with `Save`/`Load`/`SaveNull`. **This is the game's serialization
contract, and any type the ModApi adds to a save must honour it.**

---

## 5. Policy and project effects — the critical finding

This was the open unknown for goal #2. It is now resolved.

### 5a. `Policies.LaunchPolicyEffect` is the dispatch site

```csharp
public static void LaunchPolicyEffect(
    Policy policy, int alliance, float timediff = 0f, bool startupset = false)
```

At `Policies.cs` ~line 544. The body is a **~1,200-line `switch` on
`policy.UniquePolicyID`** with 117 `case` labels. Each arm hardcodes that
policy's effect: adjusting subsidy factors, unlocking content, shifting
national support, setting trade-node ownership, constructing railroad
segments, applying rating modifiers.

Two structural facts that matter more than the arms themselves:

1. **IDs are duplicated with a `+100` offset for the second side.** Arms are
   written `case 0: case 100:`, `case 1: case 101:`, and so on. Policy ids
   `0–99` and `100–199` are the Union and Confederate instances of the same
   policy, falling through to shared code that then indexes
   `GameVars.alliance[alliance]`. This is the "numeric suffix = nation index"
   convention from `notes/02`, appearing here as an *id block* offset rather
   than a filename suffix.

2. **There is no `default:` arm.** An unrecognised `UniquePolicyID` falls
   straight through the switch and the method completes normally — it then
   records the policy in `GameVars.alliance[alliance].alreadyactivatedacts`
   and logs it. **A modder can already add a policy with a new id and have the
   game accept, research and track it; it will simply have no effect.**

> **This is the hook library's reason to exist, precisely located.** A Harmony
> **postfix** on `LaunchPolicyEffect` receives `(policy, alliance, timediff,
> startupset)`, and effects registered against unused ids compose with the
> game's own switch rather than fighting it. No prefix, no transpiler, no
> reimplementation of the 117 existing arms. The `plan.md` Phase 4 sketch
> (`GTCW.ModApi.Projects.Register(...)`) is implementable as designed.

Supporting members of `Policies`, all static and all hookable:

- `DeactivatePolicyEffect(Policy policy, int alliance)` (~518) — the mirror of
  the above. A registered effect needs a teardown path here, or it will
  persist after the policy is removed.
- `UpdatePolicyEffects(int alliance, float timediff = 0f, bool startsetup = false)`
  (~479) — the recurring per-tick application, distinct from the one-shot
  launch.
- `CanBeActivated(int alliance, Policy policy)` (~404) — gating.
- `AddResearch` / `RemoveResearch` / `UpdatePolicyResearch` / `UpdateCounter`
- `UpdateAllTooltips(int activatedpolicies = -1)` (~265) — the tooltip
  generator, i.e. the authoritative description of what each policy claims to
  do. **Phase 2's primary documentation source.**
- `CheckAIPolicyChange(int alliance)` (~1801) — how the AI chooses policies.
- `IsDeactivated(Policy policy)` (~1741) — contains at least one
  date-hardcoded special case (id 36, keyed to a 1861 date).
- `UpdateSubsidyEffect`, `UpdateGrandStrategies`, `UpdateTradeNodesEurope`,
  `HandoverForts`, `LaunchNewspaperMessage`, `GetAvailablePolicies`

`Policy` itself is a `MonoBehaviour` **authored on prefabs** — it carries
`TextMeshProUGUI` and `Image` references alongside its data
(`UniquePolicyID`, `validscenarios`, `NeededPrePolicies`, `ResearchDuration`,
`IsAct`, `ReferedChapter`, tooltip/headline/picture fields). Static registries
`Policy.allpolicies` and `Policy.grandpolicies` are populated by
`Policy.InitializeStatics(bool skiplevelcheck = false)`.

> **Consequence:** policies are not purely data-driven. Adding one needs a
> prefab, which confirms the "prefab-authored mechanics" risk row in
> `plan.md`. Whether the ModApi can register a policy without shipping a
> prefab — e.g. by cloning an existing one at runtime and rewriting its fields
> — is the next thing to establish.

### 5b. Projects have *two* dispatch sites

In `Projects.cs`:

- `UpdateOneTimeProjectsEffects(int projectid, int alliance)` (~392) — a
  `switch (projectid)` with 12 arms, for one-shot grants. Observed effects:
  adding to `GameVars.alliance[alliance].weaponstock[...]` by weapon index,
  and `GameVars.nation[m].AdjustSupport(...)`. Several arms read their
  magnitude from `GamePrefs` (e.g. `machinegunprojectstock`,
  `project_propagandainfl`) rather than from a literal — **so some project
  magnitudes are already data-tunable even though the effect is hardcoded.**
- `UpdateProjectEffects()` (~495) — the recurring path. Iterates alliances and
  their active `projects[]`, with a `switch` on the project id inside the loop.

And the Rosetta Stone for both:

- `GetProjectEffect(int id, float effect, float secondeffect = 0f)` (~710) —
  builds the human-readable effect string per id. **This maps id → meaning and
  is the cheapest possible route to documenting every project.** Same role
  that `Get*Tooltip` plays elsewhere.

Other useful members: `LoadedProjects.Import` (§4c), `GetLoadedProjectFromID`,
`GetProjectNameByID`, `IsAppointable`, `AppointProject`, `CheckProjectUnlocks`,
`IsProjectAvailableInScenario`, `HasPolicy`, `GetRequiredPolicies`,
`FulfillsDLC`, `GetSubsidyCost(int level)`.

**Neither project switch has a `default:` arm either** (`Projects.cs` contains
no `default:` at all). So the same conclusion holds as for policies: an
unrecognised project id is accepted, appointable and tracked, and simply does
nothing — leaving the effect slot free for a registered handler.

> **Both effect systems are open at the top end.** Policies and projects can
> each be extended with new ids today, and a postfix hook can supply their
> behaviour. This is the single most important structural fact found so far,
> and it is what makes goal #2 worth building.

---

## 6. There is no version constant

`plan.md` assumed a version gate could read a game version at runtime. It
cannot, at least not from code: the only thing resembling one is
`MainMenuControls.currentversion`, a `static float` **parsed out of a UI label
at runtime** — `GameObject.Find("UICampaign/VersionID")`, substring of the
`TextMeshProUGUI` text. `GameVars.lastversionupdate` is then set from it.

So the version is authored in a scene asset, not in the assembly, and is only
available once the campaign UI exists.

> **This vindicates the signature-manifest approach.** The hook library's
> version gate should key on the `Assembly-CSharp.dll` hash or MVID (as
> `notes/00` already records a SHA-256 for), not on an in-game version string.
> Reading `MainMenuControls.currentversion` is still useful for *reporting* a
> mismatch to the user in their own terms, but it is too late and too fragile
> to gate on.

---

## 7. What this changes in the plan

- **Phase 1's `C`-prefix task is done and was wrong.** Replaced by the
  global-vs-namespaced rule plus the third-party exclusion list in §1.
- **The `campaignbase/gamecontroller` anchor is confirmed** and now has a
  concrete component list (§3).
- **Phase 2 Track A gets a real schema**, not an inferred one. The ModddingTool
  workbook gave field *layout*; the parsers give field *types, widths and
  order*. Where they disagree, the parser wins.
- **Phase 4's layered-override design is confirmed as new capability** (§4b).
- **Phase 4's effect registration is confirmed as implementable** via a
  postfix on `LaunchPolicyEffect`, because there is no `default:` arm (§5a).
- **Phase 3's version gate must hash the assembly**, not read a version
  string (§6).

## 8. Next, in order

1. Decide whether a policy can be registered without shipping a prefab
   (runtime clone of an existing `Policy` + field rewrite). This is the gating
   question for the whole effect-registration design.
2. Harvest `Policies.UpdateAllTooltips` and `Projects.GetProjectEffect` into
   `notes/03-policies-and-projects.md` — id → effect, for all 117 policy arms
   and every project id.
3. Map `GameVars.alliance[]` and `GameVars.nation[]` — the state every effect
   writes to, and therefore the ModApi's core data surface.
   `AdjustSubsidyFactor` and `AdjustSupport` are the two mutators seen so far.
4. Decode `SceneManagement.currentoperation`'s value set (3 and 8 are known to
   force the `Config/` pref fallback).
5. Inventory the remaining `GamePrefs` loaders to finish the `Config/` schema,
   now that the format conventions are pinned down.
