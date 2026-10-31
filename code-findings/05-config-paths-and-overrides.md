# 05 — Config paths and the override mechanism

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `GamePrefs`, and the 121 `new StreamReader`
sites across 40 types
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
| `Config/` (commanders) | `GameVars.import_commanders("Config/")`, called from `BattleController` and `MainMenu` |
| `Config/<DataFile>` | `GameGuide`, filename from a field |

Nothing reads `Application.dataPath`. The only absolute paths are three
`Application.streamingAssetsPath` asset-bundle loads (`BattlefieldSetup` for
the campaign and battle maps, `VideoScreen` for videos), and none of the
config loading uses them. An earlier version said two sites used
`Application.dataPath`.

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
| `GamePrefs.LoadDLCWLConfig(string folder = "")` | `{folder}/dlcwl_config.dat` | `Config/dlcwl_config.dat` |

An earlier version listed only the first three. The `folder` comes from the
caller:

| Caller | `folder` passed to `loadunitprefs` / `LoadDLCWLConfig` (and `loadcampaigncontrols`) |
| --- | --- |
| `MainMenu` (`MainMenu.cs:306–312`) | none, so `Config/`; when a scenario is selected, `LoadDLCWLConfig` alone gets that scenario's folder (`:773`) |
| `CampaignController.Awake` (`:63–66`) | `Campaigns/{leveltoload}/{subleveltoload}` |
| `BattleController` (`:61–71`), standalone battle | `Battlefields/{leveltoload}/{subleveltoload}`, or `Battlefields/DebugArmySetup` with `debug_loaddebugarmysetup` |
| `BattleController`, campaign battle (`currentoperation` 3 or 8) | `Campaigns/{currentcampaignscenario}` |

So `unitprefs.txt` and `dlcwl_config.dat` follow the battle into the
campaign folder in a campaign battle, while `battleprefs.txt` (below) drops
to `Config/`. No campaign folder ships any of the four files. Seven
battlefields ship `unitprefs.txt` (000/C, 009/A, 014/A, 020/A, 021/A, 022/A,
027/A; see [34](34-perks.md) §1.1) and five ship `battleprefs.txt` (009/A,
014/A, 021/A, 022/A, 027/A), not counting `Backup/` and `prefs/` copies the
loaders never open.

```
Config/<file>   ◄── replaced wholesale by ──   <scenario>/<file>
└──────────── the game's entire two-entry chain ────────────┘
```

### The scale-dependent prefs finding, explained

`loadcontrols()` falls back to `Config/battleprefs.txt` if the battlefield
file is missing **or** if `SceneManagement.currentoperation` is `3` or `8`.

Those two values are now decoded
([03-scene-wiring](03-scene-wiring.md)): **`3` is a fresh campaign battle and
`8` is a campaign battle loaded from a save** — i.e. exactly the two cases
where a battle is being fought inside a campaign.

> **So the rule is: campaign battles always use the global
> `Config/battleprefs.txt`; only standalone battles (`currentoperation` 1 and
> 7) honour a battlefield-local override.**

That is the mechanism behind the "scale-dependent prefs" observation in
[07-config-data-model](07-config-data-model.md), and it is sharper than
"depends on scenario scale": it depends on *how the battle was entered*. The
same battlefield fought standalone and fought from a campaign can load
different prefs. A mod that patches `battleprefs.txt` must know which source
won, and that is a runtime-mode question, not a file-layout question.

### Why this matters for the ModApi

> **Additive, field-level config overrides are new capability, not a
> reimplementation.** Two mods that each want to change one field of
> `unitprefs.txt` cannot both win today — the second one to ship a file
> silently replaces the first. With a merge layer they can.

The override layer should slot in **after** the game's own resolution, so a
mod patch applies to whichever file actually won. Modders then reason about one
rule: *the effective value, whatever its source.*

## Open questions

- ~~The campaign `folder` argument's origin.~~ **Resolved** — see the caller
  table above.
- ~~Whether every `GamePrefs` file supports scenario override.~~ **Resolved:**
  of its nine reader sites, four take an override (the table above) and five
  are fixed `Config/` literals: `debug.txt`, `ui.txt`, `ui_battle.txt`,
  `avatar.dat` and `armygroupcols.dat`.
- Whether any loader writes back to `Config/` beyond `Filter`.
