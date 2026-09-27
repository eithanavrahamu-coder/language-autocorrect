import { ArrowUpFromLine, Search } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.15.1.css';

const WORD = 'weather';

// Scenes: 0 an empty search bar at the bottom of the screen · 1–7 typing, one letter each · 8 the whole word ·
// 9 the text clears. The badge sits above the cursor all along: there's no room below it.
const SCENES = [900, ...WORD.split('').map(() => 150), 2200, 450];

function Art() {
  const { ref, step } = useTimeline(SCENES, WORD.length + 1);
  const typed = WORD.slice(0, Math.min(step, WORD.length));
  return (
    <div ref={ref} className="v3-15-1" data-clear={step === SCENES.length - 1 || undefined}>
      <div className="screen">
        <div className="window">
          <div className="bar"><i /><i /><i /></div>
          <div className="rows"><i /><i /><i /></div>
        </div>
        <div className="search">
          <Search size={14} strokeWidth={2.4} />
          <span className="field">
            <span className="text">{typed}</span>
            <span className="caret">
              <span className="badge" key={step === SCENES.length - 1 ? 'off' : 'on'}>EN</span>
            </span>
            {!typed && <span className="hint">Search</span>}
          </span>
        </div>
      </div>
      <div className="stand" />
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: ArrowUpFromLine,
  title: 'The badge moves above when there’s no room below',
  text: (
    <p>
      The language badge sits just below the text cursor. When you type in a search bar at the bottom of the screen,
      or on the last line of a window, there’s no room for it there, so it now shows just above the cursor instead of
      hanging off the edge. It also stays on the screen you’re typing on when you have more than one.
    </p>
  ),
  Art,
} satisfies ReleaseNote;
