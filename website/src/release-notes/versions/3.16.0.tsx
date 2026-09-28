import { MessageCircleQuestionMark } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.16.0.css';

// Scenes: 0 the word was fixed · 1 Backspace undoes it (the fifth time) · 2 the card asks · 3 pointer to Stop fixing ·
// 4 click · 5 "won't be fixed anymore" · 6 the card goes
const SCENES = [900, 250, 1300, 700, 250, 1600, 800];

function Art() {
  const { ref, step } = useTimeline(SCENES, 2);
  const card = step >= 2 && step <= 5 ? (step === 5 ? 'done' : 'ask') : undefined;
  return (
    <div ref={ref} className="v3-16-0" data-card={card}>
      <div className="card">
        <div className="ask">
          <div className="words">
            <span className="title">Stop fixing <b>akuo</b>?</span>
            <span className="note">You undid it 5 times.</span>
          </div>
          <span className="choice">Keep fixing</span>
          <span className="choice strong" data-down={step === 4 || undefined}>Stop fixing</span>
        </div>
        <div className="done">
          <svg viewBox="0 0 24 24"><path d="M20 6 9 17l-5-5" /></svg>
          <span><b>akuo</b> won’t be fixed anymore</span>
        </div>
      </div>
      <div className="box">
        <span className="word" dir="auto">{step === 0 ? 'שלום' : 'akuo'}</span>
        <span className="caret" />
      </div>
      <span className="key" data-down={step === 1 || undefined}>Backspace</span>
      <svg className="pointer" data-at={step >= 3 && step <= 4 ? 'stop' : 'rest'} data-down={step === 4 || undefined} viewBox="0 0 20 24">
        <path d="M2 1.5v17l4.6-4.2 3.1 7 2.8-1.2-3.1-6.8h6.3z" />
      </svg>
    </div>
  );
}

export default {
  date: '2026-09-29',
  icon: MessageCircleQuestionMark,
  title: 'Asked before a word stops being fixed',
  text: (
    <>
      <p>
        Keep undoing the same fix? Language Autocorrect can now ask you about it. Once you’ve undone a word 5 times, a
        small card by your text asks <b>“Stop fixing akuo?”</b> Click <b>Stop fixing</b> and it goes on your Never fix
        list, or <b>Keep fixing</b> to leave things as they are. Ignore the card and it asks again the next time.
      </p>
      <p>
        It’s off at first. Turn it on in setup, or any time in <b>Never fix → Ask about words you keep undoing</b>,
        where you can also choose how many undos it waits for. <b>Learn from undos</b>, which adds a word to the list
        by itself without asking, is still there if you prefer it.
      </p>
      <p>
        Also new: the website now counts how many times the app is downloaded (GitHub keeps the count; nothing else
        about you).
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
