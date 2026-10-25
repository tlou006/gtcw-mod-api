# Summary — How the simulation is driven

**Covers:** [10-simulation-tick](10-simulation-tick.md) and
[31-retreat](31-retreat.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Everything runs on `Update()` — **there is no fixed timestep anywhere**, and
`Regiment.FixedUpdate` is an empty stub. A single global frame counter,
`GameVars.frame`, is incremented in exactly one place (`MainMenuControls`), and
the controllers' `Update` methods are **boot state machines** (~50 numbered
frame gates) rather than simulation. The actual per-frame work lives in
`BattleUnits.Update`, which serves both the campaign and battle scenes by
branching on `IsCampaign`, and in `Regiment.Update` per unit. Expensive
campaign work is gated on **accumulated game time** against a `GamePrefs`
value, cheaper work is staggered by `GameVars.frame % N`, and the clock itself
comes from UniStorm — the third-party weather asset, which holds the only
`Time.deltaTime` in the simulation path. One game day is **240/gamespeed real
minutes in a battle** and **2.5/gamespeed in the campaign**: the two scenes read
the same `GamePrefs` field from two different files.

## The findings

| | Finding | Consequence |
| --- | --- | --- |
| 1 | **No fixed timestep.** 192 of 211 game types implement `Update()`; only two implement `FixedUpdate` and one of those is empty. | Tick cadence is frame-rate dependent. A patch on a per-frame method costs frame rate × unit count. Don't assume determinism across machines. |
| 2 | **One global frame counter**, `GameVars.frame++` in `MainMenuControls.Update`, immediately after the batch queue drains. | The only clock a plugin can trust for ordering — but see the rewind below. |
| 3 | **Controllers boot, they don't simulate.** `CampaignController.Update` has 55 `if (frame == N)` gates, `BattleController.Update` 48, spanning frames 1–51. | This is the **authoritative initialization order**, i.e. the answer to "when is it safe to touch X?" |
| 4 | **`BattleUnits.Update` is the real driver**, split on `IsCampaign`; `Regiment.Update` (458 lines) is the per-entity tick. | One hook point covers both scenes — but it must respect the flag. |
| 5 | **Five scheduling mechanisms**: game-time thresholds, frame-modulo staggering, config-driven iteration counts, readiness gates, and a day-counter rollover test. | Each is a different hook point with different semantics. Copy the game-time pattern, not the modulo one. |
| 6 | **Game time comes from UniStorm**, a third-party asset, and **`Time.deltaTime` has no consumer in any simulation class** — 113 of its 155 occurrences are inside UniStorm itself. | Changing time flow means touching `UniStormWeatherSystem_C`. `GameVars.SetGameSpeed(float)` is the better hook. A slow machine samples the same war less often; it does not run a slower war. |
| 7 | **The batch queue is 16 named ids** with a frame-delay offset and **no default branch**. | A mod can carry its own deferred operations through the game's queue and inherit its ordering and pacing. |
| 8 | **Battle and campaign do not share the day length.** `battleprefs.txt` sets `battledaylengthinmins = 120`, `campaignprefs.txt` sets the same field to **1.25**, and the campaign loader runs second. | A game day is **240/gamespeed real minutes in a battle** and **2.5/gamespeed in the campaign** — 12 real minutes vs **3.0 real seconds** at each ceiling. |
| 9 | **The battle night is a seven-state machine** that fast-forwards at `SetGameSpeed(1000f, overrideminmax: true)` and pauses at dawn, leaving `gamespeed == 1`. | The only place the speed clamp is deliberately bypassed, and the reason your afternoon speed setting is gone the next morning. |
| 10 | **Pause stops the clock, not the frame.** `gamepaused` has thirteen readers; every frame-paced rolling sweep in the campaign branch keeps running. | A paused campaign keeps converging its derived panels. Clock-gated work cannot fire, because the clock is stopped. |
| 11 | **The speed ladder is a UI convention.** The +/− keys step by 1.0, so every integer speed up to the array tail is reachable and the panel highlights none of the off-ladder ones. | A modder changing the ladder arrays has not changed the reachable speed set. |

## Load-order milestones worth knowing

From the campaign boot sequence:

| Frame | Milestone |
| --- | --- |
| 3 | Map init — **the sequence stalls here until the map is built** |
| 8 | `Policies.UpdatePolicyEffects(..., startsetup: true)`; full UI init |
| 11–12 | Regiments initialized (`allregimentsinitialized`) |
| 25 | All saved subsystems loaded |
| 30 | Ownerships, frontlines; **`currentoperation = 2`** — campaign is live |
| 37 | `Projects.UpdateProjectEffects()` |
| 50 | Chapter update; game speed set to 0 (paused, ready) |

So a policy effect must be registered **before frame 8**, and anything reading
units must wait until **after frame 12**.

## Three traps

- **`GameVars.frame` rewinds during load.** `if (BFS.initialized < 3 &&
  GameVars.frame > 3) GameVars.frame = 3;` holds the sequence until the map is
  ready. It is a *program counter for the boot sequence*, not "frames since
  start". Code that treats it as monotonic will be wrong exactly on slow map
  loads — the hardest case to reproduce.
- **The batch queue drains before the increment**, so an operation scheduled
  for frame N runs while `GameVars.frame` still reads N−1.
- **Modulo staggering keys off the display frame rate**, so `% 85` is a
  different real-world interval on every machine. `MainMenuControls.Awake` sets
  `Application.targetFrameRate = 60`, which bounds it — but Unity ignores that
  cap whenever the player has VSync on, so a 144 Hz monitor runs every
  frame-paced subsystem 2.4× faster.

## For the ModApi

- **Schedule deferred work through `SceneManagement.batchoperation`** rather
  than starting coroutines — it inherits the game's ordering and pacing. The
  `if`-chain dispatch has no default, so a mod-defined id passes through
  harmlessly and a postfix on `UpdateBatchOperations` can pick it up. Arguments
  are boxed `object`s cast unchecked, so a mod id must own its own argument
  discipline.
- **Expose load-order events from the boot sequence**, not raw frame numbers.
  Modders should ask for `OnUnitsReady`, not `frame >= 12`.
- **Rate-correct, don't tick-correct.** `Economy.UpdateEconomyEstimates(num2)`
  takes the elapsed delta; that is the pattern to copy, because cadence is not
  stable.
- **`GamePrefs.updatetimeforcampaigndata` is the macro-economy tick interval**
  and is already a config value — a mod changing it changes how often a large
  part of the campaign simulation runs.

## And one of the things that keeps running is the retreat queue

[31-retreat](31-retreat.md) opens `Retreat`, the one campaign subsystem no
earlier file had touched. `Retreat.WorkDownRetreatOrders()` is called every
frame from `BattleUnits.Update` and is one of the three things that keep going
while the game is paused — so every rout, withdrawal and depot fallback in the
campaign is resolved by a queue that the pause does not stop.

Two things in it reach beyond the retreat system itself.

> **Holding the right mouse button suspends every pending retreat.** The guard
> `if (retreatorder == null || Input.GetMouseButton(1) || GameVars.frame < 50)
> return;` sits at the top of the pump and again at the top of the obstacle
> preparation, so a player panning the camera freezes both sides' retreats for
> as long as the drag lasts. It is the only place in the repo so far where an
> input state gates a simulation process.

> **`Retreat` owns the only mechanism that makes units physically block
> movement.** It enables a `navmeshobstacle` on every unit of the alliances
> that are *not* pathing, sizes it at `blockrangecampaign` — a hardcoded
> **30f** static with no loader — capped by that unit's own distance to its
> nearest enemy, and switches the whole set off again afterwards.
> `AICampaign` borrows the same machinery for its campaign movement orders
> through a single global `currentlyactivated` slot, and every claim or release
> calls `IncreaseAllOrdersFrames`, which pushes **every** pending retreat back
> two frames. So retreat latency is coupled to AI movement traffic.

The destination search itself is sound in design and has one severe defect:
with no friendly town, fort, depot, trade node or unrouted friendly unit in
range, it returns **the nearest enemy's position**, and four of its six call
sites pass an argument that makes the disintegrate check miss that — so **a
cut-off unit ordered to withdraw marches at the enemy at double-quick**
([31](31-retreat.md) §4.1).

## Still open

- `BattleController`'s 48 frame gates are not transcribed; the battle boot
  order is known to exist but not documented step by step.
- The full `GamePrefs` tick-tuning surface beyond the seventeen clock intervals
  and eleven frame periods now tabulated in [10](10-simulation-tick.md) §10.
- Whether Unity's `maximumDeltaTime` is overridden in the project settings. At
  its 0.333 s default a single long frame loses game time, and at campaign
  speed 50 that is 11 % of a game day. The answer is in `globalgamemanagers`
  and needs an asset dump.
- Whether any frame-paced rolling sweep besides the historical-event cursor can
  fail to complete inside its reset window at campaign speed 50.

`UpdateEndOfMonthFunctions`, `eodcycle` and whether `Autocalc` shares
`Regiment`'s combat model are all now answered — see
[10](10-simulation-tick.md) §9, §8 and the closing note.
