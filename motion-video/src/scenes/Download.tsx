import { AbsoluteFill, useCurrentFrame } from 'remotion';
import { ArrowDownToLine, Check, Globe, LoaderCircle } from 'lucide-react';
import { easeIn, easeInOut, lerp, pop, prog, rise } from '../anim';
import { AppIcon, Badge, Burst, ClickRing, Flip, Pointer, Sfx } from '../components/bits';
import { C, FONT_BODY, FONT_DISPLAY } from '../theme';

// Global 1316–1620 (bars 22–26). The click lands on frame 1380 (bar 23) and the last chord on 1440 (bar 24).
const CLICK = 64;
const DONE = 86;
const BACK = 118;
const FINAL = 124;
const BUTTON = { x: 960, y: 642 };
const EDGE_BADGES: [string, number, number][] = [
  ['he', 150, 180], ['ru', 110, 520], ['ar', 200, 870], ['el', 560, 1000], ['ko', 1360, 1000],
  ['th', 1720, 870], ['de', 1810, 520], ['uk', 1770, 180], ['fr', 1380, 70], ['ka', 540, 70],
];

export function Download() {
  const f = useCurrentFrame();
  const logo = pop(f, 2, { damping: 11, stiffness: 160 });
  const btnIn = pop(f, 24, { damping: 12, stiffness: 200 });
  const press = Math.max(0, 1 - Math.abs(f - CLICK - 1) / 4);
  const cursorT = prog(f, 30, 28, easeInOut);
  const cx = lerp(1620, BUTTON.x + 40, cursorT), cy = lerp(1120, BUTTON.y + 20, cursorT);
  const cursorOut = prog(f, 100, 16, easeIn);
  const state = f < CLICK ? 0 : f < DONE ? 1 : f < BACK ? 2 : 3;
  const chipIn = pop(f, DONE + 2, { damping: 14, stiffness: 200 });
  const chipOut = prog(f, BACK - 4, 10, easeIn);
  const glow = 0.5 + 0.5 * Math.sin(f * 0.12);

  const label = [
    <><ArrowDownToLine size={46} strokeWidth={2.4} /> Download for Windows</>,
    <><LoaderCircle size={46} strokeWidth={2.4} style={{ transform: `rotate(${f * 14}deg)` }} /> Downloading…</>,
    <><Check size={46} strokeWidth={3} /> Downloaded</>,
    <><ArrowDownToLine size={46} strokeWidth={2.4} /> Download for Windows</>,
  ][state];

  return (
    <AbsoluteFill style={{ fontFamily: FONT_BODY, color: C.text }}>
      {EDGE_BADGES.map(([code, x, y], i) => {
        const s = pop(f, 6 + i * 1.5, { damping: 13, stiffness: 140 });
        return (
          <div key={code} style={{ position: 'absolute', left: x, top: y + Math.sin(f * 0.05 + i * 2) * 12, transform: `translate(-50%, -50%) scale(${s}) rotate(${(i % 2 ? 1 : -1) * 8 + Math.sin(f * 0.03 + i) * 4}deg)`, opacity: 0.9 * Math.min(1, s * 2) }}>
            <Badge code={code} h={i % 3 === 0 ? 62 : 52} />
          </div>
        );
      })}

      {/* logo, name, promise */}
      <div style={{ position: 'absolute', left: 960 - 85, top: 130, transform: `scale(${logo}) rotate(${(1 - logo) * -25}deg)` }}>
        <AppIcon size={170} twinkle={prog(f, 6, 16) + prog(f, FINAL, 16)} />
      </div>
      <div style={{ position: 'absolute', top: 330, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 112, letterSpacing: '-0.04em', ...rise(f, 9, { dist: 40 }) }}>
        Language Autocorrect
      </div>
      <div style={{ position: 'absolute', top: 476, width: '100%', textAlign: 'center', fontSize: 42, color: C.muted, ...rise(f, 16) }}>
        Type in all your languages without watching the keyboard.
      </div>

      {/* the button */}
      <div style={{ position: 'absolute', left: BUTTON.x - 360, top: BUTTON.y - 64, width: 720, height: 128, transform: `scale(${btnIn * (1 - press * 0.05)})` }}>
        <div style={{ position: 'absolute', inset: -30, borderRadius: 60, background: 'radial-gradient(closest-side, rgba(88,71,224,.35), transparent)', opacity: 0.5 + glow * 0.5 }} />
        <div
          style={{
            position: 'absolute', inset: 0, borderRadius: 34, background: state === 2 ? C.green : C.accent, color: '#fff',
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 22, fontSize: 48, fontWeight: 650,
            boxShadow: `0 24px 50px -16px ${state === 2 ? 'rgba(22,163,74,.6)' : 'rgba(88,71,224,.65)'}, inset 0 1px 0 rgba(255,255,255,.25)`,
            overflow: 'hidden',
          }}
        >
          <Flip p={state === 0 ? 1 : prog(f, [CLICK, DONE, BACK][state - 1], 8, easeInOut)} from={<span />} to={<span style={{ display: 'flex', alignItems: 'center', gap: 22 }}>{label}</span>} />
        </div>
      </div>
      <ClickRing x={BUTTON.x + 40} y={BUTTON.y + 20} t={f - CLICK} color="#fff" />
      <Burst x={BUTTON.x} y={BUTTON.y} t={f - DONE} count={16} radius={420} colors={[C.green, '#F5B941', C.accent, '#2563EB']} />

      {/* the file, like a browser's download bubble */}
      {f >= DONE && f < BACK + 8 && (
        <div style={{ position: 'absolute', top: 760, width: '100%', display: 'flex', justifyContent: 'center' }}>
          <div
            style={{
              display: 'flex', alignItems: 'center', gap: 22, padding: '18px 34px 18px 20px', borderRadius: 24, background: C.card,
              border: `1.5px solid ${C.line}`, boxShadow: '0 24px 50px -20px rgba(40,30,90,.3)',
              transform: `translateY(${(1 - chipIn) * 40 + chipOut * 30}px)`, opacity: Math.min(1, chipIn * 2) * (1 - chipOut),
            }}
          >
            <AppIcon size={64} />
            <div>
              <div style={{ fontSize: 30, fontWeight: 600 }}>LanguageAutocorrect.exe</div>
              <div style={{ fontSize: 24, color: C.muted }}>Open it to install.</div>
            </div>
          </div>
        </div>
      )}

      {/* where to get it */}
      {f >= FINAL && (
        <>
          <div style={{ position: 'absolute', top: 760, width: '100%', display: 'flex', justifyContent: 'center', ...rise(f, FINAL, { dist: 30 }) }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 18, padding: '18px 38px', borderRadius: 999, background: C.card, border: `1.5px solid ${C.line}`, fontSize: 46, fontWeight: 600, color: C.accent, boxShadow: '0 18px 40px -20px rgba(40,30,90,.3)' }}>
              <Globe size={44} strokeWidth={2} />
              language-autocorrect.world
            </div>
          </div>
          <div style={{ position: 'absolute', top: 880, width: '100%', textAlign: 'center', fontSize: 32, color: C.muted, ...rise(f, FINAL + 6, { dist: 20 }) }}>
            Free · Windows 10 &amp; 11
          </div>
        </>
      )}

      {f >= 28 && f < 118 && <Pointer x={cx} y={cy + cursorOut * 400} press={press} opacity={1 - cursorOut} />}

      {/* sound */}
      <Sfx name="shimmer" at={3} volume={0.45} />
      <Sfx name="swipe" at={9} volume={0.25} />
      {[0, 1, 2, 3].map((i) => <Sfx key={i} name={`pop-${i + 1}` as 'pop-1'} at={6 + i * 4} volume={0.2} />)}
      <Sfx name="pop-3" at={24} volume={0.4} />
      <Sfx name="click" at={CLICK} volume={0.8} />
      <Sfx name="swipe" at={CLICK + 1} volume={0.25} />
      <Sfx name="success" at={DONE} volume={0.7} />
      <Sfx name="swipe" at={BACK} volume={0.25} />
      <Sfx name="shimmer" at={FINAL} volume={0.5} />
      <Sfx name="pop-5" at={FINAL + 2} volume={0.3} />
    </AbsoluteFill>
  );
}
