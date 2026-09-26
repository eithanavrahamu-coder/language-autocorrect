import { easeIn, ramp, useTime } from '../anim.ts';
import type { Caption as CaptionTiming } from '../timeline.ts';
import { C, FONT_DISPLAY } from '../theme.ts';

/**
 * A headline in the website's style: bold display type, with a second part in a faint color
 * ("You keep typing. It keeps up."). Words rise in one after another, and leave together.
 */
export function Caption({ caption, top = 118, size = 84 }: { caption: CaptionTiming; top?: number; size?: number }) {
  const t = useTime();
  if (t < caption.in - 0.05 || t > caption.out + 0.05) return null;

  const leave = ramp(t, caption.out - 0.24, 0.24, easeIn);
  const words = [
    ...caption.text.split(' ').map((w, i) => ({ w, faint: false, at: caption.in + i * 0.05 })),
    ...(caption.faint ?? '').split(' ').filter(Boolean).map((w, i) => ({
      w,
      faint: true,
      at: (caption.faintIn ?? caption.in + caption.text.split(' ').length * 0.05) + i * 0.05,
    })),
  ];

  return (
    <div
      style={{
        position: 'absolute',
        left: 0,
        right: 0,
        top,
        textAlign: 'center',
        fontFamily: FONT_DISPLAY,
        fontWeight: 700,
        fontSize: size,
        lineHeight: 1.05,
        letterSpacing: '-0.035em',
        color: C.text,
        opacity: 1 - leave,
        transform: `translateY(${-leave * 22}px)`,
        filter: leave > 0 ? `blur(${leave * 6}px)` : undefined,
      }}
    >
      {words.map(({ w, faint, at }, i) => {
        const p = ramp(t, at, 0.5);
        return (
          <span
            key={i}
            style={{
              display: 'inline-block',
              marginRight: i < words.length - 1 ? '0.24em' : 0,
              color: faint ? C.faint : C.text,
              opacity: p,
              transform: `translateY(${(1 - p) * 30}px)`,
              filter: p < 1 ? `blur(${(1 - p) * 8}px)` : undefined,
            }}
          >
            {w}
          </span>
        );
      })}
    </div>
  );
}
