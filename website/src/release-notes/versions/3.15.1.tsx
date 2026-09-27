import { ArrowUpFromLine } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.15.1.css';

const LINES = ['Hi Dana,', 'See you at 8.', 'Bye!'];

// Scenes: 0–2 the cursor on each line, the badge below it · 2 is the window's last line, so the badge goes above ·
// 3 the text clears
const SCENES = [1300, 1300, 2400, 500];

function Art() {
  const { ref, step } = useTimeline(SCENES, 2);
  const line = Math.min(step, LINES.length - 1);
  const above = line === LINES.length - 1;
  return (
    <div ref={ref} className="v3-15-1" data-clear={step === 3 || undefined}>
      <div className="window">
        <div className="bar"><i /><i /><i /></div>
        <div className="page">
          {LINES.map((text, i) => (
            <div key={i} className="line" data-shown={i <= line || undefined}>
              {text}
              {i === line && (
                <span className="caret">
                  <span className="badge" data-above={above || undefined}>EN</span>
                </span>
              )}
            </div>
          ))}
        </div>
      </div>
      <div className="taskbar"><i /><i /><i /></div>
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: ArrowUpFromLine,
  title: 'The badge moves above when there’s no room below',
  text: (
    <p>
      The language badge sits just below the text cursor. When you type on the last line of a window, or at the
      bottom of the screen, there’s no room for it there, so it now shows just above the cursor instead of hanging
      off the window’s edge. It also stays on the screen you’re typing on when you have more than one.
    </p>
  ),
  Art,
} satisfies ReleaseNote;
