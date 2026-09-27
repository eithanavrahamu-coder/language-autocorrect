import type { ReactNode } from 'react';
import { easeOut, ramp, useTime } from '../anim.ts';
import { alpha, C, FONT_BODY } from '../theme.ts';
import { BadgeTrack } from './Badge.tsx';

/**
 * A fixed word as the app shows it: it sharpens out of a blur over 0.35 s and glows for a moment
 * (the website's `fixed-glow`, which the app copies in src/LanguageAutocorrect/FixFlash.cs).
 */
export function FixedWord({ text, at, size, glow = true }: { text: string; at: number; size: number; glow?: boolean }) {
  const t = useTime();
  const s = size / 18;
  const p = ramp(t, at, 0.35);
  const g = glow && t >= at ? 1 - ramp(t, at + 0.54, 1.26, easeOut) : 0;
  return (
    <span
      style={{
        opacity: p,
        filter: p < 1 ? `blur(${(1 - p) * 6 * Math.min(s, 3)}px)` : undefined,
        background: g > 0 ? alpha(C.accent, 0.22 * g) : undefined,
        borderRadius: 5 * Math.min(s, 3),
        padding: '0 0.06em',
        margin: '0 -0.06em',
      }}
    >
      {text}
    </span>
  );
}

/** A wavy red line under a word that came out wrong. */
export function Wrong({ children, at }: { children: ReactNode; at: number }) {
  const t = useTime();
  const p = ramp(t, at, 0.3);
  return (
    <span
      style={{
        textDecorationLine: p > 0 ? 'underline' : 'none',
        textDecorationStyle: 'wavy',
        textDecorationColor: alpha(C.danger, 0.85 * p),
        textDecorationThickness: '0.045em',
        textUnderlineOffset: '0.2em',
        textDecorationSkipInk: 'none',
      }}
    >
      {children}
    </span>
  );
}

/**
 * The text cursor with the language badge just below it and to its right, as in the app. It stays solid while
 * typing and blinks when idle.
 */
function Caret({ size, lastKey, badge, show }: {
  size: number;
  lastKey: number;
  badge: { at: number; lang: string }[];
  show: number;
}) {
  const t = useTime();
  const idle = t - lastKey - 0.5;
  const on = idle < 0 || idle % 1.1 < 0.55;
  const width = Math.max(4, size * 0.045);
  return (
    <span
      style={{
        position: 'relative',
        display: 'inline-block',
        width,
        height: '1.15em',
        margin: `0 ${width * 0.6}px`,
        verticalAlign: '-0.2em',
        borderRadius: width / 2,
        background: on ? C.text : 'transparent',
        opacity: ramp(t, show, 0.2),
      }}
    >
      <span style={{ position: 'absolute', left: width * 0.9, top: `calc(100% + ${size * 0.07}px)`, lineHeight: 0 }}>
        <BadgeTrack steps={badge} height={size * 0.5} />
      </span>
    </span>
  );
}

/**
 * The text box: a white card with one line of text in the middle, the cursor and its badge at the end of the line,
 * and room above it for the fix card.
 */
export function Field({ width, height, size, line, lastKey, badge, caretIn, above }: {
  width: number;
  height: number;
  size: number;
  line: ReactNode;
  lastKey: number;
  badge: { at: number; lang: string }[];
  caretIn: number;
  above?: ReactNode;
}) {
  return (
    <div
      style={{
        position: 'relative',
        width,
        height,
        boxSizing: 'border-box',
        borderRadius: Math.min(40, height * 0.16),
        background: C.card,
        border: `2px solid ${C.line}`,
        boxShadow: `0 2px 2px rgba(20, 18, 15, 0.04), 0 ${height * 0.1}px ${height * 0.26}px ${-height * 0.06}px rgba(20, 18, 15, 0.14), 0 ${height * 0.3}px ${height * 0.6}px ${-height * 0.2}px rgba(40, 30, 90, 0.2)`,
        display: 'grid',
        placeItems: 'center',
      }}
    >
      <div
        dir="auto"
        style={{
          fontFamily: FONT_BODY,
          fontWeight: 500,
          fontSize: size,
          lineHeight: 1.2,
          color: C.text,
          whiteSpace: 'pre',
          unicodeBidi: 'plaintext',
          textAlign: 'center',
          // Room for the badge below the line.
          paddingBottom: size * 0.32,
        }}
      >
        {line}
        <Caret size={size} lastKey={lastKey} badge={badge} show={caretIn} />
      </div>
      {above && (
        <div style={{ position: 'absolute', left: 0, right: 0, bottom: `calc(100% + ${height * 0.1}px)`, display: 'flex', justifyContent: 'center' }}>
          {above}
        </div>
      )}
    </div>
  );
}
