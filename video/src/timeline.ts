// Every moment of the video, in seconds. The pictures (src/scenes) and the sound (scripts/sound.ts) both read this
// file, so a key click always lands on the frame its key goes down.
// Plain data only: scripts/sound.ts runs it in Node directly.

export const FPS = 60;
export const SECONDS = 20;
export const WIDTH = 1920;
export const HEIGHT = 1080;

/** When each key of `keys` goes down: the website demo's uneven rhythm (75–130 ms apart), stretched by `pace`. */
export function typing(keys: string, start: number, pace = 1): number[] {
  const times: number[] = [];
  let t = start;
  for (let i = 0; i < keys.length; i++) {
    times.push(t);
    t += ((75 + ((i * 37) % 55)) / 1000) * pace;
  }
  return times;
}

export type Caption = { in: number; out: number; text: string; faint?: string; faintIn?: number };

// ---------- 1. The problem, and the fix (0–5.5 s) ----------
// "hello" typed with the Hebrew keyboard on comes out as יקךךם, and Space turns it back into "hello": one of the
// app's own examples (src/LayoutBuddy/UI/app.html, website/src/demo/scenes.ts).
const heroKeys = typing('hello', 0.95, 1.25);
export const HERO = {
  start: 0,
  end: 5.6,
  cardIn: 0.05,
  caretIn: 0.45,
  badgeIn: 0.55,
  keys: 'hello',
  typed: 'יקךךם',
  keyTimes: heroKeys,
  squiggle: heroKeys[heroKeys.length - 1] + 0.3,
  keycapsOut: 2.0,
  spaceIn: 2.12,
  space: 2.45,
  fix: 2.5,
  fixed: 'hello',
  switchAt: 2.8,
  then: ' there',
  thenTimes: typing(' there', 3.55),
  out: 5.22,
  ask: { in: 0.25, out: 2.08, text: 'Forgot to switch keyboards?' } as Caption,
  press: { in: 2.14, out: 3.3, text: 'Press Space.', faint: 'It’s fixed.', faintIn: 2.55 } as Caption,
  switches: { in: 3.35, out: 5.25, text: 'It switches the keyboard', faint: 'for you.', faintIn: 3.6 } as Caption,
};

// ---------- 2. The languages (5.5–10.1 s) ----------
// Each card starts on the English keyboard, as in the website's examples (checked with the app's engine).
const cardWords = [
  { lang: 'he', keys: 'akuo', fixed: 'שלום' },
  { lang: 'ru', keys: 'ghbdtn', fixed: 'привет' },
  { lang: 'ar', keys: 'lvpfh', fixed: 'مرحبا' },
  { lang: 'el', keys: 'kalhm;era', fixed: 'καλημέρα' },
  { lang: 'ko', keys: 'dkssud', fixed: '안녕' },
  { lang: 'th', keys: 'l;ylfu', fixed: 'สวัสดี' },
];
export const LANGS = {
  start: 5.4,
  end: 10.2,
  cards: cardWords.map((c, i) => {
    const cardIn = 5.52 + i * 0.26;
    const keyTimes = typing(c.keys, cardIn + 0.2, 0.55);
    const fix = keyTimes[keyTimes.length - 1] + 0.14;
    return { ...c, cardIn, keyTimes, fix, switchAt: fix + 0.1 };
  }),
  gridOut: 8.1,
  burst: 8.3,
  burstStep: 0.03,
  out: 9.9,
  every: { in: 5.5, out: 8.08, text: 'In every language', faint: 'you type.' } as Caption,
  count: { in: 8.35, out: 9.92, text: '{count} languages.', faint: 'Use as many as you like.', faintIn: 8.65 } as Caption,
};

// ---------- 3. Undo (10.1–12.7 s) ----------
export const UNDO = {
  start: 10.0,
  end: 12.8,
  cardIn: 10.15,
  typed: 'ghbdtn',
  fixed: 'привет',
  cardShown: 10.3,
  backspaceIn: 10.7,
  backspace: 11.05,
  undo: 11.1,
  switchAt: 11.25,
  out: 12.5,
  ask: { in: 10.25, out: 12.48, text: 'Meant it?', faint: 'Backspace undoes it.', faintIn: 11.15 } as Caption,
};

// ---------- 4. Every app (12.7–15.7 s) ----------
// The app window's own sample of recent fixes (src/LayoutBuddy/UI/app.html).
export const APPS = {
  start: 12.6,
  end: 15.8,
  cardIn: 12.75,
  rows: [
    { at: 12.95, time: '14:32', typed: 'ha jh akuo', fixed: 'יש חי שלום', app: 'WhatsApp' },
    { at: 13.08, time: '14:30', typed: 'יקךךם', fixed: 'hello', app: 'Chrome' },
    { at: 13.21, time: '14:12', typed: 't,v', fixed: 'אתה', app: 'Outlook' },
    { at: 13.34, time: '13:58', typed: 'rcv', fixed: 'רבה', app: 'Word' },
  ],
  out: 15.45,
  every: { in: 12.8, out: 15.45, text: 'Works in every app.' } as Caption,
  privacy: 13.75,
};

// ---------- 5. The end (15.7–20 s) ----------
export const OUTRO = {
  start: 15.6,
  end: SECONDS,
  icon: 15.8,
  name: 16.05,
  tagline: 16.35,
  button: 16.75,
  address: 16.9,
  // The badge after the name goes through a few languages.
  badges: ['en', 'he', 'ru', 'el', 'ar', 'ko', 'th', 'en'].map((lang, i) => ({ lang, at: 16.25 + i * 0.45 })),
};
