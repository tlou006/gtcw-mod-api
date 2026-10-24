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
| [Morale](code-findings/summary-morale.md) | The morale slots and rally pipeline, the 3-to-5× loss amplifier, `fightingspirit` as a politics→combat cap, cohesion and the terrain table, four unwired or mis-wired features |
| [Logistics](code-findings/summary-logistics.md) | The two supply models, depots as economy buildings, the 5% shortage cliff, replacements down the supply pipe, attrition in men per week, fatigue, drill, and the enumerations reference |
| [Politics](code-findings/summary-politics.md) | State support as the manpower tap — the two recruiting pools, contracts and reenlistment — **national morale as the only loss condition** with every term that moves it priced, foreign intervention, and the broken support breakdown |
| [Economy](code-findings/summary-economy.md) | `IIP` nodes as local markets, S-curve pricing, boolean production recipes, and the debt→rating→interest loop |

**And the mechanics write-ups so far:**

| Write-up | Covers |
| --- | --- |
| [What the game shows you, and what it actually does](mechanics-findings/ui-vs-simulation.md) | The numbers and words the interface gets wrong, and a verified list of what it gets right |
| [The war economy](mechanics-findings/the-war-economy.md) | Local prices and the price curve, how goods move, blockade, the five taxes, debt and interest, and the scripted boom/bust cycle |
| [Production and workforce](mechanics-findings/production-and-workforce.md) | The labour map that limits every building, slavery, what recruitment costs your industry, fertility, and where new companies come from |
| [The manual against the code](mechanics-findings/manual-vs-code.md) | The official manual cross-checked against the shipped data in full — economy, combat, supply, commanders, politics, naval — what it confirms, and where it misleads |
| [Time and speed](mechanics-findings/time-and-speed.md) | What a game day costs in real minutes in each scene, what the speed buttons do, why fast-forwarding starves the AI without changing the war, what pausing stops, and how often every number is recalculated |
| [Why units break](mechanics-findings/why-units-break.md) | Morale against cohesion, the two break thresholds and the casualty cliff that nothing recovers, what rallying can and cannot get back, and the campaign cap on battle morale |
| [What kills your men](mechanics-findings/what-kills-your-men.md) | Where a brigade's men go in men per week — the wounded pool you control, disease as a steady state, desertion, condition, fatigue and drill |
| [Supply in practice](mechanics-findings/supply-in-practice.md) | What a depot is for, why an army starves in friendly territory, what a shortage costs in morale and men, battlefield supply and readiness |
| [The manpower tap](mechanics-findings/the-manpower-tap.md) | Where men come from and what spends them: the two pools per state, the lifetime quota, the ninety-day men, replacements at 26 men a week, and what every support term is worth |
| [How you lose](mechanics-findings/how-you-lose.md) | The only loss condition and what moves it: why battles are a rounding error and support and towns are the war, the 1864 election cliff, and what low morale costs before it kills you |

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
