// What each physical key types in each language: a port of the app's KeyMap (src/LanguageAutocorrect.Engine/KeyMap.cs).
// A physical key is named by the character it types on a US keyboard ("q", ";"), or its shifted character ("Q", ":").
import info from '../generated/app-info.json';

export type Language = (typeof info.languages)[number];

export const LANGUAGES: Language[] = info.languages;
export const byCode = (code: string): Language => LANGUAGES.find(l => l.code === code)!;

export const PHYSICAL_KEYS = '`1234567890-=qwertyuiop[]\\asdfghjkl;\'zxcvbnm,./';
export const SHIFTED_KEYS = '~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:"ZXCVBNM<>?';

type Token = { text: string; combining: string; dead: boolean };
type KeyMap = { plain: Token[]; shifted: Token[] };

const parse = (tokens: string[]): Token[] =>
  tokens.map(t => (t.length >= 3 && t[0] === '~'
    ? { text: t.slice(2), combining: t[1], dead: true }
    : { text: t, combining: '', dead: false }));

const maps = new Map<string, KeyMap>();
function mapFor(lang: Language): KeyMap {
  let m = maps.get(lang.code);
  if (!m) {
    const plain = parse(lang.keyboard);
    const shifted = lang.shiftKeyboard
      ? parse(lang.shiftKeyboard)
      : plain.map((t, i) =>
          lang.hasCase && !t.dead && t.text.length === 1 && t.text.toUpperCase() !== t.text
            ? { ...t, text: t.text.toUpperCase() }
            : { text: SHIFTED_KEYS[i], combining: '', dead: false });
    m = { plain, shifted };
    maps.set(lang.code, m);
  }
  return m;
}

function lookup(map: KeyMap, key: string): Token | null {
  let i = PHYSICAL_KEYS.indexOf(key);
  if (i >= 0) return map.plain[i];
  i = SHIFTED_KEYS.indexOf(key);
  return i >= 0 ? map.shifted[i] : null;
}

/**
 * Windows keyboards join an accent only with letters that have it in their alphabet's basic block: Latin-1 (á é ñ ü),
 * Greek (ά ϊ) and Cyrillic (ѓ ќ). Anything else (ḿ, ṽ) is typed as the accent, then the letter.
 */
const isBasicLetter = (c: string) => {
  const n = c.charCodeAt(0);
  return (n >= 0xc0 && n <= 0xff) || (n >= 0x370 && n <= 0x4ff);
};

/** The text a sequence of physical keys types in a language, with dead keys (΄ + α = ά) combined. */
export function render(keys: string, code: string): string {
  const map = mapFor(byCode(code));
  let out = '';
  for (let i = 0; i < keys.length; i++) {
    const t = lookup(map, keys[i]);
    if (!t) { out += keys[i]; continue; }
    if (t.dead) {
      const next = i + 1 < keys.length ? lookup(map, keys[i + 1]) : null;
      // Two accent keys type both accents (´ + ` = ´`).
      if (next?.dead) { out += t.text + next.text; i++; continue; }
      if (next && next.text.length === 1) {
        const composed = (next.text + t.combining).normalize('NFC');
        if (composed.length === 1 && isBasicLetter(composed)) { out += composed; i++; continue; }
      }
      out += t.text;
      continue;
    }
    out += t.text;
  }
  return out;
}

const CODES = [
  'Backquote', 'Digit1', 'Digit2', 'Digit3', 'Digit4', 'Digit5', 'Digit6', 'Digit7', 'Digit8', 'Digit9', 'Digit0',
  'Minus', 'Equal', 'KeyQ', 'KeyW', 'KeyE', 'KeyR', 'KeyT', 'KeyY', 'KeyU', 'KeyI', 'KeyO', 'KeyP', 'BracketLeft',
  'BracketRight', 'Backslash', 'KeyA', 'KeyS', 'KeyD', 'KeyF', 'KeyG', 'KeyH', 'KeyJ', 'KeyK', 'KeyL', 'Semicolon',
  'Quote', 'KeyZ', 'KeyX', 'KeyC', 'KeyV', 'KeyB', 'KeyN', 'KeyM', 'Comma', 'Period', 'Slash',
];

/** The physical key of a keyboard event, whatever keyboard the visitor has on: "g", or "G" with Shift. */
export function keyOf(e: { code: string; shiftKey: boolean }): string | null {
  const i = CODES.indexOf(e.code);
  if (i < 0) return null;
  return e.shiftKey ? SHIFTED_KEYS[i] : PHYSICAL_KEYS[i];
}
