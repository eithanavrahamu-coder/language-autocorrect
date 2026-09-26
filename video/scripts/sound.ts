// Makes the video's soundtrack, public/soundtrack.wav: the sound effects, synthesized here, over the music
// (scripts/music.ts). Every sound is placed at its moment in src/timeline.ts, which the pictures use too, and nothing
// is a recording, so there are no licenses to worry about.
// `npm run sound` (Node runs this TypeScript file directly). Needs ffmpeg, to measure the loudness.
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { APPS, HERO, LANGS, OUTRO, SECONDS, UNDO } from '../src/timeline.ts';
import { music } from './music.ts';
import {
  bell, Filter, hiss, hz, knock, type Mode, normalized, note, place, RATE, random, room, samples, type Sound, stereo,
  type Stereo,
} from './synth.ts';

const root = path.resolve(import.meta.dirname, '..');

// ---------- the sounds ----------

/** A keyboard key: the finger meets it, it hits bottom (the loudest part), and it comes back up. */
function key(seed: number, kind: 'letter' | 'space' | 'backspace' = 'letter'): Sound {
  const rnd = random(seed * 7919 + 17);
  const vary = (x: number, by = 0.12) => x * (1 + (rnd() * 2 - 1) * by);
  const low = kind === 'space' ? 0.6 : kind === 'backspace' ? 0.82 : 1;
  const ring = (f0: number, tau: number): Mode[] => [
    { f: vary(f0 * low), tau: tau * (2 - low), a: 0.5 },
    { f: vary(3.4 * f0 * low), tau: tau * 0.55, a: 0.24 },
    { f: vary(6.9 * f0 * low), tau: tau * 0.32, a: 0.13 },
    { f: vary(15 * f0 * low, 0.08), tau: tau * 0.2, a: 0.07 },
  ];
  const out = new Float32Array(samples(0.36));
  knock(out, 0, 0.5, rnd, { click: 3200, clickTau: 0.0007, band: vary(2600), bandTau: 0.005, bandAmp: 1.6, modes: ring(250, 0.011) });
  const bottom = vary(0.0095, 0.25);
  knock(out, bottom, 1, rnd, {
    click: 2400, clickTau: 0.0009, band: vary(1700 * (0.5 + low / 2)), bandTau: 0.008, bandAmp: 2.2, modes: ring(205, 0.016),
  });
  if (kind === 'space') {
    // The long key's stabilizer rattles a little.
    knock(out, bottom + 0.006, 0.28, rnd, { click: 4000, clickTau: 0.0006, band: 3400, bandTau: 0.004, bandAmp: 1.2, modes: ring(430, 0.006) });
  }
  const up = vary(kind === 'space' ? 0.14 : 0.1, 0.15);
  knock(out, up, 0.26, rnd, { click: 4200, clickTau: 0.0006, band: vary(3400), bandTau: 0.004, bandAmp: 1.2, modes: ring(330, 0.007) });
  return { data: normalized(out), hit: 0 };
}

/**
 * The fix: a quick bright run of four notes. `step` moves it up the E major pentatonic scale (E F# G# B C#), so the
 * languages' fixes climb, and every note fits the music's chords.
 */
function sparkle(step: number): Sound {
  const rnd = random(100 + step);
  const out = new Float32Array(samples(1.7));
  const scale = [76, 78, 80, 83, 85];
  const degree = (d: number) => scale[d % 5] + 12 * Math.floor(d / 5);
  [0, 2, 3, 5].forEach((d, i) => bell(out, 0.004 + i * 0.042, hz(degree(step + d)), [0.5, 0.42, 0.42, 0.55][i], 0.24));
  hiss(out, 0.01, 0.7, 0.05, 7000, rnd);
  return { data: normalized(out), hit: 0 };
}

/** A soft bubble pop, for things appearing. */
function pop(pitch: number, seed: number): Sound {
  const rnd = random(seed + 300);
  const high = new Filter('high', 3000);
  const out = new Float32Array(samples(0.2));
  let phase = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    phase += (2 * Math.PI * (360 + 1150 * Math.exp(-t / 0.011)) * pitch) / RATE;
    out[i] = Math.min(1, t / 0.0008) * Math.exp(-t / 0.03) * Math.sin(phase) + 0.25 * high.run(rnd() * 2 - 1) * Math.exp(-t / 0.0012);
  }
  return { data: normalized(out), hit: 0 };
}

/** Two quick notes, up or down: the keyboard switching. */
function blip(up: boolean): Sound {
  const out = new Float32Array(samples(0.4));
  const [a, b] = up ? [880, 1318.51] : [1318.51, 880];
  note(out, 0, a, 0.8, 0.035);
  note(out, 0.065, b, 1, 0.05);
  return { data: normalized(out), hit: 0 };
}

/** A small wooden tick, for rows landing and badges changing. */
function tick(pitch: number, seed: number): Sound {
  const rnd = random(seed + 500);
  const out = new Float32Array(samples(0.1));
  knock(out, 0, 1, rnd, {
    click: 5000, clickTau: 0.0006, band: 3000 * pitch, bandTau: 0.003, bandAmp: 0.8,
    modes: [{ f: 1900 * pitch, tau: 0.008, a: 0.6 }, { f: 4200 * pitch, tau: 0.004, a: 0.3 }, { f: 6800 * pitch, tau: 0.0025, a: 0.12 }],
  });
  return { data: normalized(out), hit: 0 };
}

/** Air moving past, its pitch sliding from `from` to `to` Hz; loudest at `peak` (0–1) of the way through. */
function whoosh(duration: number, from: number, to: number, seed: number, peak = 0.55): Sound {
  const rnd = random(seed + 700);
  const band = new Filter('band', from, 0.8), low = new Filter('low', 700, 0.7);
  const out = new Float32Array(samples(duration));
  for (let i = 0; i < out.length; i++) {
    const x = i / out.length;
    if (i % 32 === 0) band.tune(from * (to / from) ** x, 0.8);
    const env = x < peak ? Math.sin(((x / peak) * Math.PI) / 2) ** 2 : Math.cos((((x - peak) / (1 - peak)) * Math.PI) / 2) ** 2;
    const noise = rnd() * 2 - 1;
    out[i] = env * (band.run(noise) + 0.35 * low.run(noise));
  }
  return { data: normalized(out), hit: duration * peak };
}

/** Undo: the fix's notes played backwards, swelling up into the moment, then two falling notes. */
function rewind(): Sound {
  const swell = new Float32Array(samples(0.5));
  [1318.51, 987.77, 830.61, 659.25].forEach((f, i) => bell(swell, i * 0.03, f, 0.5, 0.18));
  swell.reverse();
  const out = new Float32Array(samples(0.9));
  out.set(swell, 0);
  note(out, 0.5, 1318.51, 0.35, 0.03);
  note(out, 0.56, 880, 0.45, 0.05);
  return { data: normalized(out), hit: 0.5 };
}

/** A soft "uh-oh": two low muted notes, for the word that came out wrong. */
function wrong(): Sound {
  const out = new Float32Array(samples(0.5));
  note(out, 0, 329.63, 0.8, 0.06);
  note(out, 0.1, 261.63, 1, 0.09);
  const low = new Filter('low', 1200, 0.7);
  for (let i = 0; i < out.length; i++) out[i] = low.run(out[i]);
  return { data: normalized(out), hit: 0 };
}

/** The end: a deep soft thump under a bright E major chord, strummed upwards. */
function chime(): Sound {
  const rnd = random(900);
  const out = new Float32Array(samples(4.3));
  let phase = 0;
  for (let i = 0; i < samples(1.6); i++) {
    const t = i / RATE;
    phase += (2 * Math.PI * (44 + 70 * Math.exp(-t / 0.05))) / RATE;
    out[i] += 0.9 * Math.min(1, t / 0.004) * Math.exp(-t / 0.35) * Math.sin(phase);
  }
  [329.63, 493.88, 659.25, 830.61, 987.77, 1318.51].forEach((f, i) =>
    bell(out, 0.01 + i * 0.028, f, [0.42, 0.34, 0.36, 0.28, 0.24, 0.26][i], [1.9, 1.6, 1.4, 1.2, 1.05, 0.9][i]));
  hiss(out, 0.02, 1.4, 0.035, 8000, rnd);
  return { data: normalized(out), hit: 0 };
}

/** Two soft bell notes, for the privacy line. */
function reassure(): Sound {
  const out = new Float32Array(samples(2));
  bell(out, 0, 987.77, 0.5, 0.45);
  bell(out, 0.09, 1318.51, 0.45, 0.5);
  return { data: normalized(out), hit: 0 };
}

/** A tiny high "ting", for the light running across the icon. */
function glint(): Sound {
  const out = new Float32Array(samples(1));
  bell(out, 0, 2637.02, 0.6, 0.16);
  bell(out, 0.05, 3951.07, 0.4, 0.14);
  return { data: normalized(out), hit: 0 };
}

// ---------- the sound effects, in order ----------

type Event = { at: number; sound: Sound; gain: number; pan: number; wet: number };
const events: Event[] = [];
const play = (at: number, sound: Sound, gain: number, pan = 0, wet = 0.15) => events.push({ at, sound, gain, pan, wet });

// Eight slightly different keys, used in turn, so the typing never sounds like one sample repeated.
const letters = Array.from({ length: 8 }, (_, i) => key(i + 1));
const spaces = [key(21, 'space'), key(22, 'space')];
const backspace = key(31, 'backspace');
let typed = 0;
const rnd = random(4242);
const ROWS = ['1234567890', 'qwertyuiop', "asdfghjkl;'", 'zxcvbnm,./'];
/** Keys on the left of the keyboard sound a little to the left, and so on. */
function panOf(k: string): number {
  for (const row of ROWS) {
    const i = row.indexOf(k.toLowerCase());
    if (i >= 0) return (i / (row.length - 1) - 0.5) * 0.5;
  }
  return 0;
}
function type(keys: string, times: number[], gain: number, pan = 0) {
  times.forEach((t, i) => {
    if (keys[i] === ' ') play(t, spaces[typed++ % 2], gain * 1.1, pan, 0.12);
    else play(t, letters[(typed++ * 5 + 3) % 8], gain * (0.88 + 0.24 * rnd()), pan + panOf(keys[i]), 0.12);
  });
}

// 1. The problem, and the fix
play(HERO.cardIn + 0.12, whoosh(0.6, 300, 2400, 1, 0.5), 0.22, -0.2, 0.3);
play(HERO.badgeIn, pop(1.1, 1), 0.2, 0.1, 0.2);
type(HERO.keys, HERO.keyTimes, 0.48);
play(HERO.squiggle, wrong(), 0.13, 0, 0.2);
play(HERO.spaceIn, tick(0.8, 1), 0.1);
play(HERO.space, spaces[0], 0.6, 0, 0.12);
play(HERO.fix, sparkle(0), 0.42, 0, 0.35);
play(HERO.fix + 0.03, pop(1.3, 2), 0.18, 0, 0.2);
play(HERO.switchAt, blip(true), 0.22, 0.15, 0.25);
type(HERO.then, HERO.thenTimes, 0.42);
play(HERO.out + 0.15, whoosh(0.55, 2400, 500, 2, 0.45), 0.2, 0.2, 0.3);

// 2. The languages: each fix a step up the scale, then the badges landing
LANGS.cards.forEach((c, i) => {
  const side = ((i % 3) - 1) * 0.45;
  play(c.cardIn, pop(0.9 + i * 0.05, 10 + i), 0.12, side);
  type(c.keys, c.keyTimes, 0.2, side * 0.6);
  play(c.fix, sparkle(i), 0.24, side, 0.35);
});
play(LANGS.gridOut + 0.2, whoosh(0.5, 2600, 400, 3, 0.4), 0.2, 0, 0.3);
const languageCount: number = JSON.parse(readFileSync(path.join(root, 'src/generated/app-info.json'), 'utf8')).languages.length;
for (let i = 0; i < languageCount; i++) {
  play(LANGS.burst + i * LANGS.burstStep + 0.04, pop(0.8 + i * 0.035, 30 + i), 0.13, ((i % 9) / 8 - 0.5) * 0.8, 0.25);
}
play(LANGS.out, whoosh(0.5, 600, 3000, 4, 0.5), 0.18, -0.2, 0.3);

// 3. Undo
play(UNDO.cardIn, pop(1, 3), 0.16);
play(UNDO.cardIn + 0.02, sparkle(0), 0.16, 0, 0.35);
play(UNDO.cardShown, pop(1.3, 4), 0.14);
play(UNDO.backspaceIn, tick(0.8, 2), 0.1);
play(UNDO.backspace, backspace, 0.55, 0.3, 0.12);
play(UNDO.undo, rewind(), 0.36, 0, 0.3);
play(UNDO.switchAt, blip(false), 0.2, 0.15, 0.25);
play(UNDO.out + 0.1, whoosh(0.5, 2200, 450, 5, 0.45), 0.18, 0.2, 0.3);

// 4. Every app
play(APPS.cardIn + 0.1, whoosh(0.5, 400, 2000, 6, 0.5), 0.14, -0.1, 0.3);
APPS.rows.forEach((r, i) => {
  play(r.at + 0.04, tick(1 + i * 0.08, 10 + i), 0.24, 0.1 * i - 0.15);
  play(r.at + 0.1, pop(1.5 + i * 0.1, 20 + i), 0.09, 0.3);
});
play(APPS.privacy, reassure(), 0.2, 0, 0.45);
play(APPS.out + 0.12, whoosh(0.75, 3000, 300, 7, 0.35), 0.26, 0, 0.35);

// 5. The end
play(OUTRO.icon, chime(), 0.46, 0, 0.35);
play(OUTRO.name + 0.1, whoosh(0.5, 800, 3000, 8, 0.5), 0.07, 0, 0.3);
OUTRO.badges.forEach((b, i) => play(b.at, tick(1.3 + (i % 3) * 0.1, 40 + i), 0.07, 0.35, 0.2));
play(OUTRO.button, pop(0.85, 5), 0.18);
play(OUTRO.shine, glint(), 0.1, 0.1, 0.4);

// ---------- loudness ----------

function writeWav(file: string, channels: Float32Array[], bits: 24 | 32) {
  const n = channels[0].length, bytes = bits / 8;
  const data = Buffer.alloc(n * channels.length * bytes);
  let o = 0;
  for (let i = 0; i < n; i++) {
    for (const c of channels) {
      const v = Math.max(-1, Math.min(1, c[i]));
      if (bits === 32) data.writeFloatLE(v, o);
      else data.writeIntLE(Math.round(v * 8388607), o, 3);
      o += bytes;
    }
  }
  const head = Buffer.alloc(44);
  head.write('RIFF', 0);
  head.writeUInt32LE(36 + data.length, 4);
  head.write('WAVEfmt ', 8);
  head.writeUInt32LE(16, 16);
  head.writeUInt16LE(bits === 32 ? 3 : 1, 20);
  head.writeUInt16LE(channels.length, 22);
  head.writeUInt32LE(RATE, 24);
  head.writeUInt32LE(RATE * channels.length * bytes, 28);
  head.writeUInt16LE(channels.length * bytes, 32);
  head.writeUInt16LE(bits, 34);
  head.write('data', 36);
  head.writeUInt32LE(data.length, 40);
  writeFileSync(file, Buffer.concat([head, data]));
}

mkdirSync(path.join(root, 'out'), { recursive: true });
mkdirSync(path.join(root, 'public'), { recursive: true });
const probe = path.join(root, 'out/soundtrack-probe.wav');

/** Integrated loudness (LUFS) and true peak (dBFS), measured by ffmpeg the way streaming sites do (EBU R128). */
function measure(sound: Stereo): { lufs: number; peak: number } {
  writeWav(probe, [sound.left, sound.right], 32);
  const run = spawnSync('ffmpeg', ['-hide_banner', '-nostats', '-i', probe, '-af', 'ebur128=peak=true', '-f', 'null', '-'], { encoding: 'utf8' });
  const summary = run.stderr.slice(run.stderr.lastIndexOf('Summary:'));
  const lufs = /I:\s+(-?[\d.]+) LUFS/.exec(summary);
  const peak = /Peak:\s+(-?[\d.]+) dBFS/.exec(summary);
  if (!lufs || !peak) throw new Error('Could not measure the loudness with ffmpeg:\n' + run.stderr.slice(-2000));
  return { lufs: Number(lufs[1]), peak: Number(peak[1]) };
}

/** How high the wave goes around sample `i`, including between samples (where a player's output can overshoot). */
function peakNear(x: Float32Array, i: number): number {
  const a = x[Math.max(0, i - 1)], b = x[i], c = x[Math.min(x.length - 1, i + 1)], d = x[Math.min(x.length - 1, i + 2)];
  let peak = Math.abs(b);
  for (const t of [0.25, 0.5, 0.75]) {
    // Catmull-Rom interpolation between b and c.
    const v = b + 0.5 * t * (c - a + t * (2 * a - 5 * b + 4 * c - d + t * (3 * (b - c) + d - a)));
    peak = Math.max(peak, Math.abs(v));
  }
  return peak;
}

/**
 * Keeps peaks under `ceiling` without distorting: it looks 3 ms ahead, lowers the level smoothly before a peak,
 * and lets it come back up over about 80 ms. Returns the result, and how far down it had to go at most.
 */
function limited(sound: Stereo, gain: number, ceiling: number): [Stereo, number] {
  const { left: l, right: r } = sound;
  const n = l.length, look = samples(0.003), release = Math.exp(-1 / samples(0.08));
  const need = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const p = Math.max(peakNear(l, i), peakNear(r, i)) * gain;
    need[i] = p > ceiling ? ceiling / p : 1;
  }
  // The smallest level needed in the next `look` samples.
  const ahead = new Float32Array(n);
  const queue = new Int32Array(n);
  let head = 0, tail = 0;
  for (let k = 0; k < n + look; k++) {
    if (k < n) {
      while (tail > head && need[queue[tail - 1]] >= need[k]) tail--;
      queue[tail++] = k;
    }
    const i = k - look;
    if (i >= 0) {
      while (queue[head] < i) head++;
      ahead[i] = need[queue[head]];
    }
  }
  const out = stereo(n);
  // Averaged over the last `look` samples (1 before the start), the level is down in time for every peak.
  let sum = look, level = 1, lowest = 1;
  for (let i = 0; i < n; i++) {
    sum += ahead[i] - (i >= look ? ahead[i - look] : 1);
    level = Math.min(sum / look, 1 - (1 - level) * release);
    lowest = Math.min(lowest, level);
    out.left[i] = l[i] * gain * level;
    out.right[i] = r[i] * gain * level;
  }
  return [out, lowest];
}

// ---------- mixing ----------

const length = samples(SECONDS);
const effects = stereo(length), effectsSend = new Float32Array(length);
for (const e of events) place(effects, e.at - e.sound.hit, e.sound.data, e.gain, e.pan, effectsSend, e.wet);
room(effectsSend, effects, 1.6);

const tune = music(length);
room(tune.send, tune.mix, 1.6);

// The music sits MUSIC_BELOW dB under the effects (by loudness)...
const MUSIC_BELOW = 4;
const effectsLevel = measure(effects), musicLevel = measure(tune.mix);
const musicGain = 10 ** ((effectsLevel.lufs - MUSIC_BELOW - musicLevel.lufs) / 20);

// ...and steps back up to 5 dB more while an effect plays (like music under a voice), coming back over a quarter second.
let effectsPeak = 0;
for (let i = 0; i < length; i++) effectsPeak = Math.max(effectsPeak, Math.abs(effects.left[i]), Math.abs(effects.right[i]));
const attack = Math.exp(-1 / samples(0.004)), recover = Math.exp(-1 / samples(0.25));
const mix = stereo(length);
let follow = 0;
for (let i = 0; i < length; i++) {
  const x = Math.max(Math.abs(effects.left[i]), Math.abs(effects.right[i])) / effectsPeak;
  follow = x > follow ? x + (follow - x) * attack : x + (follow - x) * recover;
  const g = musicGain * (1 - 0.44 * Math.min(1, follow / 0.25));
  mix.left[i] = effects.left[i] + tune.mix.left[i] * g;
  mix.right[i] = effects.right[i] + tune.mix.right[i] * g;
}

if (process.env.SOUND_DEBUG) {
  // The effects and the music on their own, to listen to or measure; and how loud each was.
  const musicAlone = stereo(length);
  for (let i = 0; i < length; i++) {
    musicAlone.left[i] = tune.mix.left[i] * musicGain;
    musicAlone.right[i] = tune.mix.right[i] * musicGain;
  }
  writeWav(path.join(root, 'out/effects-alone.wav'), [effects.left, effects.right], 32);
  writeWav(path.join(root, 'out/music-alone.wav'), [musicAlone.left, musicAlone.right], 32);
  console.log(`effects ${effectsLevel.lufs} LUFS, music ${musicLevel.lufs} LUFS before its gain of ${(20 * Math.log10(musicGain)).toFixed(1)} dB`);
  // The loudest moments before any limiting, to balance the sounds by.
  const peaks: { at: number; db: number }[] = [];
  const step = samples(0.05);
  for (let s = 0; s < length; s += step) {
    let p = 0;
    for (let i = s; i < Math.min(length, s + step); i++) p = Math.max(p, Math.abs(mix.left[i]), Math.abs(mix.right[i]));
    peaks.push({ at: s / RATE, db: 20 * Math.log10(p || 1e-9) });
  }
  peaks.sort((a, b) => b.db - a.db);
  console.log(peaks.slice(0, 12).map(p => `${p.at.toFixed(2)}s ${p.db.toFixed(1)} dB`).join('\n'));
}

// ---------- the two soundtracks ----------

/**
 * Finishes a soundtrack and saves it as public/`name`: nothing above 16 kHz (nobody hears it, video sites drop it, and
 * it makes peaks overshoot between samples), a fade over the last half second so nothing stops dead, and -16 LUFS
 * (what most sites and phones expect) with peaks kept 1.5 dB under full scale.
 */
function finish(sound: Stereo, name: string, what: string) {
  const out = stereo(length);
  for (const side of ['left', 'right'] as const) {
    const a = new Filter('low', 16000, 0.541), b = new Filter('low', 16000, 1.307);
    for (let i = 0; i < length; i++) out[side][i] = b.run(a.run(sound[side][i]));
  }
  const fade = samples(0.5);
  for (let i = 0; i < fade; i++) {
    const g = Math.cos(((i + 1) / fade) * (Math.PI / 2)) ** 2;
    out.left[length - fade + i] *= g;
    out.right[length - fade + i] *= g;
  }
  const TARGET = -16, CEILING = 10 ** (-1.5 / 20);
  let gain = 1, result: [Stereo, number] = [out, 1], level = { lufs: 0, peak: 0 };
  for (let round = 0; round < 4; round++) {
    result = limited(out, gain, CEILING);
    level = measure(result[0]);
    if (Math.abs(level.lufs - TARGET) < 0.3) break;
    gain *= 10 ** ((TARGET - level.lufs) / 20);
  }
  const file = path.join(root, 'public', name);
  writeWav(file, [result[0].left, result[0].right], 24);
  console.log(`${what} → ${path.relative(root, file)}: ${level.lufs.toFixed(1)} LUFS, true peak ${level.peak.toFixed(1)} dBFS, ` +
    `loudest moments turned down by ${(-20 * Math.log10(result[1])).toFixed(1)} dB`);
}

finish(mix, 'soundtrack.wav', `${events.length} sounds over the music (${MUSIC_BELOW} dB under them)`);
finish(effects, 'soundtrack-no-music.wav', `${events.length} sounds, no music`);
