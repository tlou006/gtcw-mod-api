# Summary — The config and save data layer

**Covers:** [05-config-paths-and-overrides](05-config-paths-and-overrides.md),
[06-config-file-format](06-config-file-format.md),
[07-config-data-model](07-config-data-model.md),
[08-save-load-surface](08-save-load-surface.md),
[29-dead-prefs-audit](29-dead-prefs-audit.md)
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

## And a lot of it is not read at all

[29-dead-prefs-audit](29-dead-prefs-audit.md) finishes the sweep
[07](07-config-data-model.md) started. Of `GamePrefs`' 2,215 distinct fields,
**144 are parsed from a config file and never read**, and **4** are declared
without even a loader line. Ninety of the 144 are gameplay-facing rather than
presentation or scheduling plumbing.

> **So about one labelled number in fifteen does nothing**, and only about a
> quarter of those carry the publisher's own `[deactivated]` or `-> inactive`
> marker. The other hundred-odd look exactly like live settings.

Twenty-three of them are one cut: an abandoned subsidy system whose labels name
**transportation**, **trade war**, **civil order** and **funding** categories
that the shipped six do not include, with
`basicsubsidiescost` still carrying two trailing slots marked `n/a`.

Chasing the rest turned up what a mechanical sweep cannot — five places where
something *else* reads the wrong field:

- **`SpottedEnemy` builds its mark-creation probability from
  `spottingfactor[0..1]`** — a per-unit-type *concealment* array shipping −50
  and −60 — instead of `timefactorspottings` (0.1 / 0.003 game years). The
  negative product trips a `Mathf.Max(0.001f, …)` clamp, inflating the
  probability about a thousandfold, so **your intelligence rating and both
  intelligence perks have no effect on how often an enemy formation leaves you
  a last-seen mark**.
- **`CheckOffensiveMovements` reads `winterdefensiveoperationsmodifier`**, so
  winter costs the AI ×0.2 rather than the ×0.05 its own dedicated — and
  unread — constant specifies.
- **`forthitpoints` is unread**, so a fort's level buys garrison capacity and
  siege setup time but **not durability**.
- **`decreaseexpcapcommanderifdefamed` is unread** while its veteran sibling is
  live.
- **Three of the five `powerbalanceinfo*` tiers are unread.**

One earlier reading is withdrawn: the `[deactivated]` tag does not lie
anywhere. The apparent exception was a **twin pref** — the same label text
attached to two fields of the same shipped value in two different files, one
live and one dead. Four such pairs exist, which makes "find the field by its
label" an unsafe move.

## Still open

- **A label-to-field map for all 2,215 prefs.** Needs the loaders simulated in
  order rather than matched line by line; it would make every future pref
  question a lookup ([29](29-dead-prefs-audit.md) §10).
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
