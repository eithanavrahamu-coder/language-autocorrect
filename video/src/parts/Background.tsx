import { AbsoluteFill } from 'remotion';
import { useTime } from '../anim.ts';
import { alpha, C } from '../theme.ts';

/**
 * The page's warm off-white with three soft lights in the icon's colors (blue, violet, green), drifting slowly,
 * like the glow behind the website's demo and the app's setup window.
 */
export function Background() {
  const t = useTime();
  const light = (color: string, x: number, y: number, w: number, h: number, a: number, phase: number) => {
    const dx = 70 * Math.sin(t * 0.33 + phase);
    const dy = 45 * Math.cos(t * 0.27 + phase * 1.3);
    return `radial-gradient(${w}px ${h}px at ${x + dx}px ${y + dy}px, ${alpha(color, a)}, ${alpha(color, 0)})`;
  };
  return (
    <AbsoluteFill
      style={{
        background: [
          light(C.en, 470, 420, 720, 560, 0.15, 0),
          light(C.accent, 1010, 250, 820, 620, 0.16, 2.1),
          light(C.he, 1480, 780, 700, 560, 0.13, 4.2),
          C.bg,
        ].join(', '),
      }}
    />
  );
}
