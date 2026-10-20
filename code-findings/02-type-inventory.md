# 02 — Type inventory and the god classes

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll`, global namespace only (see
[01-assembly-partition](01-assembly-partition.md))
**Summary:** [summary-architecture](summary-architecture.md)

Sorted by line count in the decompiled output. Line counts are a proxy for
behavioural weight, not a measurement — a 3,000-line UI class is still mostly
UI. The top four hold most of the game's behaviour.

| Type | Lines | Role |
| --- | --- | --- |
| `Regiment` | 28,569 | The single most important type. A unit both on the battlefield *and* in the campaign: state, movement, morale, firing, melee, supply, orders. |
| `BattleUnits` | 15,493 | Battle-side unit management, spawning, the per-tick unit loop. On `GameController`. |
| `GamePrefs` | 10,471 | The global tuning table. Every `Config/*.txt` pref and its parser. |
| `GameVars` | 8,406 | Global mutable game state: `alliance[]`, `nation[]`, dates, flags. |
| `AICampaign` | 8,374 | Strategic AI. |
| `DLC_WL` | 7,932 | DLC content — commanders, actions, biographies, rankings, items. Largely self-contained. |
| `ImportExportUnitData` | 7,552 | The save/load and scenario-data layer. 29 reader sites. See [08-save-load-surface](08-save-load-surface.md). |
| `PanelHandler` | 7,162 | UI panel orchestration. |
| `BattleUI` | 5,680 | Battle HUD. On `GameController`. |
| `Autocalc` | 5,612 | Auto-resolve combat model. |
| `AIBattle` | 5,458 | Tactical AI. On `GameController`. |
| `Tools` | 4,375 | Static utility grab-bag: geometry, `Tools.Date`, string partitioning, PNG/texture IO. |
| `CampaignMainPanel` | 4,032 | Campaign map UI. |
| `CBuilding` | 3,271 | Campaign buildings, incl. the nested `AIPlacement` geometry (position, rotation, radius/angle) — so AI building placement is geometric and lives on the component. |
| `BattlefieldSetup` | 3,061 | Terrain / battlefield construction. On `GameController`. |
| `Economy` | 2,564 | Production, resources, trade. On `GameController`. |
| `Policies` | 1,926 | **Policy effect dispatch.** See [09-policy-and-project-effects](09-policy-and-project-effects.md). |
| `Projects` | 1,042 | **Project effect dispatch and the `projects.dat` parser.** See [09](09-policy-and-project-effects.md) and [06](06-config-file-format.md). |

`Regiment` is almost certainly the class behind the `>b__887_0` compiler lambda
name noted during the obfuscation check — an index that high implies ~900
methods in one type.

## Classification of the 211 game types

Classified by evidence rather than by name: counting references to UI types
(`TextMeshProUGUI`, `UnityEngine.UI`, `Button`, `Image`, `RectTransform`),
`StreamReader`/`StreamWriter` sites, rendering types (`Renderer`, `Material`,
`Mesh`, `Shader`, `Texture2D`, `Terrain`) and tick methods.

| Bucket | Count | Notes |
| --- | --- | --- |
| **UI-dominant** | ~67 | The largest group, as expected for a Unity game. Mostly `*Panel`, `*List`, `*Button`, `Tooltip`, `PanelHandler`, `MainMenu*`. |
| **Rendering / glue-dominant** | ~26 | `Projectile`, `Smoke`, `Flag`, `FireArc`, `OrderMark`, `RangeMark`, `*Textures`, `ModelAnimator`, `SpriteAnimator`, `Heatmap`, `ConnectionLine`. |
| **Has file IO** | 50 | Overlaps the others — see [08-save-load-surface](08-save-load-surface.md). Concentrated in `ImportExportUnitData` (66 sites), `DLC_WL` (43), `GameVars` (22), `Autocalc` (10), `GamePrefs` (9). |
| **Simulation / AI** | ~20 | `Regiment`, `BattleUnits`, `Autocalc`, `AICampaign`, `AIBattle`, `AI_Manager`, `AI_Unit`, `Economy`, `Frontline`/`Frontline2`, `Retreat`, `SeaInvasion`, `BattleFront`, `FogOfWar`, `Camp`. |

> **The buckets overlap heavily, and that is the finding.** The codebase does
> not separate concerns by type: `Policies`, `Projects`, `Statistics`,
> `Finances`, `Production` and `Trade` all classify as UI-dominant *because
> they drive their own panels directly*, while also holding the simulation
> logic and, in `Projects`' case, the config parser. `Camp` is UI, rendering
> and simulation at once.

For the hook library this means **there is rarely a clean "model" to bind
to.** A mod that wants to change a number often has to reach into a type whose
other half is UI code, which is an argument for the ModApi exposing
purpose-built accessors rather than surfacing game types directly — exactly the
`Bindings` boundary the plan already specifies.

It also means the simulation/UI split is a poor filter for goal 1: reading
`CampaignTopPanel` to learn what a value *means* is often faster than reading
the simulation, because the UI is where values get labelled.

### `OQOCCODODC` — a dead stub

The only game type that is **not** a `MonoBehaviour` is `public class
OQOCCODODC {}` — empty, three lines, referenced from nowhere. Almost certainly
a build or protector artifact.

> It is **not** evidence of obfuscation: one orphaned garbled name among 211
> otherwise clean PascalCase types. Recorded so nobody chases it.

## Other notable game types

Not yet studied, but confirmed present and game-owned:

`Statistics`, `Messages`, `Saving`, `Finances`, `Production`, `Trade`, `Town`,
`Ship`, `Fleet`, `Vessel`, `Camp`, `OOB`, `Objectives`, `CampaignObjective`,
`CampaignStrategy`, `CampaignController`, `BattleController`,
`SceneManagement`, `FogOfWar`, `Frontline` / `Frontline2`, `WeaponList`,
`UnitPerk`, `Injuries`, `AvatarManagement`, `Tooltip`, `Weather`,
`WeatherFunctions`, `Policy`, `Chart`, `Heatmap`, `Diary`, `Newspaper`,
`Briefing`, `Tutorial`, `GameGuide`, `Questionnaire`, `License`, `IIP`,
`Dispatch`, `Retreat`, `SeaInvasion`, `ArmyGroup`, `ArmyNames`, `ArmySpawner`,
`Artillery`, `Coy`, `BattleFront`, `BattleMonument`, `SoundEngine`,
`TerrainManagement`, `Palette`, `Filter`, `FilterMap`, `MapFilter`,
`MapSymbols`.

## `EntryPoint` is a false friend

It is a *battlefield* entry point — a `MonoBehaviour` marking where
reinforcements walk onto the map. It carries `entryangle`,
`entrypointposition`, `trooptargetposition`, `owner`, and claims its owner via
`OnTriggerEnter` when a friendly `Regiment` block touches it.

**It is not a program entry point.** There is no `Main`; the game starts from
Unity scene load like any Unity game. For lifecycle, see
[03-scene-wiring](03-scene-wiring.md).

`EntryPoint` is, however, a good minimal example of the game's serialization
contract — three fields, three lines, via `Load(StreamReader)` /
`Save(StreamWriter)`. See [08-save-load-surface](08-save-load-surface.md).

## Open questions

- ~~The per-frame simulation tick is not yet located.~~ **Resolved** — see
  [10-simulation-tick](10-simulation-tick.md). `BattleUnits.Update` is the
  subsystem driver, `Regiment.Update` the per-entity tick, and the controllers'
  `Update` methods are boot state machines rather than simulation.
- `Autocalc` (auto-resolve) and the live battle model presumably share
  parameters. Whether they share *code* is unknown, and matters: a mod that
  changes combat must change both or players will see auto-resolve diverge
  from manual battles. `Autocalc` has 10 file-IO sites, which is unexpected for
  a pure calculation and worth a look.
- `DLC_WL`'s boundary with the base game — it has its own config files and
  reader set, so it may be cleanly separable or may be entangled.
