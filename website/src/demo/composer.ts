// The text box of the demo: words typed so far, the word being typed (as physical keys), the keyboard that
// is on, and the last fix (so Backspace right after it can undo it, like in the app).
import { render } from './keyboard';
import type { Fix } from './detect';

export type Word = { id: number; text: string; from?: string };
export type Message = { id: number; text: string; lang: string };
export type Toast = { id: number; from: string; to: string; undone?: boolean };

export type DemoState = {
  layout: string;
  words: Word[];
  /** Text of the current word that wasn't typed as keys here (after Backspace into a finished word). */
  tail: string;
  /** Physical keys of the current word. */
  keys: string;
  lastFix: { wordId: number; typed: string; prevLayout: string; at: number } | null;
  messages: Message[];
  toast: Toast | null;
};

export type DemoAction =
  | { type: 'reset'; layout: string }
  | { type: 'key'; key: string }
  | { type: 'commit'; fix: Fix | null; now: number }
  | { type: 'backspace'; now: number }
  | { type: 'undo'; now: number }
  | { type: 'layout'; layout: string }
  | { type: 'send' }
  | { type: 'hideToast'; id: number };

let nextId = 1;
const BACKSPACE_UNDO_MS = 1500;
const CTRL_Z_UNDO_MS = 5000;
const MAX_MESSAGES = 3;

export const initialDemo = (layout: string): DemoState =>
  ({ layout, words: [], tail: '', keys: '', lastFix: null, messages: [], toast: null });

export const currentWord = (s: DemoState) => s.tail + render(s.keys, s.layout);

function undo(s: DemoState, now: number, windowMs: number): DemoState | null {
  const f = s.lastFix;
  const last = s.words.at(-1);
  if (!f || s.keys || s.tail || !last || last.id !== f.wordId || now - f.at > windowMs) return null;
  return {
    ...s,
    words: [...s.words.slice(0, -1), { id: nextId++, text: f.typed }],
    layout: f.prevLayout,
    lastFix: null,
    toast: { id: nextId++, from: last.text, to: f.typed, undone: true },
  };
}

export function demoReducer(s: DemoState, a: DemoAction): DemoState {
  switch (a.type) {
    case 'reset':
      return { ...s, layout: a.layout, words: [], tail: '', keys: '', lastFix: null, toast: null };
    case 'key':
      return { ...s, keys: s.keys + a.key, lastFix: null };
    case 'layout':
      return { ...s, layout: a.layout, lastFix: null };
    case 'commit': {
      const typed = currentWord(s);
      if (!typed) return s;
      const base = { ...s, tail: '', keys: '' };
      if (a.fix && !s.tail) {
        const id = nextId++;
        return {
          ...base,
          words: [...s.words, { id, text: a.fix.text, from: typed }],
          layout: a.fix.lang,
          lastFix: { wordId: id, typed, prevLayout: s.layout, at: a.now },
          toast: { id: nextId++, from: typed, to: a.fix.text },
        };
      }
      return { ...base, words: [...s.words, { id: nextId++, text: typed }], lastFix: null };
    }
    case 'backspace': {
      const undone = undo(s, a.now, BACKSPACE_UNDO_MS);
      if (undone) return undone;
      if (s.keys) return { ...s, keys: s.keys.slice(0, -1), lastFix: null };
      if (s.tail) {
        const letters = [...new Intl.Segmenter().segment(s.tail)].map(g => g.segment);
        return { ...s, tail: letters.slice(0, -1).join(''), lastFix: null };
      }
      const last = s.words.at(-1);
      if (!last) return s;
      // Deleting the space after a word makes it the current word again.
      return { ...s, words: s.words.slice(0, -1), tail: last.text, lastFix: null };
    }
    case 'undo':
      return undo(s, a.now, CTRL_Z_UNDO_MS) ?? s;
    case 'send': {
      const text = [...s.words.map(w => w.text), currentWord(s)].join(' ').trim();
      if (!text) return s;
      return {
        ...s,
        words: [], tail: '', keys: '', lastFix: null,
        messages: [...s.messages, { id: nextId++, text, lang: s.layout }].slice(-MAX_MESSAGES),
      };
    }
    case 'hideToast':
      return s.toast?.id === a.id ? { ...s, toast: null } : s;
  }
}
