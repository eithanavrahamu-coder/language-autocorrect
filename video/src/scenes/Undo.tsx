import { Delete } from 'lucide-react';
import { AbsoluteFill } from 'remotion';
import { easeIn, pop, ramp, useTime } from '../anim.ts';
import { Caption } from '../parts/Caption.tsx';
import { FixCard } from '../parts/FixCard.tsx';
import { Field, FixedWord } from '../parts/Field.tsx';
import { Keycap } from '../parts/Keycap.tsx';
import { UNDO } from '../timeline.ts';

const SIZE = 116;

/** 10.1–12.7 s: a word was fixed, but it was meant as typed: Backspace brings it back. */
export function Undo() {
  const t = useTime();
  if (t < UNDO.start || t > UNDO.end) return null;

  const undone = t >= UNDO.undo;
  const line = undone
    ? <FixedWord text={UNDO.typed} at={UNDO.undo} size={SIZE} glow={false} />
    : <FixedWord text={UNDO.fixed} at={UNDO.cardIn} size={SIZE} />;

  const inP = pop(t, UNDO.cardIn);
  const out = ramp(t, UNDO.out, 0.28, easeIn);
  const keyOut = ramp(t, UNDO.out - 0.1, 0.25, easeIn);
  const zoom = 1 + 0.025 * ramp(t, UNDO.cardIn, UNDO.out - UNDO.cardIn, x => x);

  return (
    <AbsoluteFill>
      <Caption caption={UNDO.ask} />
      <AbsoluteFill
        style={{
          alignItems: 'center',
          top: 400,
          opacity: Math.min(1, inP) * (1 - out),
          transform: `translateY(${(1 - inP) * 40 - out * 50}px) scale(${(0.92 + 0.08 * inP) * zoom})`,
          filter: out > 0 ? `blur(${out * 8}px)` : undefined,
        }}
      >
        <Field
          width={1240}
          height={290}
          size={SIZE}
          line={line}
          lastKey={Math.max(UNDO.cardIn, t >= UNDO.backspace ? UNDO.backspace : 0)}
          caretIn={UNDO.cardIn}
          badge={[{ at: UNDO.cardIn, lang: 'ru' }, { at: UNDO.switchAt, lang: 'en' }]}
          above={
            <>
              <FixCard typed={UNDO.typed} fixed={UNDO.fixed} at={UNDO.cardShown} gone={UNDO.undo} />
              <FixCard typed={UNDO.typed} fixed={UNDO.fixed} at={UNDO.undo} undone />
            </>
          }
        />
      </AbsoluteFill>
      <AbsoluteFill
        style={{
          top: 800,
          alignItems: 'center',
          opacity: t < UNDO.backspaceIn ? 0 : 1 - keyOut,
          transform: `translateY(${keyOut * 30}px)`,
        }}
      >
        <Keycap
          label="Backspace"
          units={3.4}
          appear={UNDO.backspaceIn}
          presses={[UNDO.backspace]}
          icon={<Delete size={40} strokeWidth={2.2} />}
        />
      </AbsoluteFill>
    </AbsoluteFill>
  );
}
