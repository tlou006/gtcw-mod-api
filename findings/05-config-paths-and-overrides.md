# 05 — Config paths and the override mechanism

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GamePrefs`, and the ~121 `StreamReader`
sites across 39 types
**Summary:** [summary-data-layer](summary-data-layer.md)

This confirms from code what [07-config-data-model](07-config-data-model.md)
inferred from the official ModdingTool workbook and Quick Guide.

## Paths are relative string literals

There is no path-resolution service, no virtual file system, no asset
abstraction. Config files are opened by **literal relative path**, resolved
against the process working directory (the game install root).

Confirmed literals:

| Path | Referenced from |
| --- | --- |
| `Config/aistrategies.dat` | `AIBattle.StrategiesImportFileName` |
| `Config/actions.dat` | `DLC_WL.ConfigFileNameActions` |
| `Config/items.dat` | `DLC_WL.ConfigFileNameItems` |
| `Config/dlcwl_rankings.dat` | `DLC_WL.ConfigFileNameRankings` |
| `Config/resources.dat` | `Economy` |
| `Config/armynames{i}.dat` | `ArmyNames` — `{i}` is the nation index |
| `Config/unithierarchydescr{n}.txt` | `DLC_WL`, `{n}` = `GamePrefs.commandhierarchyshift` |
| `Config/avatar.dat`, `Config/armygroupcols.dat` | `GamePrefs` |
| `Config/Filters.dat` | `Filter` — **written as well as read** |
| `Config/ui.txt`, `Config/ui_battle.txt` | `GamePrefs` |
| `Config/unitprefs.txt`, `Config/battleprefs.txt`, `Config/campaignprefs.txt` | `GamePrefs` |
| `Config/debug.txt` | `GamePrefs` |
| `Config/questionnaire.dat`, `Config/tooltips_dlcwl.txt`, `Config/dlcwl_config.dat` | `DLC_WL` (existence-checked) |
| `Config/` (commanders) | `GameVars.import_commanders("Config/")`, called from `BattleController` |
| `Config/<DataFile>` | `GameGuide`, filename from a field |

Only two sites touch `Application.dataPath` at all (`BattlefieldSetup`,
`VideoScreen`), and none of the config loading does.

> **This is good news for modding.** The data layer is plain `System.IO`
> against relative paths. It is interceptable with ordinary Harmony hooks on
> the reader methods — or even at the file-system level — and needs no
> Unity-specific machinery, no `ResourceManager` shim, no asset bundle work.

Note that `Config/Filters.dat` is *written* by the game (`Filter` saves UI
filter state there), so `Config/` is not a read-only directory. A mod must not
assume everything under it is authored content.

## The override mechanism is whole-file replacement

The Quick Guide's "is then loaded instead of" wording is literal. The idiom
throughout `GamePrefs` is:

1. build the scenario-local path;
2. if that file does **not** exist, reassign the path variable to the
   `Config/` default;
3. open whichever path survived.

One path wins entirely. **There is no field-level merge and no third layer.**

Confirmed instances:

| Method | Scenario path | Fallback |
| --- | --- | --- |
| `GamePrefs.loadcampaigncontrols(string folder)` | `{folder}/campaignprefs.txt` | `Config/campaignprefs.txt` |
| `GamePrefs.loadunitprefs(string folder = "")` | `{folder}/unitprefs.txt` | `Config/unitprefs.txt` |
| `GamePrefs.loadcontrols()` | `Battlefields/{leveltoload}/{subleveltoload}/battleprefs.txt` | `Config/battleprefs.txt` |

```
Config/<file>   ◄── replaced wholesale by ──   <scenario>/<file>
└──────────── the game's entire two-entry chain ────────────┘
```

### The scale-dependent prefs finding, explained

`loadcontrols()` falls back to `Config/battleprefs.txt` if the battlefield
file is missing **or** if `SceneManagement.currentoperation` is `3` or `8`.

That is the mechanism behind the "scale-dependent prefs" observation in
[07-config-data-model](07-config-data-model.md): the same pref file is loaded
from a different place depending on what the game is currently doing, not only
on what files exist. A mod that patches `battleprefs.txt` values must know
which source actually won, and that depends on runtime mode.

### Why this matters for the ModApi

> **Additive, field-level config overrides are new capability, not a
> reimplementation.** Two mods that each want to change one field of
> `unitprefs.txt` cannot both win today — the second one to ship a file
> silently replaces the first. With a merge layer they can.

The override layer should slot in **after** the game's own resolution, so a
mod patch applies to whichever file actually won. Modders then reason about one
rule: *the effective value, whatever its source.*

## Open questions

- The scenario-folder path conventions are only partly pinned down here
  (`Battlefields/{level}/{sublevel}/` for battles; the campaign `folder`
  argument's origin is not traced).
- `GamePrefs` has 9 reader sites and more pref files than the three with a
  confirmed fallback. Whether *every* pref file supports scenario override, or
  only those three, is unverified — it matters for documenting what modders can
  override.
- Whether any loader writes back to `Config/` beyond `Filter`.
