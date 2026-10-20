# 08 — The save/load surface

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `ImportExportUnitData` (7,552 lines, 29
`StreamReader` sites)
**Summary:** [summary-data-layer](summary-data-layer.md)

`ImportExportUnitData` is the serialization layer. It is organised as paired
`Save*`/`Load*` methods — or `Create*`/`Overwrite*` where the write path
generates a fresh file — one pair per subsystem, almost all taking a single
`folder` string.

## The subsystem pairs

`ShipTypeData`, `WeaponsData`, `TechnologyData`, `NationsData`
(`Save`/`Overwrite`), `ScenarioFile` (`Save`/`Load`), `UnitFiles`
(`Create`/`Overwrite`), `AdditionalUnitData`, `AdditionalCommanderInfo`,
`GarrisonReferences`, `Buildings`, `Commanders`, `Spottings`, `RailroadData`,
`IIPAndTownData`, `ChartDatabases`, `BattleObjectives`, `BattleEntryPoints`,
`Pontoons`, `BattleBuildings`, `FrontlineData`, `Obstacles`, `BattleUnitData`,
`Artillery`, `BattleSideInfo`, `LastGamePlayed`.

The `Save`/`Overwrite` rather than `Save`/`Load` naming on `NationsData` and
`UnitFiles` is a hint: those paths mutate existing live objects in place
instead of constructing new ones, which is a different and more fragile
operation to hook.

## Directly useful members

| Member | Why it matters |
| --- | --- |
| `CheckFileIntegrity(string loadname)` | **The game's own validator.** Read before writing ours — it defines what the game considers a loadable file. |
| `CreateVersionFile(string folder)` / `GetVersionFromFile(string folder)` | Save-data versioning, as a `float`. Note this is the *save format* version, **not** the game build version — see [04-version-identification](04-version-identification.md). |
| `ImportWeaponStock(string path)` | Takes an explicit path rather than a `folder`, consistent with `weaponstock.dat` being a campaign-scenario file rather than a `Config/` file — the retiering recorded in [07-config-data-model](07-config-data-model.md). |
| `LoadAdditionalUnitData(string folder, int mode = 0, bool skipworkdownstoredpaths = false)` | A `mode` parameter worth decoding; `skipworkdownstoredpaths` suggests pathfinding data is replayed on load. |
| `GetAllianceFromScenarioFile(string loadname)` | Reads one field without a full load — a cheap probe. |
| `LoadScenarioFile(string loadname, bool updaterealismoptions = false)` | Returns a `string`; the realism-options flag is a migration path. |
| `LoadBattleSideInfo`, `GrabRegimentFromFile(StreamReader file)` | The unit-level read primitives. |
| `ExportBattleResultData(...)` | A 40-parameter method — the full battle outcome record. An excellent map of *what the game considers a battle result*: casualties by side and state, morale and supply averages, commander fame/defamed/KIA/WIA lists, captured weapons, sunken ships by name, top units, hours fought, days passed. |

`ExportBattleResultData`'s parameter list is worth reading as documentation in
its own right — it enumerates every quantity the game tracks about a battle,
which is most of what a stats or after-action mod would want.

## The per-object serialization contract

Individual `MonoBehaviour`s implement their own pair and are driven by the
methods above:

```csharp
public void Load(StreamReader file)
public void Save(StreamWriter file)
```

`EntryPoint` is the minimal readable example — three fields, three lines, read
and written in the same order. `Tools.Date` follows the same pattern with
`Save` / `Load` / **`SaveNull`** — the last being how an absent date is written
so the line count stays stable.

Position-based reconciliation helpers exist for rejoining saved data to live
objects:

- `GetGameObjectFromPosition(List<GameObject>, StreamReader, float accuracy = 0.5f)`
- `GetRegGameObjectFromPosition(...)`
- `GetBuildingFromPosition(Building[], Vector3, float accuracy = 1f)`
- `GetTownFromData(string townname, Vector3 position, List<Town> alltowns)`
- `GetIIPFromList(...)`, `GetCampaignBuildingFromList(...)`,
  `GetCampaignRegimentFromList(...)`

> **Objects are identified by position with a float tolerance**, not by a
> stable id. That is a significant constraint: a mod that moves anything, or
> that adds objects near existing ones, risks the loader binding saved state to
> the wrong object. Anything the ModApi persists should carry its own
> identifier rather than relying on this.

## Implications for the ModApi

**This is the game's serialization contract, and any type the ModApi adds to a
save must honour it** — ordered lines, a stable line count, `SaveNull` for
absent values.

Three distinct hooking strategies, in increasing risk:

1. **Side-car files.** Write mod state to our own files keyed off the save
   folder, hooking `SceneManagement.SaveAll` and the `Load*` entry points.
   Nothing in the game's format changes, so a save stays loadable with the mod
   uninstalled. **Strongly preferred default.**
2. **Appending to existing files.** Tempting and fatal — the readers are
   positional and will either throw or silently misread. Avoid.
3. **Wrapping a `Save*`/`Load*` pair** to inject extra records. Only viable
   with a count header to adjust, and it makes saves mod-dependent.

`CheckFileIntegrity` is the thing to test against, whichever route is taken.

## Open questions

- What `CheckFileIntegrity` actually checks — field counts, parse success, or
  cross-references. Determines how much validation we must add ourselves.
- The `mode` parameter of `LoadAdditionalUnitData`.
- Whether save files carry the game build version anywhere, or only the float
  save-format version. Affects whether a save made with a mod can be detected
  as such.
- Save-folder layout and naming (autosave vs. manual, campaign vs. battle) —
  needed before side-car files can be placed correctly.
