# Summary — How the simulation is driven

**Covers:** [10-simulation-tick](10-simulation-tick.md)
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
comes from UniStorm — the third-party weather asset.

## The findings

| | Finding | Consequence |
| --- | --- | --- |
| 1 | **No fixed timestep.** 192 of 211 game types implement `Update()`; only two implement `FixedUpdate` and one of those is empty. | Tick cadence is frame-rate dependent. A patch on a per-frame method costs frame rate × unit count. Don't assume determinism across machines. |
| 2 | **One global frame counter**, `GameVars.frame++` in `MainMenuControls.Update`, immediately after the batch queue drains. | The only clock a plugin can trust for ordering — but see the rewind below. |
| 3 | **Controllers boot, they don't simulate.** `CampaignController.Update` has 55 `if (frame == N)` gates, `BattleController.Update` 48, spanning frames 1–51. | This is the **authoritative initialization order**, i.e. the answer to "when is it safe to touch X?" |
| 4 | **`BattleUnits.Update` is the real driver**, split on `IsCampaign`; `Regiment.Update` (458 lines) is the per-entity tick. | One hook point covers both scenes — but it must respect the flag. |
| 5 | **Four scheduling mechanisms**: game-time thresholds, frame-modulo staggering, config-driven iteration counts, readiness gates. | Each is a different hook point with different semantics. Copy the game-time pattern, not the modulo one. |
| 6 | **Game time comes from UniStorm**, a third-party asset. | Changing time flow means touching `UniStormWeatherSystem_C`. `GameVars.SetGameSpeed(float)` is the better hook. |
| 7 | **The batch queue is 16 named ids** with a frame-delay offset and **no default branch**. | A mod can carry its own deferred operations through the game's queue and inherit its ordering and pacing. |

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
  different real-world interval on every machine.

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

## Still open

- `BattleController`'s 48 frame gates are not transcribed; the battle boot
  order is known to exist but not documented step by step.
- `UpdateEndOfMonthFunctions` / `eodcycle` — a month boundary mechanism
  distinct from the four scheduling patterns, and a likely mod target.
- The full `GamePrefs` tick-tuning surface beyond the four confirmed values.
- **Whether `Autocalc` shares the combat model with `Regiment`** or
  reimplements it. Still the most important open question for a combat mod: if
  they diverge, auto-resolve and manual battles will disagree after any combat
  change.
