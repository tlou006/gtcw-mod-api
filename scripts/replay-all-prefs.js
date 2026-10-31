#!/usr/bin/env node
/*
 * replay-all-prefs.js
 *
 * Runs simulate-prefs-loader.js over all six GamePrefs config loaders and
 * writes one replay table per loader into an output directory:
 *
 *   replay-debug.txt     Config/debug.txt
 *   replay-ui.txt        Config/ui.txt
 *   replay-uib.txt       Config/ui_battle.txt
 *   replay-unit.txt      Config/unitprefs.txt
 *   replay-battle.txt    Config/battleprefs.txt
 *   replay-campaign.txt  Config/campaignprefs.txt
 *
 * Each loader's range is found in GamePrefs.cs rather than hardcoded: it runs
 * from the line after its `new StreamReader(` to the line before the next
 * `streamReader.Close()`. The check-*.js and count-dead-prefs.js scripts read
 * these tables.
 *
 * Usage:
 *   node scripts/replay-all-prefs.js <outDir> [decompiledDir] [configDir]
 *
 *   decompiledDir  default decompiled/Assembly-CSharp
 *   configDir      default game/Config
 */
'use strict';
const fs = require('fs'), path = require('path'), { execFileSync } = require('child_process');

const [outDir, srcDir = 'decompiled/Assembly-CSharp', cfgDir = 'game/Config'] = process.argv.slice(2);
if (!outDir) {
  console.error('usage: node scripts/replay-all-prefs.js <outDir> [decompiledDir] [configDir]');
  process.exit(2);
}
const gpPath = path.posix.join(srcDir, 'GamePrefs.cs');
const lines = fs.readFileSync(gpPath, 'utf8').split(/\r?\n/);

// How each loader's StreamReader line names its file. The unit and battle
// loaders take the path in a variable, so they are told apart by that name.
const loaders = [
  ['debug', /"Config\/debug\.txt"/, 'debug.txt'],
  ['ui', /"Config\/ui\.txt"/, 'ui.txt'],
  ['uib', /"Config\/ui_battle\.txt"/, 'ui_battle.txt'],
  ['unit', /new StreamReader\(folder, /, 'unitprefs.txt'],
  ['battle', /new StreamReader\(text, /, 'battleprefs.txt'],
  ['campaign', /"\/campaignprefs\.txt"/, 'campaignprefs.txt'],
];

fs.mkdirSync(outDir, { recursive: true });
const sim = path.join(__dirname, 'simulate-prefs-loader.js');
let failed = false;
for (const [name, re, cfg] of loaders) {
  const open = lines.findIndex(l => /new StreamReader\(/.test(l) && re.test(l));
  const close = open < 0 ? -1 : lines.findIndex((l, i) => i > open && /streamReader\.Close\(\)/.test(l));
  if (open < 0 || close < 0) { console.error(`${name}: loader not found in ${gpPath}`); failed = true; continue; }
  // 1-based: open is at line open+1, so the range is open+2 .. close (the line before Close()).
  const from = open + 2, to = close;
  const out = execFileSync(process.execPath, [sim, gpPath, String(from), String(to), path.posix.join(cfgDir, cfg), '1'], { encoding: 'utf8' });
  fs.writeFileSync(path.join(outDir, `replay-${name}.txt`), out);
  const bad = out.split(/\r?\n/).filter(l => l.startsWith('!!')).length;
  console.log(`${name}\tGamePrefs.cs ${from}-${to}\t${cfg}\t${out.split(/\r?\n/).filter(l => l && !/^[#?]/.test(l)).length} values${bad ? `, ${bad} would not parse` : ''}`);
  if (bad) failed = true;
}
process.exit(failed ? 1 : 0);
