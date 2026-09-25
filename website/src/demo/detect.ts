// A simplified version of the app's wrong-keyboard check, for the "try it yourself" demo.
// The app weighs how common each reading is and looks at the words around it; this only asks
// "is what you typed a word, and would it be one on the other keyboard?".
import { render } from './keyboard';

const loading = new Map<string, Promise<void>>();
const loaded = new Map<string, Map<string, number>>();

/** Fetches the most common words of the languages (once), so that evaluate() can use them. */
export function loadWords(codes: string[]): Promise<void> {
  return Promise.all(codes.map(code => {
    let p = loading.get(code);
    if (!p) {
      p = fetch(`${import.meta.env.BASE_URL}words/${code}.txt`)
        .then(r => (r.ok ? r.text() : Promise.reject(new Error(`${code}: ${r.status}`))))
        .then(t => { loaded.set(code, new Map(t.split('\n').map((w, i) => [w, i] as const))); });
      p.catch(() => loading.delete(code));
      loading.set(code, p);
    }
    return p;
  })).then(() => undefined);
}

const lower = (s: string, code: string) => s.toLocaleLowerCase(code);

// Keep the letters in the middle: "hello," is checked as "hello".
const trim = (s: string) => s.replace(/^[^\p{L}\p{M}]+|[^\p{L}\p{M}]+$/gu, '');

export type Fix = { text: string; lang: string };

/** The word the keys were meant to be, if they make a word only on another of the keyboards. */
export function evaluate(keys: string, current: string, languages: string[]): Fix | null {
  const currentWords = loaded.get(current);
  if (keys.length < 2 || !currentWords) return null;
  const typed = render(keys, current);
  const t = trim(typed);
  if (!t || currentWords.has(lower(t, current)) || /\d/.test(typed)) return null;

  let best: (Fix & { rank: number }) | null = null;
  for (const code of languages) {
    if (code === current) continue;
    // Shift+G renders as a capital П, so a capital first letter is kept.
    const text = render(keys, code);
    const rank = loaded.get(code)?.get(lower(text, code));
    if (rank === undefined || (best && best.rank <= rank)) continue;
    best = { text, lang: code, rank };
  }
  return best && { text: best.text, lang: best.lang };
}
