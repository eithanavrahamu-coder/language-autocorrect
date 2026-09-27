// Reads the languages from the app's code into src/generated/app-info.json, with the website's own reader
// (website/scripts/prepare.mjs). It reads the last commit rather than the files on disk, so the video shows what is
// saved, not work still in progress (such as a language being added in another window), and it changes nothing
// outside the video folder.
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const repo = path.resolve(root, '..');

function run(command: string, args: string[], cwd = repo): string {
  const result = spawnSync(command, args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
  if (result.status !== 0) throw new Error(`${command} ${args.join(' ')} failed`);
  return result.stdout;
}

const saved = mkdtempSync(path.join(tmpdir(), 'la-video-'));
try {
  const archive = path.join(saved, 'saved.tar');
  run('git', ['archive', '-o', archive, 'HEAD', 'website/scripts', 'src/LanguageAutocorrect.Engine/Languages.cs',
    'src/LanguageAutocorrect.Engine/Data', 'src/LanguageAutocorrect/LanguageAutocorrect.csproj']);
  // A plain file name: some tar versions read "C:" in a path as another computer's name.
  run('tar', ['-xf', 'saved.tar'], saved);
  process.stdout.write(run('node', [path.join(saved, 'website/scripts/prepare.mjs')]));
  mkdirSync(path.join(root, 'src/generated'), { recursive: true });
  copyFileSync(path.join(saved, 'website/src/generated/app-info.json'), path.join(root, 'src/generated/app-info.json'));
} finally {
  rmSync(saved, { recursive: true, force: true });
}
