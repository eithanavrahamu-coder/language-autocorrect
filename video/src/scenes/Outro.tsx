import { ArrowDownToLine } from 'lucide-react';
import { AbsoluteFill } from 'remotion';
import { BOUNCY, pop, ramp, useTime } from '../anim.ts';
import { BadgeTrack } from '../parts/Badge.tsx';
import { OUTRO } from '../timeline.ts';
import { alpha, C, FONT_BADGE, FONT_BODY, FONT_DISPLAY } from '../theme.ts';

export const ADDRESS = 'language-autocorrect.world';

/** 15.7–20 s: the app's icon and name, and where to get it. */
export function Outro() {
  const t = useTime();
  if (t < OUTRO.start) return null;

  const rise = (at: number, by = 30) => {
    const p = ramp(t, at, 0.6);
    return { opacity: p, transform: `translateY(${(1 - p) * by}px)`, filter: p < 1 ? `blur(${(1 - p) * 8}px)` : undefined };
  };
  const button = pop(t, OUTRO.button, { stiffness: 300, damping: 20 });

  return (
    <AbsoluteFill style={{ alignItems: 'center', fontFamily: FONT_BODY }}>
      <AppIcon />

      <div
        style={{
          position: 'absolute',
          top: 470,
          display: 'flex',
          alignItems: 'center',
          fontFamily: FONT_DISPLAY,
          fontWeight: 700,
          fontSize: 104,
          letterSpacing: '-0.035em',
          lineHeight: 1.1,
          color: C.text,
          ...rise(OUTRO.name),
        }}
      >
        Language Autocorrect
        <NameCaret />
      </div>

      <div style={{ position: 'absolute', top: 606, fontSize: 38, color: C.muted, ...rise(OUTRO.tagline, 24) }}>
        Type in all your languages without watching the keyboard.
      </div>

      <div
        style={{
          position: 'absolute',
          top: 712,
          display: 'flex',
          alignItems: 'center',
          gap: 18,
          height: 92,
          padding: '0 46px',
          borderRadius: 24,
          background: C.text,
          color: C.bg,
          fontWeight: 600,
          fontSize: 34,
          boxShadow: '0 2px 0 rgba(255, 255, 255, 0.12) inset, 0 18px 40px -14px rgba(20, 18, 15, 0.5)',
          opacity: Math.min(1, button * 1.3),
          transform: `scale(${0.8 + 0.2 * button})`,
        }}
      >
        <ArrowDownToLine size={38} strokeWidth={2.3} />
        Download free for Windows
      </div>

      <div style={{ position: 'absolute', top: 838, fontSize: 32, color: C.muted, letterSpacing: '0.005em', ...rise(OUTRO.address, 16) }}>
        {ADDRESS}
      </div>
    </AbsoluteFill>
  );
}

/** The app's icon, אA on the blue → violet → green gradient (src/LayoutBuddy/UI/app.html's .logo). */
function AppIcon() {
  const t = useTime();
  const p = pop(t, OUTRO.icon, BOUNCY);
  const size = 200;
  const halo = ramp(t, OUTRO.icon, 0.8);
  const shine = ramp(t, OUTRO.shine, 0.7);
  return (
    <div style={{ position: 'absolute', top: 222, width: size, height: size }}>
      <div
        style={{
          position: 'absolute',
          inset: -size * 0.9,
          background: `radial-gradient(closest-side, ${alpha(C.accent, 0.28)}, ${alpha(C.accent, 0)})`,
          opacity: halo,
          transform: `scale(${0.6 + 0.4 * halo})`,
        }}
      />
      <div
        style={{
          position: 'relative',
          width: size,
          height: size,
          borderRadius: size * 0.27,
          overflow: 'hidden',
          background: `linear-gradient(135deg, ${C.en}, ${C.accent} 55%, ${C.he})`,
          display: 'grid',
          placeItems: 'center',
          color: '#FFFFFF',
          fontFamily: FONT_BADGE,
          fontWeight: 700,
          fontSize: size * 0.41,
          letterSpacing: '-0.02em',
          boxShadow: `0 ${size * 0.12}px ${size * 0.3}px ${-size * 0.08}px ${alpha(C.accent, 0.55)}`,
          opacity: Math.min(1, p * 1.5),
          transform: `scale(${0.3 + 0.7 * p}) rotate(${(1 - p) * -14}deg)`,
        }}
      >
        אA
        {shine > 0 && shine < 1 && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              background: 'linear-gradient(105deg, transparent 35%, rgba(255, 255, 255, 0.45) 50%, transparent 65%)',
              transform: `translateX(${-120 + 240 * shine}%)`,
            }}
          />
        )}
      </div>
    </div>
  );
}

/** A text cursor after the name, its badge going through a few languages. */
function NameCaret() {
  const t = useTime();
  const last = OUTRO.badges.filter(b => b.at <= t).pop()?.at ?? OUTRO.name;
  const idle = t - last - 0.3;
  const on = idle < 0 || idle % 1.1 < 0.55;
  // Hangs off the end of the name, so the name itself stays centered.
  return (
    <span style={{ position: 'absolute', left: '100%', top: 0, bottom: 0, display: 'inline-flex', alignItems: 'center', gap: 16, marginLeft: 14 }}>
      <span style={{ width: 6, height: 96, borderRadius: 3, background: on ? C.text : 'transparent' }} />
      <span style={{ width: 90, display: 'inline-flex', alignItems: 'center' }}>
        <BadgeTrack steps={OUTRO.badges} height={50} />
      </span>
    </span>
  );
}
