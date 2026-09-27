import info from './generated/app-info.json';
import { LANGUAGES } from './demo/keyboard';

export const VERSION = info.version;
export const DOWNLOAD_URL = `${import.meta.env.BASE_URL}LanguageAutocorrect.exe`;
export const DOWNLOAD_SIZE = info.downloadBytes ? `${Math.round(info.downloadBytes / 1048576)} MB` : null;
export const REPO_URL = 'https://github.com/eithanavrahamu-coder/language-autocorrect';
/** Where people write about privacy (and anything else that shouldn't be a public GitHub issue). */
export const CONTACT_EMAIL = 'foldrobotics@gmail.com';
export const LANGUAGE_COUNT = LANGUAGES.length;

/** "Version 3.3.0 · 68 MB" */
export const downloadMeta = [`Version ${VERSION}`, DOWNLOAD_SIZE].filter(Boolean).join(' · ');
