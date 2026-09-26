import { ShieldCheck } from 'lucide-react';
import { AbsoluteFill } from 'remotion';
import { easeIn, pop, ramp, useTime } from '../anim.ts';
import { Caption } from '../parts/Caption.tsx';
import { FixedWord } from '../parts/Field.tsx';
import { APPS } from '../timeline.ts';
import { C, FONT_BODY } from '../theme.ts';

// The app window's "Recent fixes" list (src/LayoutBuddy/UI/app.html), 2.3 times its size.
const S = 2.3;

/** 12.7–15.7 s: the app's list of recent fixes, from WhatsApp, Chrome, Outlook and Word. */
export function Apps() {
  const t = useTime();
  if (t < APPS.start || t > APPS.end) return null;

  const inP = pop(t, APPS.cardIn);
  const out = ramp(t, APPS.out, 0.3, easeIn);
  const zoom = 1 + 0.025 * ramp(t, APPS.cardIn, APPS.out - APPS.cardIn, x => x);
  const note = ramp(t, APPS.privacy, 0.5);

  return (
    <AbsoluteFill>
      <Caption caption={APPS.every} />
      <AbsoluteFill
        style={{
          alignItems: 'center',
          top: 330,
          opacity: Math.min(1, inP) * (1 - out),
          transform: `translateY(${(1 - inP) * 60 - out * 50}px) scale(${(0.94 + 0.06 * inP) * zoom * (1 - 0.08 * out)})`,
          filter: out > 0 ? `blur(${out * 8}px)` : undefined,
        }}
      >
        <div style={{ width: 1180, fontFamily: FONT_BODY }}>
          <div style={{ fontSize: 13 * S, fontWeight: 600, color: C.muted, margin: `0 0 ${10 * S}px ${2 * S}px` }}>
            Recent fixes
          </div>
          <div
            style={{
              background: C.card,
              border: `2px solid ${C.line}`,
              borderRadius: 14 * S,
              boxShadow: '0 2px 4px rgba(20, 18, 15, 0.04), 0 30px 70px -24px rgba(40, 30, 90, 0.22)',
              overflow: 'hidden',
            }}
          >
            {APPS.rows.map((r, i) => <Row key={r.app} row={r} first={i === 0} />)}
          </div>
          <div
            style={{
              marginTop: 16 * S,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5 * S,
              fontSize: 13.5 * S,
              color: C.muted,
              opacity: note,
              transform: `translateY(${(1 - note) * 14}px)`,
            }}
          >
            <ShieldCheck size={16 * S} strokeWidth={1.8} color={C.he} />
            Private: words are checked on your PC, and nothing is sent anywhere.
          </div>
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
}

function Row({ row, first }: { row: (typeof APPS.rows)[number]; first: boolean }) {
  const t = useTime();
  const p = pop(t, row.at);
  const tag = pop(t, row.at + 0.08, { stiffness: 380, damping: 14 });
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 14 * S,
        padding: `${13 * S}px ${18 * S}px`,
        borderTop: first ? 'none' : `2px solid ${C.line}`,
        fontSize: 14 * S,
        opacity: Math.min(1, p * 1.3),
        transform: `translateY(${(1 - p) * 40}px)`,
      }}
    >
      <span style={{ color: C.faint, fontSize: 12.5 * S, width: 44 * S, flexShrink: 0, fontVariantNumeric: 'tabular-nums' }}>
        {row.time}
      </span>
      <span style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 10 * S, whiteSpace: 'nowrap' }}>
        <s dir="auto" style={{ color: C.muted, textDecorationColor: C.faint, textDecorationThickness: 1.5 * S, unicodeBidi: 'isolate' }}>
          {row.typed}
        </s>
        <span style={{ color: C.faint }}>→</span>
        <b dir="auto" style={{ fontWeight: 600, unicodeBidi: 'isolate' }}>
          <FixedWord text={row.fixed} at={row.at + 0.12} size={14 * S} />
        </b>
      </span>
      <span
        style={{
          fontSize: 12 * S,
          color: C.muted,
          background: C.hover,
          padding: `${2 * S}px ${8 * S}px`,
          borderRadius: 6 * S,
          flexShrink: 0,
          display: 'inline-block',
          opacity: Math.min(1, tag),
          transform: `scale(${0.6 + 0.4 * tag})`,
        }}
      >
        {row.app}
      </span>
    </div>
  );
}
