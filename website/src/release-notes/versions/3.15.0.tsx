import { Gift } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.15.0.css';

// Scenes: 0 the app's window · 1 "What's new" pops up · 2 pointer to Next · 3 click · 4 second page · 5 click ·
// 6 last page · 7 click Done · 8 it closes
const SCENES = [700, 900, 650, 250, 1100, 250, 1300, 250, 900];
const PAGES = [
  { version: '3.13', color: '#2563EB', glyph: <circle cx="12" cy="12" r="6.5" /> },
  { version: '3.14', color: '#5847E0', glyph: <rect x="4.5" y="7.5" width="15" height="9" rx="2" /> },
  { version: '3.15', color: '#16A34A', glyph: <path d="M12 4.5Q12.8 11.2 19.5 12Q12.8 12.8 12 19.5Q11.2 12.8 4.5 12Q11.2 11.2 12 4.5Z" /> },
];

function Art() {
  const { ref, step } = useTimeline(SCENES, 4);
  const page = step >= 6 ? 2 : step >= 4 ? 1 : 0;
  const open = step >= 1 && step <= 7;
  return (
    <div ref={ref} className="v3-15-0" data-open={open || undefined}>
      <div className="window">
        <div className="bar"><i /><i /><i /></div>
        <div className="side"><i /><i /><i /><i /></div>
        <div className="rows"><i /><i /><i /></div>
      </div>
      <div className="card">
        <div className="head"><span>What’s new</span><span>{page + 1} of 3</span></div>
        <div className="pages">
          <div className="strip" style={{ translate: `${-page * 100}% 0` }}>
            {PAGES.map(p => (
              <div key={p.version} className="page">
                <svg className="art" viewBox="0 0 24 24" style={{ background: p.color }} fill="#fff">{p.glyph}</svg>
                <div className="lines"><b>{p.version}</b><i /><i /></div>
              </div>
            ))}
          </div>
        </div>
        <div className="foot">
          <div className="dots">{PAGES.map((p, i) => <i key={p.version} data-on={i === page || undefined} />)}</div>
          <span className="next" data-down={step === 3 || step === 5 || step === 7 || undefined}>{page === 2 ? 'Done' : 'Next'}</span>
        </div>
      </div>
      <svg className="pointer" data-at={step >= 2 && step <= 7 ? 'next' : 'rest'} data-down={step === 3 || step === 5 || step === 7 || undefined} viewBox="0 0 20 24">
        <path d="M2 1.5v17l4.6-4.2 3.1 7 2.8-1.2-3.1-6.8h6.3z" />
      </svg>
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: Gift,
  title: 'See what’s new after every update',
  text: (
    <>
      <p>
        After an update, a small window like this one shows what changed, with a page for each new version. If you
        skipped a few versions, click <b>Next</b> to go through them all, or <b>Close</b> any time.
      </p>
      <p>
        Nothing is saved on your PC. Every version’s notes stay on the website: open them any time from{' '}
        <b>Settings → Updates → Release notes</b>.
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
