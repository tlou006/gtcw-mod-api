#!/usr/bin/env node
/*
 * check-dead-claims.js
 *
 * Finds GamePrefs fields that markdown calls dead ("dead", "unread", "never
 * read", "no consumers", ...) and counts lines in the decompiled source that
 * mention them other than the declaration and plain loader writes. Any hit
 * means the claim needs a second look.
 *
 * Only backticked names on the same line as the dead wording are checked, and
 * only names declared `public static` in GamePrefs.cs. A hit is a line, not a
 * proven read: `x[i] += ...` or a `.Length` loop bound will show up and has to
 * be judged by hand. count-dead-prefs.js applies the stricter rules.
 *
 * Usage:
 *   node scripts/check-dead-claims.js decompiled/Assembly-CSharp <doc.md>...
 */
'use strict';
const fs = require('fs'), path = require('path');
const [srcDir, ...docs] = process.argv.slice(2);
if (!srcDir || !docs.length) {
  console.error('usage: node scripts/check-dead-claims.js <decompiledDir> <doc.md>...');
  process.exit(2);
}
const files = fs.readdirSync(srcDir).filter(f => f.endsWith('.cs')).map(f => path.join(srcDir, f));
const srcs = files.map(f => [path.basename(f), fs.readFileSync(f, 'utf8').split(/\r?\n/)]);
const gp = srcs.find(([n]) => n === 'GamePrefs.cs')[1].join('\n');
const declRe = n => new RegExp(String.raw`public static [\w\[\], ]+ ` + n + String.raw`\b`);
const isPref = n => declRe(n).test(gp);
function reads(name) {
  const re = new RegExp(String.raw`\b` + name + String.raw`\b`);
  const write = new RegExp('^(GamePrefs\\.)?' + name + String.raw`(\[[^\]]*\])? = `);
  const hits = [];
  for (const [fn, lines] of srcs) lines.forEach((l, i) => {
    if (!re.test(l)) return;
    const t = l.trim();
    if (declRe(name).test(t) && t.startsWith('public static')) return;   // declaration
    if (write.test(t)) return;                                          // loader write / allocation
    hits.push(`${fn}:${i + 1}`);
  });
  return hits;
}
const DEAD = /\b(dead|unread|never read|never used|no consumers?|declared and never|parsed and never|nothing reads|not read)\b/i;
const out = []; const checked = new Set();
for (const doc of docs) {
  const lines = fs.readFileSync(doc, 'utf8').split(/\r?\n/);
  lines.forEach((ln, i) => {
    if (!DEAD.test(ln)) return;
    for (const m of ln.matchAll(/`(?:GamePrefs\.)?([a-z_][a-z0-9_]{5,})(?:\[[^\]`]*\])?`/g)) {
      const n = m[1]; if (!isPref(n)) continue;
      const key = doc + '|' + n; if (checked.has(key)) continue; checked.add(key);
      const h = reads(n);
      if (h.length) out.push(`${path.relative('.', doc)}:${i + 1}\t${n}\t${h.length} reads: ${h.slice(0, 4).join(', ')}\t${ln.trim().slice(0, 160)}`);
    }
  });
}
console.log(out.join('\n'));
console.error(`${checked.size} (file, identifier) pairs near dead/unread wording checked; ${out.length} have reads`);
