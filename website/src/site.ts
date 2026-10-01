import info from './generated/app-info.json';
import { LANGUAGES } from './demo/keyboard';
import type { Platform } from './platform';

export const VERSION = info.version;
export const REPO_URL = 'https://github.com/eithanavrahamu-coder/language-autocorrect';
/** Where people write about privacy (and anything else that shouldn't be a public GitHub issue). */
export const CONTACT_EMAIL = 'foldrobotics@gmail.com';
export const LANGUAGE_COUNT = LANGUAGES.length;

const megabytes = (bytes: number | null | undefined) => (bytes ? `${Math.round(bytes / 1048576)} MB` : null);

/**
 * The downloads: this version's GitHub release, which the website build creates, so GitHub counts the downloads (see
 * /downloads/). The Windows app's own updates use the copy next to this page instead (version.json), so they aren't
 * counted.
 */
export const DOWNLOADS: Record<Platform, {
  /** "Windows", "Mac" */
  name: string;
  file: string;
  url: string;
  size: string | null;
  /** What it runs on, for the line under the button. */
  needs: string;
  beta: boolean;
}> = {
  windows: {
    name: 'Windows',
    file: 'LanguageAutocorrect.exe',
    url: `${REPO_URL}/releases/download/v${VERSION}/LanguageAutocorrect.exe`,
    size: megabytes(info.downloadBytes),
    needs: 'Windows 10 & 11',
    beta: false,
  },
  mac: {
    name: 'Mac',
    file: 'LanguageAutocorrect.dmg',
    url: `${REPO_URL}/releases/download/v${VERSION}/LanguageAutocorrect.dmg`,
    size: megabytes(info.macDownloadBytes),
    needs: 'macOS 14 or newer',
    beta: true,
  },
};

/** "Version 3.3.0 · 68 MB · Windows 10 & 11" */
export const downloadMeta = (platform: Platform) => {
  const d = DOWNLOADS[platform];
  return [`Version ${VERSION}`, d.size, d.needs].filter(Boolean).join(' · ');
};
