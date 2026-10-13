# The ModdingTool as a data-model specification

**Build:** see [00-build-provenance](00-build-provenance.md)
**Summary:** [summary-data-layer](summary-data-layer.md)
**Sources:**
- `Modding/ModdingTool_1.11.xlsm` — structural inspection of the OOXML
  package (sheet names, declared filenames, header rows)
- `Modding/ModdingTool Quick Guide.pdf` (guide v1.08, older than the v1.11
  tool), read via OCR text extraction

The two sources are complementary and neither is sufficient alone: **the
workbook gives field layout, the guide gives meaning and constraints.** The
guide carries no column-level schemas — but it documents what each file
controls, which effects are hardcoded, how scenarios are laid out, and
several hard limits on modding. Read both before touching the decompiler.

> OCR caveat: the extracted text has character-level errors (`etracklist`
> for `ctracklist`, `Groupsdat` for `groups.dat`, digit/letter confusion in
> the map-ID list, and the title's "1861-1864"). Treat exact identifiers from
> the guide as approximate and confirm against the filesystem.

## Why this matters

The ModdingTool is an Excel workbook the developers ship to let modders author
game data. Its 53 sheets **write** the positional text files in `Config/`, so
its structure is an *authoritative specification* of those formats — field
order included — supplied by the developers themselves.

This inverts a large part of the plan. Phase 1 intended to recover each data
schema by reverse-engineering the game's parsers. For most files that is no
longer necessary: the schema can be read off the sheet headers, and the game's
parser is then only needed to confirm it and to learn what each field *does*.

What the workbook cannot tell us is **what the data does**, and that's where
the guide earns its place — see [Semantics and hard limits](#semantics-and-hard-limits)
below. Its most consequential disclosure is that several systems'
*effects* are hardcoded regardless of what the data files say.

## Sheet → output file

The guide organizes these into three tiers, which corrects an assumption from
the workbook-only pass: several files are **scenario-scoped, not global**.

### Tier 1 — Common data, in `Config/` (affects all scenarios)

| Sheet | Writes | Controls |
| --- | --- | --- |
| Weapons | `weapons.txt` | All weapons. Weapon images bind by weapon ID |
| Weapon classes | `weaponclasses.dat` | Class names — guide says **do not edit** |
| FortWeapons | `weapongroups.dat` | Campaign-map fort weapons; **only fort levels 1-3 used** |
| Policies | `policies.dat` | Policy text, requirements, research times, newspaper copy |
| Projects | `projects.dat` | Project prices, activation dates, descriptions |
| Resources | `resources.dat` | Tradeable goods: prices, demand |
| BuildingTypes | `buildingtypes.dat` | Campaign building data |
| Ship Types | `shiptypes.dat` | Ship types. Images bind by ship ID; **many sheet columns unused** |
| Uniforms | `colors.dat` | Available uniform colors |
| Uniforms | `colvariations.dat` | Color variation per color per nationality |
| Uniforms | `equipment.dat` | 3D-troop equipment per uniform type (hats, knapsacks, blanket rolls) |
| Commanders | `commanders.txt` | Commanders: attributes, historical rank progression. Portraits bind by commander ID |
| ArmyNames / ArmyNames CSA | `armynames0.dat` / `armynames1.dat` | Default new-army names per state |
| ActivatedObjectives | `activatedobjectives.dat` | Which objectives are active in historical battles |
| AIStrategies | `aistrategies.dat` | Historical-battle AI scripts — **objective chains with delays** |
| Quotes | `quotes.dat` | Loading-screen quotes |
| HistoricalEvents | `historicalevents.dat` | (not covered in guide v1.08) |
| CampaignObjectives | `campaignobjectives.dat` | (not covered in guide v1.08) |
| Tutorial / Tutorial B | `ctutorial.dat` / `btutorial.dat` | (not covered in guide v1.08) |
| GameGuide / C / B | `FieldBook*.dat` | (not covered in guide v1.08) |
| Sounds (B/C/L) | `bsounds.txt` / `csounds.txt` / `lsounds.txt` | Situational sounds + volume, per context |
| Track ID list (B/C/L) | `btracklist.txt` / `ctracklist.txt` / `ltracklist.txt` | Sound files used by the matching `*sounds.txt` |

Two `Config/` **subfolders** hold per-nation name lists:

- `Config/CommanderDummies/` ← `Dummy Cmdr` sheets (×4). Name lists for
  **procedurally generated commanders**, created whenever a historical
  commander isn't assigned to a unit (most fort commanders, or when no
  suitable historical figure is available).
- `Config/ShipNames/` ← `Ship names` sheets (×3). Guide notes **only files 0
  and 1 matter**, because the other nations don't construct ships — which
  independently confirms 0 = Union, 1 = CSA.

### Tier 2 — Campaign scenario files (in `Campaigns/<mapID>/<letter>/`)

| Sheet | Writes | Controls |
| --- | --- | --- |
| C-AI Personality | `aipersonalities.dat` | **Campaign AI**: personalities with tax, subsidy, policy and project priorities, including emergency behavior |
| Battle Setup | `battledata.dat` | Scenario date, sides, weather, economy, group deployment |
| Unit list | `regiments.dat` | Every unit incl. custom garrisons; each must belong to a group |
| Groups | `groups.dat` | Army HQs and navy fleets; **only HQs can be reinforcements** |
| Nations | `nations.dat` | Nationality and state data, incl. national military experience |
| Cities | `cities.dat` | Per-city ownership overrides (state ownership comes from `nations.dat`) |
| Ships | `ships.dat` | Scenario ships, assignable to fleets in `groups.dat` |
| Subsidies | `subsidies.dat` | Starting subsidies per type per nation |
| CapturedWeapons | `weaponstock.dat` | Starting weapon stock per type per nation |

Plus non-tool files: `briefing0.txt`/`briefing1.txt`, `scenariodescr.txt`
(required for main-menu listing), and an **empty `Save/` folder** for autosaves.

### Tier 3 — Historical battle scenarios (in `Battlefields/<mapID>/<letter>/`)

A subset: `battledata.dat`, `groups.dat`, `nations.dat`, `regiments.dat`,
`briefing0.txt` (Union) / `briefing1.txt` (CSA), `scenariodescr.txt`, `Save/`.

Scenario folders are named by **map ID, then a letter from `A`**. Map IDs run
roughly 000-029 for historical battle maps, 100 for a limited test map, and
200-240 for non-historical maps; campaign map 001 is the main North America
map. Every battle map needs an `A/` folder present for campaign battles to
load at all, whatever its contents.

### Helper sheets (no output file)

Orange-tabbed sheets feed the others and per the guide **should not be
edited**: `Unit types`, `Unit perks`, `Ammunition`, `Weather`,
`Ranks & Status`, `Group override`, `Group Formations`, `Macro AI`.

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

The guide confirms and sharpens this: any `Config/` preference file may be
copied into a scenario folder, and the scenario copy "is then loaded instead
of" the global one.

**Correction to an earlier assumption — since verified in code**
([05-config-paths-and-overrides](05-config-paths-and-overrides.md)). *Instead of* means
**whole-file replacement, not field-level merge.** The game has no mechanism for
overriding an individual value — a scenario either supplies a complete file or
inherits the global one entirely. So the override precedence is a two-entry
chain of whole files, not a layered merge:

```
Config/<file>   ⟵ replaced wholesale by ⟵   <scenario>/<file>
```

This makes the Phase 4 override system **more valuable than first framed, not
less**: field-level, additive, stackable overrides are a genuinely new
capability rather than a reimplementation of something the game already does.
It also explains why data mods conflict so badly today — two mods wanting one
changed value each must each ship an entire copy of the file, and whichever
loads second wins completely.

### Prefs are scale-dependent — a real mechanics finding

The guide's Wilson's Creek example is more informative than it looks. That
battle uses a 4×4-mile map instead of the default 8×8, and regiment-level
rather than brigade-level units, so its scenario folder carries custom:

- `battleprefs.txt` — "to rescale weapon ranges, etc." for the smaller map
- `unitprefs.txt` — "for regiment scale weapon lethality, etc."

So **weapon ranges are tuned to map scale, and lethality to unit scale.**
Neither is an absolute physical quantity in the simulation: the same weapon
behaves differently on a 4-mile map than an 8-mile one, and differently for a
regiment than a brigade. Any mod that changes map size or unit echelon must
retune both files, and any note about weapon numbers is meaningless without
stating which scale it assumed. Worth confirming in the parser early.

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
The guide adds a trap: in `activatedobjectives.dat` the names refer to **map
object names, which usually differ from the objective name shown in-game**.

## Semantics and hard limits

Everything here comes from the guide, and it's the half the workbook can't
supply. Several items constrain the project directly.

### Hardcoded effects — the central finding

| File | Guide says |
| --- | --- |
| `policies.dat` | "the effects are **mostly hardcoded**, but many can be balanced via `campaignprefs.txt`" |
| `projects.dat` | "the effects are **hardcoded** and adding new projects will not add any effects for them" |

These files carry *descriptions, costs, prerequisites and research times* —
not behavior. A modder can add a project and it will appear, be researchable,
and **do nothing**.

This is the strongest argument yet for the hook library, and it reframes its
purpose. The ceiling on data-only modding isn't inconvenience, it's
capability: new policies and projects with real effects are **impossible**
through data files and only reachable by patching code. Phase 4 should treat
"register a project/policy effect" as a first-class API, not an afterthought —
it's the feature that unlocks a category of mod that cannot exist today.

`campaignprefs.txt` (125 KB) rises in priority as a result: it's the
designated balance surface for hardcoded policy effects, so it likely holds a
large table of tuning constants keyed to those effects.

### Content bound by ID to assets inside the game

Portraits bind by commander ID, weapon images by weapon ID, ship images by
ship ID. Adding a new weapon or ship therefore needs a corresponding image
slot — and those live inside Unity asset bundles.

### Stated ceilings

- **Uniform colors cannot be added.** Since the v1.03 move to 3D troops each
  color needs texture files, and the guide says these "cannot be modified or
  added currently".
- **Battle main-menu map images cannot be added** — read from game files only.
- `weaponclasses.dat` — "You should not edit this."
- `weapongroups.dat` — only fort levels 1-3 are used.
- `shiptypes.dat` — many sheet columns are unused.

Each is a candidate for the hook library to unlock, and each needs
verification: "cannot currently" is a statement about the *supported* path,
not necessarily about what runtime patching can reach.

### A working runtime import path exists (v1.08+)

The game already imports external PNGs at runtime, which is both a model to
follow and a confirmed hook target:

| Asset | Format | Location | Naming |
| --- | --- | --- | --- |
| Battle flags (Perk lvl 3 units) | PNG 256×256 | `Modding/BattleFlags/<allianceID>/` | numbered from `1000` |
| Scenario images | PNG 1340×1340 | `Modding/Campaigns/` or `Modding/Battles/` | `<mapID><letter>.PNG` |
| Commander portraits | — | per game Manual, Article VI | by commander ID |

**Alliance ID: 0 = Union, 1 = CSA** — explicitly stated here, confirming the
index convention inferred from the file suffixes. The existing
`Modding/BattleFlags/0/` and `/1/` folders match.

Finding the code behind this import is high value: it's a known-good example
of the game reading arbitrary external files, and likely the cleanest place to
extend asset loading.

### AI architecture, in outline

- **Battle AI** (`aistrategies.dat`) — scripted "objective chains with
  delays", assignable per group, keyed to map object names.
- **Campaign AI** (`aipersonalities.dat`) — named personalities carrying
  priorities for tax, subsidies, policies and projects, plus emergency
  behavior.
- A separate `Macro AI` helper sheet exists, suggesting a third tier above
  both.

### Failure mode to respect

"If any of the files is missing, or contains data in incorrect form, the
scenario will most likely fail to load." The game's data layer is **brittle
and fails hard** — which is exactly why the hook library's own overrides must
fail soft and validate before injecting, rather than inheriting this behavior.

## Leads for decompiling

The workbook leaks real implementation detail:

1. **`CBuilding` is a named game class — now verified in the assembly.** The
   `BuildingTypes` sheet notes that building names, ranking and levels are set
   "directly in the prefabs [script: Cbuilding] in Unity". Decompiling that
   name resolves to a real type: **`public class CBuilding : MonoBehaviour`**,
   carrying nested types including an `AIPlacement` class with position,
   rotation and radius/angle fields — so AI building placement is geometric
   and lives on the building component itself.

   > **Casing:** the real class is `CBuilding`, not `Cbuilding` as the
   > ModdingTool spells it. Treat identifiers quoted in the tool and guide as
   > case-approximate and confirm against the assembly before searching.

   > **Correction (Phase 1):** this does *not* confirm a `C`-prefix
   > convention. The full export shows only `CBuilding` and `CBuildingIcon`
   > carry the prefix. Game code is instead identified by sitting in the
   > **global namespace** while third-party code keeps its own — see
   > [01-assembly-partition](01-assembly-partition.md). `CBuilding` is 3,271 lines,
   > so it is a major type regardless.
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
- **Which hardcoded policy/project effects are reachable from
  `campaignprefs.txt`, and which are not balanceable at all?** This divides
  Phase 4's work into "expose the existing knob" versus "patch the effect".
- Are the stated ceilings (uniform colors, menu map images) genuinely
  unreachable, or only unsupported through data files? Runtime patching may
  reach what the ModdingTool cannot.
- Where is the v1.08 external-PNG import implemented? Best-known example of
  the game loading arbitrary external files.
- What sits above battle AI and campaign AI, given the `Macro AI` sheet?
- `historicalevents.dat`, `campaignobjectives.dat`, the tutorials and the
  FieldBook files postdate guide v1.08 and are undocumented — these need the
  parser treatment.
