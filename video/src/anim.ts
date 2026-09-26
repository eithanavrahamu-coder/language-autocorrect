import { Easing, interpolate, spring, useCurrentFrame } from 'remotion';
import { FPS } from './timeline.ts';

/** The website's ease-out curve, cubic-bezier(.2, .8, .2, 1). */
export const easeOut = Easing.bezier(0.2, 0.8, 0.2, 1);
export const easeIn = Easing.bezier(0.5, 0, 0.75, 0);

/** The current time in seconds. */
export function useTime(): number {
  return useCurrentFrame() / FPS;
}

/** 0 → 1 over `duration` seconds from `at`. */
export function ramp(t: number, at: number, duration: number, easing = easeOut): number {
  return interpolate(t, [at, at + duration], [0, 1], {
    easing,
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
}

/** The website's spring (stiffness 520, damping 34) from 0 to 1, starting at `at`; it overshoots by about 3%. */
export function pop(t: number, at: number, config: { stiffness?: number; damping?: number; mass?: number } = {}): number {
  if (t < at) return 0;
  return spring({
    frame: (t - at) * FPS,
    fps: FPS,
    config: { stiffness: 520, damping: 34, mass: 1, ...config },
  });
}

/** A springier, bouncier version for things that should feel playful. */
export const BOUNCY = { stiffness: 260, damping: 16 };

/** How far a key is down (0–1): quickly down when pressed, then back up. */
export function keyDown(t: number, presses: number[]): number {
  let down = 0;
  for (const p of presses) {
    if (t < p) continue;
    down = Math.max(down, interpolate(t - p, [0, 0.03, 0.09, 0.2], [0, 1, 1, 0], {
      extrapolateLeft: 'clamp',
      extrapolateRight: 'clamp',
    }));
  }
  return down;
}

/** How many of `times` have passed. */
export const countUntil = (t: number, times: number[]) => times.filter(x => x <= t).length;
