import { loadFont } from '@remotion/fonts';
import bricolageLatin from '@fontsource-variable/bricolage-grotesque/files/bricolage-grotesque-latin-standard-normal.woff2';
import onestLatin from '@fontsource-variable/onest/files/onest-latin-wght-normal.woff2';
import onestCyrillic from '@fontsource-variable/onest/files/onest-cyrillic-wght-normal.woff2';

// The website's fonts (website/src/index.css). Hebrew, Arabic, Korean, Thai and the rest fall back to Windows' own
// Segoe UI family, like they do in the app.
const LATIN = 'U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD';
const CYRILLIC = 'U+0301,U+0400-045F,U+0490-0491,U+04B0-04B1,U+2116';
loadFont({ family: 'Bricolage Grotesque', url: bricolageLatin, weight: '200 800', unicodeRange: LATIN });
loadFont({ family: 'Onest', url: onestLatin, weight: '100 900', unicodeRange: LATIN });
loadFont({ family: 'Onest', url: onestCyrillic, weight: '100 900', unicodeRange: CYRILLIC });

export const FONT_DISPLAY = "'Bricolage Grotesque', 'Onest', 'Segoe UI Variable Display', 'Segoe UI', sans-serif";
export const FONT_BODY = "'Onest', 'Segoe UI Variable Text', 'Segoe UI', 'Malgun Gothic', 'Leelawadee UI', sans-serif";
/** The app draws its badges in bold Segoe UI. */
export const FONT_BADGE = "'Segoe UI', 'Malgun Gothic', 'Leelawadee UI', sans-serif";

/** The app's own colors (src/LanguageAutocorrect/UI/app.html), the same ones the website uses. */
export const C = {
  bg: '#FBFAF8',
  bg2: '#F4F2EE',
  card: '#FFFFFF',
  text: '#1B1A18',
  muted: '#75706A',
  faint: '#A7A29B',
  line: '#E9E5DF',
  accent: '#5847E0',
  accentSoft: '#EEEBFF',
  green: '#16A34A',
  red: '#E5484D',
  dark: '#121211',
  darkCard: '#1D1D1B',
  darkLine: '#2F2E2B',
  darkText: '#F2F0EC',
  darkMuted: '#A29E97',
};

/** The logo's gradient (website/scripts/icons.mjs). */
export const LOGO_GRADIENT = 'linear-gradient(135deg, #2563EB 0%, #5847E0 58%, #16A34A 100%)';
/** The warm side panel of the app and setup windows. */
export const WARM_GRADIENT = 'linear-gradient(160deg, #FDBA4D 0%, #FF8A5B 45%, #FF5C7A 100%)';

export type Lang = { code: string; name: string; native: string; badge: string; color: string };

/** A copy of the language registry in src/LanguageAutocorrect.Engine/Languages.cs (name, badge and color). */
export const LANGS: Lang[] = [
  { code: 'en', name: 'English', native: 'English', badge: 'EN', color: '#2563EB' },
  { code: 'he', name: 'Hebrew', native: 'עברית', badge: 'עב', color: '#16A34A' },
  { code: 'ru', name: 'Russian', native: 'Русский', badge: 'РУ', color: '#DC2626' },
  { code: 'ar', name: 'Arabic', native: 'العربية', badge: 'ع', color: '#0D9488' },
  { code: 'uk', name: 'Ukrainian', native: 'Українська', badge: 'УК', color: '#CA8A04' },
  { code: 'fa', name: 'Persian', native: 'فارسی', badge: 'فا', color: '#9333EA' },
  { code: 'el', name: 'Greek', native: 'Ελληνικά', badge: 'ΕΛ', color: '#0284C7' },
  { code: 'fr', name: 'French', native: 'Français', badge: 'FR', color: '#4F46E5' },
  { code: 'de', name: 'German', native: 'Deutsch', badge: 'DE', color: '#EA580C' },
  { code: 'bg', name: 'Bulgarian', native: 'Български', badge: 'БГ', color: '#047857' },
  { code: 'sr', name: 'Serbian', native: 'Српски', badge: 'СР', color: '#BE123C' },
  { code: 'mk', name: 'Macedonian', native: 'Македонски', badge: 'МК', color: '#B45309' },
  { code: 'kk', name: 'Kazakh', native: 'Қазақша', badge: 'ҚЗ', color: '#0E7490' },
  { code: 'ka', name: 'Georgian', native: 'ქართული', badge: 'ქა', color: '#A21CAF' },
  { code: 'hy', name: 'Armenian', native: 'Հայերեն', badge: 'ՀԱ', color: '#4D7C0F' },
  { code: 'ko', name: 'Korean', native: '한국어', badge: '한', color: '#475569' },
  { code: 'th', name: 'Thai', native: 'ไทย', badge: 'ไท', color: '#DB2777' },
  { code: 'es', name: 'Spanish', native: 'Español', badge: 'ES', color: '#D97706' },
  { code: 'pt', name: 'Portuguese', native: 'Português', badge: 'PT', color: '#15803D' },
  { code: 'tr', name: 'Turkish', native: 'Türkçe', badge: 'TR', color: '#991B1B' },
  { code: 'it', name: 'Italian', native: 'Italiano', badge: 'IT', color: '#7C3AED' },
  { code: 'ur', name: 'Urdu', native: 'اردو', badge: 'ار', color: '#166534' },
];
export const lang = (code: string) => LANGS.find((l) => l.code === code)!;
