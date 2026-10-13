# 04 — There is no version constant in the assembly

**Build:** see [00-build-provenance](00-build-provenance.md)
**Source:** `Assembly-CSharp.dll` — `MainMenuControls`, `GameVars`
**Summary:** [summary-architecture](summary-architecture.md)

## The finding

The hook library needs to know which game build it is running against, so it
can refuse or degrade rather than mispatch. The assumption was that the
assembly would hold a version constant to read.

**It does not.** A search for version fields across all 251 game types returns
three, and only one is the game version:

| Field | What it actually is |
| --- | --- |
| `MainMenuControls.currentversion` (`static float`) | The game version — but see below |
| `GameVars.lastversionupdate` (`static float`) | Set *from* `currentversion`; tracks "version at last update" for changelog/migration purposes |
| `RoadObjectScript.version` (`static string`) | Third-party (EasyRoads3D) |

`MainMenuControls.currentversion` is **parsed out of a UI label at runtime**:
it finds `UICampaign/VersionID`, reads that `TextMeshProUGUI`'s text, and takes
a 6-character substring, then `float.Parse`s it.

So the version is:

- **authored in a scene asset**, not in code — it ships in the Unity scene
  data, not in `Assembly-CSharp.dll`;
- **unavailable until the campaign UI exists** — far too late for a load-time
  gate;
- **a `float`** (6 chars, so something like `1.0923`), which means version
  comparison inherits float semantics and cannot express a four-part version;
- **fragile** — it breaks if that label is restyled, renamed, localized, or
  prefixed.

## Consequence for the version gate

> **Hashing `Assembly-CSharp.dll` is not merely the convenient option — it is
> the only viable one.**

The plan already specified a signature manifest keyed on the assembly hash
(`00-build-provenance.md` records a SHA-256 for this build). That stands
unchanged, and this finding removes the alternative rather than adding work.

Design notes:

- Gate on the **assembly hash or MVID**, computed at plugin load, before any
  patch is applied. Both are available without the game being in any
  particular scene.
- The hash is a *whole-assembly* identity, so it changes on every patch even
  when nothing a given mod touches has moved. That is why the plan pairs it
  with a **per-method signature manifest** — the hash says "something changed",
  the manifest says "and it was this". Without the manifest, every patch day
  looks like a total break.
- `MainMenuControls.currentversion` is still worth reading **for reporting**,
  once the campaign UI is up: it lets a mismatch be phrased in the player's own
  terms ("this mod was built for 1.09, you are running 1.11") instead of as a
  hash. Read it defensively — null-check the `Find`, and do not let a
  `float.Parse` failure take down the plugin.

## Open questions

- The MVID's stability across rebuilds of the *same* source is not verified
  here; if the developers' CI rebuilds without changes, MVID would churn while
  behaviour did not. The SHA-256 of the file has the same property. This is why
  the manifest matters more than the identity.
- Whether a version string also exists in a `Config/` file or in
  `ImportExportUnitData.CreateVersionFile`'s output
  ([08-save-load-surface](08-save-load-surface.md) notes a float save-data
  version, which is a *different* version number — save format, not game
  build). Worth confirming the two are not conflated anywhere.
