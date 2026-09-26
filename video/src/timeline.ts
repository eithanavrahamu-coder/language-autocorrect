// Every moment of the video, in seconds. The pictures (src/scenes) and the sound (scripts/sound.ts and
// scripts/music.ts) all read this file, so a key click lands on the frame its key goes down, and the big moments land
// on the music's beat.
// Plain data only: the sound scripts run it in Node directly.

export const FPS = 60;
export const SECONDS = 20;
export const WIDTH = 1920;
export const HEIGHT = 1080;

// ---------- the music's beat ----------
// 124 beats a minute. Beat 0 is the drop, when Space fixes the first word; "17 languages" (beat 12) and the logo
// (beat 28) land on later bar lines.
export const BPM = 124;
export const BEAT = 60 / BPM;
export const DROP = 2.5;
/** When beat `n` is (0 is the drop; fractions are eighth and sixteenth notes; negative ones come before it). */
export const beat = (n: number) => DROP + n * BEAT;

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

// ---------- 1. The problem, and the fix (0–5.4 s) ----------
// "hello" typed with the Hebrew keyboard on comes out as יקךךם, and Space turns it back into "hello": one of the
// app's own examples (src/LayoutBuddy/UI/app.html, website/src/demo/scenes.ts).
const heroKeys = typing('hello', 0.95, 1.25);
export const HERO = {
  start: 0,
  end: 5.5,
  cardIn: 0.05,
  caretIn: 0.45,
  badgeIn: 0.55,
  keys: 'hello',
  typed: 'יקךךם',
  keyTimes: heroKeys,
  squiggle: heroKeys[heroKeys.length - 1] + 0.3,
  keycapsOut: 2.0,
  spaceIn: 2.12,
  space: DROP - 0.05,
  fix: DROP,
  fixed: 'hello',
  switchAt: beat(0.5),
  then: ' there',
  thenTimes: typing(' there', 3.55),
  out: 5.1,
  ask: { in: 0.25, out: 2.08, text: 'Forgot to switch keyboards?' } as Caption,
  press: { in: 2.14, out: 3.3, text: 'Press Space.', faint: 'It’s fixed.', faintIn: 2.55 } as Caption,
  switches: { in: 3.35, out: 5.12, text: 'It switches the keyboard', faint: 'for you.', faintIn: 3.6 } as Caption,
};

// ---------- 2. The languages (5.4–10.2 s) ----------
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
  start: 5.2,
  end: 10.3,
  cards: cardWords.map((c, i) => {
    // The words are fixed on six eighth notes in a row, so their sparkles play as a little run over the music.
    const fix = beat(7 + i / 2);
    const keyTimes = typing(c.keys, 0, 0.55);
    const start = fix - 0.14 - keyTimes[keyTimes.length - 1];
    return { ...c, cardIn: start - 0.2, keyTimes: keyTimes.map(t => t + start), fix, switchAt: fix + 0.1 };
  }),
  gridOut: 8.1,
  burst: beat(12),
  burstStep: BEAT / 16,
  out: 10.0,
  every: { in: 5.4, out: 8.08, text: 'In every language', faint: 'you type.' } as Caption,
  count: { in: 8.35, out: 10.0, text: '{count} languages.', faint: 'Use as many as you like.', faintIn: 8.65 } as Caption,
};

// ---------- 3. Undo (10.2–12.7 s) ----------
export const UNDO = {
  start: 10.1,
  end: 12.8,
  cardIn: beat(16),
  typed: 'ghbdtn',
  fixed: 'привет',
  cardShown: beat(16) + 0.15,
  backspaceIn: 10.8,
  backspace: beat(18) - 0.05,
  undo: beat(18),
  switchAt: beat(18.25),
  out: 12.5,
  ask: { in: 10.3, out: 12.48, text: 'Meant it?', faint: 'Backspace undoes it.', faintIn: beat(18) + 0.05 } as Caption,
};

// ---------- 4. Every app (12.7–15.8 s) ----------
// The app window's own sample of recent fixes (src/LayoutBuddy/UI/app.html). The rows land on sixteenth notes.
export const APPS = {
  start: 12.6,
  end: 15.8,
  cardIn: beat(21),
  rows: [
    { at: beat(21.5), time: '14:32', typed: 'ha jh akuo', fixed: 'יש חי שלום', app: 'WhatsApp' },
    { at: beat(21.75), time: '14:30', typed: 'יקךךם', fixed: 'hello', app: 'Chrome' },
    { at: beat(22), time: '14:12', typed: 't,v', fixed: 'אתה', app: 'Outlook' },
    { at: beat(22.25), time: '13:58', typed: 'rcv', fixed: 'רבה', app: 'Word' },
  ],
  out: 15.45,
  every: { in: 12.7, out: 15.45, text: 'Works in every app.' } as Caption,
  privacy: beat(24),
};

// ---------- 5. The end (15.8–20 s) ----------
export const OUTRO = {
  start: 15.8,
  end: SECONDS,
  icon: beat(28),
  name: beat(28.5),
  tagline: beat(29),
  button: beat(30),
  address: beat(30.5),
  shine: beat(31),
  // The badge after the name goes through a few languages, one a beat.
  badges: ['en', 'he', 'ru', 'el', 'ar', 'th', 'en'].map((lang, i) => ({ lang, at: beat(29 + i) })),
};
