# Tools

Third-party tooling goes here. **The binaries themselves are gitignored** —
download them yourself rather than vendoring them into the repo.

Record what you actually used, with versions, so findings stay reproducible:

| Tool | Version | Installed at | Used for |
| --- | --- | --- | --- |
| .NET SDK | 10.0.401 | `C:\Program Files\dotnet\sdk` | Building the hook library; hosting `ilspycmd` |
| `ilspycmd` | 11.1.0.9782 | `%USERPROFILE%\.dotnet\tools\ilspycmd.exe` | Scripted/repeatable decompilation to a browsable tree |
| ILSpy (GUI) | 11.1.0.9782 | `%LOCALAPPDATA%\Programs\ILSpy` | Interactive browsing, cross-references, single-type lookups |

Installed 2026-10-12. `ilspycmd` comes from NuGet
(`dotnet tool install -g ilspycmd`) and is **not** included in the ILSpy MSI,
which ships the GUI only.

> **PATH caveat:** `%USERPROFILE%\.dotnet\tools` is added to PATH by the
> installer but won't appear in already-open shells. Either reopen the shell
> or invoke the tool by full path.

Version 11.1 targets .NET 10, so it runs on the SDK above with no
roll-forward configuration. Older `ilspycmd` releases target net8.0/net9.0 and
would need `DOTNET_ROLL_FORWARD=Major` or a matching runtime installed.

Typical Unity reversing stack:

- **AssetRipper / AssetStudio** — asset and scene extraction
- **Il2CppDumper** — IL2CPP metadata → C# signatures (for IL2CPP builds)
- **ILSpy / dnSpy / dotPeek** — managed assembly decompilation (for Mono builds)
- **Ghidra / IDA** — native `GameAssembly` analysis, with dumper output applied

Check whether the build is Mono or IL2CPP first: a `*_Data/Managed/` folder
with `Assembly-CSharp.dll` means Mono; `GameAssembly.dll` plus
`global-metadata.dat` means IL2CPP. That decides the whole toolchain.
