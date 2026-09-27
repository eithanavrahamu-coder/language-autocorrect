import { useEffect, useRef, useState } from 'react';
import { useInView, useReducedMotion } from 'motion/react';

/**
 * Steps through an animation's scenes, one after another, and starts over: returns the scene to show. It waits
 * while the animation is off screen, and stays on the `still` scene for people who turned animations off.
 * @param durations how long each scene lasts, in ms
 */
export function useTimeline<T extends HTMLElement = HTMLDivElement>(durations: readonly number[], still: number) {
  const ref = useRef<T>(null);
  const inView = useInView(ref, { amount: .3 });
  const reduced = useReducedMotion();
  const [step, setStep] = useState(0);
  useEffect(() => {
    if (!inView || reduced) return;
    const t = setTimeout(() => setStep(s => (s + 1) % durations.length), durations[step]);
    return () => clearTimeout(t);
  }, [step, inView, reduced, durations]);
  return { ref, step: reduced ? still : step };
}
