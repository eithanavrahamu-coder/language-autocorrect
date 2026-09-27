import { readFile } from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');

/** The app's version, read from its project file so the website never shows a stale number. */
export async function appVersion() {
  const csproj = await readFile(path.join(root, 'src/LanguageAutocorrect/LanguageAutocorrect.csproj'), 'utf8');
  const m = csproj.match(/<Version>([^<]+)<\/Version>/);
  if (!m) throw new Error('No <Version> in LanguageAutocorrect.csproj');
  return m[1];
}
