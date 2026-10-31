#!/usr/bin/env node
/*
 * count-dead-prefs.js
 *
 * Independently counts GamePrefs fields that one of the six config loaders
 * assigns and nothing reads, the census in code-findings/29-dead-prefs-audit.md.
 *
 * A read is any line mentioning the field except, inside GamePrefs.cs, its
 * declaration, a plain loader write (`x = ...` or `x[i] = ...`) and a
 * `for (...; i < x.Length; ...)` loop that only fills it. Outside GamePrefs.cs
 * a read must be qualified as `GamePrefs.x`: the assembly has no
 * `using static`, so a bare name elsewhere is a different variable.
 *
 * The six replay tables do not cover the DLC loader (dlcwl_config.dat), so
 * the count here comes out three lower than findings/29's 144: the three dead
 * DLC prefs are counted there by hand.
 *
 * Usage:
 *   node scripts/replay-all-prefs.js <replayDir>
 *   node scripts/count-dead-prefs.js <replayDir> decompiled/Assembly-CSharp
 *
 * Prints `<loader>\t<field>` for each dead field; the totals go to stderr.
 */
'use strict';
const fs = require('fs'), path = require('path');
const [replayDir, srcDir] = process.argv.slice(2);
if (!replayDir || !srcDir) {
  console.error('usage: node scripts/count-dead-prefs.js <replayDir> <decompiledDir>');
  process.exit(2);
}
const srcs = fs.readdirSync(srcDir).filter(f => f.endsWith('.cs'))
  .map(f => [f, fs.readFileSync(path.join(srcDir, f), 'utf8').split(/\r?\n/)]);
const gp = srcs.find(([n]) => n === 'GamePrefs.cs')[1];
const declared = new Set();
for (const l of gp) { const m = l.match(/^\tpublic static [\w\[\], <>]+? (\w+)( =|;)/); if (m) declared.add(m[1]); }
const loaded = new Map(); // name -> source file
for (const f of ['unit', 'battle', 'campaign', 'ui', 'uib', 'debug']) {
  for (const line of fs.readFileSync(path.join(replayDir, `replay-${f}.txt`), 'utf8').split(/\r?\n/)) {
    if (!line || line[0] === '#' || line.startsWith('??')) continue;
    const b = line.replace(/^!! /, '').split('\t')[0].replace(/[\[{].*$/, '');
    if (declared.has(b) && !loaded.has(b)) loaded.set(b, f);
  }
}
const dead = [];
for (const [name, f] of loaded) {
  const re = new RegExp(String.raw`\b` + name + String.raw`\b`);
  // Outside GamePrefs a consumer must qualify the name (no `using static` in the assembly).
  const qualified = new RegExp(String.raw`\bGamePrefs\.` + name + String.raw`\b`);
  const write = new RegExp('^(GamePrefs\\.)?' + name + String.raw`(\[[^\]]*\])? = `);
  const lenLoop = new RegExp(String.raw`^for \(int \w+ = 0; \w+ <=? ` + name + String.raw`\.(Length|Count)`);
  let reads = 0;
  for (const [fn, lines] of srcs) for (const l of lines) {
    if (!(fn === 'GamePrefs.cs' ? re : qualified).test(l)) continue;
    const t = l.trim();
    if (fn === 'GamePrefs.cs' && (t.startsWith('public static') || write.test(t) || lenLoop.test(t))) continue;
    reads++;
  }
  if (reads === 0) dead.push(`${f}\t${name}`);
}
console.log(dead.sort().join('\n'));
console.error(`declared ${declared.size}; loaded by the six loaders ${loaded.size}; loaded and never read ${dead.length}`);
