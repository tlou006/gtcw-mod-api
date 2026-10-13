# 06 — The config file format, exactly

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `Projects.LoadedProjects.Import`,
`GamePrefs` loaders, `Tools`; cross-checked against the shipped
`Config/projects.dat`
**Summary:** [summary-data-layer](summary-data-layer.md)

The format is positional and line-oriented with a count header. The parsers
are the authoritative schema: where they disagree with the ModdingTool
workbook ([07-config-data-model](07-config-data-model.md)), **the parser
wins.**

## Two dialects

| | `.dat` files | `.txt` pref files |
| --- | --- | --- |
| Shape | one value per line | **alternating label/value lines** |
| Header | leading line = record count | leading lines discarded |
| Records | fields in fixed order, one per line | flat list of prefs |

In the `.txt` dialect the parsers read a line and **discard it**, then read the
value. The labels are documentation for humans and are never parsed.

> **So a label can be edited freely, but a line can never be inserted or
> removed.** This is the single easiest way to break a pref file, and it fails
> silently-then-catastrophically: every subsequent value shifts by one.

## Encoding and line endings

- **Line endings are CRLF.**
- Pref files are read with `Encoding.Default` — the system ANSI codepage, **not
  UTF-8.**

Any tool that rewrites a config file must preserve both, or it will change
field widths and corrupt non-ASCII text. Writing UTF-8 (let alone UTF-8 with a
BOM) will break the first line it touches.

## List-valued fields: fixed-width, semicolon-separated

Three `Tools` helpers encode the entire convention:

```csharp
public static List<string> GetStringPartitions(string strvar, int partlen = 3, bool usecomma = true)
public static List<int>    GetIntPartitions   (string strvar, int partlen = 3, bool usecomma = true)
public static string       TransformToLineBreaks(string textbeforebreaks)
```

- A list field is **`partlen` characters per entry plus a one-character
  separator**, repeated, with a **trailing separator**. The separator in the
  shipped files is **`;`**. So the default `partlen = 3` produces `001;002;`.
- `usecomma` is a misnomer: the helpers only *skip one character*, they never
  check which. Any single-character separator parses identically.
- Entries are **zero-padded to the full width**. An entry that does not fill
  `partlen` exactly is **silently dropped**, not an error.
- `TransformToLineBreaks` decodes `\n` and `\t` escapes inside a single-line
  string field. That is how multi-line prose survives a line-oriented format.
- An empty line is a legitimate empty list.

> **`GetIntPartitions` uses `int.TryParse` and silently skips anything
> unparseable.** This is the one place the data layer does *not* hard-fail,
> which makes a typo'd id in a list field **invisible** rather than loud. Worth
> remembering when debugging a mod that "does nothing" — and worth our
> validator reporting rather than copying.

## Worked example: `projects.dat`

From `Projects.LoadedProjects.Import(string filename)`. **A count header, then
18 lines per record** — note that is 18 *lines* for 16 *fields*, because the
two image paths each consume two lines.

| Line | Field | Encoding | Example (record 0) |
| --- | --- | --- | --- |
| 1 | `projectid` | int | `0` |
| 2 | `projectname` | string | `Austrian Rifles` |
| 3 | `alliances` | width 1 + `;` | `0;1;` |
| 4 | `requiredpolicyids` | width 3 + `;` | *(empty)* |
| 5 | `subsidytype` | int | `5` |
| 6 | `subsidycostfirstlevel` | float | `2500000` |
| 7 | `repeating` | `bool.Parse` | `false` |
| 8 | `usedgroup` | int | `0` |
| 9–10 | `imagepath_big` | **two** lines, joined with `/` | `UI/Weapons/Rifles` + `17` |
| 11–12 | `imagepath_small` | **two** lines, joined with `/` | `UI/ProjectImages` + `0_s` |
| 13 | `prosadescr` | string, escapes decoded | *(flavour text)* |
| 14 | `effectsdescr` | string, escapes decoded | *(effect summary)* |
| 15 | `datefrom[0]` | `dd-mm-yyyy`, offsets (0,2)(3,2)(6,4) | `01-01-1862` |
| 16 | `datefrom[1]` | same; **skipped if < 10 chars** | *(empty)* |
| 17 | `applicablescenarios` | width 3 + `;` | `001;002;` |
| 18 | `requireddlcids` | width 2 + `;` | *(empty)* |

Verified line-by-line against the shipped `Config/projects.dat` (125 records).
The split image path is an easy off-by-two when hand-editing.

`subsidytype` indexes the subsidy categories; `GetSubsidyCost(int level)` turns
`subsidycostfirstlevel` into a per-level cost, so the file stores only the
first level's cost and the curve is in code.

## Dates

```csharp
Tools.Date(int _day, int _month, int _year, float _hourfraction = -1f)
```

**Day first — the file format is `dd-mm-yyyy`.** The parser feeds the
substrings in file order, so the separator is `-` and the offsets are fixed.

The constructor is unusually forgiving for this codebase:

- clamps `day > 31` → 31 and `month > 12` → 12, logging a warning;
- zeroes the whole date if any component is `<= 0`;
- so **a malformed date degrades to "no date" rather than throwing.**

`Date` also precomputes `yearfraction`, which is what most date comparisons
actually use, plus an optional `hourfraction` (`-1` when unset).

## The format is brittle by construction

Field count, field order, line count and character offsets are all
load-bearing, and `int.Parse` / `float.Parse` / `bool.Parse` are **unguarded**.
A missing line does not degrade — it either throws or silently shifts every
subsequent field.

This is the "hard-fail data layer" described in the Quick Guide, now
explained. Consequences for the ModApi:

- **Validate before injecting.** Overrides must be checked and rejected
  individually rather than inheriting the game's all-or-nothing behaviour.
- `ImportExportUnitData.CheckFileIntegrity` is the game's own validator — read
  it for a reference on what *it* considers valid
  ([08-save-load-surface](08-save-load-surface.md)).
- A generated-schema approach is viable precisely *because* the format is this
  rigid: the parsers are mechanical enough to transcribe into typed models.

## Open questions

- Whether any `.dat` file uses a separator other than `;`, which the helpers
  would parse happily and a hand-written writer would get wrong.
- Whether `partlen` varies per call site beyond the three widths seen so far
  (1, 2, 3). A full sweep of `GetStringPartitions`/`GetIntPartitions` call
  sites would settle every list field in every file at once — **cheap, high
  value, not yet done.**
- The dual-unit question (yd/m, lb/kg, ft/s–m/s) is still open; it lives in the
  ModdingTool's export macros, not in the game's reader. See
  [07-config-data-model](07-config-data-model.md).
- `float.Parse` with no `CultureInfo` argument uses the current culture, so a
  comma-decimal locale would parse `2500000` fine but `1.5` as `15`. Whether
  the game forces an invariant culture at startup is **unverified and
  important** — it would explain locale-specific modding bugs.
