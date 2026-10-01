import { AbsoluteFill, useCurrentFrame } from 'remotion';
import { Keyboard } from 'lucide-react';
import { caretOn, easeIn, easeInOut, keyTimes, leave, pop, prog, rise, typedCount } from '../anim';
import { Badge, Burst, Caret, Flip, Keycap, Sfx, TypingSounds } from '../components/bits';
import { C, FONT_BODY, FONT_DISPLAY, lang } from '../theme';

// Global 355–600 (bars 6–9). Frames below are the scene's own; Space lands on frame 420 (bar 7).
const WRONG = 'ghbdtn';
const RIGHT = 'привет';
const TYPE = keyTimes(23, WRONG.length, 5);
const SPACE = 65;
const FLIP = 66;
const SWITCH = 74;
const MORE_TEXT = 'как дела';
const MORE = keyTimes(94, MORE_TEXT.length, 3);
const OUT1 = 128;
const CARDS = 134;
const BEATS = [140, 155, 170, 185, 200, 215]; // frames 495…570 of the video, one flip per beat
const OUT2 = 237;

// Each of these was checked with the app's engine (website/src/demo/scenes.ts): typed on the English keyboard.
const EXAMPLES = [
  { code: 'he', keys: 'akuo', word: 'שלום' },
  { code: 'ar', keys: 'lvpfh', word: 'مرحبا' },
  { code: 'el', keys: 'kalhm;era', word: 'καλημέρα' },
  { code: 'de', keys: 'Yeit', word: 'Zeit' },
  { code: 'ko', keys: 'dkssud', word: '안녕' },
  { code: 'th', keys: 'l;ylfu', word: 'สวัสดี' },
];

function Heading({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) {
  return (
    <div
      style={{
        position: 'absolute', top: 140, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, fontWeight: 700,
        fontSize: 88, letterSpacing: '-0.035em', color: C.text, ...style,
      }}
    >
      {children}
    </div>
  );
}

export function CoreFeature() {
  const f = useCurrentFrame();
  const enter = prog(f, 0, 18);
  const out1 = prog(f, OUT1, 12, easeIn);

  // ---- the text in the box
  const fixed = f >= FLIP;
  const typed = typedCount(TYPE, f);
  const more = MORE_TEXT.slice(0, typedCount(MORE, f));
  const lastKey = Math.max(...[...TYPE, SPACE, ...MORE].filter((t) => t <= f), -100);
  const flash = prog(f, FLIP, 6) * (1 - prog(f, FLIP + 22, 18));
  const badgeP = prog(f, SWITCH - 5, 10, easeInOut);
  const badgeS = 1 + 0.25 * Math.sin(Math.PI * prog(f, SWITCH - 2, 12));

  return (
    <AbsoluteFill style={{ fontFamily: FONT_BODY }}>

      {/* ---------- part 1: one word */}
      {f < OUT1 + 14 && (
        <AbsoluteFill style={{ opacity: 1 - out1, transform: `translateY(${-out1 * 60}px)` }}>
          {f < SPACE + 14 && (
            <Heading style={{ ...rise(f, 4), ...(f >= SPACE + 8 ? leave(f, SPACE + 8, { dur: 8 }) : {}) }}>
              Type the word. Press <span style={{ color: C.accent }}>Space</span>.
            </Heading>
          )}
          {f >= SPACE + 14 && (
            <Heading style={rise(f, SPACE + 14)}>
              It’s fixed, and the keyboard switches too.
            </Heading>
          )}

          {/* the text box */}
          <div
            style={{
              position: 'absolute', left: 340, top: 400, width: 1240, height: 230, borderRadius: 36, background: C.card,
              border: `1.5px solid ${C.line}`, display: 'flex', alignItems: 'center', padding: '0 70px',
              boxShadow: `0 1px 2px rgba(20,18,15,.04), 0 30px 70px -24px rgba(40,30,90,.25), 0 0 0 ${flash * 10}px rgba(88,71,224,${0.12 * flash})`,
              opacity: enter, transform: `translateY(${(1 - enter) * 50}px) scale(${0.96 + enter * 0.04})`,
            }}
          >
            <span style={{ fontSize: 112, fontWeight: 500, whiteSpace: 'pre', color: C.text, letterSpacing: '-0.01em' }}>
              {!fixed && WRONG.slice(0, typed)}
              {fixed && (
                <span style={{ background: `rgba(88,71,224,${0.12 * flash})`, borderRadius: 18, margin: '0 -10px', padding: '0 10px' }}>
                  {[...WRONG].map((ch, i) => (
                    <Flip
                      key={i} from={ch} to={RIGHT[i]} p={prog(f, FLIP + i * 1.6, 9, easeInOut)}
                      style={{ color: f < FLIP + 26 ? C.accent : C.text }}
                    />
                  ))}
                </span>
              )}
              {f >= SPACE && ' '}
              {more}
            </span>
            <span style={{ position: 'relative', display: 'inline-block' }}>
              <Caret h={120} on={caretOn(f, lastKey)} style={{ marginLeft: 6 }} />
              <span style={{ position: 'absolute', left: 22, top: -30, transform: `scale(${badgeS})`, transformOrigin: '0 100%' }}>
                <Flip p={badgeP} from={<Badge code="en" h={50} />} to={<Badge code="ru" h={50} />} />
              </span>
            </span>
          </div>
          <Burst x={600} y={515} t={f - FLIP - 2} count={14} inner={210} radius={330} />

          {/* the "recent fix" chip, as in the app */}
          {f >= SWITCH && (
            <div
              style={{
                position: 'absolute', left: 380, top: 312, display: 'flex', alignItems: 'baseline', gap: 16, padding: '14px 26px',
                borderRadius: 20, background: C.card, border: `1.5px solid ${C.line}`, fontSize: 40,
                boxShadow: '0 12px 30px -12px rgba(20,18,15,.2)',
                transform: `scale(${pop(f, SWITCH, { damping: 13, stiffness: 220 })}) translateY(${Math.sin(f * 0.08) * 4}px)`, transformOrigin: '0 100%',
              }}
            >
              <s style={{ color: C.faint, textDecorationThickness: 3 }}>{WRONG}</s>
              <span style={{ color: C.faint }}>→</span>
              <b style={{ color: C.text, fontWeight: 650 }}>{RIGHT}</b>
            </div>
          )}

          {/* Space bar, then what happened */}
          {f >= 40 && f < SPACE + 20 && (
            <div
              style={{
                position: 'absolute', top: 712, width: '100%', display: 'flex', justifyContent: 'center',
                ...rise(f, 40, { dist: 40, dur: 14 }), ...(f >= SPACE + 10 ? leave(f, SPACE + 10, { dist: 30, dur: 8 }) : {}),
              }}
            >
              <Keycap label="Space" w={660} h={124} fontSize={42} press={f >= SPACE && f < SPACE + 6 ? 1 : 0} glow={prog(f, SPACE, 3) * (1 - prog(f, SPACE + 4, 10))} />
            </div>
          )}
          {f >= SPACE + 18 && (
            <div style={{ position: 'absolute', top: 730, width: '100%', display: 'flex', justifyContent: 'center', ...rise(f, SPACE + 18) }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 40, color: C.muted }}>
                <Keyboard size={44} strokeWidth={1.7} />
                Switched to <b style={{ color: lang('ru').color, fontWeight: 650 }}>Russian</b>
              </div>
            </div>
          )}
        </AbsoluteFill>
      )}

      {/* ---------- part 2: six more languages, one per beat */}
      {f >= CARDS - 4 && (
        <AbsoluteFill>
          <Heading style={{ ...rise(f, CARDS - 4), ...(f >= OUT2 ? leave(f, OUT2, { dur: 8 }) : {}) }}>
            Works in every language you type.
          </Heading>
          {EXAMPLES.map((ex, i) => {
            const col = i % 3, row = Math.floor(i / 3);
            const x = 215 + col * 510, y = 330 + row * 270;
            const at = BEATS[i];
            const inS = pop(f, CARDS + i * 2.5, { damping: 14, stiffness: 180 });
            const p = prog(f, at, 10, easeInOut);
            const pName = prog(f, at + 2, 10, easeInOut), pWord = prog(f, at + 3, 10, easeInOut);
            const hit = Math.sin(Math.PI * prog(f, at, 14));
            const out = prog(f, OUT2 + i, 10, easeIn);
            const l = lang(ex.code);
            return (
              <div
                key={ex.code}
                style={{
                  position: 'absolute', left: x, top: y, width: 470, height: 230, borderRadius: 30, background: C.card,
                  border: `2px solid ${f >= at ? l.color + '55' : C.line}`, padding: '26px 30px',
                  boxShadow: `0 1px 2px rgba(20,18,15,.04), 0 24px 50px -20px rgba(40,30,90,${0.18 + hit * 0.2})`,
                  transform: `translateX(${-out * 160}px) scale(${inS * (1 + hit * 0.05)})`, opacity: Math.min(1, inS * 1.5) * (1 - out),
                  display: 'flex', flexDirection: 'column', gap: 18,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 16, fontSize: 30, color: C.muted }}>
                  <Flip p={p} from={<Badge code="en" h={44} />} to={<Badge code={ex.code} h={44} />} />
                  <Flip p={pName} from="English keyboard" to={l.name} />
                </div>
                <div style={{ fontSize: 76, fontWeight: 500, lineHeight: 1.15, height: 100, display: 'flex', alignItems: 'center' }}>
                  <Flip
                    p={pWord}
                    from={<span style={{ color: C.faint }}>{ex.keys}</span>}
                    to={<span style={{ color: C.text, fontWeight: 600 }} dir="auto">{ex.word}</span>}
                  />
                </div>
              </div>
            );
          })}
        </AbsoluteFill>
      )}

      {/* sound */}
      <TypingSounds times={TYPE} text={WRONG} volume={0.75} />
      <Sfx name="space" at={SPACE} volume={0.85} />
      <Sfx name="fix" at={SPACE + 1} volume={0.85} />
      <Sfx name="switch" at={SWITCH - 3} volume={0.55} />
      <TypingSounds times={MORE} text={MORE_TEXT} volume={0.6} />
      <Sfx name="whoosh" at={OUT1 - 3} volume={0.4} />
      <Sfx name="swipe" at={CARDS} volume={0.3} />
      {BEATS.map((at, i) => <Sfx key={i} name={`pop-${i + 1}` as 'pop-1'} at={at} volume={0.5} />)}
      <Sfx name="whoosh" at={OUT2 - 1} volume={0.45} />
    </AbsoluteFill>
  );
}
