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
> second is cheaper, but `import_units` is not the only reader of that
> folder's `version.dat`: `import_battledata` reads it too and, below
> **0.8811**, replaces each group's readiness value with 1 — and stores
> `readiness = value / 100`, so every deployed group would start at **1 %
> readiness**. Bringing the file to the current format is the safer route.
> An earlier version of this note pointed instead at two `num < 14` /
> `num < 16` comparisons said to be in `import_units`; they are in
> `RefreshUnitTypes`, where `num` is a unit type, not the version.

**The field is morale.** `num17` is passed as the trailing argument to
`create_regiment`, whose signature ends `bool isgarrison = false, float morale
= 1f`, and is compared against `GamePrefs.moralecracklevel` immediately after.
So the legacy branch's `1f` is full morale, and appending `1` per record
reproduces exactly what the file was written to mean rather than inventing
data — which is why this fix needs no reference file, unlike §3.

Fixed by [`scripts/fix-debug-regiments.ps1`](../scripts/fix-debug-regiments.ps1).
Two details the script has to respect, both different from `nations.dat`:
`import_units` opens this file as **UTF-8** (as it does `groups.dat`) where
`import_nations` uses `Encoding.Default`, and **neither shipped copy has a
BOM** — a BOM would be fatal, since the first line is a record count and
`int.Parse` would choke on the mark.

Record field 1 is read and discarded and appears to be a sequential index (0,
1, 2 …), with field 2 the name. Useful as a boundary check: if the record
arithmetic were off by one, a tool walking records would print indices or empty
strings where names belong.

## 5. `ships.dat` is missing, and it does not matter

`ImportShips` opens `text + "/ships.dat"` with a bare `StreamReader` and no
`File.Exists` check, and `DebugArmySetup/` ships no such file while
`Battlefields/000/A/` does. That looked like a third blocker. **It is not.**

`ImportShips` has exactly one caller, and it is in the wrong scene:

| Method | Called from | Calls `ImportShips` |
| --- | --- | --- |
| `InitCampaignBattleUnits` | `CampaignController` | **yes** |
| `InitBattleUnits` | `BattleController` | no |

So a standalone battle — the only thing the debug flag applies to, since
`currentoperation == 3` always beats it (§2) — never reads `ships.dat`. No file
is needed, and adding an empty one would be dead weight. (A count of `0` would
be valid if it were ever needed: the loop is `for (j < num)` and
`streamReader.Close()` follows immediately, with no trailing reads.)

**The campaign side of the flag is a dead end regardless.** Every `IsCampaign`
branch redirects to `Campaigns/DebugArmySetup`, and **no such folder ships** —
not in the install, not anywhere. So the flag would fail on the first file it
touched. `debug_loaddebugarmysetup` is effectively **battle-only**, whatever its
`debug.txt` label implies.

### Loading a campaign with the flag on kills the process

Not a graceful failure. Confirmed live, and worth spelling out because the game
gives **no diagnostic whatsoever**.

`CampaignController.Awake()` resolves its data folder before anything else:

```csharp
string foldername = (!GamePrefs.debug_loaddebugarmysetup)
    ? ("Campaigns/" + GamePrefs.leveltoload + "/" + GamePrefs.subleveltoload)
    : "Campaigns/DebugArmySetup";
...
GameVars.import_nations(foldername);
```

`import_nations` opens a `StreamReader` on a path that does not exist, so it
throws **inside `Awake`**. The controller never finishes initialising, the
scene proceeds with a half-built hierarchy, and the process dies shortly after
with a native access violation (`c0000005`, faulting module `ntdll.dll`) —
a Windows crash dump, not a managed stack trace.

The symptom to recognise: **the boot sequence produces no frame-gated work at
all.** `CampaignController.Update` throws every frame from the same missing
state, so nothing downstream of frame 3 ever happens. In ModApi terms, not even
`MapReady` fires.

The ModApi now logs a precise error on campaign load when the flag is set
(`LifecyclePatches.WarnIfDebugArmySetupWillCrash`). It warns rather than
clearing the flag, since silently undoing something a developer set by hand is
its own trap.

## 6. Resolved: the game forces invariant culture at startup

[summary-data-layer](summary-data-layer.md) listed this as open and
"unverified and important": every `float.Parse` in the data layer is called
without a `CultureInfo`, so on a comma-decimal locale `1.5` would parse as
`15`.

**It is handled.** `GameVars.SetCultureInfo()` assigns
`CultureInfo.DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture`
to `InvariantCulture`, and it is called from `MainMenuControls.Awake()` — the
first thing the game runs — as well as from `loaddebugs`, `MainMenu`, and
`Options.OnEnable` (an earlier version said `Options.SaveConfigs`).

Two caveats worth keeping:

- It sets the *default* thread culture rather than assigning
  `CurrentCulture` outright, so a thread whose culture had already been set
  explicitly would keep it. Nothing in the assembly does that.
- **A BepInEx plugin's `Awake` can run before `MainMenuControls.Awake`.** So
  the ModApi must not rely on the game having set the culture yet: any parsing
  the library does itself should pass `CultureInfo.InvariantCulture`
  explicitly, or call `SetCultureInfo` first.

## 7. Where that leaves the fixture

**`debug_loaddebugarmysetup` now loads.** Verified live: with the flag on and
the 1st Manassas scenario selected from the menus, the battle came up carrying
the debug folder's own order of battle — "1st Brigade", "U.S. Cavalry",
"Patterson's Artillery" — with no exception in the log.

| File | State |
| --- | --- |
| `nations.dat` | **fixed** — 1310 to 1326 lines (§3) |
| `regiments.dat` | **fixed** — 799 to 820 lines (§4) |
| `groups.dat` | already current — 32 lines per group, same as `000/A` |
| `ships.dat` | not needed on the battle path (§5) |
| `battledata.dat` | **current after all** — see below |

Both repairs keep CRLF, keep each file's own encoding, leave the original
fields byte-identical, and back up what they replace.

> **`battledata.dat` was never stale.** It had been the last suspect, on the
> strength of a line count that differs from `000/A` (188 against 398). That
> was never evidence: `import_battledata` has **no version gate on which lines it reads** (its one
> gate, `num < 0.8811f`, overrides a value already read) and
> **four count-driven variable-length loops**, so two battles with different
> numbers of objectives and zones legitimately differ in length. The successful
> load settles it — `import_battledata`'s own redirect excludes only
> `currentoperation` 3 and 6, and a standalone battle is 1, so the debug
> folder's copy is what parsed.

So the stale-file count in this folder was **two**, not the three or four the
line-count survey suggested. The two that broke it were both findable by exact
division against a record count; the two that looked suspicious but were fine
were the two with no such invariant. Worth remembering as a method: on this
format, *a line count that divides evenly is evidence, and a line count that
merely differs is not.*

## Still open

- ~~What `num`'s `< 14` / `< 16` comparisons in `import_units` mean.~~ They
  are not in `import_units`: they are in `BattleUnits.RefreshUnitTypes`
  (~10515), where `num` is `GetHighestSubordinateUnittype` — 14 is Division,
  16 Army.
- Whether a Steam update restores the publisher's stale copies. Both scripts
  are idempotent and take `-Verify`, so re-running them after a patch is the
  cheap check.
