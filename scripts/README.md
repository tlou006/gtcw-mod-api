# Scripts

Analysis and parsing tooling written for this project. Tracked in git —
unlike `tools/`, which holds third-party binaries you download separately.

Keep scripts reading from paths under `game/`, `extracted/` or `decompiled/`
so they stay runnable without hardcoded absolute paths.

## Index

| Script | What it does |
| --- | --- |
| [`simulate-prefs-loader.js`](simulate-prefs-loader.js) | Replays a range of a decompiled `GamePrefs` loader against a config file, counting `ReadLine()` calls, and prints which config line each variable actually receives. The way to read a pref's shipped value — labels are not proof (see the verification rules in [`code-findings/README.md`](../code-findings/README.md)). `--search N` finds the right start line; `!!` marks a value that would not parse. Node.js, no dependencies |
| [`replay-all-prefs.js`](replay-all-prefs.js) | Runs the simulator over all six config loaders (debug, ui, ui_battle, unitprefs, battleprefs, campaignprefs) and writes `replay-<loader>.txt` tables into a directory. Finds each loader's range in `GamePrefs.cs` itself, so it survives a re-decompile. Exits non-zero if any value would not parse. The four checkers below read these tables |
| [`check-pref-quotes.js`](check-pref-quotes.js) | Compares every pref value quoted in markdown (`` `name` = 0.7``, `` `name(0.7)` ``, `40 / 80 / 120` sequences) against the replays and lists mismatches |
| [`check-pref-mentions.js`](check-pref-mentions.js) | Loose version of the above: any mention of a pref name, backticked or not, with numbers after it. Noisy by design, for quotes the strict checker can't parse |
| [`check-dead-claims.js`](check-dead-claims.js) | For prefs that markdown calls dead, unread or never read, lists source lines that still mention them |
| [`count-dead-prefs.js`](count-dead-prefs.js) | Independent census of loaded-but-never-read prefs for [29](../code-findings/29-dead-prefs-audit.md): 141 from the six loaders, plus three DLC prefs counted there by hand, gives 144 |
| [`fix-debug-nations.ps1`](fix-debug-nations.ps1) | Upgrades `Battlefields/DebugArmySetup/nations.dat` from the legacy 40-line alliance block to the current 42-line one ([32](../code-findings/32-debug-army-setup.md)) |
| [`fix-debug-regiments.ps1`](fix-debug-regiments.ps1) | Upgrades the same folder's `regiments.dat` from the legacy 38-line record to the current 39-line one |
