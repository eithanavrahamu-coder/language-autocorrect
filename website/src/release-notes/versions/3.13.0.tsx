import { Globe } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import './3.13.0.css';

// The logo's white mark, as in app.html: the globe's rim, its sparkle, and the lines across the globe.
const RIM = 'M185.6 118A71.8 71.8 0 1 1 138 70.4A40.5 40.5 0 0 1 137.9 86A57 57 0 1 0 170 118.1A40.5 40.5 0 0 1 185.6 118Z';
const SPARKLE = 'M177.7 48Q182.5 73.5 208 78.3Q182.5 83.2 177.7 108.7Q172.8 83.2 147.3 78.3Q172.8 73.5 177.7 48Z';
const MERIDIAN = 'M117 74.6A29.6 64.4 0 0 0 117 203.4A29.6 64.4 0 0 0 117 74.6';
const EQUATOR = 'M52.6 139H181.4';
const STAR = 'M12 0Q13 11 24 12Q13 13 12 24Q11 13 0 12Q11 11 12 0Z';

function Art() {
  return (
    <div className="v3-13-0">
      <div className="icon">
        <svg viewBox="0 0 256 256" width="136" height="136">
          <defs>
            <linearGradient id="v3-13-0-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor="#2563EB" />
              <stop offset=".58" stopColor="#5847E0" />
              <stop offset="1" stopColor="#16A34A" />
            </linearGradient>
          </defs>
          <rect width="256" height="256" rx="58" fill="url(#v3-13-0-gradient)" />
          <path d={RIM} fill="#fff" />
          <path className="sparkle" d={SPARKLE} fill="#fff" />
          <g fill="none" stroke="#fff" strokeWidth="14.7">
            <path className="line meridian" d={MERIDIAN} pathLength={1} />
            <path className="line equator" d={EQUATOR} pathLength={1} />
          </g>
        </svg>
        <span className="sheen" />
      </div>
      {['one', 'two', 'three'].map(n => (
        <svg key={n} className={`star ${n}`} viewBox="0 0 24 24"><path d={STAR} fill="currentColor" /></svg>
      ))}
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: Globe,
  title: 'A new logo',
  text: (
    <p>
      Language Autocorrect has a new look: a globe with a sparkle, on a blue-to-green gradient. You’ll see it on the
      taskbar, in the Start menu, next to the clock and on the website.
    </p>
  ),
  Art,
} satisfies ReleaseNote;
