# 03 — Scene wiring and lifecycle: where a plugin attaches

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GameObject.Find` call sites,
`SceneManagement`
**Summary:** [summary-architecture](summary-architecture.md)

## Singletons are located by name, not by instance property

The codebase has no service locator and no `static Instance` pattern. It finds
its own singletons by well-known `GameObject` name. **202 call sites** target
one object:

```
GameObject.Find("GameController").GetComponent<T>()
```

This confirms the `campaignbase/gamecontroller` scene path that the official
ModdingTool leaks (see [07-config-data-model](07-config-data-model.md)).

### Components on `GameController`

By call-site count — a rough measure of how central each one is:

| Component | Sites |
| --- | --- |
| `BattleUnits` | 68 |
| `BattlefieldSetup` | 52 |
| `BattleUI` | 35 |
| `Economy` | 7 |
| `SpottedEnemy` | 4 |
| `Smoke` | 1 |
| `FogOfWar` | 1 |
| `AIBattle` | 1 |

### Other scene anchors

| Anchor | Sites | Note |
| --- | --- | --- |
| `UI` | 84 | Battle-layer UI root |
| `UniStormSystemEditor` | 53 | Third-party (weather asset) |
| `Cam_Pivot` | 30 | Camera rig |
| `WeatherObj` | 14 | |
| `ProceduralObjects` | 14 | Runtime-generated geometry parent |
| `SoundEngine` | 8 | |
| `Units` | 6 | Unit parent |
| `PreloadedModels` | 6 | |
| `UICampaign` | 5 | Campaign-layer UI root; also holds `VersionID` ([04](04-version-identification.md)) |
| `LeftGuns` | 5 | |

## Implications for the hook library

**The good news.** `GameObject.Find` is trivial to mimic and needs no
reflection, so the Bindings layer can expose typed accessors to the live game
singletons without touching private state.

**The constraint.** `Find` returns `null` in scenes where the object does not
exist, and much of the game guards on exactly that. The ModApi must do the
same, and **must not assume `GameController` exists at plugin-load time** — it
is scene-scoped, not global. A plugin that resolves its bindings eagerly in
`Awake` will get nulls.

**The cost.** `GameObject.Find` is a scene-graph walk by string. The game calls
it 202 times from scattered sites, which suggests it is not treated as
expensive, but a plugin should resolve once per scene load and cache rather
than copying the game's habit into a per-frame path.

## Lifecycle: `SceneManagement`

`SceneManagement` is the lifecycle hub and the natural event surface for the
ModApi. All static, all viable Harmony targets:

| Method | Event it corresponds to |
| --- | --- |
| `LoadCampaignScenario(string manuallevel, string manualsublevel, int chosenpresident, List<Policy> chosenpolicies, LoadingScreen.BattleResult bresult)` | Campaign started / loaded |
| `LoadCampaignBattleScenario(bool loadfromsave)` | Battle started from within a campaign |
| `LoadBattleScenario(string manuallevel, string manualsublevel, bool loadfromsave)` | Standalone battle started |
| `PrepareLaunchingBattleFromCampaign(string bfsceneid, string sublevel, GameObject monumentobjref)` | Campaign → battle transition, before load |
| `LoadBattleScenarioFromCampaign()` | (private) the transition itself |
| `SaveAll(bool saveunits, bool saveobjects, bool savecommanders, bool savecampaigndata, bool isautosave, string manualsavename)` | Save, with granular flags incl. autosave |
| `ExitBattleScenario(bool resetsceneoperation)` | Battle ended |
| `ExitCampaignBattleAndRebuildCampaign()` | Battle ended, returning to campaign |
| `ExitCampaignScenario(bool resetsceneoperation)` | Campaign ended |
| `RebuildScenes()` | Scene teardown/rebuild |
| `UpdateBatchOperations()` | The deferred-work pump |

`SaveAll`'s flags are worth noting: a mod that persists its own state should
hook it and respect `isautosave`, or it will write on every autosave tick.

### The `BatchOperation` queue

`SceneManagement` carries a nested `BatchOperation` type:

```csharp
public BatchOperation(int _operationid, int _startoffset = 0,
                      object _argument0 = null, ... object _argument7 = null)
```

An operation id, a start offset, and up to **eight boxed `object` arguments**
— the game's own deferred-work queue, pumped by `UpdateBatchOperations()`.
Worth understanding before adding deferred work of our own: there may be
ordering guarantees (or ordering bugs) that a mod's own queue would not
inherit.

### `SceneManagement.currentoperation`

An `int` used as a mode flag. It **changes config loading behaviour** — values
`3` and `8` force the `Config/` fallback in `GamePrefs.loadcontrols()` (see
[05-config-paths-and-overrides](05-config-paths-and-overrides.md)).

The full enumeration is unknown and worth decoding: it is effectively the
game's own notion of "what kind of thing am I running right now", which is
exactly what a mod needs to know before touching anything.

## Open questions

- The full value set of `SceneManagement.currentoperation`.
- Whether `GameController` is the same `GameObject` across the campaign and
  battle scenes, or a distinct one per scene with overlapping component sets.
  The component list above mixes campaign (`Economy`) and battle
  (`BattlefieldSetup`, `FogOfWar`) concerns, which suggests per-scene objects
  sharing a name — meaning a cached binding must be invalidated on scene load.
- Where `Policy.InitializeStatics` is called from in the load order, since
  registering a policy has to happen before it.
