import { Undo2 } from 'lucide-react';
import { easeOut, pop, ramp, useTime } from '../anim.ts';
import { C, FONT_BODY } from '../theme.ts';

/**
 * The small card that appears above the text after a fix: "ghbdtn → привет", or "Kept as typed: ghbdtn" after an
 * undo. It springs in, stays 2.6 seconds and fades away, like the app's (src/LanguageAutocorrect/FixCardForm.cs).
 * `font` is its text size (the app's is 15px); everything else scales with it.
 */
export function FixCard({ typed, fixed, at, undone = false, stay = 2.6, gone, font = 36 }: {
  typed: string;
  fixed: string;
  at: number;
  undone?: boolean;
  stay?: number;
  /** Leaves at once at this moment (when another card takes its place). */
  gone?: number;
  font?: number;
}) {
  const t = useTime();
  if (t < at || (gone !== undefined && t >= gone)) return null;
  const s = font / 15;
  const x = pop(t, at);
  const out = ramp(t, at + stay, 0.18, easeOut);
  if (out >= 1) return null;

  const opacity = Math.min(1, x) * (1 - out);
  const lift = 10 * (1 - x) + 6 * out;
  const scale = (0.95 + 0.05 * x) * (1 - 0.02 * out);
  return (
    <div
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 12 * s,
        padding: `${8 * s}px ${14 * s}px`,
        borderRadius: 12 * s,
        background: C.card,
        border: `${Math.max(1, s * 0.7)}px solid ${C.line}`,
        boxShadow: `0 ${8 * s}px ${24 * s}px ${-8 * s}px rgba(20, 18, 15, 0.22)`,
        fontFamily: FONT_BODY,
        fontSize: font,
        lineHeight: 1.6,
        whiteSpace: 'nowrap',
        opacity,
        transform: `translateY(${lift * s}px) scale(${scale})`,
      }}
    >
      {undone ? (
        <>
          <Undo2 size={font} strokeWidth={2} color={C.text} />
          <span style={{ color: C.text }}>Kept as typed:</span>
          <b dir="auto" style={{ fontWeight: 650, color: C.text }}>{typed}</b>
        </>
      ) : (
        <span style={{ display: 'inline-flex', alignItems: 'baseline', gap: '0.45em' }}>
          <s
            dir="auto"
            style={{
              color: C.muted,
              textDecorationColor: C.faint,
              textDecorationThickness: 1.5 * s,
              unicodeBidi: 'isolate',
            }}
          >
            {typed}
          </s>
          <span style={{ color: C.faint }}>→</span>
          <b dir="auto" style={{ fontWeight: 650, color: C.text, unicodeBidi: 'isolate' }}>{fixed}</b>
        </span>
      )}
    </div>
  );
}
