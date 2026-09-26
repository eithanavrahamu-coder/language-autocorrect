import { AbsoluteFill } from 'remotion';
import { countUntil, easeIn, pop, ramp, useTime } from '../anim.ts';
import { Badge } from '../parts/Badge.tsx';
import { Caption } from '../parts/Caption.tsx';
import { Field, FixedWord } from '../parts/Field.tsx';
import { LANGS } from '../timeline.ts';
import { LANGUAGES } from '../theme.ts';

const CARD = { width: 520, height: 200, size: 60, gap: 40, top: 330 };

/** 5.5–10.1 s: six languages fixed one after another, then every language's badge. */
export function Languages() {
  const t = useTime();
  if (t < LANGS.start || t > LANGS.end) return null;
  return (
    <AbsoluteFill>
      <Caption caption={LANGS.every} />
      <Caption caption={{ ...LANGS.count, text: LANGS.count.text.replace('{count}', String(LANGUAGES.length)) }} />
      {t < LANGS.gridOut + 0.45 && <Grid />}
      {t >= LANGS.burst && <Burst />}
    </AbsoluteFill>
  );
}

function Grid() {
  const t = useTime();
  const center = { x: 960, y: CARD.top + CARD.height + CARD.gap / 2 };
  return (
    <>
      {LANGS.cards.map((c, i) => {
        const col = i % 3, row = Math.floor(i / 3);
        const x = 960 + (col - 1) * (CARD.width + CARD.gap);
        const y = CARD.top + CARD.height / 2 + row * (CARD.height + CARD.gap);
        const inP = pop(t, c.cardIn);
        if (inP <= 0) return null;
        // Leaving: the cards fly apart and fade, making room for the badges bursting out of the middle.
        const out = ramp(t, LANGS.gridOut + i * 0.015, 0.3, easeIn);
        const away = Math.hypot(x - center.x, y - center.y);
        const dx = ((x - center.x) / away) * 220 * out, dy = ((y - center.y) / away) * 220 * out;

        const fixed = t >= c.fix;
        const typed = c.keys.slice(0, countUntil(t, c.keyTimes));
        const lastKey = Math.max(c.cardIn + 0.1, ...c.keyTimes.filter(k => k <= t), fixed ? c.fix : 0);
        return (
          <div
            key={c.lang}
            style={{
              position: 'absolute',
              left: x - CARD.width / 2,
              top: y - CARD.height / 2,
              opacity: Math.min(1, inP * 1.4) * (1 - out),
              transform: `translate(${dx}px, ${dy + (1 - inP) * 36}px) scale(${(0.86 + 0.14 * inP) * (1 - 0.15 * out)})`,
            }}
          >
            <Field
              width={CARD.width}
              height={CARD.height}
              size={CARD.size}
              line={fixed ? <FixedWord text={c.fixed} at={c.fix} size={CARD.size} /> : typed}
              lastKey={lastKey}
              caretIn={c.cardIn + 0.1}
              badge={[{ at: c.cardIn + 0.1, lang: 'en' }, { at: c.switchAt, lang: c.lang }]}
            />
          </div>
        );
      })}
    </>
  );
}

const TILE = { height: 112, width: 120, gap: 28, rows: [436, 588] };

function Burst() {
  const t = useTime();
  const perRow = Math.ceil(LANGUAGES.length / 2);
  const from = { x: 960, y: 570 };
  return (
    <>
      {LANGUAGES.map((l, i) => {
        const row = i < perRow ? 0 : 1;
        const inRow = row === 0 ? perRow : LANGUAGES.length - perRow;
        const j = row === 0 ? i : i - perRow;
        const x = 960 + (j - (inRow - 1) / 2) * (TILE.width + TILE.gap);
        const y = TILE.rows[row] + TILE.height / 2;
        const at = LANGS.burst + i * LANGS.burstStep;
        const p = pop(t, at, { stiffness: 230, damping: 17 });
        if (p <= 0) return null;
        const out = ramp(t, LANGS.out - 0.28 + i * 0.008, 0.28, easeIn);
        const px = from.x + (x - from.x) * p;
        const py = from.y + (y - from.y) * p - out * 90;
        const turn = (i % 2 ? 1 : -1) * (10 + (i % 5) * 3) * (1 - p);
        return (
          <div
            key={l.code}
            style={{
              position: 'absolute',
              left: px - TILE.width / 2,
              top: py - TILE.height / 2,
              width: TILE.width,
              display: 'flex',
              justifyContent: 'center',
              opacity: Math.min(1, p * 2) * (1 - out),
              transform: `scale(${0.35 + 0.65 * p}) rotate(${turn}deg)`,
            }}
          >
            <Badge code={l.code} height={TILE.height} look="tile" />
          </div>
        );
      })}
    </>
  );
}
