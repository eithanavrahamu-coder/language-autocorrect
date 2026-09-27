import { Keyboard } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.14.0.css';

// Scenes: 0 start over (hidden) · 1 appear · 2 pointer to the box · 3 click in it · 4–7 type a, k, u, o ·
// 8 press Space · 9 fixed · 10 fade out
const SCENES = [300, 400, 700, 450, 200, 200, 200, 450, 300, 2700, 500];
const KEYS = 'akuo', WORD = 'שלום';

function Art() {
  const { ref, step } = useTimeline(SCENES, 9);
  const typed = KEYS.slice(0, Math.min(4, Math.max(0, step - 3)));
  const fixed = step >= 9;
  const pointer = step <= 1 ? 'rest' : step <= 3 ? 'box' : 'gone';
  return (
    <div
      ref={ref}
      className="v3-14-0"
      data-hidden={step === 0 || undefined}
      data-out={step === 10 || undefined}
      data-focused={(step >= 3) || undefined}
      data-fixed={fixed || undefined}
    >
      <div className="fix"><s>{KEYS}</s><span>→</span><b dir="rtl">{WORD}</b></div>
      <div className="box">
        {fixed ? <span className="word he" dir="rtl" key="he">{WORD}</span> : <span className="word">{typed}</span>}
        <span className="caret" />
        <span className={`badge ${fixed ? 'he' : 'en'}`} key={fixed ? 'he' : 'en'}>{fixed ? 'HE' : 'EN'}</span>
      </div>
      <div className="foot">
        <span className="label">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <rect x="2.5" y="6" width="19" height="12" rx="2.5" /><path d="M6.5 10h.01M10 10h.01M13.5 10h.01M17 10h.01M8 14h8" />
          </svg>
          {fixed ? 'Switched to Hebrew' : 'English keyboard'}
        </span>
        <span className="space" data-down={step === 8 || undefined}>Space</span>
      </div>
      <svg className="pointer" data-at={pointer} data-down={step === 3 || undefined} viewBox="0 0 20 24">
        <path d="M2 1.5v17l4.6-4.2 3.1 7 2.8-1.2-3.1-6.8h6.3z" />
      </svg>
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: Keyboard,
  title: 'Try it before you install',
  text: (
    <>
      <p>
        On the first screen of setup, click <b>Try it now</b> (or the text box) and type a word as if your keyboard
        were set to your other language. Press Space and watch it get fixed and the keyboard switch, just like in
        your other apps.
      </p>
      <p>Changed your mind? Press Backspace right after the fix to undo it.</p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
