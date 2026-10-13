# Tools

Third-party tooling goes here. **The binaries themselves are gitignored** —
download them yourself rather than vendoring them into the repo.

Record what you actually used, with versions, so findings stay reproducible:

| Tool | Version | Used for |
| --- | --- | --- |
|  |  |  |

Typical Unity reversing stack:

- **AssetRipper / AssetStudio** — asset and scene extraction
- **Il2CppDumper** — IL2CPP metadata → C# signatures (for IL2CPP builds)
- **ILSpy / dnSpy / dotPeek** — managed assembly decompilation (for Mono builds)
- **Ghidra / IDA** — native `GameAssembly` analysis, with dumper output applied

Check whether the build is Mono or IL2CPP first: a `*_Data/Managed/` folder
with `Assembly-CSharp.dll` means Mono; `GameAssembly.dll` plus
`global-metadata.dat` means IL2CPP. That decides the whole toolchain.
