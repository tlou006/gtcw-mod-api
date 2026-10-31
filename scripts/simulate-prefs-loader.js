#!/usr/bin/env node
/*
 * simulate-prefs-loader.js
 *
 * Reads a GamePrefs config value the way the game does: by position.
 *
 * The prefs loaders in GamePrefs (loadunitprefs, loadcontrols,
 * loadcampaigncontrols, ...) walk a text file with StreamReader.ReadLine(),
 * discarding a label line and parsing the next line as the value. Nothing ties
 * a value to the label printed above it except the count of ReadLine() calls
 * before it, so the label is a claim about the value, not proof. This script
 * replays a range of the decompiled loader statement by statement, counting
 * ReadLine() calls, and prints which config line each variable actually
 * receives.
 *
 * Usage:
 *   node scripts/simulate-prefs-loader.js <loader.cs> <csFrom> <csTo> <config.txt> <cfgLine> [options]
 *
 *   loader.cs  usually decompiled/Assembly-CSharp/GamePrefs.cs
 *   csFrom     first line of the loader range to replay (1-based, inclusive)
 *   csTo       last line of the range (inclusive)
 *   config.txt the file that loader reads, e.g. game/Config/unitprefs.txt
 *   cfgLine    the config line consumed by the first ReadLine() in the range
 *
 * Options:
 *   --search N   cfgLine is a guess: try every start within N lines of it and
 *                use the first one under which every typed value parses.
 *                Prints the candidates. Use this rather than counting by hand.
 *   --only RE    print only variables whose name matches the regex RE
 *   --set K=V    seed a loop bound that was read before csFrom, e.g.
 *                --set unittypes=19 (repeatable)
 *
 * Output, one line per assignment, tab-separated:
 *   variable  value  cfg <line>  label: <the line above the value>
 * followed by a summary. "!!" marks a value that would not parse as its
 * declared type: the game would throw on that line, so your start line or
 * range is wrong. "??" marks a statement the script did not understand.
 *
 * Supported loader idioms: `[text = ]streamReader.ReadLine();`,
 * `x = T.Parse(streamReader.ReadLine());` for float/int/bool, plain string
 * reads, 1-D and 2-D array element targets (`a[i]`, `a[i, j]`),
 * `a = new T[n]` / `new T[n, m]` (recorded for `.Length`), and
 * `for (int i = 0; i < BOUND; i++)` (or `<=`) where BOUND is a literal, an
 * int read earlier in the replay, a --set value, `array.Length`, or a sum or
 * product of those, and typed local reads such as
 * `int n = int.Parse(streamReader.ReadLine());`. An `if` block
 * whose body reads the file stops the replay with a message, because which
 * branch runs depends on runtime state (usually a version gate): simulate
 * each branch as its own range. An `if` block that does not read is skipped.
 *
 * Example (battle-perk block of unitprefs.txt, as checked in
 * code-findings/34-perks.md §1):
 *   node scripts/simulate-prefs-loader.js decompiled/Assembly-CSharp/GamePrefs.cs \
 *        5593 5836 game/Config/unitprefs.txt 1676 --search 5
 */
'use strict';
const fs = require('fs');

function usage(msg) {
  if (msg) console.error('error: ' + msg);
  console.error('usage: node simulate-prefs-loader.js <loader.cs> <csFrom> <csTo> <config.txt> <cfgLine> [--search N] [--only RE] [--set K=V]');
  process.exit(2);
}

const argv = process.argv.slice(2);
const pos = [];
const opts = { search: 0, only: null, set: {} };
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === '--search') opts.search = parseInt(argv[++i], 10);
  else if (a === '--only') opts.only = new RegExp(argv[++i]);
  else if (a === '--set') {
    const [k, v] = String(argv[++i]).split('=');
    if (!k || v === undefined || isNaN(+v)) usage('--set needs NAME=NUMBER');
    opts.set[k] = +v;
  } else pos.push(a);
}
if (pos.length !== 5) usage();
const [csPath, csFromS, csToS, cfgPath, cfgLineS] = pos;
const csFrom = +csFromS, csTo = +csToS, cfgLine = +cfgLineS;
if (![csFrom, csTo, cfgLine].every(Number.isInteger) || csFrom < 1 || csTo < csFrom || cfgLine < 1) usage('line numbers must be positive integers with csFrom <= csTo');

const src = fs.readFileSync(csPath, 'utf8').split(/\r?\n/);
// The game opens these files with Encoding.Default; latin1 keeps byte-for-byte line content.
const cfg = fs.readFileSync(cfgPath, 'latin1').split(/\r?\n/);
const body = src.slice(csFrom - 1, csTo);

// Value shapes the game's Parse calls accept. SetCultureInfo forces
// InvariantCulture at startup (code-findings/32), so '.' is the decimal mark.
const PARSES = {
  float: s => /^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(s.trim()),
  int: s => /^[-+]?\d+$/.test(s.trim()),
  bool: s => /^(true|false)$/i.test(s.trim()),
  string: () => true,
};

const RE_FOR = /^for \(int (\w+) = 0; \1 (<=?) ([^;]+); \1\+\+\)$/;
const RE_SKIP_READ = /^(?:\w+ = )?streamReader\.ReadLine\(\);$/;
const RE_ASSIGN = /^(?:(?:int|float|bool|string|var) )?([\w.]+)(?:\[([\w\s,]+)\])? = (?:(float|int|bool)\.Parse\()?streamReader\.ReadLine\(\)\)?;$/;
const RE_NEW = /^([\w.]+) = new (?:float|int|bool|string)\[([^,\]]+)(?:, ([^\]]+))?\];$/;
const RE_IF = /^(?:else )?if \(|^else$/;

// Index of the closing brace that matches the block opening after line i.
function blockEnd(lines, i) {
  let depth = 0;
  for (let j = i + 1; j < lines.length; j++) {
    const t = lines[j].trim();
    if (t === '{') depth++;
    else if (t === '}') { depth--; if (depth === 0) return j; }
  }
  return -1;
}

class Stop extends Error {}

function simulate(startLine, quiet) {
  let ptr = startLine; // next config line (1-based) to be read
  const env = Object.assign({}, opts.set);
  const rows = [];
  const unknown = [];
  let failures = 0;
  // A loop bound or array size: a literal, a known name, `x.Length`, or a
  // sum/difference/product of those. Undefined if any part is unknown.
  const bound = expr => {
    const e = String(expr).trim();
    if (/^\d+$/.test(e)) return +e;
    if (e in env) return env[e];
    if (!/^[\w.\s+*-]+$/.test(e)) return undefined;
    let unknownPart = false;
    const js = e.replace(/[A-Za-z_][\w.]*/g, id => {
      if (id in env) return '(' + env[id] + ')';
      unknownPart = true;
      return '0';
    });
    return unknownPart ? undefined : Function('return (' + js + ');')();
  };

  function run(lines, base) {
    for (let i = 0; i < lines.length; i++) {
      let t = lines[i].trim();
      const csLine = base + i;
      // A multi-line initializer (`x = new List<float>` / `{` / ... / `};`)
      // is one statement: join it so the multi-read path below sees it whole.
      if (/^[\w.\[\], ]+ = new [\w<>\[\], ]+$/.test(t) && (lines[i + 1] || '').trim() === '{') {
        let j = i + 1;
        while (j < lines.length && lines[j].trim() !== '};') j++;
        if (j >= lines.length) throw new Stop(`cs ${csLine}: unterminated initializer`);
        t = lines.slice(i, j + 1).map(l => l.trim()).join(' ');
        i = j;
      }
      if (!t || t === '{' || t === '}') continue;
      let m;
      if ((m = t.match(RE_FOR))) {
        const end = blockEnd(lines, i);
        const limit = bound(m[3]);
        const n = limit === undefined ? undefined : (m[2] === '<=' ? limit + 1 : limit);
        if (end < 0) throw new Stop(`unbalanced braces after cs ${csLine}`);
        if (n === undefined) throw new Stop(`cs ${csLine}: loop bound '${m[3]}' unknown; pass --set NAME=N for each unknown name`);
        for (let k = 0; k < n; k++) { env[m[1]] = k; run(lines.slice(i + 2, end), base + i + 2); }
        i = end;
        continue;
      }
      if (RE_IF.test(t)) {
        // Single-statement ifs without braces are not used by these loaders.
        const end = blockEnd(lines, i);
        if (end < 0) throw new Stop(`cs ${csLine}: unbraced if not supported`);
        if (lines.slice(i + 1, end).some(l => l.includes('ReadLine'))) {
          throw new Stop(`cs ${csLine}: conditional read (${t}); replay each branch as its own range`);
        }
        i = end;
        continue;
      }
      if (RE_SKIP_READ.test(t)) { ptr++; continue; }
      if ((m = t.match(RE_ASSIGN))) {
        let name = m[1];
        if (m[2] !== undefined) {
          const idx = m[2].split(',').map(s => s.trim()).map(s => (bound(s) !== undefined ? bound(s) : s));
          name += '[' + idx.join(', ') + ']';
        }
        const type = m[3] || 'string';
        const value = cfg[ptr - 1] === undefined ? '<EOF>' : cfg[ptr - 1];
        const ok = value !== '<EOF>' && PARSES[type](value);
        if (!ok) failures++;
        if (type === 'int' && ok && m[2] === undefined) env[name] = parseInt(value, 10);
        rows.push({ name, value: value.trim(), line: ptr, label: (cfg[ptr - 2] || '').trim(), ok, type });
        ptr++;
        continue;
      }
      if ((m = t.match(/^(?:int )?(\w+) = (\d+);$/))) { env[m[1]] = +m[2]; continue; }
      if ((m = t.match(RE_NEW))) {
        const a = bound(m[2]);
        if (a !== undefined) env[m[1] + '.Length'] = m[3] === undefined ? a : a * (bound(m[3]) || 0);
        continue;
      }
      // Several typed reads in one statement, e.g.
      // `v = new Vector2(float.Parse(streamReader.ReadLine()), float.Parse(streamReader.ReadLine()));`
      const multi = t.match(/^(?:(?:int|float|bool|string|var|Vector[234]|Color) )?([\w.]+(?:\[[\w\s,]+\])?) = .*;$/);
      const reads = [...t.matchAll(/(float|int|bool)\.Parse\(streamReader\.ReadLine\(\)\)/g)];
      if (multi && reads.length > 0 && reads.length === (t.match(/ReadLine/g) || []).length) {
        reads.forEach((r, k) => {
          const type = r[1];
          const value = cfg[ptr - 1] === undefined ? '<EOF>' : cfg[ptr - 1];
          const ok = value !== '<EOF>' && PARSES[type](value);
          if (!ok) failures++;
          rows.push({ name: `${multi[1]}{${k}}`, value: value.trim(), line: ptr, label: (cfg[ptr - 2] || '').trim(), ok, type });
          ptr++;
        });
        continue;
      }
      if (t.includes('ReadLine')) throw new Stop(`cs ${csLine}: unrecognised read: ${t}`);
      unknown.push(`cs ${csLine}: ${t}`);
    }
  }

  let stopped = null;
  try { run(body, csFrom); } catch (e) { if (e instanceof Stop) stopped = e.message; else throw e; }
  if (!quiet) return { rows, unknown, failures, stopped, end: ptr };
  return { failures, stopped, count: rows.length };
}

let start = cfgLine;
if (opts.search > 0) {
  const candidates = [];
  for (let s = Math.max(1, cfgLine - opts.search); s <= cfgLine + opts.search; s++) {
    const r = simulate(s, true);
    if (!r.stopped && r.failures === 0 && r.count > 0) candidates.push(s);
  }
  console.log(`# --search ${opts.search}: start lines with every value parsing: ${candidates.length ? candidates.join(', ') : 'none'}`);
  if (candidates.length === 0) { console.log('# no clean start; showing the guess as given'); }
  else {
    start = candidates.includes(cfgLine) ? cfgLine : candidates[0];
    if (candidates.length > 1) console.log('# more than one start parses cleanly: compare the labels below before trusting any of them');
  }
}

const res = simulate(start, false);
console.log(`# replay of ${csPath} ${csFrom}-${csTo} against ${cfgPath} from line ${start}`);
for (const r of res.rows) {
  if (opts.only && !opts.only.test(r.name)) continue;
  console.log(`${r.ok ? '' : '!! '}${r.name}\t${r.value}\tcfg ${r.line}\tlabel: ${r.label.slice(0, 120)}`);
}
console.log(`# ${res.rows.length} values, ${res.failures} that would not parse as their type, next config line ${res.end}`);
if (res.unknown.length) {
  console.log(`# ${res.unknown.length} statements not understood (no file reads; ignored):`);
  for (const u of res.unknown.slice(0, 20)) console.log(`?? ${u}`);
  if (res.unknown.length > 20) console.log(`?? ... ${res.unknown.length - 20} more`);
}
if (res.stopped) { console.log(`# STOPPED: ${res.stopped}`); process.exitCode = 1; }
else if (res.failures) process.exitCode = 1;
