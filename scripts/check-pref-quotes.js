#!/usr/bin/env node
/*
 * check-pref-quotes.js
 *
 * Compares pref values quoted in markdown against the loader replays.
 *
 * A quote is a backticked pref name followed by a number: `name(0.7)`,
 * `name` = 0.7, `name` is 0.7, `name` (0.7), or a slash sequence such as
 * `name` 40 / 80 / 120 checked element by element against an array. A quote
 * matches if it equals any value the replay assigns to that name, or that
 * value times 100 (percentages). Fields that no loader assigns fall back to
 * their GamePrefs initializer. Numbers after `~` or `§` are skipped.
 *
 * Usage:
 *   node scripts/replay-all-prefs.js <replayDir>
 *   node scripts/check-pref-quotes.js <replayDir> decompiled/Assembly-CSharp/GamePrefs.cs <doc.md>...
 *
 * Prints one tab-separated line per mismatch (file:line, name, quoted,
 * actual, source table, the line); a count goes to stderr. Review each hit by
 * hand: a quote can be of a different build, of a battlefield override, or of
 * a value computed from the pref.
 */
'use strict';
const fs = require('fs'), path = require('path');
const [replayDir, gpPath, ...docs] = process.argv.slice(2);
if (!replayDir || !gpPath || !docs.length) {
  console.error('usage: node scripts/check-pref-quotes.js <replayDir> <GamePrefs.cs> <doc.md>...');
  process.exit(2);
}
const table = {}; // name -> {vals:[...], src}
for (const f of ['unit', 'battle', 'campaign', 'ui', 'uib']) {
  const p = path.join(replayDir, `replay-${f}.txt`);
  if (!fs.existsSync(p)) continue;
  for (const line of fs.readFileSync(p, 'utf8').split(/\r?\n/)) {
    if (!line || line.startsWith('#') || line.startsWith('??')) continue;
    const [nameRaw, value, cfgl] = line.replace(/^!! /, '').split('\t');
    const base = nameRaw.replace(/[\[{].*$/, '');
    if (/^(num\d*|text\d*|array\d*|i|j|k)$/.test(base)) continue;   // loader locals, not prefs
    (table[base] = table[base] || { vals: [], src: f }).vals.push({ name: nameRaw, value, cfg: cfgl });
  }
}
// initializer-only fields (constants never loaded from a file)
const gp = fs.readFileSync(gpPath, 'utf8');
for (const m of gp.matchAll(/public static (?:float|int|bool) (\w+) = ([-\w.]+?)f?;/g)) {
  if (!table[m[1]]) table[m[1]] = { vals: [{ name: m[1], value: m[2], cfg: 'initializer' }], src: 'init' };
}
const norm = s => s.replace(/−/g, '-').replace(/\*\*/g, '');
const num = s => parseFloat(s);
const out = []; let checked = 0;
for (const doc of docs) {
  const text = norm(fs.readFileSync(doc, 'utf8'));
  const lines = text.split(/\r?\n/);
  lines.forEach((ln, li) => {
    for (const m of ln.matchAll(/`([a-z_][a-z0-9_]*)(\[[^\]`]*\])?(?:\(([-0-9.]+)\))?`/g)) {
      const name = m[1]; const entry = table[name]; if (!entry) continue;
      // quoted number: inline (x), or first number shortly after the closing backtick
      let quoted = m[3];
      const after = ln.slice(m.index + m[0].length, m.index + m[0].length + 40);
      if (quoted === undefined) {
        const a = after.match(/^\s*(?:\(|=|is|ships|of|,)?\s*(?:shipped\s+)?([-+]?\d*\.?\d+)(?![\d~§])/);
        if (a && !/^\s*[~§]/.test(after)) quoted = a[1];
      }
      if (quoted === undefined) continue;
      const q = num(quoted); if (isNaN(q)) continue;
      const vals = entry.vals.map(v => num(v.value)).filter(v => !isNaN(v));
      if (vals.length === 0) continue;
      // accept if it matches any element, or a sequence "a / b / c" matching the array
      let ok = vals.some(v => Math.abs(v - q) < 1e-9 || Math.abs(v * 100 - q) < 1e-6);
      const seq = after.match(/^\s*\(?\s*([-+]?\d*\.?\d+(?:\s*\/\s*[-+]?\d*\.?\d+)+)/);
      if (seq) {
        const s = seq[1].split('/').map(x => num(x));
        ok = s.length === vals.length ? s.every((x, i) => Math.abs(x - vals[i]) < 1e-9) : ok && s.every(x => vals.some(v => Math.abs(v - x) < 1e-9));
      }
      checked++; if (!ok) out.push(`${path.relative('.', doc)}:${li + 1}\t${name}${m[2] || ''}\tquoted ${seq ? seq[1] : quoted}\tactual ${entry.vals.slice(0, 8).map(v => v.value).join(' / ')}\t[${entry.src}]\t${ln.trim().slice(0, 140)}`);
    }
  });
}
console.log(out.join('\n'));
console.error(`${checked} quotes checked, ${out.length} possible mismatches; table has ${Object.keys(table).length} names`);
