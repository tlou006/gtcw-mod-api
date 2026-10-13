# The ModdingTool as a data-model specification

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Modding/ModdingTool_1.11.xlsm` (structural inspection of the
OOXML package — sheet names, declared filenames, header rows)

## Why this matters

The ModdingTool is an Excel workbook the developers ship to let modders author
game data. Its 53 sheets **write** the positional text files in `Config/`, so
its structure is an *authoritative specification* of those formats — field
order included — supplied by the developers themselves.

This inverts a large part of the plan. Phase 1 intended to recover each data
schema by reverse-engineering the game's parsers. For most files that is no
longer necessary: the schema can be read off the sheet headers, and the game's
parser is then only needed to confirm it and to learn what each field *does*.

The `Quick Guide` PDF shipped alongside does not document the data model
(verified manually) — the workbook itself carries the information.

## Sheet → output file

| Sheet | Writes | Lives in |
| --- | --- | --- |
| Weapons | `weapons.txt` | `Config/` |
| Weapon classes | `weaponclasses.dat` | `Config/` |
| FortWeapons | `weapongroups.dat` | `Config/` |
| CapturedWeapons | `weaponstock.dat` | `Config/` |
| Policies | `policies.dat` | `Config/` |
| Projects | `projects.dat` | `Config/` |
| Resources | `resources.dat` | `Config/` |
| BuildingTypes | `buildingtypes.dat` | `Config/` |
| Subsidies | `subsidies.dat` | `Config/` |
| Ship Types | `shiptypes.dat` | `Config/` |
| Ships | `ships.dat` | `Config/` |
| Cities | `cities.dat` | `Config/` |
| Uniforms | `colvariations.dat` | `Config/` |
| Commanders | `commanders.txt` | `Config/` |
| ArmyNames / ArmyNames CSA | `armynames0.dat` / `armynames1.dat` | `Config/` |
| HistoricalEvents | `historicalevents.dat` | `Config/` |
| CampaignObjectives | `campaignobjectives.dat` | `Config/` |
| ActivatedObjectives | `activatedobjectives.dat` | `Config/` |
| Quotes | `quotes.dat` | `Config/` |
| Tutorial / Tutorial B | `ctutorial.dat` / `btutorial.dat` | `Config/` |
| GameGuide / C / B | `FieldBookMainMenu.dat` / `FieldBookCampaign.dat` / `FieldBookBattle.dat` | `Config/` |
| Sounds (B/C/L) | `bsounds.txt` / `csounds.txt` / `lsounds.txt` | `Config/` |
| Track ID list (B/C/L) | `btracklist.txt` / `ctracklist.txt` / `ltracklist.txt` | `Config/` |
| AIStrategies | `aistrategies.dat` | `Config/` |
| **Unit list** | **`regiments.dat`** | **scenario folder** |
| **Groups** | **`groups.dat`** | **scenario folder** |
| **Battle Setup** | **`battledata.dat`** | **scenario folder** |
| **Nations** | **`nations.dat`** | **scenario folder** |

Sheets with no declared output are helper/lookup tables feeding the others:
`Unit types`, `Unit perks`, `Ammunition`, `Weather`, `Ranks & Status`,
`Group override`, `Group Formations`, `Macro AI`, `C-AI Personality`,
`Dummy Cmdr` (×4, one per nation), `Ship names` (×3).

### Naming conventions, decoded

- **`b`/`c`/`l` prefix** on sound and tracklist files = **Battle / Campaign /
  Loading**. The `(B)`/`(C)`/`(L)` sheet suffixes confirm it, and the same
  prefix convention explains `btutorial.dat` vs `ctutorial.dat` and
  `tooltips_battle.txt` vs `tooltips_campaign.txt`.
- **Numeric suffix = nation index.** `ArmyNames` → `armynames0.dat`,
  `ArmyNames CSA` → `armynames1.dat`, so **0 = USA, 1 = CSA**. The same index
  almost certainly applies to `firstname_0..4`, `lastname_0..4`,
  `shipnames_0..7` and `ranks0.txt`, with indices 2-4 being the foreign powers
  implied by the `Dummy Cmdr GB / FR / SP` sheets (Britain, France, Spain).

### Two-tier data: global vs scenario-local

Four files — `regiments.dat`, `groups.dat`, `battledata.dat`, `nations.dat` —
are **not** global config. They belong to a scenario or battle folder, and
`debug.txt` names exactly this quartet for its "Debug Army Setup" mode
(`Config/ArmySetup/`). `debug.txt` also notes that a scenario folder may carry
its own `battleprefs`/`unitprefs` subfiles, which then **override** the global
ones in `Config/`.

So the data model already has an override mechanism with a defined precedence:
**scenario-local beats global.** Worth understanding fully before designing the
hook library's override system (plan Phase 4) — matching the game's own
layering will feel native to modders, and may let the override system reuse
the game's existing precedence logic rather than inventing a parallel one.

## Schema observations

Field names come straight from the sheet header rows. A few structural points
that generalize:

**Dual-unit columns.** `Weapons` carries every physical quantity twice —
range in yards *and* metres, calibre in inches *and* mm, weight in lb *and*
kg, muzzle velocity in ft/s *and* m/s. Almost certainly one member of each
pair is a convenience formula for the author and only one is serialized.
*Open question:* which unit reaches `weapons.txt`. Resolve by reading the
parser, since guessing wrong scales every ballistic value incorrectly.

**Derived/ID-like numerics in header rows.** Several header cells hold bare
numbers (e.g. in `Weapons`, `Policies`, `BuildingTypes`) where labels would be
expected. These may be localization string IDs, given that the game keeps
tooltip text in separate `tooltips_*.txt` files. *Open question*, and if
confirmed it means **UI text is addressed by numeric ID** — which the hook
library would need to respect to add translatable strings.

**String-keyed coupling to Unity scene objects.** `AIStrategies` states that
objective names "need to fit 1:1 to the objective names used in Unity (case
sensitive!)". So AI strategy data is bound to scene objects by exact string
match — a fragile seam, and a likely source of silent failure for modders.

## Leads for decompiling

The workbook leaks real implementation detail:

1. **`Cbuilding` is a named game class.** The `BuildingTypes` sheet notes that
   building names, ranking and levels are set "directly in the prefabs
   [script: Cbuilding] in Unity". That is a confirmed class name, and it
   suggests a **`C` prefix convention** for game classes — a strong starting
   filter when searching the decompiled tree.
2. **Some mechanics live in prefabs, not in code or config.** Building
   identity, ranking and level progression are authored as scene/prefab data.
   This is a real limit on data-only modding — and a direct argument for the
   hook library, which can reach prefab-authored values at runtime that no
   config edit can touch.
3. **Building icons are attached to a specific scene path** —
   `campaignbase/gamecontroller/economy section` — naming both a scene
   (`campaignbase`) and a controller object (`gamecontroller`). Likely the
   campaign-layer root, and a good anchor for locating the campaign tick loop.
4. **Debug Army Setup is a usable test harness.** The ModdingTool can emit the
   four ArmySetup files and `debug.txt` can load them. That gives a
   *supported* path to boot a deterministic custom battle — exactly what's
   needed to test hooks repeatably, without clicking through the UI each run.

## Open questions

- Which unit of each dual-unit pair is serialized?
- Are the bare numerics in header rows localization IDs?
- Does the VBA actually write all these files, or do some sheets only feed
  others? `xl/vbaProject.bin` (218 KB, single `Module1`) holds the export
  routines, but VBA source is RLE-compressed per MS-OVBA and not greppable —
  extracting it needs a proper CFB/VBA decompressor. **High value**: that
  module is the exact serializer, field order included.
- What is the count-header convention per file? `weapons.txt` opens with `87`
  then `0`, suggesting `count` followed by records — but `aistrategies.dat`
  opens `176`, `0`, `0`, then a name, so the preamble differs per file.
- Where do `Campaigns/001/` and `Battlefields/` fit relative to scenario-local
  overrides?
