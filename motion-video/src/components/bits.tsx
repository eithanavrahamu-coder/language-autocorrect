import type { CSSProperties, ReactNode } from 'react';
import { AbsoluteFill, staticFile, useCurrentFrame } from 'remotion';
import { Audio } from '@remotion/media';
import { Minus, Square, X } from 'lucide-react';
import { C, FONT_BADGE, FONT_BODY, LOGO_GRADIENT, lang } from '../theme';

// ---------------------------------------------------------------- logo

/** The app's white mark (the copy in src/LanguageAutocorrect/UI/app.html), with the sparkle on its own so it can twinkle. */
export function LogoMark({ size, twinkle = 0, color = '#fff' }: { size: number; twinkle?: number; color?: string }) {
  const s = 1 + 0.35 * Math.sin(Math.PI * Math.min(1, Math.max(0, twinkle)));
  return (
    <svg width={size} height={size} viewBox="0 0 256 256" style={{ color, display: 'block', overflow: 'visible' }}>
      <path d="M185.6 118A71.8 71.8 0 1 1 138 70.4A40.5 40.5 0 0 1 137.9 86A57 57 0 1 0 170 118.1A40.5 40.5 0 0 1 185.6 118Z" fill="currentColor" />
      <path d="M117 74.6A29.6 64.4 0 0 0 117 203.4A29.6 64.4 0 0 0 117 74.6M52.6 139H181.4" fill="none" stroke="currentColor" strokeWidth={14.7} />
      <g transform={`translate(177.7 78.3) rotate(${twinkle * 90}) scale(${s}) translate(-177.7 -78.3)`}>
        <path d="M177.7 48Q182.5 73.5 208 78.3Q182.5 83.2 177.7 108.7Q172.8 83.2 147.3 78.3Q172.8 73.5 177.7 48Z" fill="currentColor" />
      </g>
    </svg>
  );
}

/** The app icon: the mark on the blue → violet → green rounded square. */
export function AppIcon({ size, twinkle = 0, style }: { size: number; twinkle?: number; style?: CSSProperties }) {
  return (
    <div
      style={{
        width: size, height: size, borderRadius: (size * 58) / 256, background: LOGO_GRADIENT, flexShrink: 0,
        boxShadow: `0 ${size * 0.04}px ${size * 0.1}px rgba(40,30,120,.18), 0 ${size * 0.12}px ${size * 0.3}px -${size * 0.08}px rgba(88,71,224,.45), inset 0 1px 0 rgba(255,255,255,.25)`,
        ...style,
      }}
    >
      <LogoMark size={size} twinkle={twinkle} />
    </div>
  );
}

// ---------------------------------------------------------------- language badge

/** The colored badge the app shows next to the text cursor (26×17 in the app, corners a third of its height). */
export function Badge({ code, h = 40, style }: { code: string; h?: number; style?: CSSProperties }) {
  const l = lang(code);
  return (
    <span
      style={{
        display: 'inline-grid', placeItems: 'center', height: h, minWidth: h * 1.5, padding: `0 ${h * 0.22}px`,
        borderRadius: h / 3, background: l.color, color: '#fff', fontFamily: FONT_BADGE, fontWeight: 700,
        fontSize: h * 0.5, lineHeight: 1, whiteSpace: 'nowrap', flexShrink: 0,
        boxShadow: `0 ${h * 0.08}px ${h * 0.25}px -${h * 0.05}px ${l.color}88`,
        ...style,
      }}
    >
      {l.badge}
    </span>
  );
}

/**
 * Turns from one thing to another like a split-flap sign: the old one tips away, the new one tips in.
 * `p` goes 0 → 1.
 */
export function Flip({ from, to, p, style }: { from: ReactNode; to: ReactNode; p: number; style?: CSSProperties }) {
  const first = p < 0.5;
  const a = first ? p * 2 * 90 : (1 - (p - 0.5) * 2) * -90;
  return (
    <span style={{ display: 'inline-block', perspective: 600, ...style }}>
      <span
        style={{
          display: 'inline-block', transform: `rotateX(${a}deg)`, transformOrigin: '50% 55%',
          opacity: 1 - Math.abs(a) / 140,
        }}
      >
        {first ? from : to}
      </span>
    </span>
  );
}

// ---------------------------------------------------------------- keys, caret, pointer

export function Keycap({
  label, w = 120, h = 104, press = 0, dark = false, glow = 0, fontSize, style,
}: {
  label: ReactNode; w?: number; h?: number; press?: number; dark?: boolean; glow?: number; fontSize?: number; style?: CSSProperties;
}) {
  const depth = h * 0.09;
  return (
    <div style={{ width: w, height: h, position: 'relative', flexShrink: 0, ...style }}>
      <div style={{ position: 'absolute', inset: 0, top: depth, borderRadius: h * 0.2, background: dark ? '#0A0A09' : '#D8D2C8' }} />
      <div
        style={{
          position: 'absolute', left: 0, right: 0, top: press * depth * 0.85, height: h - depth, borderRadius: h * 0.2,
          background: dark ? '#2A2A27' : '#FFFFFF', border: `1.5px solid ${dark ? '#3A3936' : C.line}`,
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 10,
          color: dark ? C.darkText : C.text, fontFamily: FONT_BODY, fontWeight: 600, fontSize: fontSize ?? h * 0.3,
          boxShadow: glow ? `0 0 0 ${3 + glow * 4}px ${C.accent}${Math.round(glow * 80).toString(16).padStart(2, '0')}, 0 0 ${40 * glow}px ${C.accent}88` : undefined,
        }}
      >
        {label}
      </div>
    </div>
  );
}

export function Caret({ h, on, color = C.text, style }: { h: number; on: boolean; color?: string; style?: CSSProperties }) {
  return (
    <span
      style={{
        display: 'inline-block', width: Math.max(3, h * 0.045), height: h, background: color, borderRadius: 2,
        opacity: on ? 1 : 0, verticalAlign: 'middle', ...style,
      }}
    />
  );
}

/** The Windows arrow pointer, tip at (x, y). `press` 0 → 1 squeezes it a little for a click. */
export function Pointer({ x, y, press = 0, opacity = 1, scale = 1.25 }: { x: number; y: number; press?: number; opacity?: number; scale?: number }) {
  return (
    <svg
      width={34 * scale} height={48 * scale} viewBox="0 0 34 48"
      style={{
        position: 'absolute', left: x - 3 * scale, top: y - 2 * scale, opacity, overflow: 'visible',
        transform: `scale(${1 - press * 0.12})`, transformOrigin: '3px 2px',
        filter: 'drop-shadow(0 4px 6px rgba(0,0,0,.28))', zIndex: 50,
      }}
    >
      <path d="M3 2V36.5L11.6 28.4L17.2 41.4L23.4 38.7L17.9 26.1H29.6Z" fill="#fff" stroke="#111" strokeWidth={2.2} strokeLinejoin="round" />
    </svg>
  );
}

/** An expanding ring where something was clicked. */
export function ClickRing({ x, y, t, color = C.accent }: { x: number; y: number; t: number; color?: string }) {
  if (t < 0 || t > 18) return null;
  const p = t / 18;
  const r = 14 + p * 60;
  return (
    <div
      style={{
        position: 'absolute', left: x - r, top: y - r, width: r * 2, height: r * 2, borderRadius: '50%',
        border: `${4 * (1 - p) + 1}px solid ${color}`, opacity: 1 - p, zIndex: 49,
      }}
    />
  );
}

// ---------------------------------------------------------------- window

export function Window({
  title, icon, width, height, children, style, bodyStyle, dark = false,
}: {
  title?: ReactNode; icon?: ReactNode; width: number; height: number; children: ReactNode; style?: CSSProperties;
  bodyStyle?: CSSProperties; dark?: boolean;
}) {
  const fg = dark ? C.darkMuted : C.muted;
  return (
    <div
      style={{
        width, height, borderRadius: 26, overflow: 'hidden', display: 'flex', flexDirection: 'column',
        background: dark ? C.darkCard : C.card, border: `1px solid ${dark ? C.darkLine : C.line}`,
        boxShadow: dark
          ? '0 0 0 1px rgba(255,255,255,.03), 0 40px 90px -20px rgba(0,0,0,.7)'
          : '0 1px 1px rgba(20,18,15,.04), 0 18px 40px -12px rgba(20,18,15,.16), 0 60px 120px -40px rgba(40,30,90,.28)',
        ...style,
      }}
    >
      {title !== undefined && (
        <div
          style={{
            height: 58, flexShrink: 0, display: 'flex', alignItems: 'center', gap: 12, padding: '0 22px',
            borderBottom: `1px solid ${dark ? C.darkLine : C.line}`, color: fg, fontFamily: FONT_BODY, fontSize: 21,
          }}
        >
          {icon}
          <span>{title}</span>
          <span style={{ marginLeft: 'auto', display: 'flex', gap: 30, alignItems: 'center', opacity: 0.8 }}>
            <Minus size={20} strokeWidth={1.6} />
            <Square size={16} strokeWidth={1.6} />
            <X size={20} strokeWidth={1.6} />
          </span>
        </div>
      )}
      <div style={{ flex: 1, position: 'relative', ...bodyStyle }}>{children}</div>
    </div>
  );
}

// ---------------------------------------------------------------- backgrounds

/** The light background: warm paper with slow-moving colored light and a faint dot grid. */
export function Backdrop({ tint = 1 }: { tint?: number }) {
  const f = useCurrentFrame();
  const t = f / 30;
  const x1 = 380 + Math.sin(t * 0.35) * 160, y1 = 260 + Math.cos(t * 0.3) * 90;
  const x2 = 1560 + Math.cos(t * 0.28) * 170, y2 = 380 + Math.sin(t * 0.33) * 120;
  const x3 = 1000 + Math.sin(t * 0.22 + 1) * 260, y3 = 900 + Math.cos(t * 0.26) * 80;
  return (
    <AbsoluteFill
      style={{
        background: [
          `radial-gradient(760px 560px at ${x1}px ${y1}px, rgba(37,99,235,${0.11 * tint}), transparent 70%)`,
          `radial-gradient(820px 620px at ${x2}px ${y2}px, rgba(88,71,224,${0.13 * tint}), transparent 70%)`,
          `radial-gradient(900px 520px at ${x3}px ${y3}px, rgba(22,163,74,${0.08 * tint}), transparent 70%)`,
          C.bg,
        ].join(','),
      }}
    >
      <AbsoluteFill
        style={{
          backgroundImage: 'radial-gradient(rgba(27,26,24,.075) 1.3px, transparent 1.6px)',
          backgroundSize: '34px 34px',
          backgroundPosition: `${(f * 0.2) % 34}px 0px`,
          maskImage: 'radial-gradient(ellipse 75% 70% at 50% 50%, #000 30%, transparent 100%)',
        }}
      />
    </AbsoluteFill>
  );
}

// ---------------------------------------------------------------- sparkles

const STAR = 'M0 -1Q.19 -.19 1 0Q.19 .19 0 1Q-.19 .19 -1 0Q-.19 -.19 0 -1Z';

/** A burst of little four-point stars flying out from (x, y), starting `t` frames ago; `inner` keeps them clear of the middle. */
export function Burst({ x, y, t, count = 10, radius = 170, inner = 0, colors = [C.accent, '#F5B941', '#2563EB', '#16A34A'], size = 22, seed = 1 }: {
  x: number; y: number; t: number; count?: number; radius?: number; inner?: number; colors?: string[]; size?: number; seed?: number;
}) {
  if (t < 0 || t > 30) return null;
  const p = t / 30;
  const ease = 1 - (1 - p) ** 3;
  return (
    <svg style={{ position: 'absolute', left: 0, top: 0, overflow: 'visible' }} width={1} height={1}>
      {Array.from({ length: count }, (_, i) => {
        const r = (((i * 73 + seed * 31) % 100) / 100) * 0.5 + 0.6;
        const a = (i / count) * Math.PI * 2 + seed;
        const d = inner + (radius - inner) * r * ease;
        const s = size * (0.6 + r * 0.6) * (1 - p) * Math.min(1, t / 4);
        return (
          <path
            key={i} d={STAR} fill={colors[i % colors.length]}
            transform={`translate(${x + Math.cos(a) * d} ${y + Math.sin(a) * d}) rotate(${p * 120}) scale(${s})`}
          />
        );
      })}
    </svg>
  );
}

// ---------------------------------------------------------------- sound

export type SfxName =
  | 'key-1' | 'key-2' | 'key-3' | 'key-4' | 'key-5' | 'key-6' | 'space' | 'enter' | 'backspace'
  | 'fix' | 'switch' | 'pop-1' | 'pop-2' | 'pop-3' | 'pop-4' | 'pop-5' | 'pop-6'
  | 'whoosh' | 'whoosh-big' | 'error' | 'click' | 'thud' | 'thud-high' | 'impact' | 'shimmer' | 'success'
  | 'tick' | 'toggle' | 'swipe' | 'smash';

/** One sound effect from public/audio (made by scripts/make-audio.mjs), starting at frame `at` of its scene. */
export function Sfx({ name, at, volume = 1 }: { name: SfxName; at: number; volume?: number }) {
  return <Audio src={staticFile(`audio/${name}.wav`)} from={Math.round(at)} volume={volume} name={name} />;
}

const KEY_ORDER: SfxName[] = ['key-1', 'key-4', 'key-2', 'key-6', 'key-3', 'key-5', 'key-2', 'key-1', 'key-5'];
const KEY_LEVEL = [1, 0.85, 0.95, 0.8, 1, 0.9, 0.85];

/** A click for every keystroke; spaces get the space bar's deeper sound. */
export function TypingSounds({ times, text, volume = 0.5 }: { times: number[]; text?: string; volume?: number }) {
  return (
    <>
      {times.map((t, i) => (
        <Sfx key={i} name={text?.[i] === ' ' ? 'space' : KEY_ORDER[i % KEY_ORDER.length]} at={t} volume={volume * KEY_LEVEL[i % KEY_LEVEL.length]} />
      ))}
    </>
  );
}
