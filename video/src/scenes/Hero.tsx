import { AbsoluteFill } from 'remotion';
import { countUntil, easeIn, pop, ramp, useTime } from '../anim.ts';
import { Caption } from '../parts/Caption.tsx';
import { FixCard } from '../parts/FixCard.tsx';
import { Field, FixedWord, Wrong } from '../parts/Field.tsx';
import { Keycap } from '../parts/Keycap.tsx';
import { HERO } from '../timeline.ts';

const SIZE = 116;

/** 0–5.5 s: "hello" typed on the Hebrew keyboard comes out wrong; Space fixes it and switches the keyboard. */
export function Hero() {
  const t = useTime();
  if (t > HERO.end) return null;

  const typedCount = countUntil(t, HERO.keyTimes);
  const thenCount = countUntil(t, HERO.thenTimes);
  const fixed = t >= HERO.fix;
  const lastKey = Math.max(
    ...[...HERO.keyTimes, HERO.space, ...HERO.thenTimes].filter(k => k <= t),
    HERO.caretIn,
  );

  const line = fixed ? (
    <>
      <FixedWord text={HERO.fixed} at={HERO.fix} size={SIZE} />
      {HERO.then.slice(0, thenCount)}
    </>
  ) : (
    <Wrong at={HERO.squiggle}>{HERO.typed.slice(0, typedCount)}</Wrong>
  );

  // The box springs in, and leaves at the end rising and fading.
  const inP = pop(t, HERO.cardIn);
  const out = ramp(t, HERO.out, 0.3, easeIn);
  // A slow push in, so the picture never stands still.
  const zoom = 1 + 0.03 * ramp(t, 0, HERO.out, x => x);

  // The keys under the box: the letters, then Space.
  const lettersOut = ramp(t, HERO.keycapsOut, 0.25, easeIn);
  const spaceOut = ramp(t, 3.15, 0.25, easeIn);

  return (
    <AbsoluteFill>
      <Caption caption={HERO.ask} />
      <Caption caption={HERO.press} />
      <Caption caption={HERO.switches} />

      <AbsoluteFill
        style={{
          alignItems: 'center',
          top: 400,
          opacity: Math.min(1, inP) * (1 - out),
          transform: `translateY(${(1 - inP) * 40 - out * 50}px) scale(${(0.92 + 0.08 * inP) * zoom * (1 - out * 0.05)})`,
          filter: out > 0 ? `blur(${out * 8}px)` : undefined,
        }}
      >
        <Field
          width={1240}
          height={290}
          size={SIZE}
          line={line}
          lastKey={lastKey}
          caretIn={HERO.caretIn}
          badge={[{ at: HERO.badgeIn, lang: 'he' }, { at: HERO.switchAt, lang: 'en' }]}
          above={<FixCard typed={HERO.typed} fixed={HERO.fixed} at={HERO.fix + 0.03} />}
        />
      </AbsoluteFill>

      {/* The keys pressed, with their English and Hebrew letters */}
      <AbsoluteFill
        style={{
          top: 800,
          flexDirection: 'row',
          justifyContent: 'center',
          alignItems: 'flex-start',
          gap: 22,
          opacity: 1 - lettersOut,
          transform: `translateY(${lettersOut * 30}px)`,
        }}
      >
        {[...HERO.keys].map((k, i) => (
          <Keycap
            key={i}
            label={k.toUpperCase()}
            sub={HERO.typed[i]}
            appear={HERO.keyTimes[i] - 0.04}
            presses={[HERO.keyTimes[i]]}
          />
        ))}
      </AbsoluteFill>

      <AbsoluteFill
        style={{
          top: 800,
          alignItems: 'center',
          opacity: t < HERO.spaceIn ? 0 : 1 - spaceOut,
          transform: `translateY(${spaceOut * 30}px)`,
        }}
      >
        <Keycap label="Space" units={5} appear={HERO.spaceIn} presses={[HERO.space]} />
      </AbsoluteFill>
    </AbsoluteFill>
  );
}
