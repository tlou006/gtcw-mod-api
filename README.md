# decompile-gtcw

Reverse-engineering notes and tooling for a Unity game, for research and
interoperability purposes.

**Goals:** document the game's mechanics from its code, and ship a C# hook
library so other modders can extend the game without reversing it themselves.
See [`plan.md`](plan.md) for the phased approach, and
[`code-findings/00-build-provenance.md`](code-findings/00-build-provenance.md) for which build
the findings apply to.

### Findings so far

Research is written up in two layers. [`code-findings/`](code-findings/README.md)
answers *what the code does* — detailed `NN-<topic>.md` files with
`summary-<topic>.md` overviews. [`mechanics-findings/`](mechanics-findings/README.md)
answers *what that means at the table*, in the player's units rather than the
engine's. **Start with a summary:**

| Summary | Covers |
| --- | --- |
| [Architecture](code-findings/summary-architecture.md) | Game code vs. third-party, the god classes, scene wiring and lifecycle, version identification |
| [Data layer](code-findings/summary-data-layer.md) | Config paths and override resolution, the exact file format, the `Config/` data model, the save/load surface |
| [Effects](code-findings/summary-effects.md) | Policy and project effect dispatch — and why new ids can be injected |
| [Simulation](code-findings/summary-simulation.md) | How the game is driven: the frame counter, the boot state machines, the four scheduling patterns |
| [Combat](code-findings/summary-combat.md) | The three combat models, the weapon unit-system split, `firepower[3]`, accuracy, melee and casualty formulas |
| [Morale](code-findings/summary-morale.md) | The morale slots and rally pipeline, `fightingspirit` as a politics→combat cap, cohesion, two unwired features |
| [Logistics](code-findings/summary-logistics.md) | The two supply models, depots as economy buildings, the 5% shortage cliff, attrition, fatigue, training, and the enumerations reference |
| [Politics](code-findings/summary-politics.md) | State support as the manpower tap, national morale and the loss condition, foreign intervention, and the broken support breakdown |
| [Economy](code-findings/summary-economy.md) | `IIP` nodes as local markets, S-curve pricing, boolean production recipes, and the debt→rating→interest loop |

**And the mechanics write-ups so far:**

| Write-up | Covers |
| --- | --- |
| [What the game shows you, and what it actually does](mechanics-findings/ui-vs-simulation.md) | The numbers and words the interface gets wrong, and a verified list of what it gets right |
| [The war economy](mechanics-findings/the-war-economy.md) | Local prices and the price curve, how goods move, blockade, the five taxes, debt and interest, and the scripted boom/bust cycle |
| [Production and workforce](mechanics-findings/production-and-workforce.md) | The labour map that limits every building, slavery, what recruitment costs your industry, fertility, and where new companies come from |
| [The manual against the code](mechanics-findings/manual-vs-code.md) | The official manual cross-checked against the shipped data — economy, combat, supply, commanders, politics — what it confirms, and where it misleads |

[`code-findings/00-build-provenance.md`](code-findings/00-build-provenance.md) records
which build everything was written against.

## What lives in this repo

This repo tracks **original work only** — notes, analysis scripts, and
reimplementations written here. The game's own files and anything mechanically
derived from them (decompiled source, dumped metadata, ripped assets) are
copyrighted by the publisher and are excluded by `.gitignore`.

| Path | Tracked | Contents |
| --- | --- | --- |
| `code-findings/` | yes | Research write-ups: formats, structures, call graphs, behavior |
| `mechanics-findings/` | yes | What that behavior means as gameplay, sourced back to `code-findings/` |
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
├── code-findings/      # what the code does, plus summary-*.md overviews
├── mechanics-findings/ # what that means as gameplay
├── scripts/            # analysis / parsing tooling
├── tools/              # (ignored) IL2CPP dumpers, AssetRipper, dnSpy, ILSpy, ...
├── game/               # (ignored) local copy of the game
└── decompiled/         # (ignored) generated output
```

## Setup

Nothing to track here, but for reproducibility record in `code-findings/` which
tool versions you used:

- The game build (version string, store, install date) under analysis
- Which decompiler/dumper produced any output you reference

## Workflow

1. Keep a pristine copy of the game install outside this repo, or in `game/`.
2. Run dumpers/extractors into `decompiled/` or `extracted/`.
3. Write what you *learned* into `code-findings/` — file formats, struct layouts,
   protocol shapes, logic descriptions. Quote only the small snippets a note
   actually needs to make sense.
4. Translate it into `mechanics-findings/` once a subsystem is understood —
   what the behavior means for a player, sourced back to the code file.
5. Put any parser or reimplementation you write in `scripts/`.

## Scope

Research and interoperability. This repo is not a redistribution channel for
the game's code or assets, and shouldn't become one — note the publishing or
distribution restrictions in the game's EULA before sharing anything derived
from it.
