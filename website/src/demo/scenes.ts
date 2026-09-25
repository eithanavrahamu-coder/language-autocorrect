// The examples the hero demo plays. Each fix was checked with the app's own engine
// (typing the keys on the start keyboard with both languages on gives exactly `fixed`).
export type Scene = {
  /** The language the example is about (its chip). */
  lang: string;
  /** Keyboard on when typing starts. */
  start: string;
  /** Physical keys typed on the wrong keyboard (US names). */
  keys: string;
  /** What the app turns them into when Space is pressed. */
  fixed: string;
  /** Keyboard after the fix. */
  to: string;
  /** Keys typed afterwards, now on the right keyboard. */
  then?: string;
};

export const SCENES: Scene[] = [
  { lang: 'ru', start: 'en', keys: 'ghbdtn', fixed: 'привет', to: 'ru', then: 'rfr ltkf' },
  { lang: 'he', start: 'en', keys: 'akuo', fixed: 'שלום', to: 'he', then: 'nv akunl' },
  { lang: 'en', start: 'he', keys: 'hello', fixed: 'hello', to: 'en', then: 'there' },
  { lang: 'ar', start: 'en', keys: 'lvpfh', fixed: 'مرحبا', to: 'ar' },
  { lang: 'el', start: 'en', keys: 'kalhm;era', fixed: 'καλημέρα', to: 'el' },
  { lang: 'de', start: 'en', keys: 'Yeit', fixed: 'Zeit', to: 'de' },
  { lang: 'ko', start: 'en', keys: 'dkssud', fixed: '안녕', to: 'ko' },
  { lang: 'th', start: 'en', keys: 'l;ylfu', fixed: 'สวัสดี', to: 'th' },
  { lang: 'fa', start: 'en', keys: 'sghl', fixed: 'سلام', to: 'fa' },
  { lang: 'ka', start: 'en', keys: 'gamarjoba', fixed: 'გამარჯობა', to: 'ka' },
];

/** A word to suggest in "try it yourself", with the keys that type it. */
export const TRY_WORDS: Record<string, { word: string; keys: string }> = {
  he: { word: 'שלום', keys: 'akuo' },
  ru: { word: 'привет', keys: 'ghbdtn' },
  uk: { word: 'привіт', keys: 'ghbdsn' },
  ar: { word: 'مرحبا', keys: 'lvpfh' },
  fa: { word: 'سلام', keys: 'sghl' },
  el: { word: 'γεια', keys: 'geia' },
};
