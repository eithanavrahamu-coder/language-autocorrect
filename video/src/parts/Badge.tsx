import { pop, useTime } from '../anim.ts';
import { byCode, FONT_BADGE, mix } from '../theme.ts';

/**
 * The colored language badge. `cursor` is the one the app shows next to the text cursor (26×17 with a gentle gradient,
 * a thin darker rim and a light top edge: src/LanguageAutocorrect/BadgeArt.cs); `tile` is the website's big square one.
 */
export function Badge({ code, height, look = 'cursor' }: { code: string; height: number; look?: 'cursor' | 'tile' }) {
  const lang = byCode(code);
  const cursor = look === 'cursor';
  const s = height / (cursor ? 17 : 44);
  const stroke = Math.max(1, s * 0.9);
  return (
    <span
      style={{
        display: 'inline-grid',
        placeItems: 'center',
        boxSizing: 'border-box',
        height,
        minWidth: (cursor ? 26 : 44) * s,
        padding: `0 ${(cursor ? 4 : 9) * s}px`,
        borderRadius: (cursor ? 6 : 12) * s,
        background: `linear-gradient(${mix(lang.color, '#FFFFFF', 0.18)}, ${mix(lang.color, '#000000', 0.1)})`,
        boxShadow: [
          `inset 0 0 0 ${stroke}px ${mix(lang.color, '#000000', 0.45, 0.35)}`,
          `inset 0 ${stroke}px 0 rgba(255, 255, 255, 0.4)`,
          cursor
            ? `0 ${2 * s}px ${6 * s}px rgba(0, 0, 0, 0.18)`
            : `0 ${5 * s}px ${14 * s}px ${-4 * s}px ${mix(lang.color, '#000000', 0.2, 0.55)}`,
        ].join(', '),
        color: '#FFFFFF',
        fontFamily: FONT_BADGE,
        fontWeight: 700,
        fontSize: (cursor ? 11 : 17) * s,
        lineHeight: 1,
        whiteSpace: 'nowrap',
      }}
    >
      {lang.badge}
    </span>
  );
}

/**
 * A badge that changes language at the given moments, as the app's does: the old one shrinks away while the new one
 * springs in (src/LanguageAutocorrect/IndicatorForm.cs). The first step pops in the same way.
 */
export function BadgeTrack({ steps, height, look }: {
  steps: { at: number; lang: string }[];
  height: number;
  look?: 'cursor' | 'tile';
}) {
  const t = useTime();
  let i = -1;
  steps.forEach((s, k) => { if (t >= s.at) i = k; });
  if (i < 0) return null;
  const now = steps[i];
  const before = i > 0 ? steps[i - 1] : null;
  const x = pop(t, now.at);
  const switching = before && t - now.at < 0.45;
  return (
    <span style={{ display: 'inline-grid', justifyItems: 'start', alignItems: 'start' }}>
      {switching && (
        <span style={{ gridArea: '1 / 1', transform: `scale(${1 - 0.5 * x})`, opacity: Math.max(0, 1 - x) }}>
          <Badge code={before.lang} height={height} look={look} />
        </span>
      )}
      <span style={{ gridArea: '1 / 1', transform: `scale(${0.5 + 0.5 * x})`, opacity: Math.min(1, x) }}>
        <Badge code={now.lang} height={height} look={look} />
      </span>
    </span>
  );
}
