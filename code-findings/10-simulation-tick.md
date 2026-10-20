# 10 — The simulation tick

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `MainMenuControls`, `CampaignController`,
`BattleController`, `BattleUnits`, `Regiment`, `SceneManagement`
**Summary:** [summary-simulation](summary-simulation.md)

This closes the last open item in `plan.md` Phase 1. The static structure was
mapped in [01](01-assembly-partition.md)–[03](03-scene-wiring.md); this is how
it is *driven*.

## Finding 1 — there is no fixed timestep

Across all 211 game types, exactly **two** implement `FixedUpdate`:
`HarvestCheck` and `Regiment`. And **`Regiment.FixedUpdate` is an empty
stub** — a leftover, not a tick.

> **The entire simulation runs on `Update()`**, at the display frame rate, with
> no fixed-timestep path at all.

192 of the 211 game types implement `Update()`. Seven implement `LateUpdate`
(`BattleUI`, `Camera_Battlefield`, `CampaignMainPanel`, `Coy`, `Filter`,
`FleetInfoPanel`, `Saving`).

Consequences:

- **Tick cadence is frame-rate dependent.** The game compensates with
  game-time deltas (`timediff` parameters) and accumulated-time thresholds
  rather than by fixing the step, so *rates* are mostly frame-rate independent
  even though *cadence* is not.
- A Harmony patch on a per-frame method runs at display frame rate. On a
  144 Hz machine that is ~2.4× the work per second versus 60 Hz. Anything a
  mod adds to `Regiment.Update` is multiplied by the unit count **and** the
  frame rate.
- Determinism across machines should not be assumed anywhere.

## Finding 2 — one global frame counter, pumped from the menu controller

```
MainMenuControls.Update()
  ├─ SceneManagement.UpdateBatchOperations()   // drain the deferred queue
  └─ GameVars.frame++                          // the only increment, anywhere
```

`GameVars.frame` is incremented in exactly **one** place: `MainMenuControls`,
which lives on `MainGameController` in the `Main` scene. It is a monotonic
global frame counter — except where the boot sequence deliberately rewinds it
(below).

The ordering matters: the batch queue is drained **before** the increment, so
an operation scheduled for frame N runs on the frame where `GameVars.frame`
still reads N−1.

## Finding 3 — three tiers of tick

### Tier 1: the boot state machine

`CampaignController.Update` and `BattleController.Update` are **~95%
initialization**, written as a long chain of `if (GameVars.frame == N)` blocks
— 55 frame gates in the campaign controller, 48 in the battle controller,
spanning frames 1–51.

This is the game's load sequence, one step per frame, with `LoadingScreen.CallState()`
interleaved to advance the progress bar. A sample of the campaign sequence:

| Frame | What happens |
| --- | --- |
| 1 | Game speed reset; scenario start date; splatmaps |
| 2 | DLC init; objectives; historical events import |
| 3 | `InitCampaignMap1` / `InitCampaignMap2` (gated — see below) |
| 4 | `OverwriteNationsData` when loading a save |
| 5 | Fog of war init, both sides |
| 7 | `InitCampaignBattleUnits` |
| 8 | `Policies.UpdatePolicyEffects(..., startsetup: true)` for both alliances; full UI init |
| 11–12 | `initializeregiments` → `allregimentsinitialized` |
| 13 | `GameVars.ResetPopulationValues()` — **only when `currentoperation == 0`**, i.e. a new campaign; a loaded save keeps its saved `population` ([23](23-game-state.md) §5.1) |
| 21 | `DiaryReference.Initialize()` |
| 25 | Load every saved subsystem (buildings, garrisons, spottings, towns, charts, railroads, AI, weather) |
| 30 | Initial ownerships; frontlines; **`currentoperation = 2`** (campaign now running) |
| 31–34 | Subsidies, weapon stock, campaign data runs |
| 37 | `Projects.UpdateProjectEffects()` |
| 41 | Commander promotions; recruit pool; depot stock |
| 49–50 | Tutorial; chapter update; AI personalities logged |

> **This is the authoritative initialization order**, and therefore the answer
> to "when is it safe for a plugin to touch X?" Registering a policy effect,
> for instance, must happen before frame 8; anything that reads regiments must
> wait until after frame 12.

**The counter can rewind.** `if (BFS.initialized < 3 && GameVars.frame > 3)
GameVars.frame = 3;` holds the whole sequence at frame 3 until the map
finishes building. So `GameVars.frame` is **not** a reliable monotonic clock
during load — it is a program counter for the boot sequence, and a plugin that
treats it as "frames since start" will be wrong.

### Tier 2: the steady-state subsystem driver

`BattleUnits.Update` is the real simulation driver, and it serves **both**
scenes by branching on its own `IsCampaign` flag.

Campaign branch, per frame: retreat orders, camp update, frontline/railroad
reset, commander histories (×10 per frame when unpaused), AI defensive-move and
fort-construction orders, optional sea-invasion and raid forces, filter maps,
support filter, economy flow lines, historical events, diary, transport
capacities, newspaper queue, end-of-month functions.

Battle branch, per frame: weather checks, time issues, side stats and AI task
updates, commander and unit rankings, left guns, DLC commander relations and
incidents, reinforcement arrivals, autosave check.

### Tier 3: per-entity

`Regiment.Update` is 458 lines and runs **per unit**. `Regiment` also caches
its scene lookups in `UpdateReferences()`, which resolves
`GameObject.Find("GameController")` three times plus `UI`, `SoundEngine` and
`Battlemap/Units`, branching on `calledfromcampaign` and `bunits.IsCampaign`.

The next-largest per-frame bodies: `BattleUI` (269), `BattleFront` (162),
`MainMenuControls` (88), `DebugInfo` (78), `Autocalc` (68).

## Finding 4 — four distinct scheduling mechanisms

The game does **not** tick everything every frame. Four patterns, all worth
knowing because each is a different hook point:

1. **Accumulated game-time thresholds.** The expensive campaign work is gated
   on elapsed *game* time, not frames:

   ```
   num2 = campaignFloatingDate - campaigndataupdate;
   if (num2 > GamePrefs.updatetimeforcampaigndata) { ... }
   ```

   Inside that gate: `UpdateAllianceData`, `GameVars.UpdateNationsData`,
   `Economy.UpdateMacroEconomy`, `Economy.UpdateEconomyEstimates(num2)`,
   `UpdateWorkforceFluctuation`, `UpdateCommanderInChief`,
   `Policy.CheckForChapterUpdate`, objective accomplishment checks, railroad
   construction, ship imports and production capacity.

   > **The macro-economy tick interval is a config value.** `GamePrefs`
   > exposes `updatetimeforcampaigndata`, so the simulation's coarse cadence is
   > already data-tunable — and a mod that changes it changes how often a large
   > part of the campaign simulation runs.

   Note `UpdateEconomyEstimates(num2)` receives the actual elapsed delta, so it
   is rate-correct rather than per-tick-correct. That is the pattern to copy.

2. **Frame-modulo staggering.** Cheaper or less urgent subsystems are spread
   across frames by `GameVars.frame % N`:

   | Modulo | Work |
   | --- | --- |
   | `% 6` | `Diary.CheckDiaryEventFireUp` |
   | `% 17` | `Diary.UpdateEventQueue` |
   | `% 30` | `DLC_WL.CommanderRelations.UpdateOperations` (battle) |
   | `% 40` | `DLC_WL.UpdateIncidents` (battle) |
   | `% 85` | `DLC_WLRef.CheckForIncidents` (both) |
   | `% 100` | `Vessel.UpdatePlayerShipMessages` |

   These are co-prime-ish values picked to avoid clustering. **Because they key
   off the display frame rate, the real-world interval varies with
   performance** — one more reason not to expect determinism.

3. **Config-driven iteration counts.** Some work is done N times per frame,
   with N from config: `for (k = 0; k < GamePrefs.workforcemapupdates; k++)
   Economy.UpdateFilterMaps();`. Also `UpdateCommanderHistories()` ×10 per
   frame when unpaused.

4. **Frame gates on readiness.** `if (GameVars.frame > 28)`,
   `if (GameVars.frame >= 50)` and similar guard work that must not run during
   load.

## Finding 5 — game time comes from the weather asset

Campaign date and time are read from **UniStorm**, the third-party weather and
time-of-day package ([01-assembly-partition](01-assembly-partition.md)):

```
GameVars.currenttimefromstart = f(uniStormSystem.yearCounter,
                                  monthCounter, dayCounter, Hour)
GameVars.currentmonth    = uniStormSystem.monthCounter - 1   // clamped 0..11
GameVars.currenthourofday = uniStormSystem.Hour
```

`BattleUnits.Update` also watches `uniStormSystem.yearCounter` for rollover to
increment `year` and `GameVars.year`, and `Newspaper.CheckHistoricalEvents` is
called with UniStorm's day/month directly.

> **The game clock is an asset-store component, not game code.** A mod that
> wants to change time flow has to go through `UniStormWeatherSystem_C` — a
> third-party type the ModApi's Bindings layer would otherwise have no reason
> to expose. `GameVars.SetGameSpeed(float)` is the game's own wrapper and the
> better hook point.

The derivation also hardcodes the epoch (`- 1860`) and carries a
`GameVars.debugmonthadjustment` offset.

### How fast a game day actually is

`GameVars.SetGameSpeed` is also where real time meets game time, and it is one
line:

```
uniStormSystem.dayLength = uniStormSystem.nightLength
    = GamePrefs.battledaylengthinmins / gamespeed;
```

`battledaylengthinmins = 120` (`battleprefs.txt`, *"Day Length in Realtime
Minutes (calculated out of a day of 6-18h)"*), and it is set for **both** day
and night, so one full game day is **`240 / gamespeed` real minutes**. There is
no campaign override — battle and campaign share the one pref.

The speed ladders are hardcoded in `GameVars`:
`gamespeedbattles = {1, 2, 5, 20}` and `gamespeedcampaigns = {1, 5, 20, 50}`,
and `SetGameSpeed` clamps to `GamePrefs.mingamespeed` (**0.1**) and to the
**array tail** — 20 in battle, 50 in the campaign. So the shortest game day is
**4.8 real minutes** at campaign speed 50.

> **`GamePrefs.maxgamespeed` has no consumer anywhere in the assembly.** The
> config file exposes *"Maximum Gamespeed 10"* and nothing reads it; the real
> ceiling is the array tail. Raising the cap is a code change, not a data one.

That arithmetic is what makes
[25-newspaper-and-events](25-newspaper-and-events.md) §2.4 safe: the historical
event catalogue is swept one record per frame, and 119 records inside 288
seconds needs only 0.41 fps.

## Finding 6 — the deferred-work queue is a 16-id dispatch

`SceneManagement.batchoperation` is a `List<BatchOperation>` drained by
`UpdateBatchOperations()` once per frame. The ids are named statics:

| Id | Name | Id | Name |
| --- | --- | --- | --- |
| 0 | `bo_showmainmenu` | 8 | `bo_loadcampaignbattle` |
| 1 | `bo_hidemainmenu` | 9 | `bo_savecampaigndata` |
| 2 | `bo_resetloadingscreen` | 10 | `bo_savehistoricbattle` |
| 3 | `bo_loadhistoricbattle` | 11 | `bo_loadhistoricbattlefromsave` |
| 4 | `bo_rebuildscene` | 12 | `bo_savecampaignbattle` |
| 5 | `bo_closebattlescenario` | 13 | `bo_loadcampaignbattlefromsave` |
| 6 | `bo_loadcampaignscenario` | 14 | `bo_showblackscreen` |
| 7 | `bo_exitcampaignscenario` | 15 | `bo_hideblackscreen` |

**`startoffset` is a frame delay:** an operation runs once
`GameVars.frame >= framecount + startoffset`. Callers chain operations by
assigning increasing offsets, e.g. the campaign quit path queues
`showblackscreen` (0), `exitcampaignscenario` (1), `rebuildscene` (2),
`showmainmenu` (2), `hideblackscreen` (2).

Dispatch is a **chain of independent `if` statements, not a `switch`, and has
no default branch** — so an unknown operation id is silently ignored. That is
the same open-at-the-top-end property as the effect dispatch
([09](09-policy-and-project-effects.md)), and it means a postfix on
`UpdateBatchOperations` could carry mod-defined operations through the game's
own queue.

> **This is the right place for a mod to schedule deferred work**, rather than
> starting its own coroutines: it inherits the game's ordering and its frame
> pacing. Note the arguments are boxed `object`s cast unchecked, so a
> mod-defined id must own its own argument discipline.

## Open questions

- `BattleController.Update`'s 48 frame gates are not transcribed here; the
  battle boot order is known to exist but not documented step by step.
- `UpdateEndOfMonthFunctions` and `eodcycle` imply a month/day boundary
  mechanism distinct from the four scheduling patterns above. Worth tracing —
  monthly economy effects are a likely mod target.
- The full `GamePrefs` tick-tuning surface. `updatetimeforcampaigndata`,
  `workforcemapupdates`, `fireblockupdatespercycle` and
  `modelprefupdatecycle` are confirmed; there are probably more.
- Whether `Autocalc` (auto-resolve) runs off this tick at all, or resolves in
  one shot — and whether it shares the combat model with `Regiment`. Still the
  most important open question for a combat mod.
- `Regiment.FixedUpdate` being an empty stub suggests the simulation was once
  fixed-step. Not worth chasing, but it means physics-adjacent code may still
  assume a fixed step somewhere.
