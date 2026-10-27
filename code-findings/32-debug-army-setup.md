# The debug army setup, and what `debug.txt` can still do

**Covers:** `GamePrefs.loaddebugs`, `GameVars.import_nations`,
`BattleUnits.import_units` / `import_battledata` / `ImportShips`,
`Battlefields/DebugArmySetup/`
**Build:** see [00-build-provenance](00-build-provenance.md)

Opened while building the hook library's test fixture. `plan.md` had called
this "a deterministic test harness, for free", on the strength of the
`debug.txt` labels. The labels oversell it, and this file is what the code
actually supports.

## In one paragraph

**`debug_loaddebugarmysetup` is live, but booting into a scenario is not.**
`loaddebugs()` reads the first seven lines of `debug.txt` into a **throwaway
variable** and parses the eighth, so the "Scene Management Operation at start",
scenario and sub-level prefs are consumed only to keep the line offsets right —
exactly what their `[deactivated]` tags claim. The army-setup flag itself works
and redirects `import_nations`, `import_units`, `import_battledata` and
`ImportShips` to `Battlefields/DebugArmySetup`. That folder, however, ships
**stale**: `nations.dat` was two fields short per alliance, `regiments.dat` is
one field short per regiment, and `ships.dat` is absent while `ImportShips`
opens it with no existence check.

## 1. What the flag does, and what it does not

| `debug.txt` line | Pref | Status |
| --- | --- | --- |
| 2 | Scene Management Operation at start | **dead** — read into a discarded local |
| 4 | Scenario file to load | **dead** — same |
| 6 | Unit setup sub-file (A–Z) | **dead** — same |
| 8 | `debug_loaddebugarmysetup` | **live** |
| 10 | `debug_concealment` | live |

```csharp
// GamePrefs.loaddebugs()
string text = "";
GameVars.SetCultureInfo();
...
text = streamReader.ReadLine();   // x7 — lines 1-7, all discarded
debug_loaddebugarmysetup = bool.Parse(streamReader.ReadLine());
```

> **So there is no way to boot this build straight into a battle or campaign
> from `debug.txt`.** The flag replaces the *army data* of a scenario you load
> through the menus; it does not load the scenario. A UI-free fixture needs
> something else — a patch on `SceneManagement.LoadBattleScenario`, which the
> hook library can do itself.

**The label on line 7 is also stale.** It says the four files belong in
`Config/ArmySetup`; every call site reads `Battlefields/DebugArmySetup`
(or `Campaigns/DebugArmySetup` when `IsCampaign`). Labels are never parsed, so
the code wins — another instance of the rule from
[06-config-file-format](06-config-file-format.md).

## 2. Which files the flag actually redirects

The redirect is written out longhand at each site rather than resolved once,
with slightly different conditions each time:

| Method | Condition for the debug folder |
| --- | --- |
| `BattleUnits` boot (`import_nations`) | `debug_loaddebugarmysetup` |
| `import_units` | `debug_loaddebugarmysetup && currentoperation != 3` |
| `import_battledata` | `debug_loaddebugarmysetup && currentoperation != 3` |
| `ImportShips` | `debug_loaddebugarmysetup && currentoperation != 3 && != 6` |
| `BattleController` prefs/DLC config | `debug_loaddebugarmysetup` |
| `Briefing` | `debug_loaddebugarmysetup` |

`currentoperation == 3` is a battle launched from the campaign, which always
wins over the flag — so the debug setup only applies to a **standalone** battle.

## 3. `nations.dat` was two fields short — and no version gate saves it

[23-game-state](23-game-state.md) §3.4 already identified this file and named
the two missing fields — `volunteersadj` and `contractlength`, the last two of
each alliance block. What is added here is the confirmation by diff and the
arithmetic the fix needs.

The shipped file is `1 + 52x19 + 1 + 8x40 = 1310` lines; the current format is
`8x42`, so **1326**. Diffed against `Battlefields/000/A/nations.dat`, which has
the same 52 states in the current format: the first 40 lines align field for
field, the alliance names align 1:1 (Union, Confederacy, Great Britain, France,
Spain, Mexico, Sea Trade, Non War States), and the legacy file's last line per
block is the intervention list, which is field 40. So the additions go at the
end of each block, and a shipped battlefield file supplies the values.

§3.4 also names two other legacy files, `Battlefields/014/B` and `019/B`. Both
are unreachable — scenario discovery requires a `ScenarioDescr.txt` neither
has — so they need no fix, and the script deliberately targets only the debug
folder.

The full 42-field alliance schema is 20 scalars, `taxcaps[5]`, `debt`,
`subsidies[8]`, then `nationalmorale`, `battleswon`, `casualtiessuffered`,
`commanderinchief`, the researched-policy list, the intervention list,
`volunteersadj` and `contractlength`. (`tax`/`taxcaps` are `float[5]` and
`subsidies` is `float[8]`, from `GameVars.Alliance`.)

**`import_nations` does read a version, and it does not help here:**

```csharp
if ((versionFromFile >= 0.7403f) | !foldername.Contains("Save"))
    alliance[l].contractlength = int.Parse(streamReader.ReadLine());
```

A `Battlefields` path contains no `"Save"`, so the branch is **always** taken
and line 42 is always read. Dropping a `version.dat` declaring an old version
cannot rescue the file; the file itself has to be current. That is why
`debug_loaddebugarmysetup` throws `FormatException` in this build.

Fixed by [`scripts/fix-debug-nations.ps1`](../scripts/fix-debug-nations.ps1),
which merges the two values per alliance out of a shipped reference, verifies
the alliance names align before writing, and backs up the original. The repo
tracks the transformation, never the data.

## 4. `regiments.dat` is one field short per regiment, and here a version *does* gate

`DebugArmySetup/regiments.dat` is `1 + 21x38` lines; `000/A/regiments.dat` is
`1 + 55x39`. The extra field is version-gated inside the regiment loop:

```csharp
float num17 = ((!(num >= 0.7802f)) ? 1f : float.Parse(streamReader.ReadLine()));
```

read immediately after `lasttransferupdate`, near the end of the record. But
the gate cannot save this file either, because of how `num` is obtained:

```csharp
float num = ImportExportUnitData.GetVersionFromFile(text + "/");
if (num == 0f) num = MainMenuControls.currentversion;
```

`GetVersionFromFile` returns 0 when `version.dat` is absent — and
`DebugArmySetup` has no `version.dat` — so `num` falls back to the **current
game version**, which is far above 0.7802. The reader therefore expects the
39th line and the 38-line records shift by one from the second regiment on.

> **Unlike `nations.dat`, this one has two possible fixes:** add the field to
> each record, or drop a `version.dat` declaring something below 0.7802. The
> second is cheaper but changes `num` everywhere else it is consumed in
> `import_units`, including two comparisons whose scale is unexplained
> (`num < 14`, and `num >= grp[i].regref.unittyp && num < 16`), so bringing the
> file to the current format is the safer route.

## 5. `ships.dat` is missing and the read is unguarded

`ImportShips` opens `text + "/ships.dat"` with a bare `StreamReader` and no
`File.Exists` check. `Battlefields/000/A/` ships one; `DebugArmySetup/` does
not. Whether that throws for a land battle depends on whether `ImportShips` is
reached at all in that path, which is **not yet confirmed** — but a one-line
`ships.dat` with a count of `0` is the obvious inoculation.

## 6. Resolved: the game forces invariant culture at startup

[summary-data-layer](summary-data-layer.md) listed this as open and
"unverified and important": every `float.Parse` in the data layer is called
without a `CultureInfo`, so on a comma-decimal locale `1.5` would parse as
`15`.

**It is handled.** `GameVars.SetCultureInfo()` assigns
`CultureInfo.DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture`
to `InvariantCulture`, and it is called from `MainMenuControls.Awake()` — the
first thing the game runs — as well as from `loaddebugs`, `MainMenu`, and
`Options.SaveConfigs`.

Two caveats worth keeping:

- It sets the *default* thread culture rather than assigning
  `CurrentCulture` outright, so a thread whose culture had already been set
  explicitly would keep it. Nothing in the assembly does that.
- **A BepInEx plugin's `Awake` can run before `MainMenuControls.Awake`.** So
  the ModApi must not rely on the game having set the culture yet: any parsing
  the library does itself should pass `CultureInfo.InvariantCulture`
  explicitly, or call `SetCultureInfo` first.

## Still open

- Whether `ImportShips` is reached for a standalone land battle (§5).
- What `num`'s `< 14` / `< 16` comparisons in `import_units` mean, given `num`
  is otherwise a version like `0.7802` or the current version.
- Whether `Campaigns/DebugArmySetup` exists or is referenced only
  aspirationally — the `IsCampaign` branches name it, but no such folder ships.
- Whether `battledata.dat`'s debug copy is current. Its line count differs from
  `000/A` (188 vs 398), but the record layout has not been checked, so this is
  unknown rather than broken.
