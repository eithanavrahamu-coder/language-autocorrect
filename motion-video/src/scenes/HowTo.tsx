import type { CSSProperties, ReactNode } from 'react';
import { AbsoluteFill, useCurrentFrame } from 'remotion';
import { Check, ChevronUp, Delete, Keyboard, ListChecks, Undo2, Volume2, Wifi } from 'lucide-react';
import { caretOn, easeIn, easeInOut, keyTimes, lerp, pop, prog, typedCount } from '../anim';
import { AppIcon, Badge, Caret, ClickRing, Flip, Keycap, Pointer, Sfx, TypingSounds } from '../components/bits';
import { C, FONT_BODY, FONT_DISPLAY, LOGO_GRADIENT, WARM_GRADIENT, lang } from '../theme';

// Global 595–960 (bars 10–15). Frames below are the scene's own. Three steps, two bars each.
const S = [5, 125, 245, 400];
const STEPS = [
  { title: 'Download and run it', text: 'Click Get started, then Install. No administrator rights needed.', pill: 'Install' },
  { title: 'Pick your languages', text: 'The keyboards you already have in Windows are switched on for you.', pill: 'Languages' },
  { title: 'Just type', text: 'It runs in the tray. Press Space and a wrong-keyboard word is fixed. Backspace right after undoes it.', pill: 'Type' },
];

/** Where a pointer is: glides between waypoints [frame, x, y]. */
function along(f: number, path: [number, number, number][]) {
  if (f <= path[0][0]) return { x: path[0][1], y: path[0][2] };
  for (let i = 1; i < path.length; i++) {
    const [t1, x1, y1] = path[i], [t0, x0, y0] = path[i - 1];
    if (f <= t1) { const p = prog(f, t0, t1 - t0, easeInOut); return { x: lerp(x0, x1, p), y: lerp(y0, y1, p) }; }
  }
  const last = path[path.length - 1];
  return { x: last[1], y: last[2] };
}
const pressAt = (f: number, at: number) => Math.max(0, 1 - Math.abs(f - at - 1) / 3);

function slide(f: number, start: number, end: number): CSSProperties {
  const p = prog(f, start, 16), q = prog(f, end - 10, 10, easeIn);
  return { opacity: p * (1 - q), transform: `translateX(${(1 - p) * 90 - q * 90}px)` };
}

// ---------------------------------------------------------------- step 1: setup

const GET_STARTED = { x: 1636, y: 819 };

function SetupMock({ f }: { f: number }) {
  const screen = f < 98 ? 1 : 2; // the welcome screen is drawn on its own below, so it can cross-fade
  // each screen fills the panel below the progress bar, sliding in from the right
  const swap = (at: number): CSSProperties => ({
    position: 'absolute', left: 0, right: 0, top: 45, bottom: 0, padding: '0 44px',
    opacity: prog(f, at, 8), transform: `translateX(${(1 - prog(f, at, 10)) * 40}px)`,
  });
  const installing = prog(f, 77, 20, easeInOut);
  const btn = (label: ReactNode, at: number) => (
    <div
      style={{
        position: 'absolute', right: 44, bottom: 40, width: 200, height: 62, borderRadius: 14, background: C.text, color: '#fff',
        display: 'grid', placeItems: 'center', fontSize: 23, fontWeight: 600, transform: `scale(${1 - pressAt(f, at) * 0.06})`,
      }}
    >
      {label}
    </div>
  );
  return (
    <div style={{ position: 'absolute', left: 860, top: 290, width: 920, height: 600, borderRadius: 26, overflow: 'hidden', display: 'flex', background: C.bg, border: `1px solid ${C.line}`, boxShadow: '0 30px 80px -24px rgba(40,30,90,.32)' }}>
      <div style={{ width: 340, background: WARM_GRADIENT, position: 'relative', color: '#fff' }}>
        <div style={{ position: 'absolute', left: 26, top: 24, display: 'flex', alignItems: 'center', gap: 14, fontSize: 21, fontWeight: 600 }}>
          <AppIcon size={50} /> Language Autocorrect
        </div>
        <div style={{ position: 'absolute', left: 34, top: 210, width: 272, height: 150, borderRadius: 22, background: '#fff', boxShadow: '0 20px 40px -16px rgba(120,40,40,.35)', display: 'flex', alignItems: 'center', padding: '0 28px', color: C.text }}>
          <span style={{ fontSize: 46 }}>שלום</span>
          <span style={{ position: 'relative' }}>
            <Caret h={52} on style={{ marginLeft: 3 }} />
            <Badge code="he" h={28} style={{ position: 'absolute', left: 10, top: -16 }} />
          </span>
        </div>
        <div style={{ position: 'absolute', left: 30, right: 30, bottom: 70, textAlign: 'center', fontSize: 19, lineHeight: 1.45, fontWeight: 500 }}>
          Type on the wrong keyboard, and it’s fixed when you press Space.
        </div>
      </div>
      <div style={{ flex: 1, position: 'relative', padding: '40px 44px' }}>
        <div style={{ display: 'flex', gap: 8 }}>
          {[0, 1, 2, 3].map((i) => (
            <div key={i} style={{ flex: 1, height: 5, borderRadius: 3, background: i <= (f < 44 ? 0 : [0, 2, 3][screen]) ? C.text : C.line }} />
          ))}
        </div>
        {f < 46 && (
          <div style={{ position: 'absolute', left: 0, right: 0, top: 45, bottom: 0, padding: '0 44px', opacity: 1 - prog(f, 41, 4), transform: `translateX(${-prog(f, 41, 6) * 40}px)` }}>
            <div style={{ marginTop: 48, fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 38, lineHeight: 1.1, letterSpacing: '-0.03em' }}>
              Type in every language without looking at the keyboard
            </div>
            <div style={{ marginTop: 18, fontSize: 20, color: C.muted, lineHeight: 1.45 }}>
              When a word comes out on the wrong keyboard, it’s fixed the moment you press Space.
            </div>
            {[
              [<ListChecks key="a" size={22} color="#E5484D" />, 'Fixes wrong-keyboard words', '#FDECEC'],
              [<Badge key="b" code="en" h={24} />, 'Shows your language at the cursor', '#E6F4F1'],
              [<Undo2 key="c" size={22} color="#2563EB" />, 'Easy to undo', '#E8F0FD'],
            ].map(([icon, label, bg], i) => (
              <div key={i} style={{ marginTop: i ? 14 : 30, display: 'flex', alignItems: 'center', gap: 16, fontSize: 20, fontWeight: 500 }}>
                <span style={{ width: 46, height: 46, borderRadius: 12, background: bg as string, display: 'grid', placeItems: 'center' }}>{icon}</span>
                {label}
              </div>
            ))}
            <div style={{ position: 'absolute', left: 44, bottom: 58, fontSize: 19, color: C.muted, textDecoration: 'underline', textUnderlineOffset: 6 }}>Run without installing</div>
            {btn('Get started', 40)}
          </div>
        )}
        {f >= 44 && screen === 1 && (
          <div style={swap(44)}>
            <div style={{ marginTop: 48, fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 40, letterSpacing: '-0.03em' }}>Ready to install</div>
            {['No administrator rights needed', 'Starts with Windows', 'Your settings stay on this PC'].map((t, i) => (
              <div key={t} style={{ marginTop: i ? 16 : 34, display: 'flex', alignItems: 'center', gap: 14, fontSize: 22 }}>
                <span style={{ width: 34, height: 34, borderRadius: 17, background: '#E7F6EC', display: 'grid', placeItems: 'center' }}><Check size={20} color={C.green} strokeWidth={3} /></span>
                {t}
              </div>
            ))}
            {f >= 77 && (
              <div style={{ position: 'absolute', left: 44, right: 44, bottom: 122, height: 10, borderRadius: 5, background: C.line, overflow: 'hidden' }}>
                <div style={{ width: `${installing * 100}%`, height: '100%', background: LOGO_GRADIENT }} />
              </div>
            )}
            {btn(f >= 77 ? 'Installing…' : 'Install', 75)}
          </div>
        )}
        {screen === 2 && (
          <div style={{ ...swap(98), top: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 22 }}>
            <div style={{ width: 120, height: 120, borderRadius: 60, background: C.green, display: 'grid', placeItems: 'center', transform: `scale(${pop(f, 98, { damping: 10, stiffness: 200 })})`, boxShadow: '0 20px 40px -14px rgba(22,163,74,.6)' }}>
              <Check size={70} color="#fff" strokeWidth={3} />
            </div>
            <div style={{ fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 44, letterSpacing: '-0.03em' }}>All set!</div>
            <div style={{ fontSize: 22, color: C.muted }}>It’s running in the tray.</div>
          </div>
        )}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- step 2: languages

const ROWS = ['en', 'he', 'ru', 'ar', 'el', 'de'];
const ON_AT: Record<string, number> = { en: 150, he: 156, ru: 187 };
const RU_TOGGLE = { x: 1668, y: 605 };

function Toggle({ on }: { on: number }) {
  return (
    <div style={{ width: 76, height: 42, borderRadius: 21, position: 'relative', background: on > 0.5 ? C.text : '#fff', border: `2px solid ${on > 0.5 ? C.text : '#C9C4BC'}` }}>
      <div style={{ position: 'absolute', top: 5, left: lerp(6, 40, on), width: 28, height: 28, borderRadius: 14, background: on > 0.5 ? '#fff' : '#77726B' }} />
    </div>
  );
}

function LanguagesMock({ f }: { f: number }) {
  return (
    <div style={{ position: 'absolute', left: 880, top: 300, width: 860, height: 620, borderRadius: 30, background: C.card, border: `1px solid ${C.line}`, boxShadow: '0 30px 80px -24px rgba(40,30,90,.3)', overflow: 'hidden' }}>
      <div style={{ height: 90, display: 'flex', alignItems: 'center', padding: '0 40px', fontSize: 26, color: C.muted, borderBottom: `1px solid ${C.line}` }}>
        Your languages
      </div>
      {ROWS.map((code, i) => {
        const l = lang(code);
        const at = ON_AT[code];
        const on = at ? prog(f, at, 6, easeInOut) : 0;
        const inP = prog(f, 128 + i * 3, 12);
        const flash = at ? Math.sin(Math.PI * prog(f, at, 16)) : 0;
        return (
          <div
            key={code}
            style={{
              height: 86, display: 'flex', alignItems: 'center', gap: 22, padding: '0 40px', borderBottom: `1px solid ${C.line}`,
              opacity: inP, transform: `translateY(${(1 - inP) * 20}px)`, background: `rgba(88,71,224,${0.07 * flash})`,
            }}
          >
            <Badge code={code} h={46} />
            <span style={{ fontSize: 30, fontWeight: 600 }}>{l.native}</span>
            {code !== 'en' && <span style={{ fontSize: 25, color: C.muted }}>{l.name}</span>}
            <span style={{ marginLeft: 'auto' }}><Toggle on={on} /></span>
          </div>
        );
      })}
    </div>
  );
}

// ---------------------------------------------------------------- step 3: typing anywhere

const TYPE3 = keyTimes(262, 4, 4);
const SPACE3 = 280, UNDO = 314;

function DesktopMock({ f }: { f: number }) {
  const fixP = prog(f, SPACE3 + 1, 9, easeInOut) * (1 - prog(f, UNDO + 1, 9, easeInOut));
  const badgeP = prog(f, SPACE3 + 4, 8) * (1 - prog(f, UNDO + 3, 8));
  const lastKey = Math.max(...[...TYPE3, SPACE3, UNDO].filter((t) => t <= f), -100);
  const tipIn = prog(f, 296, 12);
  const trayPulse = Math.sin(Math.PI * prog(f, SPACE3 + 4, 14)) + Math.sin(Math.PI * prog(f, UNDO + 3, 14));
  const word = f < SPACE3 + 1 ? 'akuo'.slice(0, typedCount(TYPE3, f)) : null;
  return (
    <div style={{ position: 'absolute', left: 870, top: 290, width: 900, height: 620, borderRadius: 26, overflow: 'hidden', border: `1px solid ${C.line}`, boxShadow: '0 30px 80px -24px rgba(40,30,90,.32)', background: 'linear-gradient(135deg, #D9E6FF 0%, #ECE5FF 50%, #DDF3E6 100%)' }}>
      <div style={{ position: 'absolute', left: 90, top: 70, width: 720, height: 250, borderRadius: 20, background: '#fff', boxShadow: '0 20px 50px -20px rgba(40,30,90,.35)', overflow: 'hidden' }}>
        <div style={{ height: 50, display: 'flex', alignItems: 'center', padding: '0 20px', fontSize: 19, color: C.muted, borderBottom: `1px solid ${C.line}` }}>Notes</div>
        <div style={{ height: 200, display: 'flex', alignItems: 'center', padding: '0 46px', fontSize: 78, fontWeight: 500 }}>
          {word !== null ? <span>{word}</span> : (
            <Flip p={fixP} from={<span>akuo</span>} to={<span style={{ color: f < SPACE3 + 30 ? C.accent : C.text }}>שלום</span>} />
          )}
          {f >= SPACE3 && !(f >= UNDO) && <span>&nbsp;</span>}
          <span style={{ position: 'relative' }}>
            <Caret h={86} on={caretOn(f, lastKey)} style={{ marginLeft: 4 }} />
            <span style={{ position: 'absolute', left: 16, top: -30 }}>
              <Flip p={badgeP} from={<Badge code="en" h={40} />} to={<Badge code="he" h={40} />} />
            </span>
          </span>
          {f >= UNDO + 4 && (
            <span style={{ position: 'absolute', right: 40, top: 128, display: 'inline-flex', alignItems: 'center', gap: 8, fontSize: 24, fontWeight: 600, color: C.muted, padding: '8px 16px', borderRadius: 12, background: C.bg2, transform: `scale(${pop(f, UNDO + 4)})` }}>
              <Undo2 size={22} /> Undone
            </span>
          )}
        </div>
      </div>
      {/* tip */}
      <div style={{ position: 'absolute', left: 0, right: 0, top: 390, display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 22, opacity: tipIn, transform: `translateY(${(1 - tipIn) * 20}px)` }}>
        <Keycap label={<><Delete size={30} strokeWidth={1.8} /> Backspace</>} w={250} h={84} fontSize={27} press={f >= UNDO && f < UNDO + 6 ? 1 : 0} glow={prog(f, UNDO, 3) * (1 - prog(f, UNDO + 4, 10))} />
        <span style={{ fontSize: 28, color: C.text, fontWeight: 500 }}>right after a fix undoes it</span>
      </div>
      {/* taskbar */}
      <div style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: 72, background: 'rgba(255,255,255,.82)', borderTop: '1px solid rgba(0,0,0,.06)', display: 'flex', alignItems: 'center', padding: '0 22px' }}>
        <div style={{ flex: 1, display: 'flex', justifyContent: 'center', gap: 16 }}>
          {['#5B8DEF', '#F5B941', '#34C06A', '#E5484D', '#8B7CFF'].map((c, i) => (
            <div key={i} style={{ width: 44, height: 44, borderRadius: 11, background: c, opacity: 0.85 }} />
          ))}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 18, color: '#444', fontSize: 20 }}>
          <ChevronUp size={22} />
          <span style={{ transform: `scale(${1 + trayPulse * 0.25})` }}>
            <Flip p={badgeP} from={<Badge code="en" h={30} />} to={<Badge code="he" h={30} />} />
          </span>
          <Wifi size={22} />
          <Volume2 size={22} />
          <span>14:32</span>
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- the scene

export function HowTo() {
  const f = useCurrentFrame();
  const step = f < S[1] ? 0 : f < S[2] ? 1 : 2;
  const enter = prog(f, 0, 16);

  const p1 = along(f, [[8, 1840, 1080], [34, GET_STARTED.x, GET_STARTED.y], [48, GET_STARTED.x, GET_STARTED.y], [58, 1520, 720], [70, GET_STARTED.x, GET_STARTED.y], [100, GET_STARTED.x, GET_STARTED.y], [118, 1880, 1100]]);
  const p2 = along(f, [[160, 1880, 1100], [182, RU_TOGGLE.x, RU_TOGGLE.y], [205, RU_TOGGLE.x, RU_TOGGLE.y], [228, 1900, 1120]]);

  return (
    <AbsoluteFill style={{ fontFamily: FONT_BODY, color: C.text }}>
      <div style={{ position: 'absolute', left: 140, top: 92, fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 64, letterSpacing: '-0.035em', opacity: enter, transform: `translateY(${(1 - enter) * 30}px)` }}>
        How to use it
      </div>
      {/* step pills */}
      <div style={{ position: 'absolute', right: 140, top: 100, display: 'flex', gap: 14 }}>
        {STEPS.map((s, i) => {
          const active = i === step, done = i < step;
          const inP = prog(f, 4 + i * 3, 14);
          const bump = Math.sin(Math.PI * prog(f, S[i], 12));
          return (
            <div
              key={s.pill}
              style={{
                display: 'flex', alignItems: 'center', gap: 12, padding: '12px 22px 12px 12px', borderRadius: 999, fontSize: 26, fontWeight: 600,
                background: active ? C.accent : C.card, color: active ? '#fff' : done ? C.text : C.muted,
                border: `1.5px solid ${active ? C.accent : C.line}`, opacity: inP,
                transform: `translateY(${(1 - inP) * 20}px) scale(${1 + bump * 0.08})`,
                boxShadow: active ? '0 12px 30px -10px rgba(88,71,224,.6)' : undefined,
              }}
            >
              <span style={{ width: 38, height: 38, borderRadius: 19, display: 'grid', placeItems: 'center', fontSize: 21, background: active ? 'rgba(255,255,255,.22)' : done ? '#E7F6EC' : C.bg2, color: done ? C.green : undefined }}>
                {done ? <Check size={22} strokeWidth={3} /> : i + 1}
              </span>
              {s.pill}
            </div>
          );
        })}
      </div>

      {/* the step's words */}
      {STEPS.map((s, i) => {
        if (f < S[i] - 2 || f > S[i + 1]) return null;
        const p = prog(f, S[i], 16), q = i < 2 ? prog(f, S[i + 1] - 9, 8, easeIn) : 0;
        return (
          <div key={i} style={{ position: 'absolute', left: 140, top: 300, width: 640, opacity: p * (1 - q), transform: `translateY(${(1 - p) * 40 - q * 30}px)` }}>
            <div style={{ fontFamily: FONT_DISPLAY, fontWeight: 800, fontSize: 210, lineHeight: 0.9, letterSpacing: '-0.05em', background: LOGO_GRADIENT, WebkitBackgroundClip: 'text', backgroundClip: 'text', color: 'transparent', paddingBottom: 10 }}>
              {i + 1}
            </div>
            <div style={{ marginTop: 26, fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 70, lineHeight: 1.02, letterSpacing: '-0.035em' }}>{s.title}</div>
            <div style={{ marginTop: 26, fontSize: 34, lineHeight: 1.4, color: C.muted }}>{s.text}</div>
          </div>
        );
      })}

      {/* the step's picture */}
      {f < S[1] + 2 && <div style={slide(f, S[0], S[1])}><SetupMock f={f} /></div>}
      {f >= S[1] - 2 && f < S[2] + 2 && <div style={slide(f, S[1], S[2])}><LanguagesMock f={f} /></div>}
      {f >= S[2] - 2 && <div style={slide(f, S[2], S[3])}><DesktopMock f={f} /></div>}

      {f < 120 && <Pointer x={p1.x} y={p1.y} press={pressAt(f, 40) + pressAt(f, 75)} />}
      <ClickRing x={GET_STARTED.x} y={GET_STARTED.y} t={f - 40} />
      <ClickRing x={GET_STARTED.x} y={GET_STARTED.y} t={f - 75} />
      {f >= 158 && f < 232 && <Pointer x={p2.x} y={p2.y} press={pressAt(f, 186)} />}
      <ClickRing x={RU_TOGGLE.x} y={RU_TOGGLE.y} t={f - 186} />
      {f >= S[1] - 2 && f < S[2] && (
        <div style={{ position: 'absolute', left: 880, top: 942, display: 'flex', alignItems: 'center', gap: 12, fontSize: 26, color: C.muted, opacity: prog(f, 160, 12) * (1 - prog(f, S[2] - 10, 8)) }}>
          <Keyboard size={30} strokeWidth={1.7} /> Add any language later in Settings.
        </div>
      )}

      {/* sound */}
      <Sfx name="click" at={40} volume={0.7} />
      <Sfx name="swipe" at={43} volume={0.3} />
      <Sfx name="click" at={75} volume={0.7} />
      <Sfx name="pop-4" at={98} volume={0.5} />
      <Sfx name="tick" at={99} volume={0.35} />
      <Sfx name="swipe" at={S[1]} volume={0.35} />
      {ROWS.map((_, i) => <Sfx key={i} name="tick" at={128 + i * 3} volume={0.16} />)}
      <Sfx name="toggle" at={ON_AT.en} volume={0.5} />
      <Sfx name="toggle" at={ON_AT.he} volume={0.5} />
      <Sfx name="click" at={186} volume={0.6} />
      <Sfx name="toggle" at={ON_AT.ru} volume={0.55} />
      <Sfx name="swipe" at={S[2]} volume={0.35} />
      <TypingSounds times={TYPE3} text="akuo" volume={0.65} />
      <Sfx name="space" at={SPACE3} volume={0.75} />
      <Sfx name="fix" at={SPACE3 + 1} volume={0.7} />
      <Sfx name="switch" at={SPACE3 + 4} volume={0.45} />
      <Sfx name="backspace" at={UNDO} volume={0.75} />
      <Sfx name="swipe" at={UNDO + 1} volume={0.35} />
      <Sfx name="switch" at={UNDO + 3} volume={0.3} />
    </AbsoluteFill>
  );
}
