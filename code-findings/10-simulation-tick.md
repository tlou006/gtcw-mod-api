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

- **Tick cadence is frame-rate dependent, but *rates* are not.** `Time.deltaTime`
  appears in **18 of the 251 decompiled classes and 155 times in all — and 113
  of those are inside `UniStormWeatherSystem_C`**. The remainder are two debug
  fps overlays (`DebugInfo`, `DebugInfoCampaign`), three cosmetic light fades
  (`Cannon_Firing`, `AerialExplosion`, `Flash`) and unreferenced asset-package
  demo code (`AI_Manager`/`AI_Unit`/`BoidComponent`/`MouseOrbitImproved`/
  `PlayerControlled`, from the MeshAnimator package, which nothing in the game
  calls).

  > **No simulation class reads real frame time at all.** Real time enters the
  > game in exactly one place — UniStorm's clock — and every rate in the
  > simulation is a delta of *game* time taken against that clock
  > (`GameVars.currenttimefromstart`, `GetCampaignFloatingDate()`). So a slow
  > machine does not run a slower war; it samples the same war less often.

- **The game caps itself at 60 fps — unless VSync is on.**
  `MainMenuControls.Awake` sets `Application.targetFrameRate = 60` once and
  nothing changes it. But `Options` sets `QualitySettings.vSyncCount` to 1 or 0
  from the player's VSync checkbox, and a non-zero `vSyncCount` makes Unity
  ignore `targetFrameRate` entirely. So frame-paced work runs at 60 Hz with
  VSync off and at the refresh rate with VSync on — **turning VSync on for a
  144 Hz monitor multiplies every frame-paced subsystem's rate by 2.4**.
- A Harmony patch on a per-frame method is multiplied by the unit count **and**
  the frame rate; see §10 for which subsystems are frame-paced and which are
  not.
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

`battledaylengthinmins` is set for **both** day and night, so one full game day
is **`2 × battledaylengthinmins / gamespeed` real minutes**. UniStorm's own
`Update` advances `startTime += Time.deltaTime / dayLength / 60f * 0.5f`, which
makes that arithmetic exact and the advance rate uniform — the day/night branch
only picks which of the two lengths to divide by, and the game always sets them
equal.

> **Battle and campaign do *not* share the value.** `battleprefs.txt` sets
> `battledaylengthinmins = 120` and `campaignprefs.txt` sets it to **1.25**
> — same label text, same field, parsed by two different loaders. `loadcontrols()`
> reads the battle file and `loadcampaigncontrols()` reads the campaign file
> immediately afterwards, so in the campaign scene the campaign value wins;
> `BattleController.Start` calls only `loadcontrols()`, so the battle scene gets
> 120 back. **One game day is 240/gamespeed real minutes in a battle and
> 2.5/gamespeed real minutes in the campaign** — a factor of 96.

The speed ladders are hardcoded in `GameVars`:
`gamespeedbattles = {1, 2, 5, 20}` and `gamespeedcampaigns = {1, 5, 20, 50}`,
and `SetGameSpeed` clamps to `GamePrefs.mingamespeed` (**0.1**) and to the
**array tail** — 20 in battle, 50 in the campaign. So the shortest game day is
**12 real minutes** in a battle and **3.0 real seconds** in the campaign.

| | Battle (120) | Campaign (1.25) |
| --- | --- | --- |
| speed 1 | 240 min / game day | 2.5 min / game day |
| speed 2 / 5 | 120 min | 30 s |
| speed 5 / 20 | 48 min | 7.5 s |
| speed 20 / 50 | 12 min | 3.0 s |

> **`GamePrefs.maxgamespeed` has no consumer anywhere in the assembly.** Both
> config files expose *"Maximum Gamespeed 10"* and nothing reads it; the real
> ceiling is the array tail. Raising the cap is a code change, not a data one.

That arithmetic is what bounds the historical-event cursor
([25-newspaper-and-events](25-newspaper-and-events.md) §2.4), and the corrected
campaign figure is what makes it tight rather than comfortable: a campaign game
day at speed 50 is 3.0 real seconds, i.e. `3 × fps` frames, so the 119-record
sweep needs about **40 fps** rather than the 0.41 fps the battle value implied.

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

## Finding 7 — what pausing stops, and what it does not

`GameVars.SetGameSpeed(0f)` does two things: it sets
`uniStormSystem.timeStopped = true`, which switches off the one
`Time.deltaTime` consumer that drives the clock, and it sets
`GameVars.gamepaused = true`. It assigns `gamespeed = 0` **only when
`manualset: true`** — otherwise the old speed is kept so that a later
`SetGameSpeed()` with no argument resumes it.

`gamepaused` has only **thirteen readers** in the whole assembly. The ones that
matter:

| Site | Effect of the pause |
| --- | --- |
| `AICampaign` ~5933 | the campaign AI does not run |
| `AIBattle` ~660 | the battle AI does not run — but `UpdateBlockedCrossings` and `BlockedMovingOrder.UpdateOrders` above the guard still do |
| `BattleUnits` ~1514 | `UpdateCommanderHistories()` ×10/frame stops |
| `BattleUnits` ~1572 | `Newspaper.CheckHistoricalEvents` stops |
| `Regiment` ~6495 | `CheckEnemyContactCampaign` stops |
| `Regiment` ~869 | unit animations stop advancing |
| `Newspaper` ~294 | a queued newspaper is not shown unless it set `overridepause` |
| `Vessel` ~174 | fleet updates stop |

Everything else in `BattleUnits.Update`'s campaign branch **keeps running every
frame while paused**: `Retreat.WorkDownRetreatOrders()`
([31-retreat](31-retreat.md)), `Camp.UpdateCamp()`,
the frontline and railroad-usage accumulator resets,
`AICampaign.DefensiveMovingOrder.UpdateOrders()`,
`FortConstructionOrder.UpdateFortConstructionOrders()`, the sea-invasion and
raid-force updates, `Railroad.UpdateBlinking()`,
`Economy.UpdateFilterMaps()` ×`workforcemapupdates`,
`bfs.UpdateSupportFilter()`, `Economy.UpdateFlowConnectionLines()`, all three
`Diary` entry points, and `UpdateTransportCapacities()`.

> **A paused campaign is not a frozen campaign.** The clock-gated work cannot
> fire, because the clock is stopped, but every *frame-paced rolling sweep*
> keeps sweeping — so the derived figures in the panels (workforce and support
> filter maps, transport capacity, flow lines) continue to converge while the
> player sits paused. Retreat orders queued while paused are issued; the units
> simply cannot move until the clock runs.

Two oddities in the same area:

- `Retreat.WorkDownRetreatOrders()` early-returns on
  `Input.GetMouseButton(1)` — **holding the right mouse button suspends
  retreat-order processing**, in both scenes.
- `UniStormWeatherSystem_C.InitializeUniStorm()` contains a copy of the
  per-frame time advance under `if (timeStopped)`. It is a one-shot at init,
  not a leak: the per-frame copy in `Update()` is correctly gated on
  `!timeStopped`.

## Finding 8 — the battle end-of-day cycle is a seven-state machine

This closes half of the `eodcycle` open question. `BattleUnits.CheckTimeIssues`
(called unconditionally from the battle branch of `Update`) drives
`BattleUnits.eodcycle` through states 0–6 across a nightfall:

| State | Entered when | What happens |
| --- | --- | --- |
| 0 | battle start, and after each completed cycle | normal fighting |
| 1 | `hourCounter >= GamePrefs.dayendhours[month-1]` or the day counter changes | end-of-day panel; `EatProvisionsAndForage()`, `CheckDesertionsAndMorale()`; shortcuts disabled |
| 2 | the prompt closes | `LevelAmmo()`, `ResupplyAllUnits()`, `AdjustConditionOfArmies()`; supply lines shown; **`SetGameSpeed(1000f, overrideminmax: true)`** |
| 3 | the clock passes midnight | `battlepasseddays++`; battle text and weather reset for the new date |
| 4 | the clock reaches `daystarthours[month-1]` | `SetGameSpeed(1f)` then `SetGameSpeed(0f)` — **the game pauses at dawn**; strategy panel refreshed |
| 5 | the prompt closes | readiness and enemy readiness reset to 1.0, entrenchment to 0; reinforcements arrive; routed units withdraw; deployment phase opens |
| 6 | deployment finalised | quartermaster message; every unit's `lastfatigueupdate` reset to now |

Three consequences worth stating:

- **The night is fast-forwarded at 1000×, which `overrideminmax` is there to
  allow** — it is 50× the campaign ceiling and 500× the battle one. At
  `battledaylengthinmins = 120` a full game day at 1000× is 14.4 real seconds,
  so the night costs **4.8 s in July (8 dark hours) and 9.0 s in January (15)**.
- **Dawn leaves you at speed 1.** State 4 calls `SetGameSpeed(1f)` and then
  `SetGameSpeed(0f)` with `manualset` defaulted to `false`, so `gamespeed` is
  left at 1 and only `gamepaused` is set. Whatever speed the player was using
  the previous afternoon is gone.
- **Daylight is not twelve hours.** `GamePrefs.daystarthours` /
  `dayendhours` are per-month arrays: `8–17` in January and December (9 h),
  `5–21` in July (16 h). The clock rate is uniform, so a July battle day is
  16/24 of 240/gamespeed real minutes and a January one 9/24 — **160 versus 90
  real minutes at speed 1**. The pref label, *"calculated out of a day of
  6-18h"*, describes the calibration intent, not the shipped arrays.

`eodcycle > 0` is also read at 25 other sites as "the battle is between days",
suppressing melee, routing, cohesion updates, continuous objective points,
autosave and ordinary movement.

## Finding 9 — `UpdateEndOfMonthFunctions` is a day-counter rollover detector

The other half of the open question, and it is misnamed twice over. The whole
test is:

```csharp
if (lastdayupdate > uniStormSystem.dayCounter) { … }
lastdayupdate = uniStormSystem.dayCounter;
```

The day counter only *decreases* when a month rolls over (31 → 1), so this
fires on the **first frame of a new month**, not at the end of the old one. It
is the fifth scheduling mechanism — a monotonicity break, not a threshold — and
it is independent of the four in §4.

What it does: queues the **Secretary of the Treasury's economy report** and the
**Secretary of War's intelligence report**, blinks their message buttons, and
snapshots last-month values for every alliance — `lastmonthsdebt`,
`lastmonthsprivateincome`, `lastmonthstaxrevenue`, `lastmonthmorale`,
`lastmonthinterventionprobability[2..4]`, plus clearing
`lastmonthofficerslost`, `lastmonthpoliciesresearched`,
`lastmonthprojectsresearched`, `lastmonthunitsformed` and
`lastmonthscompanyfoundings`. With the DLC active it also fires the winter- and
campaigning-season notices at `winterquartersmonthfrom` and
`winterquartersmonthuntil + 1`.

> **This is the hook point for anything monthly**, and it is the only one: the
> "last month" figures the player reads in the reports are set here and
> nowhere else. Note the order — the snapshot is taken *after* the reports are
> queued, so the report a player opens describes the month that just ended.

## Finding 10 — which subsystems are clock-paced and which are frame-paced

The distinction in §4 matters enough to tabulate, because it decides whether a
subsystem's behaviour changes with game speed or only its sampling rate.
Clock-paced intervals, converted at a year of 365.25 days:

| Interval | Pref | Value | In game time |
| --- | --- | --- | --- |
| campaign data (population, workforce, macro-economy, chapter and objective checks, railroad construction, ship imports) | `updatetimeforcampaigndata` | 0.00002 yr | **10.5 game min** |
| autocalc battle resolution | `cycleupdateforautocalcs` | 0.00004 yr | **21 game min** |
| fleet update | `fleetupdaterhythm` | 0.002 yr | 17.5 game h |
| ship-type availability (imports) | — | 0.002 yr | 17.5 game h |
| resource tax data | — | 0.005 yr | 1.83 game days |
| top toolbar + transport-capacity tooltip | — | 0.0002 yr | 1.75 game h |
| alliance data | — | 0.50 days | 12 game h |
| frontline recalculation | — | 0.0001 yr | `[deactivated]` |
| battle weather check | — | 0.05 game h | 3 game min |
| battle side stats and AI behaviour | — | 0.015 game h | 54 game s |
| battle group calculations (victory conditions) | — | 0.015 game h | 54 game s |
| battle commander rankings | — | 0.022 game h | 79 game s |
| battle unit rankings | — | 0.025 game h | 90 game s |
| battle left-guns list | — | 0.01 game h | 36 game s |
| battle cease-fire check | — | 0.01 game h | 36 game s |
| firing / reload sweep | `firingcheckupdate` | 0.0005 game h | **1.8 game s** |

Frame-paced work, with its period in frames:

| Period | Work |
| --- | --- |
| every frame | retreat orders, camp, frontline/railroad resets, AI defensive-move and fort orders, filter maps (×10), support filter, flow lines, transport capacity, diary disappearance |
| 6 / 17 | `Diary.CheckDiaryEventFireUp` / `Diary.UpdateEventQueue` |
| 15 (battle) / 25 (campaign) | `regularfunctionrunthroughcycle` — order processing, flanking, unit status, visuals, camps, fleet raids and stances |
| 30 / 40 | DLC commander operations / incidents (battle) |
| 45 (battle) / 65 (campaign) | `regularfunctionrunthroughcycleslow` — fatigue, cohesion, charge targets, supply lines, strength, raids, blockades, `CalculateFightingSpirit`, readiness actions, coordination time |
| 85 | `DLC_WLRef.CheckForIncidents` (both scenes) |
| 100 | `Vessel.UpdatePlayerShipMessages` |
| `iips.Count / iipstoupdatepercycle` | the transport-capacity rolling sweep — **1085 IIPs in the shipped 1861 save at 10 per frame ≈ 109 frames** |
| 119 | the historical-event cursor ([25](25-newspaper-and-events.md) §2.4) |

Two compensations exist, and both are partial:

- `Regiment` ~12718 widens the campaign enemy-contact search with speed:
  `num3 = Mathf.CeilToInt(Mathf.Pow(GameVars.gamespeed, 0.5f))` — the same
  square-root shape as the campaign AI's job scheduler
  ([20](20-ai-campaign.md) §2, `Max(1, FloorToInt(...))`) and the battle
  cadence asymmetry ([21](21-ai-battle.md) §1.4). Game time per frame grows
  **linearly** with speed while the search rate grows as its **square root**,
  so detection latency measured in game time grows as
  `gamespeed / ceil(sqrt(gamespeed))` — **1× at speed 1 and 6.25× at speed
  50**.
- `Regiment` ~3191 divides the fast cycle by the speed —
  `num4 = Mathf.FloorToInt(regularfunctionrunthroughcycle / GameVars.gamespeed)`
  — but **only `if (selected && GameVars.gamespeed > 1f)`**. So the unit the
  player has selected has its orders, flanking and status reprocessed on a
  speed-corrected cadence and **every other unit in the army does not**.

Nothing compensates the slow cycle, and nothing compensates the rolling sweeps.
Where the work behind a frame-paced gate integrates a game-time delta —
`lastmoraleupdate`, `lastcohesionupdate`, `lastfatigueupdate`,
`lastmovingupdate`, `lastfirecheck`, and the `ceil(fireandreloadchecks × Δ /
firingcheckupdate)` scaling in `Regiment.UpdateFiringAndReloading` — the total
is preserved and only the granularity coarsens. Where it tests a condition
(`CheckMissingOrders`, `CheckReadinessActions`, `UpdateBlockades`) the test
simply happens less often per game day.

> **One integration is not delta-exact.** `Regiment.CalculateCohesion`
> ~19558 steps cohesion with explicit Euler:
> `cohesion += (a - cohesion) * cohesionrecoveryperhour * num`, where `num` is
> the elapsed game hours. The step overshoots its target once
> `cohesionrecoveryperhour × num > 1` and oscillates above 2. Shipped, it is
> latent and comfortably so: `cohesionrecoveryperhour = 2` (×2.0 at Iron
> Discipline level 3) and `num` at battle speed 20 and 60 fps is 0.025 h, two
> orders of magnitude inside the limit. **A mod raising that rate much past 40,
> or raising the battle speed ceiling, makes the step the governing term rather
> than the rate.** The two linear terms in the same method
> (`cohesionbyfire`, `cohesioncausedbyotherunitspassing`) are exact.

Also note `Regiment.RegimentMove` ~22582 clamps the movement delta at
`num24 = 1f` — one game *hour* in a battle, one game *year* in the campaign, since
`currenttimefromstart` carries different units in the two scenes
([23](23-game-state.md)). Neither clamp is reachable at any shipped speed and
frame rate.

## Finding 11 — the speed ladder is a UI convention, not a constraint

`BattleUI.CheckShortcuts` serves speed changes from six inputs, and they do not
agree about what a speed is:

| Input | Call | Result |
| --- | --- | --- |
| `key_speed1`..`key_speed4` | `SetGameSpeed(array[0..3])` | the ladder — `{1,2,5,20}` in battle, `{1,5,20,50}` in the campaign |
| `key_increasespeed` | `SetGameSpeed(gamespeed + 1f)` | **steps by 1.0, off the ladder** |
| `key_decreasespeed` | `SetGameSpeed(gamespeed - 1f)` | likewise; from 1 this reaches 0 and **pauses** |
| `key_speed0` | `SetGameSpeed(0f)` | pause, previous speed kept |
| `key_pausegame` | `SetGameSpeed(0f, manualset: true)` and back via `gamespeedbeforekeypause` | pause, previous speed stored and restored |
| **Left Ctrl held** with a unit selected | `SetGameSpeed(0f)`, released via `SetGameSpeed()` | **pauses the game while the key is down**, for path setting |

So the +/− keys give every integer speed from 1 to the array tail, while
`SetGameSpeed`'s own panel code lights a speed button only on
`gamespeed == array[i]`. **At speed 2, 3 or 4 in the campaign no speed button
is highlighted at all** — the game is running at a rate the interface has no
way to show. The clamp still holds: `+1` from 50 lands on 50.

Two more entry points worth knowing:

- **Pausing panels.** `PanelHandler.CheckGamePausePanel` pauses on the first
  pausing panel opening and resumes on the last one closing, passing the
  player's *"stay paused when the panel closes"* option straight into
  `manualset`. With the option on, `gamespeed` is set to 0, so the stored speed
  is destroyed and a bare `SetGameSpeed()` re-pauses rather than resuming.
- **The campaign starts paused with `gamespeed == 0`.** `CampaignController`
  sets speed 0 at frame 1 and again at frame 50 with `manualset: true`.

## Open questions

- `BattleController.Update`'s 48 frame gates are not transcribed here; the
  battle boot order is known to exist but not documented step by step.
- The full `GamePrefs` tick-tuning surface. The seventeen clock-paced intervals
  and eleven frame periods in §10 are confirmed; the ~137 unverified prefs from
  the [07](07-config-data-model.md) sweep certainly hold more.
- `Regiment.FixedUpdate` being an empty stub suggests the simulation was once
  fixed-step. Not worth chasing, but it means physics-adjacent code may still
  assume a fixed step somewhere.
- Unity's `maximumDeltaTime` (default 0.333 s) clamps `Time.deltaTime`, and
  UniStorm's clock is its only simulation-side consumer, so a frame longer than
  that **loses game time**. At campaign speed 50 a 0.333 s frame is 11 % of a
  game day. Whether the project overrides the default is in
  `globalgamemanagers`, which needs an asset dump to read.
- Whether any *other* frame-paced rolling sweep besides the historical-event
  cursor can fail to complete within its reset window at campaign speed 50.
  `UpdateTransportCapacities` publishes only on wrap rather than resetting, so
  it degrades gracefully; the IIP, town and building data cycles (40, 200, 150,
  175 frames) have not been checked for a window.

Three questions this file used to carry are now closed: `UpdateEndOfMonthFunctions`
and `eodcycle` in §9 and §8, and whether `Autocalc` shares `Regiment`'s combat
model — it does not. `Autocalc.FightUnit` is a closed-form model of its own,
melee is a third and the naval autocalc of [26](26-naval-combat.md) §5 a
fourth; the land three converge on `Regiment.SufferCasualties`
([11](11-combat-resolution.md), [summary-combat](summary-combat.md)).
`Autocalc` does run off this tick, on `cycleupdateforautocalcs` (§10).
