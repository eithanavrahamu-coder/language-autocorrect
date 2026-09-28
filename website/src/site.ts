import info from './generated/app-info.json';
import { LANGUAGES } from './demo/keyboard';

export const VERSION = info.version;
export const REPO_URL = 'https://github.com/eithanavrahamu-coder/language-autocorrect';
/**
 * The download: this version's GitHub release, which the website build creates, so GitHub counts the downloads (see
 * /downloads/). The app's own updates use the copy next to this page instead (version.json), so they aren't counted.
 */
export const DOWNLOAD_URL = `${REPO_URL}/releases/download/v${VERSION}/LanguageAutocorrect.exe`;
export const DOWNLOAD_SIZE = info.downloadBytes ? `${Math.round(info.downloadBytes / 1048576)} MB` : null;
/** Where people write about privacy (and anything else that shouldn't be a public GitHub issue). */
export const CONTACT_EMAIL = 'foldrobotics@gmail.com';
export const LANGUAGE_COUNT = LANGUAGES.length;

/** "Version 3.3.0 · 68 MB" */
export const downloadMeta = [`Version ${VERSION}`, DOWNLOAD_SIZE].filter(Boolean).join(' · ');
