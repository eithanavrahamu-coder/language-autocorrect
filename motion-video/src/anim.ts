import { Easing, interpolate, spring } from 'remotion';
import type { CSSProperties } from 'react';

export const FPS = 30;
/** The music is 120 BPM: a beat is 15 frames and a bar is 60 (scripts/make-audio.mjs). */
export const BEAT = 15;
export const BAR = 60;

export const easeOut = Easing.bezier(0.2, 0.8, 0.2, 1);
export const easeInOut = Easing.bezier(0.65, 0, 0.35, 1);
export const easeIn = Easing.bezier(0.55, 0, 1, 0.45);

const clamp = { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' } as const;

/** 0 → 1 between two frames, eased. */
export const prog = (frame: number, start: number, dur: number, easing = easeOut) =>
  interpolate(frame, [start, start + dur], [0, 1], { ...clamp, easing });

export const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

/** A springy 0 → 1 (it can overshoot a little) starting at `start`. */
export const pop = (frame: number, start: number, opts: { damping?: number; stiffness?: number; mass?: number } = {}) =>
  spring({ frame: frame - start, fps: FPS, config: { damping: 14, stiffness: 170, mass: 1, ...opts } });

/** Fades and lifts something into place. */
export function rise(frame: number, start: number, { dist = 32, dur = 20 } = {}): CSSProperties {
  const p = prog(frame, start, dur);
  return { opacity: p, transform: `translateY(${(1 - p) * dist}px)` };
}

/** Fades something out while it drifts away. */
export function leave(frame: number, start: number, { dist = -24, dur = 12 } = {}): CSSProperties {
  const p = prog(frame, start, dur, easeIn);
  return { opacity: 1 - p, transform: `translateY(${p * dist}px)` };
}

/** Uneven gaps between keystrokes, so typing sounds human. */
const GAPS = [0, 1, -1, 1, 0, -1, 2, 0, -1, 1, 0, -1];
/** The frames at which each of `n` keys is pressed, starting at `start`, about `gap` frames apart. */
export function keyTimes(start: number, n: number, gap = 4): number[] {
  const out: number[] = [];
  let t = start;
  for (let i = 0; i < n; i++) {
    out.push(Math.round(t));
    t += Math.max(2, gap + (gap >= 4 ? GAPS[i % GAPS.length] : 0));
  }
  return out;
}

/** How many of the keys at `times` have been pressed by `frame`. */
export const typedCount = (times: number[], frame: number) => times.filter((t) => t <= frame).length;

/** A caret that blinks every half second, and stays solid while typing. */
export const caretOn = (frame: number, lastKey = -100) => frame - lastKey < 12 || Math.floor(frame / 15) % 2 === 0;

/** A quick shake that dies out, in px. */
export const shake = (frame: number, start: number, amount = 14, dur = 14) => {
  const t = frame - start;
  if (t < 0 || t > dur) return 0;
  return Math.sin(t * 2.2) * amount * (1 - t / dur);
};
