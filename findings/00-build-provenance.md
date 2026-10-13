# Build provenance

Which build the findings in this repo were written against. Record a new entry
here whenever the game updates — offsets, metadata indices and method bodies
all shift between builds, and a note that doesn't name its build can't be
trusted later.

## Build under analysis

| | |
| --- | --- |
| Title | Grand Tactician: The Civil War (1861-1865) |
| Steam appid | 654890 |
| Steam buildid | 14703111 |
| Steam last updated | 2026-10-13 03:24 UTC |
| Analyzed from | 2026-10-12 |
| Engine | Unity 2021.3.16f1 (`4016570cf34f`) |
| Scripting backend | **Mono** (not IL2CPP) |
| Platform | Windows x64 |

### Hashes (SHA-256)

| File | Hash |
| --- | --- |
| `Assembly-CSharp.dll` | `AE15E3E479F4D79B70E40CED42EB93A953569B8185E075B3873C7410462AE8C4` |
| `UnityPlayer.dll` | `3B07BC9314435D374B825593265362D0870958E6FA7642D85952471148625971` |

Re-check before trusting an old note:

```powershell
Get-FileHash "game\The Civil War (1861-1865)_Data\Managed\Assembly-CSharp.dll" -Algorithm SHA256
```

> **Steam updated this build on 2026-10-13, the day analysis started.** Consider
> setting the game to "Only update when launched" in Steam so a background
> update doesn't silently replace the build these notes describe.

## Source layout

Install lives at `E:\SteamLibrary\steamapps\common\Grand Tactician The Civil War (1861-1865)`
(15.66 GB, 5,210 files). `game/` holds a 778 MB copy: assemblies, config,
scenario data and small `_Data` metadata. The asset blobs were left in place
and are read from E: directly when needed.

| Left on E: | Size |
| --- | --- |
| `resources.assets` + `.resS` | 4.96 GB |
| `StreamingAssets/` | 4.71 GB |
| other large `sharedassets*` | ~5.5 GB |
| `GI/` (lightmaps) | 221 MB |

## Toolchain implication

Mono backend means the game's code is managed CIL, so a decompiler recovers
near-source C# directly — no IL2CPP dumping, no native address arithmetic,
no Ghidra pass. Use ILSpy, dnSpy or dotPeek on `Assembly-CSharp.dll`.

### Assemblies worth attention

Game code, as distinct from engine and framework assemblies:

| Assembly | Size | What it is |
| --- | --- | --- |
| `Assembly-CSharp.dll` | 3.4 MB | **The game.** All gameplay logic |
| `Assembly-CSharp-firstpass.dll` | 140 KB | Code compiled ahead of the main pass |
| `EasyRoads3Dv3.dll` + `EasyRoads3D.dll` | 1.3 MB | Third-party road/path asset |
| `TriangleNET.dll`, `MIConvexHull.dll`, `DelaunayER.dll` | 257 KB | Computational geometry — likely map/terrain generation |
| `Newtonsoft.Json.dll` | 670 KB | JSON serialization — suggests JSON save/config formats |

`TriangleNET` and `MIConvexHull` being present is a hint worth following:
Delaunay triangulation and convex hulls point at procedural terrain or
territory/region partitioning rather than hand-authored maps.

## Open questions

- Is `Assembly-CSharp.dll` obfuscated? (Check type/method naming in ILSpy first.)
- What format are campaign saves and the `Campaigns/` data files?
- What does `scenelog.dat` (4.4 MB) contain?
- Is `Modding/` a documented modding surface? It may describe data formats
  directly and save a lot of reversing.
