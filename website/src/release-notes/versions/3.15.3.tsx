import { EyeOff } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.15.3.css';

// Scenes: 0 the switch is on and the badge sits by the cursor · 1 the switch is flipped off and the badge goes
const SCENES = [1800, 1900];

function Art() {
  const { ref, step } = useTimeline(SCENES, 0);
  return (
    <div ref={ref} className="v3-15-3" data-on={step === 0 || undefined}>
      <div className="option">
        <span className="tile"><span className="mini">EN</span></span>
        <span className="label">Language badge</span>
        <span className="switch"><i /></span>
      </div>
      <div className="box">
        <span className="word">Hello</span>
        <span className="caret" />
        <span className="badge">EN</span>
      </div>
    </div>
  );
}

export default {
  date: '2026-09-28',
  icon: EyeOff,
  title: 'Turn the language badge off in setup',
  text: (
    <>
      <p>
        Prefer a cleaner screen? The <b>language badge</b> beside your text cursor can now be turned off right in
        setup, on the “A few preferences” step. It’s on by default.
      </p>
      <p>
        You can switch it on or off any time in <b>Settings → Language badge</b> (it used to be called “Indicator
        next to the cursor”). With it off, words are still fixed and the keyboard still switches; only the badge is
        gone. The icon by the clock keeps showing the language you’re typing in.
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
