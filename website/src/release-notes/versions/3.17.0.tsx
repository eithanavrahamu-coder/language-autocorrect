import { Laptop } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.17.0.css';

// A Mac: the menu bar shows the keyboard's language. "ghbdtn" typed on the English keyboard becomes "привет" on
// Space, and the menu bar switches to Russian. Scenes: 0 empty · 1 "ghb" · 2 "ghbdtn" · 3 Space · 4 fixed ·
// 5 the text clears
const SCENES = [700, 500, 700, 250, 2400, 500];
const TEXT = ['', 'ghb', 'ghbdtn', 'ghbdtn', 'привет', 'привет'];

function Art() {
  const { ref, step } = useTimeline(SCENES, 4);
  const fixed = step >= 4;
  return (
    <div ref={ref} className="v3-17-0" data-fixed={fixed || undefined} data-clear={step === 5 || undefined}>
      <div className="screen">
        <div className="menubar">
          <span className="app">Notes</span>
          <span className="spacer" />
          <span className="badge" data-lang={fixed ? 'ru' : 'en'} key={fixed ? 'ru' : 'en'}>{fixed ? 'РУ' : 'EN'}</span>
          <span className="clock">9:41</span>
        </div>
        <div className="window">
          <div className="lights"><i /><i /><i /></div>
          <div className="text">
            <span className="word" key={fixed ? 'fixed' : 'typed'}>{TEXT[step]}</span>
            <span className="caret" />
          </div>
        </div>
      </div>
      <span className="key" data-down={step === 3 || undefined}>space</span>
    </div>
  );
}

export default {
  date: '2026-10-02',
  icon: Laptop,
  title: 'Now on the Mac, too',
  text: (
    <>
      <p>
        Language Autocorrect now has a Mac app, for macOS 14 or newer. It sits in the menu bar showing your keyboard’s
        language, fixes words typed on the wrong keyboard the moment you press Space, and switches the keyboard for
        you, just like on Windows. The first time, macOS asks you to allow it under Accessibility; the app shows you
        where.
      </p>
      <p>
        It’s new, so it’s marked <b>Beta</b>. Korean, the badge next to the cursor and the fix cards are Windows-only
        for now.
      </p>
      <p>
        The website’s Download button now offers the version for your computer, with the other one a click away, and
        the privacy policy says what the Mac app keeps and why it needs that permission. On Windows nothing changes.
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
