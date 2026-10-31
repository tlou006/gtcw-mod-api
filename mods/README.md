# `mods/` — products built on the API

Mods that ship as their own release, built against `GTCW.ModApi` exactly as a
third party would build one.

| Folder | Contents |
| --- | --- |
| [`GTCW.BugFixes/`](GTCW.BugFixes/) | Opt-in repairs for the defects in [`defect-register.md`](../defect-register.md) |

## Why these are not in `src/`, and not in `samples/`

Three folders, three different jobs:

| Folder | What it is | Ships as |
| --- | --- | --- |
| `src/` | **the library** | `GTCW.ModApi*.dll` |
| `mods/` | **separate products that depend on the library** | their own plugin, their own version, their own config |
| `samples/` | teaching material — smallest thing that demonstrates an API | source only |

The split matters most for `GTCW.BugFixes`. It is a *consumer* of the library,
not a part of it: separately versioned, separately installed, separately
uninstallable, and able to be absent without the library noticing. That is a
[deliberate constraint](../plan.md#the-library-does-not-fix-the-game) — a hook
library that quietly repaired the game would make every mod built on it inherit
an invisible behaviour change, and would double-correct any mod that had worked
around the bug.

A mod here may reference the game's assemblies (`UsesGameAssemblies`), because a
real mod often must. What it may **not** do is reference `GTCW.ModApi.Runtime`:
the library's internals are not an API, and a mod that reaches into them is
exactly the coupling the layering exists to prevent. If a mod here needs
something only `Runtime` has, that is a gap in the public API and gets recorded
as one — see the end of
[`GTCW.BugFixes/IMPLEMENTATION.md`](GTCW.BugFixes/IMPLEMENTATION.md), which is
already carrying three.
