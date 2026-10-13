# decompile-gtcw

Reverse-engineering notes and tooling for a Unity game, for research and
interoperability purposes.

**Goals:** document the game's mechanics from its code, and ship a C# hook
library so other modders can extend the game without reversing it themselves.
See [`plan.md`](plan.md) for the phased approach, and
[`findings/00-build-provenance.md`](findings/00-build-provenance.md) for which build
the findings apply to.

### Findings so far

Research write-ups live in [`findings/`](findings/README.md), split into
detailed `NN-<topic>.md` files and `summary-<topic>.md` overviews. **Start with
a summary:**

| Summary | Covers |
| --- | --- |
| [Architecture](findings/summary-architecture.md) | Game code vs. third-party, the god classes, scene wiring and lifecycle, version identification |
| [Data layer](findings/summary-data-layer.md) | Config paths and override resolution, the exact file format, the `Config/` data model, the save/load surface |
| [Effects](findings/summary-effects.md) | Policy and project effect dispatch — and why new ids can be injected |
| [Simulation](findings/summary-simulation.md) | How the game is driven: the frame counter, the boot state machines, the four scheduling patterns |

[`findings/00-build-provenance.md`](findings/00-build-provenance.md) records
which build everything was written against.

## What lives in this repo

This repo tracks **original work only** — notes, analysis scripts, and
reimplementations written here. The game's own files and anything mechanically
derived from them (decompiled source, dumped metadata, ripped assets) are
copyrighted by the publisher and are excluded by `.gitignore`.

| Path | Tracked | Contents |
| --- | --- | --- |
| `findings/` | yes | Research write-ups: formats, structures, call graphs, behavior |
| `scripts/` | yes | Parsers and analysis tooling written here |
| `game/` | **no** (README only) | A copy of the game install — see `game/README.md` |
| `decompiled/`, `extracted/` | no | Decompiler and asset-extractor output |
| `tools/` | no (README only) | Third-party tools, downloaded locally |

The game lives in one dedicated folder, `game/`, and `.gitignore` excludes
everything in it. Each of the untracked folders keeps a tracked README so a
fresh clone still shows what belongs where.

## Layout

```
decompile-gtcw/
├── findings/       # research write-ups, plus summary-*.md overviews
├── scripts/        # analysis / parsing tooling
├── tools/          # (ignored) IL2CPP dumpers, AssetRipper, dnSpy, ILSpy, ...
├── game/           # (ignored) local copy of the game
└── decompiled/     # (ignored) generated output
```

## Setup

Nothing to track here, but for reproducibility record in `findings/` which
tool versions you used:

- The game build (version string, store, install date) under analysis
- Which decompiler/dumper produced any output you reference

## Workflow

1. Keep a pristine copy of the game install outside this repo, or in `game/`.
2. Run dumpers/extractors into `decompiled/` or `extracted/`.
3. Write what you *learned* into `findings/` — file formats, struct layouts,
   protocol shapes, logic descriptions. Quote only the small snippets a note
   actually needs to make sense.
4. Put any parser or reimplementation you write in `scripts/`.

## Scope

Research and interoperability. This repo is not a redistribution channel for
the game's code or assets, and shouldn't become one — note the publishing or
distribution restrictions in the game's EULA before sharing anything derived
from it.
