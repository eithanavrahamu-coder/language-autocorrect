import info from './generated/app-info.json';

/** The app's own colors (src/LanguageAutocorrect/UI/app.html), as on the website. */
export const C = {
  bg: '#FBFAF8',
  bg2: '#F4F2EE',
  card: '#FFFFFF',
  text: '#1B1A18',
  muted: '#75706A',
  faint: '#A7A29B',
  line: '#E9E5DF',
  hover: '#EDEAE5',
  accent: '#5847E0',
  accentSoft: '#EEEBFF',
  en: '#2563EB',
  he: '#16A34A',
  danger: '#D64545',
};

// The website's fonts; letters they don't have (Hebrew, Arabic, Thai, Korean...) come from Windows' own fonts.
const fallback = "'Segoe UI Variable Text', 'Segoe UI', 'Leelawadee UI', 'Malgun Gothic', sans-serif";
export const FONT_DISPLAY = `'Bricolage Grotesque Variable', 'Onest Variable', ${fallback}`;
export const FONT_BODY = `'Onest Variable', ${fallback}`;
/** The badge's font, as the app draws it (src/LanguageAutocorrect/BadgeArt.cs). */
export const FONT_BADGE = "'Segoe UI', 'Leelawadee UI', 'Malgun Gothic', sans-serif";

export type Language = (typeof info.languages)[number];

/** Read from the app's code by scripts/data.ts (`npm run data`). */
export const LANGUAGES: Language[] = info.languages;
export const byCode = (code: string): Language => {
  const lang = LANGUAGES.find(l => l.code === code);
  if (!lang) throw new Error(`No language ${code}`);
  return lang;
};

/** `color` mixed with `other` by `amount` (0–1), like CSS color-mix(), at opacity `a`. */
export function mix(color: string, other: string, amount: number, a = 1): string {
  const x = hex(color), y = hex(other);
  const c = x.map((v, i) => Math.round(v + (y[i] - v) * amount));
  return `rgba(${c.join(', ')}, ${a})`;
}

/** `color` at `alpha` opacity. */
export function alpha(color: string, a: number): string {
  return `rgba(${hex(color).join(', ')}, ${a})`;
}

function hex(color: string): number[] {
  const h = color.replace('#', '');
  return [0, 2, 4].map(i => parseInt(h.slice(i, i + 2), 16));
}
