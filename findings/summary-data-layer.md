# Summary — The config and save data layer

**Covers:** [05-config-paths-and-overrides](05-config-paths-and-overrides.md),
[06-config-file-format](06-config-file-format.md),
[07-config-data-model](07-config-data-model.md),
[08-save-load-surface](08-save-load-surface.md)
**Build:** see [00-build-provenance](00-build-provenance.md)

## In one paragraph

Config is plain `System.IO` against **relative string literals** — no asset
abstraction, no path service — so it is interceptable with ordinary hooks. The
format is positional and line-oriented: a count header, one field per line,
`;`-separated fixed-width list fields, CRLF, `Encoding.Default`. Scenario
overrides are **whole-file replacement with no field-level merge**, which makes
an additive override layer genuinely new capability rather than a
reimplementation. Saves go through `ImportExportUnitData` as ~25 paired
`Save*`/`Load*` methods, reconciling objects **by position with a float
tolerance** rather than by stable id.

## The findings

| | Finding | Consequence |
| --- | --- | --- |
| 1 | **Paths are relative literals** (`Config/resources.dat`, `Config/unitprefs.txt`, …), resolved against the install root. Only two sites touch `Application.dataPath`, none in config loading. | Interception needs no Unity-specific machinery. `Config/` is **not** read-only — `Filter` writes `Config/Filters.dat`. |
| 2 | **Override = whole-file replacement.** The idiom is: build the scenario path; if absent, reassign to `Config/`. One path wins entirely. | **Additive field-level overrides are new capability.** Two mods cannot both change one field of `unitprefs.txt` today. |
| 3 | **The format is fully decoded**, including an exact 18-line schema for `projects.dat` verified against the shipped file. Three `Tools` helpers encode every list field. | Typed models can be generated from the parsers. Where the ModdingTool workbook and the parser disagree, **the parser wins**. |
| 4 | **Saves reconcile by position**, with helpers like `GetGameObjectFromPosition(..., float accuracy = 0.5f)`. | Anything the ModApi persists must carry **its own identifier**. Side-car files are the right default; appending to game files is fatal. |

## The format, compactly

```
<count>
<field 1>          .dat  — one value per line, fixed order
<field 2>
...                .txt  — alternating label/value; labels are never parsed
```

- **List fields:** `partlen` chars per entry + a 1-char separator, trailing
  separator included. Shipped separator is `;`, default width 3 → `001;002;`.
- **Escapes:** `\n` and `\t` decoded inside single-line string fields.
- **Dates:** `dd-mm-yyyy`, day first (`Tools.Date(day, month, year)`).
- **Encoding:** CRLF, `Encoding.Default` (system ANSI) — **not UTF-8.**

## Where it fails, and how

The format is brittle by construction: field count, order, line count and
character offsets are all load-bearing, and `int.Parse` / `float.Parse` /
`bool.Parse` are unguarded. **A missing line does not degrade** — it throws, or
silently shifts every subsequent field. This is the Quick Guide's "hard-fail
data layer", now explained.

Two important exceptions to the hard-fail rule:

- **`Tools.GetIntPartitions` uses `int.TryParse` and silently drops**
  unparseable entries, so a typo'd id in a list field is **invisible**. Our
  validator should report it rather than copy it.
- **`Tools.Date` clamps and zeroes** rather than throwing, so a malformed date
  degrades to "no date".

Entries that do not fill `partlen` exactly are also silently dropped.

## For the ModApi

- Hook **post-parse**, not at the file level, so overrides apply to whichever
  file actually won the game's own resolution. Modders then reason about one
  rule: *the effective value, whatever its source.*
- **Validate before injecting**, rejecting bad overrides individually rather
  than inheriting the game's all-or-nothing failure.
  `ImportExportUnitData.CheckFileIntegrity` is the game's own validator and the
  thing to test against.
- For persistence, prefer **side-car files** keyed off the save folder, hooked
  on `SceneManagement.SaveAll`. A save then stays loadable with the mod
  uninstalled.

## Still open

- **A full sweep of `GetStringPartitions`/`GetIntPartitions` call sites** would
  settle every list field in every config file at once. Cheap, high value, not
  done.
- **Culture handling.** `float.Parse` is called with no `CultureInfo`. If the
  game does not force an invariant culture at startup, a comma-decimal locale
  parses `1.5` as `15`. Unverified and important.
- Whether *every* pref file supports scenario override, or only the three with
  a confirmed fallback.
- The dual-unit question (yd/m, lb/kg, ft/s–m/s) and localization ids — these
  live in the ModdingTool's VBA export macros, not in the game's reader.
- What `CheckFileIntegrity` actually checks; save-folder layout and naming.
