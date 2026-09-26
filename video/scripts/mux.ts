// Puts the soundtracks on the picture: one video with the music, one with only the sound effects.
// The picture is rendered without sound (`npm run render`) and each soundtrack is added here by ffmpeg, which keeps
// them exactly in sync (Remotion's own AAC audio came out 43 ms late).
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
for (const [sound, video] of [
  ['soundtrack.wav', 'LanguageAutocorrect-promo-with-music.mp4'],
  ['soundtrack-no-music.wav', 'LanguageAutocorrect-promo-no-music.mp4'],
]) {
  const run = spawnSync('ffmpeg', [
    '-v', 'error', '-y', '-i', path.join(root, 'out/picture.mp4'), '-i', path.join(root, 'public', sound),
    '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '320k', '-movflags', '+faststart',
    path.join(root, video),
  ], { stdio: 'inherit' });
  if (run.status !== 0) throw new Error(`ffmpeg could not make ${video}`);
  console.log(video);
}
