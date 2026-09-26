// Renders single frames, to look at: `npm run stills -- 1.5 2.62 8.4` saves out/at-1.50.png and so on (seconds).
import path from 'node:path';
import { bundle } from '@remotion/bundler';
import { openBrowser, renderStill, selectComposition } from '@remotion/renderer';
import { FPS } from '../src/timeline.ts';

const root = path.resolve(import.meta.dirname, '..');
const times = process.argv.slice(2).map(Number).filter(t => !Number.isNaN(t));
if (!times.length) throw new Error('Give the moments to render, in seconds: npm run stills -- 1.5 2.62');

const serveUrl = await bundle({ entryPoint: path.join(root, 'src/index.ts') });
const browser = await openBrowser('chrome');
const composition = await selectComposition({ serveUrl, id: 'Promo', puppeteerInstance: browser });
for (const t of times) {
  const output = path.join(root, `out/at-${t.toFixed(2)}.png`);
  await renderStill({ composition, serveUrl, output, frame: Math.round(t * FPS), puppeteerInstance: browser });
  console.log(path.relative(root, output));
}
await browser.close({ silent: true });
