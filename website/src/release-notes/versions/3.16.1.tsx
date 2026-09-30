import { SpellCheck } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.16.1.css';

// Typing "I'm" on the Spanish keyboard, where ' is the accent key: Windows shows "I´m", and Space fixes it to "I'm",
// with nothing left behind. Scenes: 0 empty · 1 Shift+I · 2 the accent key (Windows waits for the next letter) ·
// 3 M: "I´m" · 4 Space · 5 fixed, the keyboard is English now · 6 the text clears
const SCENES = [800, 450, 450, 650, 250, 2200, 450];
const TEXT = ['', 'I', 'I', 'I´m', 'I´m', "I'm", "I'm"];
const KEYS = ['I', '´', 'M', 'Space'];

function Art() {
  const { ref, step } = useTimeline(SCENES, 5);
  const fixed = step >= 5;
  return (
    <div ref={ref} className="v3-16-1" data-fixed={fixed || undefined} data-clear={step === 6 || undefined}>
      <div className="box">
        <span className="word" key={fixed ? 'fixed' : 'typed'}>{TEXT[step]}</span>
        <span className="caret">
          <span className="badge" data-lang={fixed ? 'en' : 'es'}>{fixed ? 'EN' : 'ES'}</span>
        </span>
      </div>
      <div className="keys">
        {KEYS.map((k, i) => (
          <span key={k} className="key" data-wide={k === 'Space' || undefined} data-down={step === i + 1 || undefined}>
            {k}
          </span>
        ))}
      </div>
      <div className="before">
        Before: <s>II'm</s>
      </div>
    </div>
  );
}

export default {
  date: '2026-09-30',
  icon: SpellCheck,
  title: 'Clean fixes on keyboards with accent keys',
  text: (
    <>
      <p>
        On Spanish, Portuguese and some other keyboards, the apostrophe key puts an accent on the next letter, so
        typing “I’m” there shows “I´m”. Language Autocorrect fixed it to “I’m”, but could leave a letter behind
        (“II’m”). It now asks your own keyboard which letters its accents join, and deletes exactly what’s on the
        screen.
      </p>
      <p>
        Also fixed: undoing a fix with Backspace keeps the keyboard in the language you went back to. Words you add
        to Never fix stay there when you change how many undos it takes. With only the Korean keyboard, English words
        typed in Hangul mode are fixed too. And Backspace after switching keyboards in the middle of a word no longer
        confuses the app.
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
