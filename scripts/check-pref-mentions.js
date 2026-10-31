#!/usr/bin/env node
/*
 * check-pref-mentions.js
 *
 * The loose companion to check-pref-quotes.js: finds a known pref name
 * anywhere on a line, backticked or not, takes the numbers in the 70
 * characters after it (up to the next pref name), and flags the mention when
 * none of those numbers equals any of the pref's replayed values, its value
 * times 100, or its magnitude. Names shorter than ten characters are skipped
 * as too likely to be ordinary words.
 *
 * Noisy by design: most hits are numbers that belong to something else on the
 * line. Use it to find quotes the strict checker cannot parse, and review
 * every hit by hand.
 *
 * Usage:
 *   node scripts/replay-all-prefs.js <replayDir>
 *   node scripts/check-pref-mentions.js <replayDir> <doc.md>...
 */
'use strict';
const fs = require('fs'), path = require('path');
const [replayDir, ...docs] = process.argv.slice(2);
if (!replayDir || !docs.length) {
  console.error('usage: node scripts/check-pref-mentions.js <replayDir> <doc.md>...');
  process.exit(2);
}
const table = {};
for (const f of ['unit', 'battle', 'campaign', 'ui', 'uib']) {
  const p = path.join(replayDir, `replay-${f}.txt`);
  for (const line of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
    if (!line || line[0] === '#' || line.startsWith('??')) continue;
    const [n, v] = line.replace(/^!! /, '').split('\t');
    const b = n.replace(/[\[{].*$/, '');
    if (/^(num\d*|text\d*|array\d*|i|j|k)$/.test(b) || b.length < 10) continue;
    (table[b] = table[b] || new Set()).add(parseFloat(v));
  }
}
const names = Object.keys(table).sort((a, b) => b.length - a.length);
const re = new RegExp(String.raw`\b(` + names.join('|') + String.raw`)\b`, 'g');
let flagged = 0, seen = 0;
for (const doc of docs) {
  const lines = fs.readFileSync(doc, 'utf8').replace(/−/g, '-').replace(/\*\*/g, '').split(/\r?\n/);
  lines.forEach((ln, i) => {
    for (const m of ln.matchAll(re)) {
      const after = ln.slice(m.index + m[0].length, m.index + m[0].length + 70);
      // stop at the next pref name so its numbers aren't attributed here
      const nxt = after.search(re); const win = nxt >= 0 ? after.slice(0, nxt) : after;
      const nums = [...win.matchAll(/(?<![~§\w.])([-+]?\d*\.?\d+)(?![\w])/g)].map(x => parseFloat(x[1]));
      if (!nums.length) continue;
      seen++;
      const vals = [...table[m[1]]].filter(v => !isNaN(v));
      if (!vals.length) continue;
      const ok = nums.some(n => vals.some(v => Math.abs(v - n) < 1e-9 || Math.abs(v * 100 - n) < 1e-6 || Math.abs(Math.abs(v) - Math.abs(n)) < 1e-9));
      if (!ok) { flagged++; console.log(`${path.relative('.', doc)}:${i + 1}\t${m[1]}\tnums ${nums.join(',')}\tactual ${vals.slice(0, 6).join(' / ')}\t${ln.trim().slice(0, 130)}`); }
    }
  });
}
console.error(`${seen} mentions with numbers checked, ${flagged} flagged`);
