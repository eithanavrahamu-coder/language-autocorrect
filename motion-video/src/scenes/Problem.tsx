import { AbsoluteFill, interpolate, useCurrentFrame } from 'remotion';
import { Delete, Keyboard } from 'lucide-react';
import { caretOn, keyTimes, leave, pop, prog, rise, shake, typedCount } from '../anim';
import { Caret, Keycap, Sfx, TypingSounds, Window } from '../components/bits';
import { C, FONT_BODY, FONT_DISPLAY } from '../theme';

// 0–240 (bars 0–3): someone types Russian with the English keyboard on, then fixes it the old way.
const WRONG = 'ghbdtn rfr ltkf';
const RIGHT = 'привет как дела';
const TYPE = keyTimes(14, WRONG.length, 4);
const ERR = 78;
const DEL = 120; // Backspace held: one letter a frame
const ALT = 138, SHIFT = 141;
const RETYPE = keyTimes(151, RIGHT.length, 2);
const WORDS = [180, 195, 210]; // Every. Single. Time.
const OUT = 224;

export function Problem() {
  const f = useCurrentFrame();

  let text: string, lastKey: number;
  if (f < DEL) { text = WRONG.slice(0, typedCount(TYPE, f)); lastKey = TYPE.filter((t) => t <= f).pop() ?? -100; }
  else if (f < RETYPE[0]) { text = WRONG.slice(0, Math.max(0, WRONG.length - (f - DEL))); lastKey = f; }
  else { text = RIGHT.slice(0, typedCount(RETYPE, f)); lastKey = RETYPE.filter((t) => t <= f).pop() ?? -100; }
  const wrong = f >= ERR && f < DEL + 8;
  const wrongGlow = prog(f, ERR, 6) * (1 - prog(f, DEL, 14));
  const switched = f >= ALT + 3;

  const dim = prog(f, WORDS[0] - 2, 10);
  const out = prog(f, OUT, 14);

  return (
    <AbsoluteFill
      style={{
        background: [
          `radial-gradient(900px 520px at 50% 52%, rgba(229,72,77,${0.2 * wrongGlow}), transparent 70%)`,
          'radial-gradient(1100px 700px at 50% 40%, rgba(88,71,224,.13), transparent 70%)',
          C.dark,
        ].join(','),
        fontFamily: FONT_BODY,
        transform: `translate(${shake(f, WORDS[0], 8, 8) + shake(f, WORDS[1], 8, 8) + shake(f, WORDS[2], 12, 10)}px, 0)`,
      }}
    >
      <AbsoluteFill
        style={{
          backgroundImage: 'radial-gradient(rgba(255,255,255,.06) 1.3px, transparent 1.6px)', backgroundSize: '34px 34px',
          maskImage: 'radial-gradient(ellipse 70% 65% at 50% 50%, #000 20%, transparent 100%)',
        }}
      />

      {/* everything about the typing, which steps back when the big words come */}
      <AbsoluteFill style={{ opacity: 1 - dim * 0.82, transform: `scale(${1 - dim * 0.06})`, filter: dim ? `blur(${dim * 6}px)` : undefined }}>
        {/* captions */}
        <div style={{ position: 'absolute', top: 150, width: '100%', textAlign: 'center', fontFamily: FONT_DISPLAY, color: C.darkText }}>
          {f < 86 && (
            <div style={{ fontSize: 72, fontWeight: 600, letterSpacing: '-0.03em', ...rise(f, 4), ...(f >= 80 ? leave(f, 80) : {}) }}>
              You start typing in Russian…
            </div>
          )}
          {f >= 84 && f < DEL && (
            <div style={{ fontSize: 72, fontWeight: 600, letterSpacing: '-0.03em', ...rise(f, 84), ...(f >= 115 ? leave(f, 115, { dur: 6 }) : {}) }}>
              …but the keyboard is on <span style={{ color: '#6E9BFF' }}>English</span>.
            </div>
          )}
          {f >= DEL && (
            <div style={{ display: 'flex', justifyContent: 'center', gap: 44, fontSize: 116, fontWeight: 700, letterSpacing: '-0.04em', marginTop: -20 }}>
              {['Delete.', 'Switch.', 'Retype.'].map((w, i) => {
                const at = DEL + i * 15;
                if (f < at) return <span key={w} style={{ opacity: 0 }}>{w}</span>;
                const s = pop(f, at, { damping: 12, stiffness: 260 });
                const current = f < at + 15 || i === 2;
                return (
                  <span key={w} style={{ display: 'inline-block', transform: `scale(${interpolate(s, [0, 1], [1.5, 1])})`, opacity: Math.min(1, (f - at) / 3) * (current ? 1 : 0.32) }}>
                    {w}
                  </span>
                );
              })}
            </div>
          )}
        </div>

        {/* the message box */}
        <div style={{ position: 'absolute', left: 0, right: 0, top: 380, display: 'flex', justifyContent: 'center', ...rise(f, 0, { dist: 40, dur: 18 }) }}>
          <div style={{ transform: `translateX(${shake(f, ERR, 18, 16)}px)` }}>
            <Window dark title="New message" width={1180} height={250} bodyStyle={{ display: 'flex', alignItems: 'center', padding: '0 60px' }}>
              <span
                style={{
                  fontSize: 88, fontWeight: 500, letterSpacing: '-0.01em', whiteSpace: 'pre',
                  color: wrong ? '#FF7B7F' : C.darkText,
                  textDecorationLine: wrong ? 'underline' : 'none', textDecorationStyle: 'wavy',
                  textDecorationColor: `rgba(255,98,104,${prog(f, ERR, 6)})`, textDecorationThickness: 4, textUnderlineOffset: 18,
                }}
              >
                {text}
              </span>
              <Caret h={96} on={caretOn(f, lastKey)} color={C.darkText} style={{ marginLeft: 4 }} />
            </Window>
          </div>
        </div>

        {/* which keyboard Windows is on */}
        <div style={{ position: 'absolute', top: 668, width: '100%', display: 'flex', justifyContent: 'center', ...rise(f, 8) }}>
          <div
            style={{
              display: 'flex', alignItems: 'center', gap: 14, padding: '14px 26px', borderRadius: 999, fontSize: 30,
              color: C.darkMuted, background: '#1D1D1B',
              border: `2px solid ${wrongGlow > 0.05 ? `rgba(255,98,104,${wrongGlow})` : C.darkLine}`,
              transform: `scale(${1 + 0.06 * Math.sin(Math.PI * prog(f, ERR + 2, 10))})`,
            }}
          >
            <Keyboard size={32} strokeWidth={1.8} />
            Keyboard: <b style={{ color: switched ? '#FF6B6B' : '#6E9BFF', fontWeight: 600 }}>{switched ? 'Russian' : 'English'}</b>
          </div>
        </div>

        {/* the keys the old way needs */}
        <div style={{ position: 'absolute', top: 790, width: '100%', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: 22 }}>
          {f >= DEL - 3 && f < ALT - 4 && (
            <div style={{ transform: `scale(${pop(f, DEL - 3, { damping: 15, stiffness: 300 }) * (1 - prog(f, ALT - 8, 4))})` }}>
              <Keycap dark w={330} h={110} press={f >= DEL && f < DEL + 15 ? 1 : 0} label={<><Delete size={40} strokeWidth={1.8} /> Backspace</>} fontSize={36} />
            </div>
          )}
          {f >= ALT - 5 && f < RETYPE[0] + 2 && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 22, transform: `scale(${pop(f, ALT - 5, { damping: 15, stiffness: 300 }) * (1 - prog(f, RETYPE[0] - 3, 5))})` }}>
              <Keycap dark w={170} h={110} press={f >= ALT && f < ALT + 9 ? 1 : 0} label="Alt" fontSize={38} />
              <span style={{ color: C.darkMuted, fontSize: 52, fontWeight: 300 }}>+</span>
              <Keycap dark w={250} h={110} press={f >= SHIFT && f < ALT + 9 ? 1 : 0} label="Shift" fontSize={38} />
            </div>
          )}
        </div>
      </AbsoluteFill>

      {/* Every. Single. Time. */}
      <AbsoluteFill
        style={{
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 50, fontFamily: FONT_DISPLAY, fontWeight: 800,
          fontSize: 190, letterSpacing: '-0.05em', color: C.darkText,
          transform: `scale(${1 - out * 0.7}) rotate(${out * -6}deg)`, opacity: 1 - out,
        }}
      >
        {['Every.', 'Single.', 'Time.'].map((w, i) => {
          const at = WORDS[i];
          const s = pop(f, at, { damping: 11, stiffness: 240 });
          return (
            <span
              key={w}
              style={{
                display: 'inline-block', opacity: f < at ? 0 : Math.min(1, (f - at) / 2),
                transform: `scale(${interpolate(s, [0, 1], [1.9, 1])})`, color: i === 2 ? '#FF6B6B' : undefined,
              }}
            >
              {w}
            </span>
          );
        })}
      </AbsoluteFill>

      {/* sound */}
      <TypingSounds times={TYPE} text={WRONG} volume={0.55} />
      <Sfx name="error" at={ERR} volume={0.75} />
      {[0, 1, 2].map((i) => <Sfx key={i} name="thud" at={DEL + i * 15} volume={0.55} />)}
      {Array.from({ length: 8 }, (_, i) => <Sfx key={i} name="backspace" at={DEL + 1 + i * 2} volume={0.32} />)}
      <Sfx name="key-2" at={ALT} volume={0.55} />
      <Sfx name="key-4" at={SHIFT} volume={0.55} />
      <TypingSounds times={RETYPE} text={RIGHT} volume={0.36} />
      <Sfx name="thud" at={WORDS[0]} volume={0.9} />
      <Sfx name="thud" at={WORDS[1]} volume={0.9} />
      <Sfx name="thud-high" at={WORDS[2]} volume={1} />
      <Sfx name="whoosh-big" at={OUT - 4} volume={0.55} />
    </AbsoluteFill>
  );
}
