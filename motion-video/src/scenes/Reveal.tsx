import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from 'remotion';
import { easeIn, easeInOut, lerp, pop, prog, rise } from '../anim';
import { AppIcon, Badge, Burst, Sfx } from '../components/bits';
import { C, FONT_DISPLAY, FONT_BODY } from '../theme';

// Global 230–360: the light breaks through and the logo lands on the drop (frame 240, bar 4).
const LOGO = 10;
const TITLE = 'Language Autocorrect';
const BADGES: [string, number, number][] = [
  ['en', 260, 190], ['he', 120, 470], ['ru', 290, 780], ['ar', 600, 950], ['el', 1320, 950], ['ko', 1630, 780],
  ['th', 1800, 470], ['de', 1660, 190], ['ka', 1290, 90], ['fr', 630, 90], ['uk', 600, 340], ['hy', 1320, 340],
];

/** The light breaking through: a circle growing from the middle, frames 1–22 of the scene. */
export const revealRadius = (f: number) =>
  interpolate(f, [1, 22], [0, 1250], { easing: Easing.bezier(0.7, 0, 0.3, 1), extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

export function Reveal() {
  const f = useCurrentFrame();
  const r = revealRadius(f);
  const logoS = pop(f, LOGO, { damping: 10, stiffness: 150 });
  const settle = prog(f, 26, 24, easeInOut);
  const logoY = lerp(520, 330, settle);
  const exit = prog(f, 106, 16, easeIn);

  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ clipPath: `circle(${r}px at 50% 50%)` }}>
        <AbsoluteFill style={{ transform: `translateY(${-exit * 50}px) scale(${1 - exit * 0.06})`, opacity: 1 - exit }}>
          {/* rings */}
          {[LOGO, LOGO + 7].map((at, i) => {
            const p = prog(f, at, 28, Easing.out(Easing.cubic));
            if (f < at || p >= 1) return null;
            const rr = 120 + p * 520;
            return (
              <div
                key={i}
                style={{
                  position: 'absolute', left: 960 - rr, top: logoY - rr, width: rr * 2, height: rr * 2, borderRadius: '50%',
                  border: `${5 - p * 4}px solid ${i ? '#2563EB' : C.accent}`, opacity: (1 - p) * 0.7,
                }}
              />
            );
          })}

          {/* languages flying out of the logo */}
          {BADGES.map(([code, x, y], i) => {
            const s = pop(f, LOGO + 2 + i * 1.2, { damping: 13, stiffness: 120 });
            const away = 1 + exit * 0.4;
            const bx = lerp(960, 960 + (x - 960) * away, s), by = lerp(logoY, 540 + (y - 540) * away, s) + Math.sin(f * 0.07 + i * 1.7) * 9;
            return (
              <div
                key={code}
                style={{
                  position: 'absolute', left: bx, top: by, transform: `translate(-50%, -50%) scale(${Math.min(1, s * 1.2)}) rotate(${(i % 2 ? 1 : -1) * (4 + (i % 3) * 3)}deg)`,
                  opacity: Math.min(1, s * 2),
                }}
              >
                <Badge code={code} h={i % 3 === 0 ? 70 : 58} />
              </div>
            );
          })}

          <Burst x={960} y={logoY} t={f - LOGO} count={14} radius={260} size={26} />

          {/* logo */}
          <div
            style={{
              position: 'absolute', left: 960 - 125, top: logoY - 125,
              transform: `scale(${logoS}) rotate(${(1 - logoS) * -30}deg)`,
            }}
          >
            <AppIcon size={250} twinkle={prog(f, LOGO + 6, 18)} />
          </div>

          {/* name */}
          <div
            style={{
              position: 'absolute', top: 520, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, fontWeight: 700,
              fontSize: 136, letterSpacing: '-0.035em', color: C.text, whiteSpace: 'pre',
            }}
          >
            {[...TITLE].map((ch, i) => {
              const p = prog(f, 28 + i * 0.8, 16);
              return (
                <span key={i} style={{ display: 'inline-block', opacity: p, transform: `translateY(${(1 - p) * 70}px) rotate(${(1 - p) * 8}deg)` }}>
                  {ch}
                </span>
              );
            })}
          </div>
          <div style={{ position: 'absolute', top: 712, width: '100%', textAlign: 'center', fontFamily: FONT_BODY, fontSize: 52, color: C.muted, ...rise(f, 50) }}>
            Fixes words typed on the <span style={{ color: C.accent, fontWeight: 600 }}>wrong keyboard</span>.
          </div>
        </AbsoluteFill>
      </AbsoluteFill>

      <Sfx name="impact" at={LOGO} volume={0.85} />
      <Sfx name="shimmer" at={LOGO + 4} volume={0.5} />
      {[0, 1, 2, 3, 4, 5].map((i) => <Sfx key={i} name={`pop-${i + 1}` as 'pop-1'} at={LOGO + 3 + i * 2} volume={0.22} />)}
      <Sfx name="swipe" at={28} volume={0.3} />
      <Sfx name="whoosh" at={104} volume={0.45} />
    </AbsoluteFill>
  );
}
