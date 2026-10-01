// Makes the video's music and sound effects from scratch: every sound is synthesized here, so there are no samples
// to license. Writes 44.1 kHz stereo WAVs to public/audio/. Deterministic: the same code always gives the same files.
//   node scripts/make-audio.mjs        (runs by itself before `npm run studio` and `npm run render`)
//
// The music is 120 BPM, so one beat is 15 video frames at 30 fps and one bar is 60. The song's sections line up
// with the scenes in src/timeline.ts (bar n starts at frame 60·n); change both together.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const SR = 44100;
const TAU = Math.PI * 2;
const OUT = path.resolve(import.meta.dirname, '../public/audio');

// ---------------------------------------------------------------- tiny DSP kit

function rng(seed) {
  return () => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
let rand = rng(4000);
const noise = () => rand() * 2 - 1;
const midi = (n) => 440 * 2 ** ((n - 69) / 12);
const mono = (sec) => new Float32Array(Math.ceil(sec * SR));
const smooth = (a, b, x) => { const t = Math.min(1, Math.max(0, (x - a) / (b - a))); return t * t * (3 - 2 * t); };

class Stereo {
  constructor(sec) { this.n = Math.ceil(sec * SR); this.L = new Float32Array(this.n); this.R = new Float32Array(this.n); }
}

/** Adds a mono sound into a stereo bus at `start` seconds, with an equal-power pan (-1 left … 1 right). */
function put(bus, src, start, gain = 1, pan = 0) {
  const a = ((pan + 1) * Math.PI) / 4, gl = Math.cos(a) * Math.SQRT2 * gain, gr = Math.sin(a) * Math.SQRT2 * gain;
  const o = Math.round(start * SR);
  for (let i = 0; i < src.length; i++) {
    const j = o + i;
    if (j < 0) continue;
    if (j >= bus.n) break;
    bus.L[j] += src[i] * gl;
    bus.R[j] += src[i] * gr;
  }
}
function putStereo(bus, src, start, gain = 1) {
  const o = Math.round(start * SR);
  for (let i = 0; i < src.n; i++) {
    const j = o + i;
    if (j < 0) continue;
    if (j >= bus.n) break;
    bus.L[j] += src.L[i] * gain;
    bus.R[j] += src.R[i] * gain;
  }
}

/** Andrew Simper's state-variable filter: low, band and high outputs, safe to sweep every sample. */
class SVF {
  constructor() { this.ic1 = 0; this.ic2 = 0; this.set(1000, 0.707); }
  set(fc, q) {
    const g = Math.tan((Math.PI * Math.min(Math.max(fc, 10), SR * 0.45)) / SR);
    this.k = 1 / q;
    this.a1 = 1 / (1 + g * (g + this.k));
    this.a2 = g * this.a1;
    this.a3 = g * this.a2;
  }
  run(v0) {
    const v3 = v0 - this.ic2;
    const v1 = this.a1 * this.ic1 + this.a2 * v3;
    const v2 = this.ic2 + this.a2 * this.ic1 + this.a3 * v3;
    this.ic1 = 2 * v1 - this.ic1;
    this.ic2 = 2 * v2 - this.ic2;
    this.low = v2; this.band = v1; this.high = v0 - this.k * v1 - v2;
    return v2;
  }
}

// Band-limited saw and square (polyBLEP), so high notes don't alias.
function blep(t, dt) {
  if (t < dt) { t /= dt; return t + t - t * t - 1; }
  if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
  return 0;
}
const saw = (p, dt) => 2 * p - 1 - blep(p, dt);
const square = (p, dt) => (p < 0.5 ? 1 : -1) + blep(p, dt) - blep((p + 0.5) % 1, dt);

/** Freeverb (Jezar's public-domain design): mono-summed input, stereo out. */
function reverb(inL, inR, { room = 0.84, damp = 0.3, predelay = 0.02, width = 1 } = {}) {
  const n = inL.length;
  const combT = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617], apT = [556, 441, 341, 225];
  const outs = [];
  const pd = Math.round(predelay * SR);
  for (const spread of [0, 23]) {
    const combs = combT.map((t) => ({ buf: new Float32Array(t + spread), i: 0, store: 0 }));
    const aps = apT.map((t) => ({ buf: new Float32Array(t + spread), i: 0 }));
    const out = new Float32Array(n);
    for (let s = 0; s < n; s++) {
      const k = s - pd;
      const x = k >= 0 ? (inL[k] + inR[k]) * 0.015 : 0;
      let y = 0;
      for (const c of combs) {
        const v = c.buf[c.i];
        c.store = v * (1 - damp) + c.store * damp;
        c.buf[c.i] = x + c.store * room;
        if (++c.i >= c.buf.length) c.i = 0;
        y += v;
      }
      for (const a of aps) {
        const b = a.buf[a.i];
        a.buf[a.i] = y + b * 0.5;
        if (++a.i >= a.buf.length) a.i = 0;
        y = b - y;
      }
      out[s] = y;
    }
    outs.push(out);
  }
  const [wl, wr] = outs, L = new Float32Array(n), R = new Float32Array(n);
  const w1 = (1 + width) / 2, w2 = (1 - width) / 2;
  for (let s = 0; s < n; s++) { L[s] = wl[s] * w1 + wr[s] * w2; R[s] = wr[s] * w1 + wl[s] * w2; }
  return { L, R };
}

/** Ping-pong echo, darker on every repeat. */
function pingpong(inL, inR, time, feedback, lowpass = 3500) {
  const n = inL.length, d = Math.round(time * SR);
  const bl = new Float32Array(d), br = new Float32Array(d);
  const L = new Float32Array(n), R = new Float32Array(n);
  const fl = new SVF(), fr = new SVF();
  fl.set(lowpass, 0.6); fr.set(lowpass, 0.6);
  for (let s = 0, i = 0; s < n; s++) {
    const dl = bl[i], dr = br[i];
    L[s] = dl; R[s] = dr;
    bl[i] = fl.run((inL[s] + inR[s]) * 0.5 + dr * feedback);
    br[i] = fr.run(dl * feedback);
    if (++i >= d) i = 0;
  }
  return { L, R };
}

function addInto(dst, src, gain = 1) {
  for (let i = 0; i < dst.n; i++) { dst.L[i] += src.L[i] * gain; dst.R[i] += src.R[i] * gain; }
}
function filterBus(bus, type, cutoffAt, q = 0.707) {
  for (const ch of [bus.L, bus.R]) {
    const f = new SVF();
    for (let i = 0; i < ch.length; i++) {
      if (i % 32 === 0) f.set(typeof cutoffAt === 'function' ? cutoffAt(i / SR) : cutoffAt, q);
      f.run(ch[i]);
      ch[i] = f[type];
    }
  }
}

function writeWav(file, L, R) {
  const n = L.length, data = Buffer.alloc(n * 4);
  for (let i = 0; i < n; i++) {
    data.writeInt16LE(Math.round(Math.max(-1, Math.min(1, L[i])) * 32767), i * 4);
    data.writeInt16LE(Math.round(Math.max(-1, Math.min(1, R[i])) * 32767), i * 4 + 2);
  }
  const h = Buffer.alloc(44);
  h.write('RIFF', 0); h.writeUInt32LE(36 + data.length, 4); h.write('WAVE', 8);
  h.write('fmt ', 12); h.writeUInt32LE(16, 16); h.writeUInt16LE(1, 20); h.writeUInt16LE(2, 22);
  h.writeUInt32LE(SR, 24); h.writeUInt32LE(SR * 4, 28); h.writeUInt16LE(4, 32); h.writeUInt16LE(16, 34);
  h.write('data', 36); h.writeUInt32LE(data.length, 40);
  writeFileSync(path.join(OUT, file), Buffer.concat([h, data]));
}

function peakOf(bus) { let p = 0; for (let i = 0; i < bus.n; i++) p = Math.max(p, Math.abs(bus.L[i]), Math.abs(bus.R[i])); return p; }
function normalize(bus, to) { const g = to / (peakOf(bus) || 1); for (let i = 0; i < bus.n; i++) { bus.L[i] *= g; bus.R[i] *= g; } }
/** Fades the last `sec` seconds to silence, so no sound ends in a click. */
function tail(bus, sec = 0.02) {
  const m = Math.min(bus.n, Math.round(sec * SR));
  for (let i = 0; i < m; i++) { const g = i / m; bus.L[bus.n - 1 - i] *= g; bus.R[bus.n - 1 - i] *= g; }
}
const stereoOf = (m) => { const s = new Stereo(m.length / SR); s.L.set(m); s.R.set(m); return s; };
function withVerb(dry, wet, opts) {
  const v = reverb(dry.L, dry.R, opts);
  for (let i = 0; i < dry.n; i++) { dry.L[i] += v.L[i] * wet; dry.R[i] += v.R[i] * wet; }
  return dry;
}

// ---------------------------------------------------------------- instruments

function kick({ f0 = 165, f1 = 47, pitchT = 0.038, decay = 0.3, len = 0.55, click = 0.3, drive = 1.8 } = {}) {
  const out = mono(len);
  let ph = 0;
  const hp = new SVF(); hp.set(3000, 0.7);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    ph += (f1 + (f0 - f1) * Math.exp(-t / pitchT)) / SR;
    const amp = Math.min(1, t / 0.0015) * Math.exp(-t / decay) * (1 - smooth(len - 0.08, len, t));
    let v = Math.tanh(Math.sin(TAU * ph) * amp * drive) / Math.tanh(drive);
    hp.run(noise());
    if (t < 0.006) v += hp.high * click * (1 - t / 0.006);
    out[i] = v;
  }
  return out;
}

function clap({ tone = 1150, decay = 0.12, len = 0.45, body = 0.3 } = {}) {
  const out = mono(len);
  const bp = new SVF(); bp.set(tone, 1.3);
  const hp = new SVF(); hp.set(500, 0.7);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    let env = 0;
    for (const o of [0, 0.0105, 0.0215]) if (t >= o) env = Math.max(env, Math.exp(-(t - o) / 0.0055));
    if (t >= 0.029) env = Math.max(env, 0.6 * Math.exp(-(t - 0.029) / decay));
    bp.run(noise());
    hp.run(bp.band);
    out[i] = hp.high * env * 2.4 + Math.sin(TAU * 188 * t) * Math.exp(-t / 0.045) * body;
    out[i] *= 1 - smooth(len - 0.05, len, t);
  }
  return out;
}

// The six square waves of a TR-808 cymbal, for a metallic shimmer under the noise.
const METAL = [205.3, 304.4, 369.6, 522.7, 540, 800];
function metal(t, ph) { let v = 0; for (let k = 0; k < 6; k++) v += (ph[k] = (ph[k] + METAL[k] * 1.7 / SR) % 1) < 0.5 ? 1 : -1; return v / 6; }

function hat({ decay = 0.032, len = 0.14, cutoff = 7800, metallic = 0.5 } = {}) {
  const out = mono(len);
  const hp = new SVF(); hp.set(cutoff, 0.9);
  const ph = [0, 0, 0, 0, 0, 0];
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    hp.run(noise() * (1 - metallic) + metal(t, ph) * metallic * 1.6);
    out[i] = hp.high * Math.exp(-t / decay) * Math.min(1, t / 0.0008) * (1 - smooth(len - 0.02, len, t));
  }
  return out;
}

function shaker({ len = 0.09 } = {}) {
  const out = mono(len);
  const bp = new SVF(); bp.set(6500, 1.2);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    bp.run(noise());
    out[i] = bp.band * smooth(0, 0.012, t) * Math.exp(-t / 0.025) * 1.6;
  }
  return out;
}

function crash({ len = 2.8, decay = 0.85 } = {}) {
  const out = mono(len);
  const hp = new SVF(); hp.set(4200, 0.7);
  const bp = new SVF(); bp.set(9000, 0.8);
  const ph = [0, 0, 0, 0, 0, 0];
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    hp.run(noise() * 0.75 + metal(t, ph) * 0.6);
    bp.run(hp.high);
    out[i] = (hp.high * 0.6 + bp.band * 0.6) * Math.exp(-t / decay) * Math.min(1, t / 0.001) * (1 - smooth(len - 0.3, len, t));
  }
  return out;
}

function snare({ decay = 0.09, len = 0.25, tone = 1800 } = {}) {
  const out = mono(len);
  const bp = new SVF(); bp.set(tone, 0.8);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    bp.run(noise());
    out[i] = (bp.band * 1.6 * Math.exp(-t / decay) + Math.sin(TAU * 200 * t) * Math.exp(-t / 0.03) * 0.5) * (1 - smooth(len - 0.03, len, t));
  }
  return out;
}

function riser(len, { from = 280, to = 7000 } = {}) {
  const out = mono(len);
  const bp = new SVF();
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, x = t / len;
    if (i % 16 === 0) bp.set(from * (to / from) ** x, 2.2);
    bp.run(noise());
    ph += (180 * 4 ** x) / SR;
    const amp = x * x * (1 - smooth(len - 0.01, len, t));
    out[i] = (bp.band * 1.4 + Math.sin(TAU * ph) * 0.12 * x) * amp;
  }
  return out;
}

const reversed = (m) => m.slice().reverse();

// ---------------------------------------------------------------- the song

const BPM = 120, BEAT = 60 / BPM, BAR = BEAT * 4, S16 = BEAT / 4;
const LENGTH = 54; // seconds; the video is 1620 frames at 30 fps

const CHORDS = {
  Am7: { pad: [57, 60, 64, 67], root: 45 },
  Fmaj7: { pad: [53, 57, 60, 64], root: 41 },
  Dm7: { pad: [53, 57, 60, 62], root: 38 },
  E7sus: { pad: [52, 57, 59, 62], root: 40 },
  G6: { pad: [55, 59, 62, 64], root: 43 },
  Em7: { pad: [52, 55, 59, 62], root: 40 },
  Gsus: { pad: [55, 60, 62, 64], root: 43 },
  Cmaj9: { pad: [48, 55, 59, 62, 64, 67], root: 36 },
};

// One entry per bar. Parts: intro (the problem), build (riser into a drop), drop, verse (how to use), outro, end.
const SONG = [
  ['Am7', 'intro'], ['Fmaj7', 'intro'], ['Dm7', 'intro2'], ['E7sus', 'build'],
  ['Fmaj7', 'drop'], ['G6', 'drop'], ['Em7', 'drop'], ['Am7', 'drop'], ['Dm7', 'drop'], ['G6', 'drop'],
  ['Fmaj7', 'verse'], ['G6', 'verse'], ['Em7', 'verse'], ['Am7', 'verse'], ['Dm7', 'verse'], ['Gsus', 'build2'],
  ['Fmaj7', 'drop2'], ['G6', 'drop2'], ['Em7', 'drop2'], ['Am7', 'drop2'], ['Dm7', 'drop2'], ['G6', 'drop2'],
  ['Fmaj7', 'outro'], ['G6', 'outro'], ['Cmaj9', 'end'],
];

// The tune for the second drop and the ending: [16th in the bar, length in 16ths, MIDI note] per bar.
const MELODY = {
  16: [[0, 3, 76], [4, 2, 79], [6, 2, 81], [8, 4, 79], [12, 4, 76]],
  17: [[0, 4, 74], [6, 2, 76], [8, 4, 74], [12, 4, 71]],
  18: [[0, 6, 79], [8, 4, 76], [12, 4, 74]],
  19: [[0, 8, 76], [10, 2, 72], [12, 4, 74]],
  20: [[0, 4, 74], [4, 2, 77], [6, 2, 81], [8, 8, 79]],
  21: [[0, 4, 83], [4, 4, 81], [8, 6, 79], [14, 2, 81]],
  22: [[0, 6, 76], [8, 4, 79], [12, 4, 81]],
  23: [[0, 4, 79], [4, 4, 81], [8, 8, 83]],
  24: [[0, 24, 84]],
};

function padVoices(bus, notes, start, dur, { attack = 0.08, release = 0.6, gain = 0.045 } = {}) {
  const detune = [-0.09, 0, 0.09], pans = [-0.65, 0, 0.65];
  const len = dur + release;
  for (const n of notes) {
    for (let v = 0; v < 3; v++) {
      const f = midi(n + detune[v]), dt = f / SR, out = mono(len);
      let p = rand();
      for (let i = 0; i < out.length; i++) {
        const t = i / SR;
        const env = smooth(0, attack, t) * (t < dur ? 1 : Math.exp(-(t - dur) / (release / 4)));
        out[i] = saw(p, dt) * env;
        p += dt; if (p >= 1) p -= 1;
      }
      put(bus, out, start, gain, pans[v]);
    }
  }
}

function bassNote(n, dur, { cutoff = 260, env = 1500, decay = 0.11, sub = 0.45, grit = 1 } = {}) {
  const len = dur + 0.05, out = mono(len), f = midi(n), dt = f / SR;
  const lp = new SVF();
  let p = 0, ps = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    if (i % 8 === 0) lp.set(cutoff + env * Math.exp(-t / decay), 1.1);
    lp.run((saw(p, dt) * 0.7 + square(p, dt) * 0.3) * grit);
    const amp = smooth(0, 0.004, t) * (1 - smooth(dur, dur + 0.045, t));
    out[i] = Math.tanh((lp.low + Math.sin(TAU * ps) * sub) * 1.4) * amp;
    p += dt; if (p >= 1) p -= 1;
    ps += f / 2 / SR;
  }
  return out;
}

function pluck(n, { len = 0.32, cutoff = 900, env = 4200, decay = 0.06 } = {}) {
  const out = mono(len), f = midi(n), dt = f / SR;
  const lp = new SVF();
  let p = rand(), q = rand();
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    if (i % 8 === 0) lp.set(cutoff + env * Math.exp(-t / decay), 1.3);
    lp.run(square(p, dt) * 0.55 + saw(q, dt * 1.004) * 0.45);
    out[i] = lp.low * Math.min(1, t / 0.002) * Math.exp(-t / 0.13) * (1 - smooth(len - 0.03, len, t));
    p += dt; if (p >= 1) p -= 1;
    q += dt * 1.004; if (q >= 1) q -= 1;
  }
  return out;
}

/** A soft electric-piano/bell voice (two-operator FM). */
function keys(n, dur, { bright = 1.6, ratio = 1, decay = 0.9, len = dur + 1.2 } = {}) {
  const out = mono(len), f = midi(n);
  let pc = 0, pm = 0, pc2 = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    const index = bright * Math.exp(-t / 0.25) + 0.25;
    pm += (f * ratio) / SR;
    const mod = Math.sin(TAU * pm) * index;
    pc += f / SR; pc2 += (f * 1.003) / SR;
    const amp = Math.min(1, t / 0.004) * Math.exp(-t / decay) * (t < dur ? 1 : Math.exp(-(t - dur) / 0.12));
    out[i] = (Math.sin(TAU * pc + mod) * 0.6 + Math.sin(TAU * pc2 + mod * 0.5) * 0.4 + Math.sin(TAU * pc * 2) * 0.08) * amp;
  }
  return out;
}

function makeMusic() {
  rand = rng(120);
  const bus = {
    drums: new Stereo(LENGTH), bass: new Stereo(LENGTH), pad: new Stereo(LENGTH),
    arp: new Stereo(LENGTH), lead: new Stereo(LENGTH), fx: new Stereo(LENGTH),
  };
  const kickTimes = [];
  const K = kick(), Kh = kick({ decay: 0.22, f0: 140 }), C = clap(), CH = hat(), OH = hat({ decay: 0.09, len: 0.3 });
  const SH = shaker(), CR = crash(), SN = snare();
  const at = (bar, beat = 0) => bar * BAR + beat * BEAT;
  const hitKick = (t, g = 0.95, k = K) => { put(bus.drums, k, t, g * 0.65); kickTimes.push({ t, g }); };

  SONG.forEach(([chordName, part], bar) => {
    const chord = CHORDS[chordName], t0 = at(bar);
    const drop = part === 'drop' || part === 'drop2' || part === 'outro';

    // ---- pad
    if (part === 'end') padVoices(bus.pad, chord.pad, t0, LENGTH - t0 - 1.6, { attack: 0.02, release: 1.4, gain: 0.05 });
    else if (part === 'build') {
      padVoices(bus.pad, chord.pad, t0, BEAT * 2, { attack: 0.05, release: 0.3 });
      padVoices(bus.pad, [52, 56, 59, 62], t0 + BEAT * 2, BEAT * 2, { attack: 0.05, release: 0.25 }); // E major: pulls into the drop
    } else padVoices(bus.pad, chord.pad, t0, BAR, { attack: part.startsWith('intro') ? 0.5 : 0.03, release: 0.5 });

    // ---- drums
    if (part === 'intro' && bar === 1) hitKick(t0, 0.5, Kh);
    if (part === 'intro2') { hitKick(t0, 0.6, Kh); hitKick(at(bar, 2), 0.6, Kh); }
    if (drop) for (let b = 0; b < 4; b++) hitKick(at(bar, b));
    if (part === 'verse') { hitKick(t0, 0.85); hitKick(at(bar, 2.5), 0.7); }
    if (part === 'build2') for (let b = 0; b < 3; b++) hitKick(at(bar, b), 0.9);
    if (part === 'end') hitKick(t0, 1);

    if (drop) for (const b of [1, 3]) put(bus.drums, C, at(bar, b), 0.5, 0.05);
    if (part === 'verse') put(bus.drums, C, at(bar, 2), 0.42, 0.05);

    for (let s = 0; s < 16; s++) {
      const t = t0 + s * S16, off = s % 4 === 2, swing = s % 2 === 1 ? 0.012 : 0;
      if (part === 'intro' && bar === 1 && off) put(bus.drums, CH, t, 0.11, 0.3);
      if (part === 'intro2' && s % 2 === 0) put(bus.drums, CH, t, s % 4 === 2 ? 0.16 : 0.09, 0.3);
      if (drop) {
        if (off) put(bus.drums, OH, t, 0.28, 0.25);
        else if (s % 2 === 1) put(bus.drums, CH, t + swing, 0.13, -0.3);
        put(bus.drums, SH, t + swing, s % 2 ? 0.12 : 0.08, -0.5);
      }
      if (part === 'verse') {
        if (s % 2 === 0) put(bus.drums, CH, t, off ? 0.2 : 0.11, 0.3);
        put(bus.drums, SH, t + swing, 0.09, -0.5);
      }
    }

    // ---- snare rolls, risers, crashes
    if (part === 'build2') {
      for (let s = 0; s < 32; s++) {
        const t = t0 + (s < 16 ? s * S16 : 2 * BEAT + (s - 16) * S16 / 2);
        if (t >= t0 + BAR - S16) break;
        put(bus.drums, SN, t, 0.08 + 0.3 * (s / 32) ** 2, (s % 2 ? 0.15 : -0.15));
      }
    }
    if (part === 'build' || part === 'build2') put(bus.fx, riser(BAR), t0, 0.3);
    if ([4, 16, 24].includes(bar)) put(bus.drums, CR, t0, 0.32, 0.2);
    if ([10, 22].includes(bar)) put(bus.drums, CR, t0, 0.16, -0.2);
    if ([4, 16, 24].includes(bar)) put(bus.fx, reversed(crash({ len: 1.2, decay: 0.5 })), t0 - 1.2, 0.18, -0.2);
    if (bar === 23) put(bus.fx, riser(BAR / 2, { from: 600 }), at(bar, 2), 0.18);

    // ---- bass
    const r = chord.root;
    if (part === 'intro' || part === 'intro2') put(bus.bass, bassNote(r, BAR - 0.05, { cutoff: 240, env: 300, sub: 0.5, grit: 0.8 }), t0, 0.2);
    if (drop) {
      for (const e of [1, 3, 5, 7]) put(bus.bass, bassNote(e === 7 ? r + 12 : r, 0.2), t0 + e * BEAT / 2, 0.62);
      put(bus.bass, bassNote(r, 0.12, { env: 600 }), t0, 0.35);
    }
    if (part === 'verse') {
      put(bus.bass, bassNote(r, 0.9, { cutoff: 180, env: 700, decay: 0.2 }), t0, 0.55);
      put(bus.bass, bassNote(r, 0.65, { cutoff: 180, env: 700, decay: 0.2 }), at(bar, 2), 0.5);
      put(bus.bass, bassNote(r + 12, 0.2, { cutoff: 200, env: 900 }), at(bar, 3.5), 0.35);
    }
    if (part === 'build2') for (let e = 0; e < 7; e++) put(bus.bass, bassNote(r, 0.2, { cutoff: 200 + e * 120 }), t0 + e * BEAT / 2, 0.5);
    if (part === 'end') put(bus.bass, bassNote(r, 3.2, { cutoff: 160, env: 900, decay: 0.3, sub: 0.9 }), t0, 0.65);

    // ---- arpeggio
    const tones = [...chord.pad].sort((a, b) => a - b).slice(0, 4).map((n) => n + 12);
    tones.push(tones[0] + 12);
    const pattern = [0, 2, 4, 2, 1, 3, 4, 3, 0, 2, 4, 2, 1, 3, 4, 1];
    const arpOn = part === 'intro2' || part === 'build' || drop || part === 'verse' || part === 'build2';
    if (arpOn) {
      for (let s = 0; s < 16; s++) {
        let n = tones[pattern[s]];
        if (part === 'build' && s >= 8) n = [64, 68, 71, 74, 76][pattern[s]]; // E major half
        const cutoff = part === 'intro2' ? 500 : part === 'build' ? 600 + s * 120 : part === 'build2' ? 900 + s * 160 : 1000;
        const g = (s % 4 === 0 ? 0.2 : 0.14) * (part === 'intro2' ? 0.6 : part === 'drop2' ? 0.8 : 1);
        put(bus.arp, pluck(n, { cutoff }), t0 + s * S16, g, s % 2 ? 0.35 : -0.35);
      }
    }
    if (part === 'end') {
      [72, 76, 79, 83, 86, 88, 91, 95].forEach((n, i) =>
        put(bus.lead, keys(n, 0.3, { bright: 1.2, decay: 0.7 }), t0 + 0.06 + i * S16, 0.07 * (1 - i * 0.06), i % 2 ? 0.4 : -0.4));
    }

    // ---- melody
    for (const [s, l, n] of MELODY[bar] ?? []) {
      put(bus.lead, keys(n, l * S16, { decay: part === 'end' ? 2.2 : 0.9 }), t0 + s * S16, 0.17, 0);
      put(bus.lead, keys(n - 12, l * S16, { bright: 0.8 }), t0 + s * S16, 0.05, 0);
    }
  });

  if (process.argv.includes('--stats')) {
    // How loud each instrument is in the intro, the first drop and the how-to part (dB RMS), for balancing the mix.
    const rms = (b, a, e) => { let s = 0; for (let i = Math.floor(a * SR); i < e * SR; i++) s += (b.L[i] + b.R[i]) ** 2 / 4; return (10 * Math.log10(s / ((e - a) * SR) + 1e-12)).toFixed(1); };
    for (const [name, b] of Object.entries(bus)) console.log(name.padEnd(6), 'intro', rms(b, 0, 6), ' drop', rms(b, 8, 20), ' verse', rms(b, 20, 30), ' drop2', rms(b, 32, 44));
  }

  // ---- mix: sidechain duck from the kick, filters, echoes and room
  const duck = new Float32Array(Math.ceil(LENGTH * SR)).fill(1);
  for (const { t, g } of kickTimes) {
    const s0 = Math.round(t * SR);
    for (let i = 0; i < 0.32 * SR; i++) {
      const dt = i / SR, d = 0.62 * g * (dt < 0.004 ? dt / 0.004 : Math.exp(-(dt - 0.004) / 0.075));
      if (s0 + i < duck.length) duck[s0 + i] = Math.min(duck[s0 + i], 1 - d);
    }
  }
  const sidechain = (b, depth) => { for (let i = 0; i < b.n; i++) { const g = 1 - (1 - duck[i]) * depth; b.L[i] *= g; b.R[i] *= g; } };

  const padCutoff = (t) => {
    const bar = t / BAR;
    if (bar < 3) return 650 * (1600 / 650) ** (bar / 3);
    if (bar < 4) return 1600 * (3600 / 1600) ** (bar - 3);
    if (bar >= 10 && bar < 15) return 2600;
    if (bar >= 15 && bar < 16) return 2600 * (4200 / 2600) ** (bar - 15);
    if (bar >= 24) return 3000 * (500 / 3000) ** Math.min(1, (t - 48) / 6);
    return 3800 + 600 * Math.sin(TAU * 0.25 * t);
  };
  filterBus(bus.pad, 'low', padCutoff, 0.9);
  filterBus(bus.pad, 'high', 140);
  filterBus(bus.arp, 'high', 220);
  sidechain(bus.pad, 1);
  sidechain(bus.bass, 1);
  sidechain(bus.arp, 0.55);
  sidechain(bus.lead, 0.3);

  const arpEcho = pingpong(bus.arp.L, bus.arp.R, BEAT * 0.75, 0.38, 3000);
  const leadEcho = pingpong(bus.lead.L, bus.lead.R, BEAT * 0.75, 0.3, 2600);
  const send = new Stereo(LENGTH);
  addInto(send, bus.pad, 1.3); addInto(send, bus.arp, 1.1); addInto(send, bus.lead, 1.5);
  addInto(send, bus.drums, 0.08); addInto(send, bus.fx, 0.6);
  const room = reverb(send.L, send.R, { room: 0.86, damp: 0.35, predelay: 0.025 });

  const master = new Stereo(LENGTH);
  addInto(master, bus.drums, 0.6);
  addInto(master, bus.bass, 0.75);
  addInto(master, bus.pad, 3.2);
  addInto(master, bus.arp, 2.1);
  addInto(master, { L: arpEcho.L, R: arpEcho.R }, 0.9);
  addInto(master, bus.lead, 2.2);
  addInto(master, { L: leadEcho.L, R: leadEcho.R }, 0.6);
  addInto(master, bus.fx, 1.2);
  addInto(master, { L: room.L, R: room.R }, 0.6);
  filterBus(master, 'high', 35);

  // Level: put the loudest moments near -1 dB, rounding off the rare peaks above that instead of clipping them.
  const abs = [];
  for (let i = 0; i < master.n; i += 7) abs.push(Math.abs(master.L[i]), Math.abs(master.R[i]));
  abs.sort((a, b) => a - b);
  const g = 0.8 / abs[Math.floor(abs.length * 0.9995)];
  const knee = 0.8, soft = (x) => { const a = Math.abs(x) * g; return Math.sign(x) * (a < knee ? a : knee + 0.19 * Math.tanh((a - knee) / 0.19)); };
  for (let i = 0; i < master.n; i++) {
    const fade = Math.min(1, i / (0.05 * SR)) * (1 - smooth(LENGTH - 2.5, LENGTH, i / SR));
    master.L[i] = soft(master.L[i]) * fade;
    master.R[i] = soft(master.R[i]) * fade;
  }
  writeWav('music.wav', master.L, master.R);
  return master;
}

// ---------------------------------------------------------------- sound effects

/** A laptop-style key: a sharp tick, a short plastic thock, and a quieter click as the key comes back up. */
function keyClick(seed, { thock = 380, tone = 2300, up = 0.06, weight = 1 } = {}) {
  rand = rng(seed);
  const len = up + 0.07, out = mono(len);
  const bp = new SVF(); bp.set(tone * (0.9 + rand() * 0.2), 2.5);
  const hp = new SVF(); hp.set(3500, 0.7);
  const bp2 = new SVF(); bp2.set(tone * 1.3, 3);
  const th = thock * (0.92 + rand() * 0.16);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, nz = noise();
    bp.run(nz); hp.run(nz); bp2.run(nz);
    let v = hp.high * Math.exp(-t / 0.0011) * 0.9;
    v += bp.band * Math.exp(-t / 0.011) * 1.4 * weight;
    v += Math.sin(TAU * th * t) * Math.exp(-t / 0.014) * 0.55 * weight;
    if (t >= up) { const u = t - up; v += (bp2.band * 1.2 + hp.high * 0.4) * Math.exp(-u / 0.006) * 0.45; }
    out[i] = v * (1 - smooth(len - 0.01, len, t));
  }
  const s = stereoOf(out);
  return withVerb(s, 0.08, { room: 0.5, damp: 0.6, predelay: 0.004, width: 0.5 });
}

/** The correction: a quick airy swipe and two glassy chimes going up. */
function fixSound() {
  rand = rng(11);
  const len = 1.6, s = new Stereo(len);
  const sw = mono(0.22), bp = new SVF();
  for (let i = 0; i < sw.length; i++) {
    const t = i / SR, x = t / 0.22;
    if (i % 16 === 0) bp.set(900 * (6 ** x), 1.6);
    bp.run(noise());
    sw[i] = bp.band * Math.sin(Math.PI * x) ** 1.5 * 0.8;
  }
  put(s, sw, 0, 0.7, -0.3);
  const chime = (f, start, gain, pan) => {
    const out = mono(len - start);
    let pc = 0, pm = 0;
    for (let i = 0; i < out.length; i++) {
      const t = i / SR;
      pm += (f * 4) / SR; pc += f / SR;
      out[i] = Math.sin(TAU * pc + Math.sin(TAU * pm) * (1.1 * Math.exp(-t / 0.08) + 0.1)) * Math.min(1, t / 0.002) * Math.exp(-t / 0.38);
    }
    put(s, out, start, gain, pan);
  };
  chime(midi(91), 0.07, 0.45, -0.15); // G6
  chime(midi(96), 0.135, 0.5, 0.2); // C7
  chime(midi(103), 0.135, 0.08, 0.2); // G7 shimmer
  withVerb(s, 0.35, { room: 0.75, damp: 0.3 });
  tail(s, 0.2);
  return s;
}

/** The badge flipping to the new keyboard: two soft ticks and a little rising blip. */
function switchSound() {
  rand = rng(12);
  const len = 0.35, out = mono(len);
  const hp = new SVF(); hp.set(4000, 0.7);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    hp.run(noise());
    let v = 0;
    for (const o of [0, 0.045]) if (t >= o) v += hp.high * Math.exp(-(t - o) / 0.0015) * 0.6;
    if (t >= 0.045) { const u = t - 0.045; ph += (1050 + 700 * Math.min(1, u / 0.03)) / SR; v += Math.sin(TAU * ph) * Math.exp(-u / 0.05) * 0.5; }
    out[i] = v * (1 - smooth(len - 0.02, len, t));
  }
  return withVerb(stereoOf(out), 0.18, { room: 0.6, damp: 0.4 });
}

/** A bubbly pop for things appearing, ending on a note of the song's key. */
function pop(note) {
  rand = rng(note);
  const len = 0.32, out = mono(len), f = midi(note);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    ph += (f * (0.55 + 0.45 * smooth(0, 0.035, t))) / SR;
    out[i] = (Math.sin(TAU * ph) + 0.15 * Math.sin(TAU * ph * 2)) * Math.min(1, t / 0.002) * Math.exp(-t / 0.06);
  }
  return withVerb(stereoOf(out), 0.2, { room: 0.6, damp: 0.4 });
}

function whoosh(len = 0.7, { low = 220, high = 2600, seed = 20 } = {}) {
  rand = rng(seed);
  const s = new Stereo(len + 0.4);
  const bpL = new SVF(), bpR = new SVF();
  for (let i = 0; i < Math.ceil(len * SR); i++) {
    const t = i / SR, x = t / len;
    const fc = low * (high / low) ** Math.sin(Math.PI * Math.min(1, x * 1.15)) ** 1.2;
    if (i % 16 === 0) { bpL.set(fc, 1.1); bpR.set(fc * 1.05, 1.1); }
    const env = Math.sin(Math.PI * x) ** 2;
    const pan = -0.8 + 1.6 * x;
    bpL.run(noise()); bpR.run(noise());
    s.L[i] = bpL.band * env * (1 - pan) * 0.9;
    s.R[i] = bpR.band * env * (1 + pan) * 0.9;
  }
  withVerb(s, 0.25, { room: 0.7, damp: 0.4 });
  tail(s, 0.1);
  return s;
}

/** A soft "uh-oh" for the wrong word: two low, rounded notes going down. */
function errorSound() {
  rand = rng(13);
  const len = 0.6, out = mono(len);
  const lp = new SVF(); lp.set(900, 0.8);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, second = t >= 0.13;
    const f = second ? midi(48) : midi(52);
    ph += f / SR;
    const u = second ? t - 0.13 : t;
    const env = Math.min(1, u / 0.006) * Math.exp(-u / (second ? 0.16 : 0.07));
    lp.run(square(ph % 1, f / SR) * 0.6 + Math.sin(TAU * ph) * 0.6);
    out[i] = lp.low * env * (1 - smooth(len - 0.03, len, t));
  }
  return withVerb(stereoOf(out), 0.15, { room: 0.6, damp: 0.5 });
}

function mouseClick() {
  rand = rng(14);
  const len = 0.16, out = mono(len);
  const bp = new SVF(); bp.set(4200, 3);
  const bp2 = new SVF(); bp2.set(3000, 3);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, nz = noise();
    bp.run(nz); bp2.run(nz);
    let v = bp.band * Math.exp(-t / 0.003) * 1.4;
    if (t > 0.075) v += bp2.band * Math.exp(-(t - 0.075) / 0.0025) * 0.8;
    out[i] = v * (1 - smooth(len - 0.01, len, t));
  }
  return withVerb(stereoOf(out), 0.1, { room: 0.5, damp: 0.6 });
}

/** A punchy low hit for big words landing on the beat. */
function thud({ f0 = 150, f1 = 55, seed = 15, bright = 1 } = {}) {
  rand = rng(seed);
  const len = 0.9, out = mono(len);
  const bp = new SVF(); bp.set(1600, 0.9);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    ph += (f1 + (f0 - f1) * Math.exp(-t / 0.05)) / SR;
    bp.run(noise());
    out[i] = (Math.tanh(Math.sin(TAU * ph) * 2) * Math.exp(-t / 0.22) * 0.9 + bp.band * Math.exp(-t / 0.03) * 0.9 * bright) *
      (1 - smooth(len - 0.1, len, t));
  }
  return withVerb(stereoOf(out), 0.3, { room: 0.8, damp: 0.4 });
}

/** The big moment the logo appears: a deep drop, a burst of air and a long tail. */
function impact() {
  rand = rng(16);
  const len = 3.2, out = mono(len);
  const lp = new SVF(); lp.set(1800, 0.7);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    ph += (36 + 90 * Math.exp(-t / 0.09)) / SR;
    lp.run(noise());
    out[i] = (Math.sin(TAU * ph) * Math.exp(-t / 0.75) * 0.9 + lp.low * Math.exp(-t / 0.18) * 0.7) * (1 - smooth(len - 0.5, len, t));
  }
  return withVerb(stereoOf(out), 0.45, { room: 0.9, damp: 0.3 });
}

/** A glittery run up the scale, for the logo and the final card. */
function shimmer() {
  rand = rng(17);
  const len = 2.6, s = new Stereo(len);
  [84, 88, 91, 95, 96, 100, 103].forEach((n, i) => put(s, keys(n, 0.15, { bright: 2.2, ratio: 3.5, decay: 0.45, len: 1 }), i * 0.045, 0.2, i % 2 ? 0.5 : -0.5));
  withVerb(s, 0.5, { room: 0.88, damp: 0.25 });
  tail(s, 0.3);
  return s;
}

/** Download finished: a bright major chord, rolled. */
function success() {
  rand = rng(18);
  const len = 2.2, s = new Stereo(len);
  [72, 76, 79, 84].forEach((n, i) => put(s, keys(n, 0.4, { bright: 1.4, decay: 0.7, len: 1.6 }), i * 0.06, 0.24, -0.3 + i * 0.2));
  withVerb(s, 0.4, { room: 0.8, damp: 0.3 });
  tail(s, 0.3);
  return s;
}

/** A tiny tick for counters and checkmarks. */
function tick(note = 96) {
  rand = rng(19);
  const len = 0.12, out = mono(len), f = midi(note);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    out[i] = Math.sin(TAU * f * t) * Math.min(1, t / 0.001) * Math.exp(-t / 0.018);
  }
  return withVerb(stereoOf(out), 0.15, { room: 0.5, damp: 0.5 });
}

/** A switch being flipped on: a click with a soft rising tone. */
function toggle() {
  rand = rng(21);
  const len = 0.25, out = mono(len);
  const bp = new SVF(); bp.set(2800, 2);
  let ph = 0;
  for (let i = 0; i < out.length; i++) {
    const t = i / SR;
    bp.run(noise());
    ph += (700 + 500 * smooth(0, 0.05, t)) / SR;
    out[i] = (bp.band * Math.exp(-t / 0.004) * 1.2 + Math.sin(TAU * ph) * Math.exp(-t / 0.05) * 0.4) * (1 - smooth(len - 0.02, len, t));
  }
  return withVerb(stereoOf(out), 0.15, { room: 0.55, damp: 0.5 });
}

/** A short "fwip" for things sliding or flipping. */
function swipe(seed = 22) {
  rand = rng(seed);
  const len = 0.2, out = mono(len);
  const bp = new SVF();
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, x = t / len;
    if (i % 16 === 0) bp.set(1200 * 5 ** x, 1.5);
    bp.run(noise());
    out[i] = bp.band * Math.sin(Math.PI * x) ** 2;
  }
  return withVerb(stereoOf(out), 0.2, { room: 0.6, damp: 0.4 });
}

/** A cracking smash, for the old shortcut being knocked away. */
function smash() {
  rand = rng(23);
  const len = 1.0, out = mono(len);
  const hp = new SVF(); hp.set(2500, 0.8);
  const bp = new SVF(); bp.set(700, 1.2);
  for (let i = 0; i < out.length; i++) {
    const t = i / SR, nz = noise();
    hp.run(nz); bp.run(nz);
    let v = bp.band * Math.exp(-t / 0.05) * 1.6;
    // a few scattered crackles
    v += hp.high * Math.exp(-t / 0.12) * (rand() < 0.02 + 0.2 * Math.exp(-t / 0.05) ? 1.6 : 0.15);
    v += Math.sin(TAU * (90 * Math.exp(-t / 0.2) + 40) * t) * Math.exp(-t / 0.15) * 0.7;
    out[i] = v * (1 - smooth(len - 0.2, len, t));
  }
  return withVerb(stereoOf(out), 0.3, { room: 0.75, damp: 0.4 });
}

// ---------------------------------------------------------------- write everything

mkdirSync(OUT, { recursive: true });
const started = Date.now();

const sfx = {
  'key-1': keyClick(101), 'key-2': keyClick(102, { thock: 340 }), 'key-3': keyClick(103, { tone: 2600 }),
  'key-4': keyClick(104, { thock: 420, up: 0.055 }), 'key-5': keyClick(105, { tone: 2100, up: 0.07 }),
  'key-6': keyClick(106, { thock: 360, tone: 2450 }),
  space: keyClick(110, { thock: 190, tone: 1300, up: 0.09, weight: 1.5 }),
  enter: keyClick(111, { thock: 230, tone: 1700, up: 0.08, weight: 1.4 }),
  backspace: keyClick(112, { thock: 300, tone: 1900, up: 0.05 }),
  fix: fixSound(), switch: switchSound(),
  'pop-1': pop(84), 'pop-2': pop(86), 'pop-3': pop(88), 'pop-4': pop(91), 'pop-5': pop(93), 'pop-6': pop(96),
  whoosh: whoosh(0.6), 'whoosh-big': whoosh(1.0, { low: 140, high: 2000, seed: 24 }), error: errorSound(),
  click: mouseClick(), thud: thud(), 'thud-high': thud({ f0: 210, f1: 80, seed: 25, bright: 1.3 }), impact: impact(),
  shimmer: shimmer(), success: success(), tick: tick(), toggle: toggle(), swipe: swipe(), smash: smash(),
};
for (const [name, s] of Object.entries(sfx)) {
  normalize(s, 0.7);
  tail(s);
  writeWav(`${name}.wav`, s.L, s.R);
}
const music = makeMusic();

console.log(`Wrote ${Object.keys(sfx).length} sound effects and music.wav (${(music.n / SR).toFixed(1)} s) to ` +
  `${path.relative(process.cwd(), OUT)} in ${((Date.now() - started) / 1000).toFixed(1)} s.`);
