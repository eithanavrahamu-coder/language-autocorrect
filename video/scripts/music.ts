// The music under the video: a light, bright dance groove in E major at 124 beats a minute, synthesized like the
// sound effects (so no licenses). It follows the story: a quiet build while the word comes out wrong, the drop when
// Space fixes it, a build through the languages up to "17 languages", a pause for undo, the groove again for the
// app list, and a last warm chord under the logo. The fix sparkles and the final chime are in the same key.
import { beat, BEAT, DROP } from '../src/timeline.ts';
import { Filter, hz, normalized, place, RATE, random, samples, saw, stereo, type Stereo } from './synth.ts';

// ---------- the instruments ----------

/** A soft, round kick drum. */
function kick(): Float32Array {
  const rnd = random(11);
  const click = new Filter('high', 2500);
  const out = new Float32Array(samples(0.45));
  let phase = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    phase += (2 * Math.PI * (46 + 110 * Math.exp(-t / 0.03))) / RATE;
    const body = Math.tanh(1.6 * Math.sin(phase)) / Math.tanh(1.6);
    out[i] = body * Math.min(1, t / 0.001) * Math.exp(-t / 0.2) + 0.2 * click.run(rnd() * 2 - 1) * Math.exp(-t / 0.002);
  }
  return normalized(out);
}

/** A hi-hat: bright noise, short (closed) or ringing (open). */
function hat(seed: number, open = false): Float32Array {
  const rnd = random(seed + 50);
  const high = new Filter('high', 7000), ring = new Filter('band', 10500, 1.5);
  const out = new Float32Array(samples(open ? 0.35 : 0.08));
  for (let i = 0; i < out.length; i++) {
    const noise = rnd() * 2 - 1;
    out[i] = (0.6 * high.run(noise) + 0.5 * ring.run(noise)) * Math.exp(-(i / RATE) / (open ? 0.09 : 0.016));
  }
  return normalized(out);
}

/** A hand clap: three quick slaps and a short tail. */
function clap(seed: number): Float32Array {
  const rnd = random(seed + 60);
  const band = new Filter('band', 1400, 1.1), high = new Filter('high', 800);
  const out = new Float32Array(samples(0.3));
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    let env = 0;
    for (const at of [0, 0.011, 0.023]) if (t >= at) env = Math.max(env, Math.exp(-(t - at) / 0.005));
    if (t >= 0.023) env = Math.max(env, 0.45 * Math.exp(-(t - 0.023) / 0.08));
    out[i] = high.run(band.run(rnd() * 2 - 1)) * env;
  }
  return normalized(out);
}

/** A big soft hit: a deep boom (`sub` of it) and a gentle crash. */
function impact(seed: number, sub: number): Float32Array {
  const rnd = random(seed + 70);
  const high = new Filter('high', 3000), low = new Filter('low', 10000);
  const out = new Float32Array(samples(1.8));
  let phase = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    phase += (2 * Math.PI * (36 + 64 * Math.exp(-t / 0.07))) / RATE;
    out[i] = sub * Math.sin(phase) * Math.min(1, t / 0.002) * Math.exp(-t / 0.45)
      + 0.3 * low.run(high.run(rnd() * 2 - 1)) * Math.min(1, t / 0.003) * Math.exp(-t / 0.5);
  }
  return normalized(out);
}

/** Noise rising in pitch and strength, up to a moment. */
function riser(duration: number, seed: number): Float32Array {
  const rnd = random(seed + 80);
  const band = new Filter('band', 300, 1.4);
  const out = new Float32Array(samples(duration));
  for (let i = 0; i < out.length; i++) {
    const x = i / out.length;
    if (i % 32 === 0) band.tune(300 * 20 ** x, 1.4);
    out[i] = band.run(rnd() * 2 - 1) * x * x;
  }
  return normalized(out);
}

/** A bass note: a warm sine with a little grit, so it's heard on small speakers too. */
function bass(freq: number, duration: number): Float32Array {
  const out = new Float32Array(samples(duration + 0.02));
  const end = samples(0.02);
  let phase = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    phase += (2 * Math.PI * freq) / RATE;
    const s = Math.sin(phase);
    const env = Math.min(1, t / 0.005) * Math.exp(-t / (duration * 0.8)) * Math.min(1, (out.length - i) / end);
    out[i] = env * (0.8 * s + 0.25 * Math.tanh(3 * s) + 0.12 * Math.sin(2 * phase));
  }
  return out;
}

/** A plucked note: two slightly detuned saws through a filter that closes quickly. */
function pluck(freq: number, duration = 0.42): Float32Array {
  const low = new Filter('low', 4000, 0.9);
  const out = new Float32Array(samples(duration));
  const d1 = freq / RATE, d2 = (freq * 1.004) / RATE;
  let p1 = 0, p2 = 0.37;
  for (let i = 0; i < out.length; i++) {
    const t = i / RATE;
    if (i % 16 === 0) low.tune(450 + 5200 * Math.exp(-t / 0.05), 0.9);
    p1 = (p1 + d1) % 1;
    p2 = (p2 + d2) % 1;
    out[i] = low.run(0.5 * (saw(p1, d1) + saw(p2, d2))) * Math.min(1, t / 0.002) * Math.exp(-t / 0.15);
  }
  return out;
}

/** The pads' brightness: opening up into the drop, darker in the pause for undo, closing slowly at the end. */
function padCutoff(t: number): number {
  if (t < DROP) return 450 * (2200 / 450) ** (t / DROP);
  if (t >= beat(16) && t < beat(20)) return 1400;
  if (t >= beat(28)) return 2600 - 1400 * Math.min(1, (t - beat(28)) / 4);
  return 2000;
}

/** A soft chord from `from` to `to` seconds: three slightly detuned saws per note and side, wide and warm. */
function pad(out: Stereo, send: Float32Array, notes: number[], from: number, to: number, fadeIn: number, level: number, seed: number) {
  const rnd = random(seed + 90);
  const gain = 0.35 * level, wet = 0.3;
  const start = samples(from), end = samples(to), rise = samples(fadeIn), fall = samples(0.3);
  for (const [side, spread] of [['left', -4], ['right', 4]] as const) {
    const channel = out[side];
    const low = new Filter('low', padCutoff(from), 0.7);
    const voices = notes.flatMap(m => [-9, 0, 8].map(cents => ({ dt: (hz(m) * 2 ** ((cents + spread) / 1200)) / RATE, p: rnd() })));
    for (let j = start; j < Math.min(channel.length, end + fall); j++) {
      if ((j - start) % 64 === 0) low.tune(padCutoff(j / RATE), 0.7);
      let s = 0;
      for (const v of voices) {
        v.p += v.dt;
        if (v.p >= 1) v.p -= 1;
        s += saw(v.p, v.dt);
      }
      const env = Math.min(1, (j - start) / rise) * (j < end ? 1 : 1 - (j - end) / fall);
      const y = (gain * env * low.run(s)) / voices.length;
      channel[j] += y;
      send[j] += y * wet * 0.5;
    }
  }
}

// ---------- the arrangement ----------

type Chord = { pad: number[]; bass: number; arp: number[] };
// E add9, B add9, C#m7 and A add9 (MIDI note numbers), voiced close together so the pads glide from one to the next.
const E: Chord = { pad: [52, 56, 59, 66], bass: 40, arp: [64, 68, 71, 76] };
const B: Chord = { pad: [51, 54, 59, 61], bass: 35, arp: [63, 66, 71, 75] };
const CSM: Chord = { pad: [52, 56, 59, 61], bass: 37, arp: [61, 64, 68, 73] };
const A: Chord = { pad: [52, 57, 59, 61], bass: 33, arp: [61, 64, 69, 73] };

/** The chords, in beats. B waits to resolve to E, which it does the moment Space fixes the word; A–B–E brings the logo. */
const CHORDS: [number, number, Chord][] = [
  [-6, 0, B], [0, 4, E], [4, 8, B], [8, 12, CSM], [12, 16, A], [16, 20, E], [20, 24, B], [24, 26, A], [26, 28, B],
  [28, 40, E],
];
const chordAt = (n: number) => (CHORDS.find(([from, to]) => n >= from && n < to) ?? CHORDS[0])[2];

/** Beats `from`, `from + step`, … before `to`. */
const beats = (from: number, to: number, step = 1) =>
  Array.from({ length: Math.ceil((to - from) / step) }, (_, i) => from + i * step);

/** The music, `length` samples long: its mix, and what of it goes to the room. */
export function music(length: number): { mix: Stereo; send: Float32Array } {
  const drums = stereo(length), tonal = stereo(length), send = new Float32Array(length);

  // Kick: every beat while the groove plays; none in the pause for undo or just before the logo, one last for it.
  const kicks = [...beats(0, 16), ...beats(20, 27), 28];
  const k = kick();
  for (const n of kicks) place(drums, beat(n), k, n === 28 ? 0.6 : 0.95);

  // Hi-hats: ticking eighths leading into the drop, getting louder; off-beats in the groove, with quiet sixteenths
  // while the languages build and under the app list; a few fading ones under the logo.
  const hats = [hat(1), hat(2), hat(3)];
  let h = 0;
  const hatAt = (n: number, gain: number) => place(drums, beat(n), hats[h % 3], gain, h++ % 2 ? 0.2 : -0.2);
  beats(-4, 0, 0.5).forEach((n, i) => hatAt(n, 0.12 + 0.05 * i));
  for (const n of [...beats(0, 16), ...beats(20, 27)]) hatAt(n + 0.5, 0.5);
  for (const n of beats(16, 20)) hatAt(n + 0.5, 0.2);
  for (const n of [...beats(6, 16), ...beats(20, 27)]) {
    hatAt(n + 0.25, 0.16);
    hatAt(n + 0.75, 0.2);
  }
  place(drums, beat(15.5), hat(9, true), 0.3, 0.2);
  beats(28, 32).forEach((n, i) => hatAt(n + 0.5, 0.4 * (1 - i / 4)));

  // Claps on the second and fourth beats, with a roll into "17 languages" and a soft one back from the pause.
  const claps = [clap(1), clap(2)];
  let c = 0;
  const clapAt = (n: number, gain: number) => place(drums, beat(n), claps[c++ % 2], gain, 0, send, 0.4);
  for (const n of [9, 11, 13, 15, 21, 23, 25]) clapAt(n, 0.55);
  clapAt(11.5, 0.4);
  clapAt(11.75, 0.5);
  clapAt(19, 0.25);
  clapAt(19.5, 0.3);
  clapAt(19.75, 0.38);

  // Risers into the drop, into "17 languages", back from the pause and into the logo, and a soft hit on arrival.
  const rise = (from: number, to: number, gain: number, seed: number) =>
    place(drums, beat(from), riser(beat(to) - beat(from), seed), gain, 0, send, 0.3);
  rise(-2.5, 0, 0.3, 1);
  rise(10, 12, 0.3, 2);
  rise(18.5, 20, 0.35, 3);
  rise(25, 28, 0.32, 4);
  place(drums, beat(0), impact(1, 1), 0.6, 0, send, 0.3);
  place(drums, beat(12), impact(2, 1), 0.6, 0, send, 0.3);
  place(drums, beat(28), impact(3, 0.3), 0.35, 0, send, 0.4);

  // Bass: short notes on the off-beats in the groove; long low notes under the pause and under the logo.
  for (const n of [...beats(0, 16), ...beats(20, 27)]) place(tonal, beat(n + 0.5), bass(hz(chordAt(n).bass), BEAT * 0.42), 0.55);
  place(tonal, beat(16), bass(hz(E.bass), BEAT * 4), 0.35);
  place(tonal, beat(28), bass(hz(E.bass), BEAT * 6), 0.32);

  // Pads: the chords all the way through, fading in at the start.
  // (Fuller in the pause for undo, where they play alone; a little softer under the logo, where the chime rings.)
  CHORDS.forEach(([from, to, chord], i) =>
    pad(tonal, send, chord.pad, Math.max(0, beat(from)), beat(to), from < 0 ? 1.4 : 0.15, from === 16 ? 2.2 : from >= 28 ? 0.75 : 1, i));

  // Plucks: the chord's notes as rolling eighths through the languages and the app list, then E G# B E, one a beat,
  // under the logo.
  const pattern = [0, 1, 2, 3, 2, 1, 2, 3];
  for (const [from, to, gain] of [[6, 16, 0.3], [20, 27.5, 0.22]]) {
    beats(from, to, 0.5).forEach((n, i) =>
      place(tonal, beat(n), pluck(hz(chordAt(n).arp[pattern[i % 8]])), gain, i % 2 ? 0.3 : -0.3, send, 0.35));
  }
  [76, 80, 83, 88].forEach((m, i) => place(tonal, beat(29 + i), pluck(hz(m), 0.9), 0.28, 0, send, 0.5));

  // Each kick pushes everything else down for a moment: the pulse of dance music.
  const pump = new Float32Array(length).fill(1);
  for (const n of kicks) {
    const start = samples(beat(n));
    for (let i = 0; i < samples(0.45) && start + i < length; i++) {
      const t = i / RATE;
      pump[start + i] = Math.min(pump[start + i], 1 - 0.4 * Math.min(1, t / 0.005) * Math.exp(-t / 0.1));
    }
  }
  const mix = stereo(length);
  for (let i = 0; i < length; i++) {
    mix.left[i] = drums.left[i] + tonal.left[i] * pump[i];
    mix.right[i] = drums.right[i] + tonal.right[i] * pump[i];
  }
  return { mix, send };
}
