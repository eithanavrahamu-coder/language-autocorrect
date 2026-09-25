// Reads what the website needs from the app itself, so the site never drifts from the app:
//   - the languages (names, badges, colors, keyboards) from src/LayoutBuddy.Engine/Languages.cs
//   - the app version from src/LayoutBuddy/LayoutBuddy.csproj
//   - the most common words of a few languages, for the "try it yourself" demo
// Runs before `npm run dev` and `npm run build`.
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { appVersion } from './app-info.mjs';

const root = path.resolve(import.meta.dirname, '../..');
const site = path.resolve(import.meta.dirname, '..');

/** Languages the in-page demo can check words in (simple keyboards, no syllable joining or unspaced text). */
export const DEMO_LANGUAGES = ['en', 'he', 'ru', 'uk', 'ar', 'fa', 'el'];
const DEMO_WORDS = 20000;

// ---------- a tiny reader for the C# in Languages.cs ----------

function readString(src, i, consts) {
  // src[i] is '"' or '$' (interpolated). Returns [value, next index].
  const interpolated = src[i] === '$';
  if (interpolated) i++;
  if (src[i] !== '"') throw new Error(`Expected a string at ${i}`);
  i++;
  let out = '';
  while (src[i] !== '"') {
    const c = src[i];
    if (c === '\\') {
      const n = src[i + 1];
      if (n === 'u') { out += String.fromCharCode(parseInt(src.slice(i + 2, i + 6), 16)); i += 6; continue; }
      out += { n: '\n', t: '\t', '0': '\0' }[n] ?? n;
      i += 2;
      continue;
    }
    if (interpolated && c === '{') {
      const end = src.indexOf('}', i);
      const name = src.slice(i + 1, end).trim();
      if (!(name in consts)) throw new Error(`Unknown constant ${name} in Languages.cs`);
      out += consts[name];
      i = end + 1;
      continue;
    }
    out += c;
    i++;
  }
  return [out, i + 1];
}

function skipSpace(src, i) {
  for (;;) {
    while (/\s/.test(src[i])) i++;
    if (src.startsWith('//', i)) { i = src.indexOf('\n', i); continue; }
    return i;
  }
}

/** Reads one argument: a string, or any other expression (returned as its source text). */
function readArg(src, i, consts) {
  i = skipSpace(src, i);
  if (src[i] === '"' || (src[i] === '$' && src[i + 1] === '"')) {
    const [v, next] = readString(src, i, consts);
    return [v, skipSpace(src, next)];
  }
  let depth = 0;
  const start = i;
  while (depth > 0 || (src[i] !== ',' && src[i] !== ')' && src[i] !== '}')) {
    if ('({['.includes(src[i])) depth++;
    if (')}]'.includes(src[i])) depth--;
    i++;
  }
  return [src.slice(start, i).trim(), i];
}

async function readLanguages() {
  const src = await readFile(path.join(root, 'src/LayoutBuddy.Engine/Languages.cs'), 'utf8');
  const consts = {};
  for (const m of src.matchAll(/const string (\w+) = /g)) {
    consts[m[1]] = readString(src, m.index + m[0].length, {})[0];
  }
  const listStart = src.indexOf('IReadOnlyList<LanguageInfo> All');
  if (listStart < 0) throw new Error('Language list not found in Languages.cs');

  const languages = [];
  const entry = /new\(Lang\.(\w+),/g;
  entry.lastIndex = listStart;
  for (let m; (m = entry.exec(src));) {
    let i = m.index + m[0].length;
    const args = [];
    for (;;) {
      const [v, next] = readArg(src, i, consts);
      args.push(v);
      i = next;
      if (src[i] === ')') break;
      i++; // ','
    }
    const [code, name, nativeName, badge, color, , , hasCase, rtl, keyboard] = args;
    const lang = {
      code, name, nativeName, badge, color,
      hasCase: hasCase === 'true', rtl: rtl === 'true',
      keyboard: keyboard.split(' '), shiftKeyboard: null, beta: false,
    };
    // An optional { Name = value, ... } block after the arguments.
    i = skipSpace(src, i + 1);
    if (src[i] === '{') {
      i++;
      for (;;) {
        i = skipSpace(src, i);
        if (src[i] === '}') break;
        const prop = /^(\w+)\s*=\s*/.exec(src.slice(i));
        i += prop[0].length;
        const [v, next] = readArg(src, i, consts);
        if (prop[1] === 'ShiftKeyboard') lang.shiftKeyboard = v.split(' ');
        if (prop[1] === 'Beta') lang.beta = v === 'true';
        i = skipSpace(src, next);
        if (src[i] === ',') i++;
      }
    }
    for (const k of [lang.keyboard, lang.shiftKeyboard].filter(Boolean)) {
      if (k.length !== 47) throw new Error(`${name}: expected 47 keys, got ${k.length}`);
    }
    languages.push(lang);
    entry.lastIndex = i;
  }
  if (languages.length < 2 || languages[0].code !== 'en') throw new Error('Could not read the languages');
  return languages;
}

async function writeWordLists() {
  const out = path.join(site, 'public/words');
  await mkdir(out, { recursive: true });
  for (const code of DEMO_LANGUAGES) {
    const text = await readFile(path.join(root, `src/LayoutBuddy.Engine/Data/${code}.txt`), 'utf8');
    const words = text.split('\n').slice(0, DEMO_WORDS).map(l => l.split(' ')[0].trim()).filter(Boolean);
    await writeFile(path.join(out, `${code}.txt`), words.join('\n'));
  }
}

const languages = await readLanguages();
const info = {
  version: await appVersion(),
  // Set by the website build on GitHub, which knows the size of the file it publishes.
  downloadBytes: Number(process.env.DOWNLOAD_BYTES) || null,
  builtAt: new Date().toISOString(),
  languages,
  demoLanguages: DEMO_LANGUAGES,
};
await mkdir(path.join(site, 'src/generated'), { recursive: true });
await writeFile(path.join(site, 'src/generated/app-info.json'), JSON.stringify(info, null, 2));
await writeWordLists();
console.log(`Language Autocorrect ${info.version}: ${languages.length} languages` +
  (info.downloadBytes ? `, download ${(info.downloadBytes / 1048576).toFixed(1)} MB` : ''));
