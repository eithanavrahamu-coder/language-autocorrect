// Building blocks for making sounds, used by the sound effects (sound.ts) and the music (music.ts): noise, filters,
// notes, a small room, and placing sounds into a stereo mix.

export const RATE = 48000;
export const samples = (seconds: number) => Math.round(seconds * RATE);
/** The frequency of MIDI note `midi` (69 is the A at 440 Hz, 64 the E above middle C). */
export const hz = (midi: number) => 440 * 2 ** ((midi - 69) / 12);

/** A sound, and where in it its moment is (a whoosh peaks in its middle, a rewind swells up to its end). */
export type Sound = { data: Float32Array; hit: number };
export type Stereo = { left: Float32Array; right: Float32Array };
export const stereo = (length: number): Stereo => ({ left: new Float32Array(length), right: new Float32Array(length) });

/** Repeatable random numbers (mulberry32), so the soundtrack comes out the same every time. */
export function random(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** A two-pole filter (the Audio EQ Cookbook's low-pass, high-pass and band-pass). */
export class Filter {
  kind: 'low' | 'high' | 'band';
  b0 = 0; b1 = 0; b2 = 0; a1 = 0; a2 = 0;
  x1 = 0; x2 = 0; y1 = 0; y2 = 0;

  constructor(kind: 'low' | 'high' | 'band', freq: number, q = 0.707) {
    this.kind = kind;
    this.tune(freq, q);
  }

  tune(freq: number, q: number) {
    const w = (2 * Math.PI * Math.min(freq, RATE * 0.45)) / RATE;
    const cos = Math.cos(w), a = Math.sin(w) / (2 * q), a0 = 1 + a;
    const [b0, b1, b2] =
      this.kind === 'low' ? [(1 - cos) / 2, 1 - cos, (1 - cos) / 2]
      : this.kind === 'high' ? [(1 + cos) / 2, -(1 + cos), (1 + cos) / 2]
      : [a, 0, -a];
    this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0;
    this.a1 = (-2 * cos) / a0; this.a2 = (1 - a) / a0;
  }

  run(x: number) {
    const y = this.b0 * x + this.b1 * this.x1 + this.b2 * this.x2 - this.a1 * this.y1 - this.a2 * this.y2;
    this.x2 = this.x1; this.x1 = x; this.y2 = this.y1; this.y1 = y;
    return y;
  }
}

export function normalized(data: Float32Array): Float32Array {
  let peak = 0;
  for (const v of data) peak = Math.max(peak, Math.abs(v));
  if (peak > 0) for (let i = 0; i < data.length; i++) data[i] /= peak;
  return data;
}

/** A sawtooth wave at phase `p` (0–1) stepping `dt` a sample, without the harsh aliasing of a plain one (PolyBLEP). */
export function saw(p: number, dt: number): number {
  let v = 2 * p - 1;
  if (p < dt) {
    const x = p / dt;
    v -= x + x - x * x - 1;
  } else if (p > 1 - dt) {
    const x = (p - 1) / dt;
    v -= x * x + x + x + 1;
  }
  return v;
}

export type Mode = { f: number; tau: number; a: number };

/** One knock: a sharp click, a burst of filtered noise, and the ringing of whatever was hit (its `modes`). */
export function knock(out: Float32Array, at: number, amp: number, rnd: () => number,
  o: { click: number; clickTau: number; band: number; bandTau: number; bandAmp: number; modes: Mode[] }) {
  const start = samples(at);
  const high = new Filter('high', o.click, 0.7);
  const band = new Filter('band', o.band, 1.3);
  const phases = o.modes.map(() => rnd() * Math.PI * 2);
  const n = Math.min(out.length - start, samples(0.15));
  for (let i = 0; i < n; i++) {
    const t = i / RATE;
    const noise = rnd() * 2 - 1;
    let v = high.run(noise) * Math.exp(-t / o.clickTau) * 0.9 + band.run(noise) * Math.exp(-t / o.bandTau) * o.bandAmp;
    for (let k = 0; k < o.modes.length; k++) {
      const m = o.modes[k];
      v += m.a * Math.sin(2 * Math.PI * m.f * t + phases[k]) * Math.exp(-t / m.tau);
    }
    out[start + i] += amp * Math.min(1, t / 0.0003) * v;
  }
}

/** A bell-like note: a sine with a few quieter overtones above it that fade faster. */
export function bell(out: Float32Array, at: number, freq: number, amp: number, tau: number) {
  const partials = [[1, 1, 1], [1.002, 0.35, 0.9], [2.76, 0.3, 0.45], [5.4, 0.11, 0.25], [8.93, 0.045, 0.15]];
  const start = samples(at);
  const n = Math.min(out.length - start, samples(tau * 7));
  for (const [ratio, level, decay] of partials) {
    const f = freq * ratio;
    if (f > 16000) continue;
    for (let i = 0; i < n; i++) {
      const t = i / RATE;
      out[start + i] += amp * level * Math.min(1, t / 0.002) * Math.exp(-t / (tau * decay)) * Math.sin(2 * Math.PI * f * t);
    }
  }
}

/** A short soft note with a little warmth (two overtones). */
export function note(out: Float32Array, at: number, freq: number, amp: number, tau: number) {
  const start = samples(at);
  const n = Math.min(out.length - start, samples(tau * 7));
  for (let i = 0; i < n; i++) {
    const t = i / RATE, w = 2 * Math.PI * freq * t;
    const env = Math.min(1, t / 0.002) * Math.exp(-t / tau);
    out[start + i] += amp * env * (Math.sin(w) + 0.3 * Math.sin(2 * w) + 0.1 * Math.sin(3 * w));
  }
}

/** Airy high noise, for sparkle. */
export function hiss(out: Float32Array, at: number, duration: number, amp: number, cutoff: number, rnd: () => number) {
  const high = new Filter('high', cutoff, 0.7);
  const start = samples(at);
  const n = Math.min(out.length - start, samples(duration));
  for (let i = 0; i < n; i++) {
    const t = i / RATE;
    out[start + i] += amp * Math.min(1, t / 0.03) * Math.exp(-t / (duration / 4)) * high.run(rnd() * 2 - 1);
  }
}

/**
 * Adds `sound` into `out` at `at` seconds, `gain` loud and panned by `pan` (-1 left … 1 right, keeping the same
 * loudness). `send` collects it for the room (the reverb), `wet` loud.
 */
export function place(out: Stereo, at: number, sound: Float32Array, gain: number, pan = 0, send?: Float32Array, wet = 0) {
  const start = samples(at);
  const angle = ((Math.max(-1, Math.min(1, pan)) + 1) * Math.PI) / 4;
  const gl = Math.cos(angle) * Math.SQRT2 * gain, gr = Math.sin(angle) * Math.SQRT2 * gain;
  for (let i = 0; i < sound.length; i++) {
    const j = start + i;
    if (j < 0 || j >= out.left.length) continue;
    out.left[j] += sound[i] * gl;
    out.right[j] += sound[i] * gr;
    if (send) send[j] += sound[i] * gain * wet;
  }
}

/** A small room (Freeverb's design), so the sounds sit together instead of in a vacuum. Adds its echo into `out`. */
export function room(send: Float32Array, out: Stereo, level: number) {
  const scale = RATE / 44100;
  const comb = (size: number) => ({ buf: new Float32Array(Math.round(size * scale)), i: 0, store: 0 });
  const pass = (size: number) => ({ buf: new Float32Array(Math.round(size * scale)), i: 0 });
  const side = (spread: number) => ({
    combs: [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map(s => comb(s + spread)),
    passes: [556, 441, 341, 225].map(s => pass(s + spread)),
  });
  const feedback = 0.86, damp = 0.3;
  const run = (s: ReturnType<typeof side>, x: number) => {
    let y = 0;
    for (const c of s.combs) {
      const echo = c.buf[c.i];
      c.store = echo * (1 - damp) + c.store * damp;
      c.buf[c.i] = x + c.store * feedback;
      c.i = (c.i + 1) % c.buf.length;
      y += echo;
    }
    for (const p of s.passes) {
      const b = p.buf[p.i];
      p.buf[p.i] = y + b * 0.5;
      p.i = (p.i + 1) % p.buf.length;
      y = b - y;
    }
    return y;
  };
  const l = side(0), r = side(23);
  for (let i = 0; i < send.length; i++) {
    out.left[i] += run(l, send[i] * 0.015) * level;
    out.right[i] += run(r, send[i] * 0.015) * level;
  }
}
