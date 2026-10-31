# Scripts

Analysis and parsing tooling written for this project. Tracked in git —
unlike `tools/`, which holds third-party binaries you download separately.

Keep scripts reading from paths under `game/`, `extracted/` or `decompiled/`
so they stay runnable without hardcoded absolute paths.

## Index

| Script | What it does |
| --- | --- |
| [`simulate-prefs-loader.js`](simulate-prefs-loader.js) | Replays a range of a decompiled `GamePrefs` loader against a config file, counting `ReadLine()` calls, and prints which config line each variable actually receives. The way to read a pref's shipped value — labels are not proof (see the verification rules in [`code-findings/README.md`](../code-findings/README.md)). `--search N` finds the right start line; `!!` marks a value that would not parse. Node.js, no dependencies |
| [`fix-debug-nations.ps1`](fix-debug-nations.ps1) | Upgrades `Battlefields/DebugArmySetup/nations.dat` from the legacy 40-line alliance block to the current 42-line one ([32](../code-findings/32-debug-army-setup.md)) |
| [`fix-debug-regiments.ps1`](fix-debug-regiments.ps1) | Upgrades the same folder's `regiments.dat` from the legacy 38-line record to the current 39-line one |
