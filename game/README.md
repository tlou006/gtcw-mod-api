# game/

Put a copy of the game install here.

**Nothing in this folder is committed.** `.gitignore` excludes `game/*` with a
single exception for this README, so the folder still exists on a fresh clone
to show where the game belongs — but the files themselves stay on your disk
only.

## What to put here

A copy of the install directory, not the original:

```
game/
├── <Game>.exe
├── <Game>_Data/
│   ├── Managed/              ← Mono build: Assembly-CSharp.dll lives here
│   ├── il2cpp_data/          ← IL2CPP build: global-metadata.dat lives here
│   ├── resources.assets
│   ├── sharedassets*.assets
│   └── level*
├── GameAssembly.dll          ← IL2CPP build only
└── UnityPlayer.dll
```

Work from a copy so that a patch, a verify-files, or an accidental write never
costs you the build you took notes against.

## Record the build in notes/

Because this folder is untracked, anything you need to remember about the build
goes in `notes/` instead — version string, platform/store, install date, and
the SHA-256 of the main assembly. Findings are only reproducible if you know
which build produced them, and a game that auto-updates will quietly
invalidate offsets.

```powershell
# From the repo root
Get-FileHash game\GameAssembly.dll -Algorithm SHA256
Get-FileHash game\*_Data\Managed\Assembly-CSharp.dll -Algorithm SHA256
```

## Which toolchain applies

Check this folder before picking tools:

| You see | Build type | Decompile with |
| --- | --- | --- |
| `*_Data/Managed/Assembly-CSharp.dll` | Mono | ILSpy, dnSpy, dotPeek |
| `GameAssembly.dll` + `global-metadata.dat` | IL2CPP | Il2CppDumper, then Ghidra/IDA |
