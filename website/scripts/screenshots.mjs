// Takes screenshots of the app's own window pages (their browser preview with sample data) for the website.
// Run on Windows, so the pages render in Segoe UI like the real app:  npm run screenshots
import { chromium } from 'playwright-core';
import { mkdir, mkdtemp, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { appVersion } from './app-info.mjs';

const root = path.resolve(import.meta.dirname, '../..');
const ui = path.join(root, 'src/LanguageAutocorrect/UI');
const out = path.join(root, 'website/src/assets/screens');
const version = await appVersion();

// The previews carry old sample version numbers; show the current one.
const tmp = await mkdtemp(path.join(tmpdir(), 'la-screens-'));
for (const [file, from] of [['app.html', /version: '[\d.]+'/], ['setup.html', /version: '[\d.]+'/]]) {
  const html = (await readFile(path.join(ui, file), 'utf8')).replace(from, `version: '${version}'`);
  await writeFile(path.join(tmp, file), html);
}

const shots = [
  { name: 'home', file: 'app.html', query: '', size: [980, 680] },
  { name: 'words', file: 'app.html', query: '?page=words', size: [980, 680] },
  { name: 'settings', file: 'app.html', query: '?page=settings', size: [980, 680] },
  { name: 'setup', file: 'setup.html', query: '', size: [960, 640] },
];

await mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'msedge' });
const encoder = await browser.newPage();
for (const scheme of ['light', 'dark']) {
  // Reduced motion shows the setup's typing demo finished (a word already fixed) instead of mid-typing.
  const context = await browser.newContext({ deviceScaleFactor: 2, colorScheme: scheme, reducedMotion: 'reduce' });
  // Home greets by the time of day; keep it the same whenever the screenshots are taken.
  await context.clock.setFixedTime(new Date(2026, 8, 1, 14, 35));
  for (const s of shots) {
    const page = await context.newPage();
    await page.setViewportSize({ width: s.size[0], height: s.size[1] });
    await page.goto(pathToFileURL(path.join(tmp, s.file)).href + s.query);
    await page.waitForTimeout(400);
    const png = await page.screenshot({ type: 'png' });
    await page.close();
    // Chromium encodes WebP, so no image library is needed.
    const webp = await encoder.evaluate(async (b64) => {
      const img = new Image();
      img.src = 'data:image/png;base64,' + b64;
      await img.decode();
      const c = document.createElement('canvas');
      c.width = img.width; c.height = img.height;
      c.getContext('2d').drawImage(img, 0, 0);
      return c.toDataURL('image/webp', 0.92).split(',')[1];
    }, png.toString('base64'));
    const file = path.join(out, `${s.name}-${scheme}.webp`);
    await writeFile(file, Buffer.from(webp, 'base64'));
    console.log('saved', path.relative(root, file));
  }
  await context.close();
}
await browser.close();
