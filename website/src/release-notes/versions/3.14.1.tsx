import { TextCursorInput } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.14.1.css';

// Scenes: 0 typing in the box · 1 pointer moves out · 2 click outside · 3 cursor and badge gone ·
// 4 pointer moves back · 5 click in the box · 6 both back
const SCENES = [1300, 700, 250, 1700, 700, 250, 1300];

function Art() {
  const { ref, step } = useTimeline(SCENES, 0);
  const focused = step <= 1 || step >= 5;
  return (
    <div ref={ref} className="v3-14-1" data-focused={focused || undefined}>
      <div className="box">
        <span className="word" dir="rtl">שלום</span>
        <span className="caret" />
        <span className="badge" key={focused ? 'on' : 'off'}>HE</span>
      </div>
      <span className="ripple" data-show={step === 2 || step === 3 || undefined} />
      <svg className="pointer" data-out={(step >= 1 && step <= 3) || undefined} data-down={step === 2 || step === 5 || undefined} viewBox="0 0 20 24">
        <path d="M2 1.5v17l4.6-4.2 3.1 7 2.8-1.2-3.1-6.8h6.3z" />
      </svg>
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: TextCursorInput,
  title: 'The badge leaves with the cursor',
  text: (
    <p>
      In <b>Try it now</b>, clicking outside the text box used to leave the little language badge floating on its
      own. Now it hides together with the blinking cursor, and both come back when you click in the box again.
    </p>
  ),
  Art,
} satisfies ReleaseNote;
