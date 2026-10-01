// Draws the app's logo (a globe with a sparkle on the blue → violet → green gradient) and saves every icon file:
// the app's .ico, the Mac app's .icns and the website's icons.  npm run icons
// app.html, setup.html and the video's Outro carry a copy of the white mark this prints; paste it there if it changes.
import { chromium } from 'playwright-core';
import { writeFile } from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const r1 = (n) => Math.round(n * 10) / 10;

/**
 * The white mark on a 256 grid. The globe's rim is a filled ring with a round notch cut out around the sparkle
 * (a plain path, no masks, so the same markup works inline in the app's pages and in the video). Sizes of 24 px
 * and less get the globe alone with thinner lines, where a sparkle would only be a smudge.
 */
function mark(small = false) {
  if (small) {
    return `<g fill="none" stroke="currentColor" stroke-width="20"><circle cx="128" cy="128" r="86"/>` +
      `<path d="M128 42A43 86 0 0 0 128 214A43 86 0 0 0 128 42M42 128H214"/></g>`;
  }
  const s = 0.92; // the whole mark, scaled around the middle
  const X = (x) => r1(128 + (x - 128) * s), Y = (y) => r1(128 + (y - 128) * s), L = (l) => r1(l * s);
  const gx = 116, gy = 140, R = 70, w = 16; // globe
  const sx = 182, sy = 74, a = 33, k = 0.16; // sparkle: center, arm length, how far its sides curve in
  const G = 44; // radius of the notch around the sparkle
  const d = Math.hypot(sx - gx, sy - gy), toward = Math.atan2(sy - gy, sx - gx);
  const cut = (r) => Math.acos((r * r + d * d - G * G) / (2 * r * d)); // where a circle of radius r meets the notch
  const at = (r, t) => `${X(gx + r * Math.cos(t))} ${Y(gy + r * Math.sin(t))}`;
  const Ro = R + w / 2, Ri = R - w / 2, co = cut(Ro), ci = cut(Ri);
  const rim = `M${at(Ro, toward + co)}A${L(Ro)} ${L(Ro)} 0 1 1 ${at(Ro, toward - co)}` +
    `A${L(G)} ${L(G)} 0 0 1 ${at(Ri, toward - ci)}A${L(Ri)} ${L(Ri)} 0 1 0 ${at(Ri, toward + ci)}` +
    `A${L(G)} ${L(G)} 0 0 1 ${at(Ro, toward + co)}Z`;
  const lens = R * 0.46;
  const lines = `M${X(gx)} ${Y(gy - R)}A${L(lens)} ${L(R)} 0 0 0 ${X(gx)} ${Y(gy + R)}` +
    `A${L(lens)} ${L(R)} 0 0 0 ${X(gx)} ${Y(gy - R)}M${X(gx - R)} ${Y(gy)}H${X(gx + R)}`;
  const q = (dx, dy) => `${X(sx + dx * k * a)} ${Y(sy + dy * k * a)}`;
  const sparkle = `M${X(sx)} ${Y(sy - a)}Q${q(1, -1)} ${X(sx + a)} ${Y(sy)}Q${q(1, 1)} ${X(sx)} ${Y(sy + a)}` +
    `Q${q(-1, 1)} ${X(sx - a)} ${Y(sy)}Q${q(-1, -1)} ${X(sx)} ${Y(sy - a)}Z`;
  return `<path d="${rim}${sparkle}" fill="currentColor"/>` +
    `<path d="${lines}" fill="none" stroke="currentColor" stroke-width="${L(w)}"/>`;
}

const icon = (size) => `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 256 256" color="#fff">
  <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#2563EB"/><stop offset=".58" stop-color="#5847E0"/><stop offset="1" stop-color="#16A34A"/>
  </linearGradient></defs>
  <rect width="256" height="256" rx="58" fill="url(#g)"/>${mark(size <= 24)}</svg>`;

/**
 * The Mac app's icon: the same square, drawn the way Mac icons are, a little smaller than the canvas with a soft
 * shadow below (824 of 1024). `small`: shown at 32 points or less, so the globe alone.
 */
const macIcon = (px, small) => `<svg xmlns="http://www.w3.org/2000/svg" width="${px}" height="${px}" viewBox="0 0 1024 1024" color="#fff">
  <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="#2563EB"/><stop offset=".58" stop-color="#5847E0"/><stop offset="1" stop-color="#16A34A"/>
  </linearGradient>
  <filter id="s" x="-20%" y="-20%" width="140%" height="140%">
    <feDropShadow dx="0" dy="12" stdDeviation="14" flood-color="#000" flood-opacity=".28"/>
  </filter></defs>
  <rect x="100" y="100" width="824" height="824" rx="185" fill="url(#g)" filter="url(#s)"/>
  <g transform="translate(100 100) scale(3.21875)">${mark(small)}</g></svg>`;

const browser = await chromium.launch({ channel: 'msedge' });
async function render(svg, size) {
  const page = await browser.newPage({ viewport: { width: size, height: size } });
  await page.setContent(`<style>body { margin: 0 } svg { display: block }</style>${svg}`);
  const png = await page.screenshot({ type: 'png', omitBackground: true });
  await page.close();
  return png;
}
const pngs = new Map();
for (const size of [16, 20, 24, 32, 40, 48, 64, 128, 256]) pngs.set(size, await render(icon(size), size));

// An .icns is a list of PNGs, each with a four-letter type: [type, pixels, points it's shown at].
const macEntries = [
  ['icp4', 16, 16], ['ic11', 32, 16], ['icp5', 32, 32], ['ic12', 64, 32], ['ic07', 128, 128], ['ic13', 256, 128],
  ['ic08', 256, 256], ['ic14', 512, 256], ['ic09', 512, 512], ['ic10', 1024, 512],
];
const macPngs = new Map();
const macParts = [];
for (const [type, px, points] of macEntries) {
  const key = `${px}/${points <= 32}`;
  if (!macPngs.has(key)) macPngs.set(key, await render(macIcon(px, points <= 32), px));
  const png = macPngs.get(key);
  const head = Buffer.alloc(8);
  head.write(type, 0, 'latin1');
  head.writeUInt32BE(8 + png.length, 4);
  macParts.push(head, png);
}
await browser.close();
const icnsBody = Buffer.concat(macParts);
const icnsHead = Buffer.alloc(8);
icnsHead.write('icns', 0, 'latin1');
icnsHead.writeUInt32BE(8 + icnsBody.length, 4);

// An .ico is a small directory of images; Windows reads PNGs inside it for every size.
const header = Buffer.alloc(6 + 16 * pngs.size);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(pngs.size, 4);
let offset = header.length, i = 0;
for (const [size, png] of pngs) {
  const e = 6 + 16 * i++;
  header.writeUInt8(size % 256, e);
  header.writeUInt8(size % 256, e + 1);
  header.writeUInt16LE(1, e + 4);
  header.writeUInt16LE(32, e + 6);
  header.writeUInt32LE(png.length, e + 8);
  header.writeUInt32LE(offset, e + 12);
  offset += png.length;
}

const files = [
  ['src/LanguageAutocorrect/Assets/LanguageAutocorrect.ico', Buffer.concat([header, ...pngs.values()])],
  ['website/public/icon.png', pngs.get(256)],
  ['website/public/favicon.png', pngs.get(32)],
  ['website/src/assets/icon-128.png', pngs.get(128)],
  ['src/LanguageAutocorrect.Mac/Assets/AppIcon.icns', Buffer.concat([icnsHead, icnsBody])],
];
for (const [file, data] of files) {
  await writeFile(path.join(root, file), data);
  console.log('saved', file);
}
console.log('\nThe mark, for app.html, setup.html and the video:\n' + mark());
