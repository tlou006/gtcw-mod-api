# Summary — Assembly architecture

**Covers:** [01-assembly-partition](01-assembly-partition.md),
[02-type-inventory](02-type-inventory.md),
[03-scene-wiring](03-scene-wiring.md),
[04-version-identification](04-version-identification.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

`Assembly-CSharp.dll` holds ~207k lines of game code in the **global
namespace**; everything namespaced is third-party asset-store code.
`Assembly-CSharp-firstpass.dll` is pure Unity Standard Assets and can be
ignored. Behaviour is concentrated in a handful of god classes — `Regiment`
(28.5k lines) above all — and the game locates its own singletons by
`GameObject.Find("GameController")`, 202 times. Lifecycle runs through
`SceneManagement`, whose static `Load*` / `Exit*` / `SaveAll` methods are the
natural event surface for a hook library. There is **no version constant in the
assembly**, so a version gate must hash the DLL.

## The four findings

| | Finding | Consequence |
| --- | --- | --- |
| 1 | **Game code = global namespace; third-party keeps its own.** ~40 third-party types leaked into the global namespace and must be excluded by name (UniStorm, EasyRoads3D, SimpleLOD). | Gives a clean filter for every later search. The `C`-prefix hypothesis was **wrong** — only `CBuilding` and `CBuildingIcon` use it. |
| 2 | **Behaviour is concentrated.** `Regiment` 28.5k, `BattleUnits` 15.5k, `GamePrefs` 10.5k, `GameVars` 8.4k lines. | Hook targets are few and obvious. `Regiment` doubles as the battlefield *and* campaign unit, so it is unavoidable. |
| 3 | **Singletons by `GameObject.Find`, lifecycle via `SceneManagement`.** `GameController` carries `BattleUnits`, `BattlefieldSetup`, `BattleUI`, `Economy`, `SpottedEnemy`, `Smoke`, `FogOfWar`, `AIBattle`. | Bindings need no reflection — but `GameController` is **scene-scoped**, so bindings must resolve per scene load, not at plugin load. |
| 4 | **No version constant.** The game parses its own version out of a `TextMeshProUGUI` label (`UICampaign/VersionID`) into a `static float`. | Hashing `Assembly-CSharp.dll` is the **only** viable version gate. Read the UI value only to phrase a mismatch for the player. |

## What it changes in the plan

- Phase 1's `C`-prefix task is **done and was wrong**; replaced by the
  namespace rule plus an exclusion list.
- The `campaignbase/gamecontroller` anchor from the ModdingTool is
  **confirmed**, with a concrete component list.
- Phase 3's version gate **must** hash the assembly — the alternative does not
  exist. This is what the signature manifest was already for.
- `SceneManagement` gives Phase 4 its event surface for free.

## Traps worth remembering

- **`EntryPoint` is not a program entry point** — it is a battlefield
  reinforcement marker. There is no `Main`.
- **`*_C` means third-party**, not "game class" — it is UniStorm's own suffix,
  the inverse of what `CBuilding` suggests.
- **`GameController` is a different object per scene**, but only one ever
  exists at a time — every transition tears the hierarchy down via a
  non-additive `LoadScene("Main")`. So a binding held across a transition does
  not go stale, it points at a **destroyed** object. Resolve after each
  scenario load, discard on `bo_rebuildscene`.

## Still open

- Whether `Autocalc` shares code, or only parameters, with the live battle
  model — a combat mod has to change both.
- Which `GameObject` hosts `CampaignController` and `BattleController`. Neither
  is fetched via `Find`, so it is unconfirmed.
- Where `Policy.InitializeStatics` sits in the load order.

## Resolved since first writing

- ~~The per-frame simulation tick has not been located.~~ See
  [summary-simulation](summary-simulation.md).
- ~~The long tail of game types is unclassified.~~ Classified by evidence in
  [02-type-inventory](02-type-inventory.md) — and the useful result is that the
  buckets **overlap heavily**, because simulation, UI and parsing routinely
  live in one type.
- ~~`SceneManagement.currentoperation`'s value set.~~ All nine values decoded
  in [03-scene-wiring](03-scene-wiring.md).
