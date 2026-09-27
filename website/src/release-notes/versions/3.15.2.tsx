import { Cloud, ShieldCheck } from 'lucide-react';
import type { ReleaseNote } from '../notes';
import { useTimeline } from '../timeline';
import './3.15.2.css';

// Scenes: 0 a word typed on the wrong keyboard · 1 fixed · 2 the word floats up and bounces off the shield: it stays
// on the PC · 3 only a question about the newest version goes up to the internet · 4 the answer comes back
const SCENES = [1100, 1300, 1700, 1100, 1500];

function Art() {
  const { ref, step } = useTimeline(SCENES, 1);
  const fixed = step >= 1;
  return (
    <div ref={ref} className="v3-15-2" data-step={step}>
      <div className="cloud"><Cloud size={34} strokeWidth={1.8} /></div>
      <div className="fence"><span className="shield"><ShieldCheck size={18} strokeWidth={2.2} /></span></div>
      <span className="ghost" dir="rtl">שלום</span>
      <span className="ping">{step === 4 ? '✓ Up to date' : 'Newest version?'}</span>
      <div className="box">
        <span className="word" dir={fixed ? 'rtl' : 'ltr'}>{fixed ? 'שלום' : 'akuo'}</span>
        <span className="caret" />
        <span className="badge" key={fixed ? 'he' : 'en'} data-he={fixed || undefined}>{fixed ? 'HE' : 'EN'}</span>
      </div>
    </div>
  );
}

export default {
  date: '2026-09-27',
  icon: ShieldCheck,
  title: 'A privacy policy, in plain words',
  text: (
    <>
      <p>
        Language Autocorrect now has a <b>privacy policy</b> on its website. In short: what you type never leaves your
        PC, the app goes online only to check for updates, and the website has no cookies or tracking.
      </p>
      <p>
        Open it from <b>Settings → Privacy and credits</b>. It also credits the word lists and everything else the
        app is built with. The site used to say the app never goes online at all, which left out the daily update
        check, so now it says exactly what does.
      </p>
    </>
  ),
  Art,
} satisfies ReleaseNote;
