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

### `SceneManagement.currentoperation` — decoded

An `int` mode flag: the game's own notion of "what am I running right now",
which is exactly what a mod needs to know before touching anything. Every
assignment site accounted for:

| Value | Meaning | Set by |
| --- | --- | --- |
| 0 | Idle / main menu | `ExitBattleScenario`, `ExitCampaignScenario` (when `resetsceneoperation`) |
| 1 | Standalone battle, fresh | `LoadBattleScenario` |
| 2 | **Campaign running** (steady state) | `CampaignController`, frame 30 |
| 3 | Campaign battle, fresh | `LoadCampaignBattleScenario`, `BattleController` |
| 4 | Campaign → battle transition in progress | `PrepareLaunchingBattleFromCampaign`, only from 2 |
| 5 | Returning from a campaign battle | `ExitCampaignBattleAndRebuildCampaign` |
| 6 | Campaign loaded from save | `PanelHandler`, `Saving` |
| 7 | Standalone battle from save | `LoadBattleScenario`, `Saving` |
| 8 | Campaign battle from save | `LoadCampaignBattleScenario`, `Saving` |

This also sharpens the config-loading consequence: `loadcontrols()` forces
`Config/battleprefs.txt` when `currentoperation` is **3 or 8 — the two
campaign-battle cases**. So campaign battles always use the global file, while
standalone battles (1 and 7) can use a battlefield-local one. See
[05-config-paths-and-overrides](05-config-paths-and-overrides.md).

## Scene lifetime: one gameplay scene at a time

There are three named scenes plus per-battlefield scenes:

| Scene | Loaded how |
| --- | --- |
| `Main` | `SceneManager.LoadScene("Main")` — **non-additive**, from `RebuildScenes()` |
| `CampaignBase` | `LoadSceneMode.Additive` |
| `BattlefieldBase` | `LoadSceneMode.Additive` |
| `<bfid>` (specific battlefield) | `LoadSceneMode.Additive`, from `BattlefieldSetup` |

**There is no `UnloadScene` or `UnloadSceneAsync` call anywhere in the
assembly.** Gameplay scenes are only ever *added*, and the only teardown is the
non-additive `LoadScene("Main")` in `RebuildScenes()`, which destroys
everything.

Every transition is therefore routed through `bo_rebuildscene`: the batch
sequences in `PanelHandler`, `Saving`, `BattleController` and
`CampaignController` all queue `rebuildscene` before loading the next scenario.

> **Resolved: `GameController` is a distinct object per scene, but only one
> exists at a time.** An earlier note here speculated that two might coexist
> during a campaign battle, making `GameObject.Find("GameController")`
> ambiguous. They cannot — the rebuild tears the hierarchy down first. The
> component sets differ per scene, and `BattleUnits.IsCampaign` (a public
> Inspector-set field with no assignment in code) tells you which flavour you
> have.

**But the consequence for bindings is stronger, not weaker.** Because the
teardown is a full scene reload, a cached component reference across a
transition is not merely stale — it points at a **destroyed** `UnityEngine.Object`,
which throws or silently compares equal to `null` depending on how it is
touched. Bindings must be resolved after each scenario load and discarded on
`bo_rebuildscene`, never held across one.

`DontDestroyOnLoad` is used for exactly one thing — the audio listener — so
nothing else survives a rebuild. `MainMenuControls` lives in `Main`, which is
why it survives *additive* loads and can own the global frame counter (see
[10-simulation-tick](10-simulation-tick.md)).

## Where to patch for session lifecycle

Established while building the ModApi's lifecycle feature, so these are the
load-bearing four rather than a survey.

**Four patch points cover every transition in the game.**

| Point | Patch | Raises |
| --- | --- | --- |
| `LoadCampaignScenario` | postfix | session started, campaign |
| `LoadBattleScenario` | postfix | session started, battle |
| `LoadCampaignBattleScenario` | postfix | session started, battle |
| `RebuildScenes` | prefix | session ending |

Two facts make that set complete, and both needed reading rather than
searching:

1. **`RebuildScenes()` has exactly one caller** — `UpdateBatchOperations`, on
   `bo_rebuildscene` — which **fifteen** sites across `MainMenu`,
   `PanelHandler`, `Saving`, `ImportExportUnitData`, `BattleController`,
   `CampaignController` and `SceneManagement` itself queue. So it is a single
   chokepoint by construction, not merely by convention.
2. **Every queued load funnels back into the three `Load*Scenario` methods.**
   `bo_loadhistoricbattle`, `bo_loadcampaignscenario`, `bo_loadcampaignbattle`
   and the two from-save variants all call one of the three, so patching the
   three catches direct and deferred loads alike.

> **The trap: do not use the `Exit*Scenario` methods for this.**
> `ExitCampaignScenario` and `ExitBattleScenario` **destroy nothing** — they
> call `ClearBattlefield`, save `lastgame`, and reset `currentoperation` to 0.
> And `ExitCampaignBattleAndRebuildCampaign`, the battle→campaign return,
> **calls neither of them**. A lifecycle hook built on `Exit*` would therefore
> leak a dead session on the most common transition in the game, while looking
> correct on the main-menu paths.

The ordering is clean in both directions, so a session never overlaps its
successor:

```
campaign → battle   bo_exitcampaignscenario → bo_rebuildscene → bo_loadcampaignbattle
battle → campaign   bo_rebuildscene → bo_loadcampaignscenario
```

`PrepareLaunchingBattleFromCampaign` only sets `currentoperation = 4` and
records the target; `SceneManagement.Update` sees that value and calls
`LoadBattleScenarioFromCampaign`, which queues the first sequence above.

Both sequences are confirmed on a live run: the campaign session ends before
the battle session begins, with no overlap.

### Quitting the game tears nothing down

> **There is exactly one `Application.Quit()` in the assembly**
> (`PanelHandler`, on the `036_Quit` button), it is a bare call, and
> **nothing in the assembly implements `OnApplicationQuit`.** No
> `RebuildScenes`, no `Exit*Scenario`, no `SaveAll`.

So quitting from a live campaign or battle runs no teardown path whatsoever.
Two consequences:

- **A teardown hook on `RebuildScenes` never sees the last session of a play
  session.** Confirmed on a live run — the log ended mid-battle with no
  teardown. The fix is `Application.quitting`, Unity's own event, which fires
  before the `OnDestroy` cascade so a subscriber still sees live objects.
- **Anything a mod persists must not wait for a session to end.** For the
  ModApi's side-car saves this means hooking `SceneManagement.SaveAll`, the
  player's own explicit save, rather than session teardown.

## Open questions

- Where `Policy.InitializeStatics` is called from in the load order, since
  registering a policy has to happen before it. The campaign boot sequence
  ([10-simulation-tick](10-simulation-tick.md)) applies policy effects at
  frame 8, so initialization is earlier than that.
- Whether the per-battlefield additive scene can be reloaded without a full
  rebuild (it is loaded by `BattlefieldSetup`, not `SceneManagement`, which
  hints at a different lifecycle).
- Which `GameObject` each of `CampaignController` and `BattleController` sits
  on. Neither is fetched via `Find("GameController")` — both use
  Inspector-assigned references instead — so their host object is unconfirmed.
