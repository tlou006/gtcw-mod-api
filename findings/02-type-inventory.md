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

- The per-frame simulation tick is not yet located. `Update`/`FixedUpdate` on
  `BattleUnits` and the campaign controllers are the obvious candidates, plus
  the turn-advance method.
- `Autocalc` (auto-resolve) and the live battle model presumably share
  parameters. Whether they share *code* is unknown, and matters: a mod that
  changes combat must change both or players will see auto-resolve diverge
  from manual battles.
- `DLC_WL`'s boundary with the base game — it has its own config files and
  reader set, so it may be cleanly separable or may be entangled.
