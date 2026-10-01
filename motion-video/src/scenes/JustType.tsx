import { AbsoluteFill, interpolate, useCurrentFrame } from 'remotion';
import { ArrowUp, Check } from 'lucide-react';
import { caretOn, easeIn, easeInOut, keyTimes, pop, prog, rise, typedCount } from '../anim';
import { Badge, Burst, Caret, Flip, Keycap, Sfx, TypingSounds, Window } from '../components/bits';
import { C, FONT_BODY, FONT_DISPLAY, LANGS, LOGO_GRADIENT } from '../theme';

// Global 950–1320 (bars 16–21, the second drop lands on frame 960 = this scene's frame 10).
const DROP = 10;
const SMASH = 40;

// Three messages, never touching the keyboard shortcut. Each was checked with the app's engine
// (website/src/demo/scenes.ts): akuo on English → שלום, "hello" typed on Hebrew → hello, ghbdtn on English → привет.
const MESSAGES = [
  { from: 'en', wrong: 'akuo', word: 'שלום', to: 'he', more: 'מה שלומך', keys: keyTimes(75, 4, 4), space: 91, moreKeys: keyTimes(98, 8, 4), send: 132 },
  { from: 'he', wrong: 'יקךךם', word: 'hello', to: 'en', more: 'there', keys: keyTimes(145, 5, 4), space: 165, moreKeys: keyTimes(171, 5, 4), send: 194 },
  { from: 'en', wrong: 'ghbdtn', word: 'привет', to: 'ru', more: 'как дела', keys: keyTimes(207, 6, 4), space: 232, moreKeys: keyTimes(239, 8, 4), send: 276 },
];
const STATS = 282;
const WALL = 298;
const WALL_OUT = 352;

function Composer({ f }: { f: number }) {
  const k = MESSAGES.findIndex((m) => f < m.send);
  const m = MESSAGES[k === -1 ? MESSAGES.length - 1 : k];
  const done = k === -1;
  const lastKey = Math.max(...MESSAGES.flatMap((x) => [...x.keys, x.space, ...x.moreKeys, x.send]).filter((t) => t <= f), -100);
  const badgeP = done ? 1 : prog(f, m.space + 3, 8, easeInOut);
  let content: React.ReactNode = null;
  if (!done) {
    if (f < m.space + 1) content = m.wrong.slice(0, typedCount(m.keys, f));
    else if (f < m.space + 10) {
      content = <Flip p={prog(f, m.space + 1, 9, easeInOut)} from={m.wrong} to={<span style={{ color: C.accent }}>{m.word}</span>} />;
    } else {
      // plain inline text from here on, so Hebrew words keep their right-to-left order
      content = (
        <span dir="auto" style={{ unicodeBidi: 'isolate' }}>
          <span style={{ color: f < m.space + 24 ? C.accent : C.text }}>{m.word}</span>
          {' ' + m.more.slice(0, typedCount(m.moreKeys, f))}
        </span>
      );
    }
  }
  const sendPress = MESSAGES.some((x) => f >= x.send && f < x.send + 5);
  return (
    <div style={{ position: 'absolute', left: 28, right: 28, bottom: 28, height: 116, borderRadius: 28, background: C.bg2, border: `1.5px solid ${C.line}`, display: 'flex', alignItems: 'center', padding: '0 20px 0 40px' }}>
      <span style={{ fontSize: 54, fontWeight: 500, whiteSpace: 'pre' }}>{content}</span>
      <span style={{ position: 'relative' }}>
        <Caret h={64} on={caretOn(f, lastKey)} style={{ marginLeft: 4 }} />
        <span style={{ position: 'absolute', left: 16, top: -20, transform: `scale(${1 + 0.25 * Math.sin(Math.PI * prog(f, m.space + 4, 12))})`, transformOrigin: '0 100%' }}>
          <Flip p={badgeP} from={<Badge code={m.from} h={38} />} to={<Badge code={m.to} h={38} />} />
        </span>
      </span>
      <div style={{ marginLeft: 'auto', width: 76, height: 76, borderRadius: 38, background: C.accent, display: 'grid', placeItems: 'center', transform: `scale(${sendPress ? 0.88 : 1})` }}>
        <ArrowUp size={40} color="#fff" strokeWidth={2.6} />
      </div>
    </div>
  );
}

function Bubbles({ f }: { f: number }) {
  // newest at the bottom; each new one pushes the others up
  const items = [
    { text: 'Say hi in all your languages!', at: 62, mine: false },
    ...MESSAGES.map((m) => ({ text: `${m.word} ${m.more}`, at: m.send, mine: true })),
  ];
  const H = 104;
  return (
    <>
      {items.map((it, i) => {
        if (f < it.at) return null;
        const above = items.slice(i + 1).reduce((sum, x) => sum + (f < x.at ? 0 : H * Math.min(1, pop(f, x.at, { damping: 14, stiffness: 200 }))), 0);
        const s = pop(f, it.at, { damping: 14, stiffness: 200 });
        return (
          <div
            key={i}
            style={{
              position: 'absolute', bottom: 172 + above, [it.mine ? 'right' : 'left']: 40, display: 'flex',
              transform: `translateY(${(1 - s) * 40}px) scale(${0.8 + 0.2 * s})`, transformOrigin: it.mine ? '100% 100%' : '0 100%', opacity: Math.min(1, s * 2),
            }}
          >
            <div
              dir="auto"
              style={{
                padding: '16px 30px', borderRadius: 30, fontSize: 42, lineHeight: 1.2,
                borderBottomRightRadius: it.mine ? 8 : 30, borderBottomLeftRadius: it.mine ? 30 : 8,
                background: it.mine ? C.accent : C.card, color: it.mine ? '#fff' : C.text,
                border: it.mine ? 'none' : `1.5px solid ${C.line}`,
                boxShadow: it.mine ? '0 12px 28px -12px rgba(88,71,224,.6)' : '0 8px 20px -12px rgba(20,18,15,.2)',
              }}
            >
              {it.text}
            </div>
          </div>
        );
      })}
    </>
  );
}

function Stat({ label, value, color, note, highlight = 0, badge }: { label: string; value: number; color: string; note: string; highlight?: number; badge?: React.ReactNode }) {
  return (
    <div
      style={{
        width: 520, height: 236, borderRadius: 30, background: C.card, border: `1.5px solid ${highlight > 0.02 ? color : C.line}`, padding: '30px 38px',
        boxShadow: `0 24px 60px -24px rgba(40,30,90,.25), 0 0 0 ${highlight * 10}px ${color}22`, transform: `scale(${1 + highlight * 0.04})`,
      }}
    >
      <div style={{ fontSize: 28, color: C.muted }}>{label}</div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 22, marginTop: 6 }}>
        <span style={{ fontFamily: FONT_DISPLAY, fontWeight: 800, fontSize: 120, lineHeight: 1.05, color, letterSpacing: '-0.04em' }}>{value}</span>
        {badge}
        <span style={{ fontSize: 30, color: C.muted, marginLeft: 'auto', alignSelf: 'flex-end', paddingBottom: 18 }}>{note}</span>
      </div>
    </div>
  );
}

export function JustType() {
  const f = useCurrentFrame();

  // ---- the colored panel that wipes in for the drop and lifts away
  const panelX = interpolate(prog(f, 0, 10, easeInOut), [0, 1], [-100, 0]);
  const panelY = interpolate(prog(f, 56, 14, easeIn), [0, 1], [0, -105]);
  const just = pop(f, DROP, { damping: 11, stiffness: 230 });
  const keysIn = pop(f, 24, { damping: 13, stiffness: 240 });
  const smash = prog(f, SMASH, 16, easeIn);
  const slash = prog(f, SMASH - 4, 5);

  // ---- chat and stats
  const chatIn = prog(f, 52, 18);
  const chatOut = prog(f, WALL - 4, 12, easeIn);
  const switches = MESSAGES.filter((m) => f >= m.space + 4).length;
  const lastSwitch = Math.max(...MESSAGES.map((m) => m.space + 4).filter((t) => t <= f), -100);
  const currentLang = (() => {
    const m = [...MESSAGES].reverse().find((x) => f >= x.space + 4);
    return m ? m.to : 'en';
  })();
  const zeroGlow = Math.sin(Math.PI * prog(f, STATS, 22));

  // ---- wall of languages
  const wallOut = prog(f, WALL_OUT, 12, easeIn);

  return (
    <AbsoluteFill style={{ fontFamily: FONT_BODY, color: C.text }}>
      {/* chat */}
      {f >= 50 && f < WALL + 10 && (
        <AbsoluteFill style={{ opacity: chatIn * (1 - chatOut), transform: `scale(${0.96 + chatIn * 0.04 - chatOut * 0.06})` }}>
          <div style={{ position: 'absolute', left: 150, top: 130 }}>
            <Window title="Messages" width={1040} height={820}>
              <Bubbles f={f} />
              <Composer f={f} />
            </Window>
          </div>
          <div style={{ position: 'absolute', left: 1250, top: 250, display: 'flex', flexDirection: 'column', gap: 40 }}>
            <div style={{ transform: `scale(${1 + 0.06 * Math.sin(Math.PI * prog(f, lastSwitch, 10))})` }}>
              <Stat
                label="Keyboard switches" value={switches} color={C.accent} note="done for you"
                badge={<Badge code={currentLang} h={56} />}
              />
            </div>
            <Stat
              label="Times you pressed Alt+Shift" value={0} color={C.green} note="not once" highlight={zeroGlow}
              badge={f >= STATS && (
                <span style={{ width: 56, height: 56, borderRadius: 28, background: C.green, display: 'grid', placeItems: 'center', transform: `scale(${pop(f, STATS, { damping: 10, stiffness: 220 })})`, boxShadow: '0 10px 24px -8px rgba(22,163,74,.6)' }}>
                  <Check size={34} color="#fff" strokeWidth={3.2} />
                </span>
              )}
            />
          </div>
        </AbsoluteFill>
      )}

      {/* the wall */}
      {f >= WALL - 2 && (
        <AbsoluteFill style={{ opacity: 1 - wallOut, transform: `scale(${1 + wallOut * 0.15})` }}>
          <div style={{ position: 'absolute', top: 210, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, fontWeight: 700, fontSize: 104, letterSpacing: '-0.04em', ...rise(f, WALL) }}>
            22 languages. <span style={{ background: LOGO_GRADIENT, WebkitBackgroundClip: 'text', backgroundClip: 'text', color: 'transparent' }}>Zero switching.</span>
          </div>
          <div style={{ position: 'absolute', left: 135, top: 470, width: 1650, display: 'flex', flexWrap: 'wrap', rowGap: 44 }}>
            {LANGS.map((l, i) => {
              const s = pop(f, WALL + 4 + i * 0.9, { damping: 12, stiffness: 200 });
              return (
                <div key={l.code} style={{ width: 150, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, transform: `scale(${s}) translateY(${Math.sin(f * 0.08 + i) * 4}px)`, opacity: Math.min(1, s * 2) }}>
                  <Badge code={l.code} h={76} />
                  <span style={{ fontSize: 24, color: C.muted, whiteSpace: 'nowrap' }}>{l.native}</span>
                </div>
              );
            })}
          </div>
        </AbsoluteFill>
      )}

      {/* the drop: Just type. No more Alt+Shift. */}
      {f < 72 && (
        <AbsoluteFill style={{ background: LOGO_GRADIENT, transform: `translate(${panelX}%, ${panelY}%)`, color: '#fff', overflow: 'hidden' }}>
          <AbsoluteFill style={{ background: 'radial-gradient(900px 600px at 50% 40%, rgba(255,255,255,.18), transparent 70%)' }} />
          <div style={{ position: 'absolute', top: 250, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, fontWeight: 800, fontSize: 240, letterSpacing: '-0.055em', transform: `scale(${interpolate(just, [0, 1], [1.7, 1])})`, opacity: f < DROP ? 0 : 1 }}>
            Just type.
          </div>
          <div style={{ position: 'absolute', top: 640, width: '100%', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 28, opacity: f < 24 ? 0 : 1, transform: `scale(${keysIn})` }}>
            <span style={{ fontSize: 66, fontWeight: 600, marginRight: 14 }}>No more</span>
            <div style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 22 }}>
              <div style={{ transform: `translate(${-smash * 320}px, ${smash * smash * 700}px) rotate(${-smash * 50}deg)`, opacity: 1 - smash }}>
                <Keycap label="Alt" w={170} h={116} fontSize={40} />
              </div>
              <span style={{ fontSize: 56, fontWeight: 300, opacity: 1 - smash }}>+</span>
              <div style={{ transform: `translate(${smash * 340}px, ${smash * smash * 640}px) rotate(${smash * 45}deg)`, opacity: 1 - smash }}>
                <Keycap label="Shift" w={250} h={116} fontSize={40} />
              </div>
              <div style={{ position: 'absolute', left: -10, right: -10, top: '50%', marginTop: -6, height: 12, borderRadius: 6, background: '#fff', boxShadow: '0 0 30px rgba(255,255,255,.8)', transform: `rotate(-8deg) scaleX(${slash})`, transformOrigin: '0 50%', opacity: 1 - prog(f, SMASH + 4, 8) }} />
            </div>
          </div>
          <Burst x={1120} y={700} t={f - SMASH} colors={['#fff', '#FFE08A', '#C9F7D9']} radius={340} count={16} size={30} />
        </AbsoluteFill>
      )}

      {/* sound */}
      <Sfx name="whoosh" at={0} volume={0.5} />
      <Sfx name="thud-high" at={DROP} volume={0.9} />
      <Sfx name="pop-2" at={24} volume={0.45} />
      <Sfx name="swipe" at={SMASH - 4} volume={0.4} />
      <Sfx name="smash" at={SMASH} volume={0.85} />
      <Sfx name="whoosh" at={55} volume={0.45} />
      <Sfx name="pop-1" at={62} volume={0.35} />
      {MESSAGES.map((m, i) => (
        <span key={i}>
          <TypingSounds times={m.keys} text={m.wrong} volume={0.65} />
          <Sfx name="space" at={m.space} volume={0.7} />
          <Sfx name="fix" at={m.space + 1} volume={0.6} />
          <Sfx name="switch" at={m.space + 4} volume={0.4} />
          <Sfx name="tick" at={m.space + 5} volume={0.35} />
          <TypingSounds times={m.moreKeys} text={m.more} volume={0.55} />
          <Sfx name="enter" at={m.send} volume={0.6} />
          <Sfx name="pop-3" at={m.send + 1} volume={0.4} />
        </span>
      ))}
      <Sfx name="success" at={STATS} volume={0.4} />
      <Sfx name="swipe" at={WALL} volume={0.3} />
      {[0, 1, 2, 3, 4, 5].map((i) => <Sfx key={i} name={`pop-${i + 1}` as 'pop-1'} at={WALL + 4 + i * 3.4} volume={0.3} />)}
      <Sfx name="whoosh" at={WALL_OUT - 2} volume={0.45} />
    </AbsoluteFill>
  );
}
