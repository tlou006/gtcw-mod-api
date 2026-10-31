# 01 — Assembly partition: game code vs. third-party

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` and `Assembly-CSharp-firstpass.dll`,
decompiled with `ilspycmd -p`
**Summary:** [summary-architecture](summary-architecture.md)

## Scale

| | Types | Lines |
| --- | --- | --- |
| `Assembly-CSharp`, global namespace | 251 | 216,659 |
| `Assembly-CSharp`, namespaced | 224 | 31,349 |
| `Assembly-CSharp-firstpass` | 105 | 9,603 |

**Game code is ~207k lines**, after subtracting ~9.4k lines of third-party
code that also sits in the global namespace.

Counts are of the `ilspycmd -p` output: one `.cs` file per top-level type,
not counting `Properties/AssemblyInfo.cs`. An earlier version counted that
file as a namespaced type (225 types, 31,354 lines) and gave the firstpass
row as 108 types and ~15,000 lines; firstpass has 105 type files in 9,603
lines (154 type declarations counting nested types).

## The partition rule

> **Global namespace = game code. Namespaced = third-party.**

Every asset-store package the developers imported kept its own namespace
(`Gaia`, `FSG.MeshAnimator`, `Jobberwocky.GeometryAlgorithms`,
`OrbCreationExtensions`, `mattatz.Triangulation2DSystem`, `Unitycoding`,
`UnityEngine.UI`). The game's own code was written without a namespace at all.

### Correction: the `C`-prefix convention does not exist

An earlier hypothesis — that game classes are `C`-prefixed, inferred from
`CBuilding` being leaked by the ModdingTool — **does not generalise.** Only
`CBuilding` and `CBuildingIcon` carry the prefix out of 251 global-namespace
types. Do not filter on it.

> **Caveat on the `_C` suffix:** it marks UniStorm's own C# ports of its
> original scripts — it is *not* a game convention. `*_C` means third-party,
> which is the inverse of what a reader might assume from `CBuilding`.

## Exception: third-party code in the global namespace

These must be excluded by name, not by namespace. ~9.4k lines across ~40
types:

| Package | Types |
| --- | --- |
| **UniStorm** (weather / time-of-day) | `UniStormWeatherSystem_C` (3,590 lines), `UniStorm_Clock_C`, `UniStormEventSystem`, `UniStormGUIMenu`, `UniStormWeatherForecast`, `UniStormDemoUI`, `UniStormTimeEventExample`, `WeatherZoneExample_C`, `GetUniStormComponents_C`, `DamageFromUniStorm`, `SavePlayerData_C`, `PlayerVitalsExample`, `DynamicSnow_C`, `DynamicSnow_Height_C`, `Climate_Zone_C`, `RandomWeather`, `RandomWeatherOnStart` |
| **EasyRoads3D** | `RoadObjectScript`, `sideObjectScript`, `runtimeScript`, `EasyRoads3DTerrainID`, `ERVegetationStudio` |
| **SimpleLOD** | `SimpleLODClickObject`, `SimpleLODDemo`, `SimpleLODDemoCamera`, `SimpleLODShowInfo`, `SimpleLODTredMill`, `LODMaker` (1,762), `LODSwitcher`, `MeshCombiner` |
| **Misc samples / utilities** | `ExampleInteractiveVoronoi2D`, `MouseOrbitImproved`, `MouseLookDesktop`, `Triangulator`, `PlantGrowthSystem`, `HideFlagsUtility`, `DrawSolidCube`, `SurfaceScript`, `MarkAnimatedScript`, `SpawnPlayer` |

`Weather.cs` and `WeatherFunctions.cs` are the game's own wrappers *around*
UniStorm, so they are game code. Do not confuse them with the asset.

## `Assembly-CSharp-firstpass` is irrelevant

105 types, entirely Unity Standard Assets (`UnityStandardAssets.*`:
FirstPerson/ThirdPerson characters, CrossPlatformInput, Effects, ImageEffects,
Utility, Water, Vehicles.Ball) plus two UI helpers (`AlphaButtonClickMask`,
`EventSystemChecker`). **Nothing game-specific.** It was exported for
completeness and can be ignored; no other finding references it.

## Reproducing this

```
ilspycmd "game/The Civil War (1861-1865)_Data/Managed/Assembly-CSharp.dll" \
  -p -o decompiled/Assembly-CSharp
```

`-p` emits a per-type `.csproj`, which is what makes the output greppable like
a normal codebase. Output goes to `decompiled/`, which is gitignored.

## Open questions

- The long tail of the 251 global-namespace types is not yet classified into
  simulation / UI / data / AI / glue. The top ~18 are
  ([02-type-inventory](02-type-inventory.md)).
- Whether any *game* type was given a namespace (the rule assumes not; not
  exhaustively checked against the 224 namespaced types).
