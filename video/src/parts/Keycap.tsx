import type { ReactNode } from 'react';
import { BOUNCY, keyDown, pop, useTime } from '../anim.ts';
import { C, FONT_BADGE, FONT_BODY, mix } from '../theme.ts';

/**
 * A big keyboard key, like the website's <kbd> keys: it pops in at `appear` and goes down at each of `presses`.
 * `label` is printed top-left (or in the middle for wide keys), `sub` bottom-right, as on a two-language keyboard.
 */
export function Keycap({ label, sub, subColor = C.he, units = 1, size = 112, appear, presses = [], icon }: {
  label: string;
  sub?: string;
  subColor?: string;
  units?: number;
  size?: number;
  appear: number;
  presses?: number[];
  icon?: ReactNode;
}) {
  const t = useTime();
  const p = pop(t, appear, BOUNCY);
  if (p <= 0) return <div style={{ width: size * units, height: size, flexShrink: 0 }} />;

  const down = keyDown(t, presses);
  const side = size * 0.08;
  const wide = units > 1;
  const radius = size * 0.2;
  return (
    <div
      style={{
        position: 'relative',
        width: size * units,
        height: size,
        flexShrink: 0,
        opacity: Math.min(1, p * 1.5),
        transform: `translateY(${(1 - p) * 34}px) scale(${0.62 + 0.38 * p})`,
      }}
    >
      {/* The key's body, seen below its top */}
      <div
        style={{
          position: 'absolute',
          inset: `${side}px 0 0 0`,
          borderRadius: radius,
          background: mix(C.line, '#000000', 0.08),
          boxShadow: `0 ${size * 0.16 * (1 - down * 0.5)}px ${size * 0.3}px ${-size * 0.1}px rgba(20, 18, 15, ${0.28 - down * 0.1})`,
        }}
      />
      {/* Its top */}
      <div
        style={{
          position: 'absolute',
          left: 0,
          right: 0,
          top: down * side * 0.85,
          height: size - side,
          boxSizing: 'border-box',
          borderRadius: radius,
          border: `${Math.max(2, size * 0.018)}px solid ${C.line}`,
          background: `linear-gradient(${C.card}, ${mix(C.card, C.accentSoft, 0.35 + 0.65 * down)})`,
          display: 'flex',
          alignItems: wide ? 'center' : 'flex-start',
          justifyContent: wide ? 'center' : 'flex-start',
          gap: size * 0.12,
          padding: wide ? 0 : `${size * 0.1}px ${size * 0.17}px`,
          fontFamily: FONT_BODY,
          fontWeight: 600,
          fontSize: wide ? size * 0.27 : size * 0.36,
          color: wide ? C.muted : C.text,
          lineHeight: 1.2,
        }}
      >
        {icon}
        {label}
        {sub && (
          <span
            style={{
              position: 'absolute',
              right: size * 0.15,
              bottom: size * 0.08,
              fontFamily: FONT_BADGE,
              fontWeight: 600,
              fontSize: size * 0.27,
              color: subColor,
            }}
          >
            {sub}
          </span>
        )}
      </div>
    </div>
  );
}
