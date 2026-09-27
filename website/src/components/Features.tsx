import { useEffect, useRef, useState } from 'react';
import { AnimatePresence, motion, useInView } from 'motion/react';
import { AppWindow, AudioLines, BookX, KeyRound, ShieldCheck } from 'lucide-react';
import { Badge, FixPair, Kbd, Reveal, SectionHead } from './bits';
import './Features.css';

const STEPS = [
  {
    title: 'Type on whatever keyboard is on',
    text: 'Forgot to switch? Just keep going. For a moment the letters come out wrong.',
    visual: (
      <span className="step-keys" aria-hidden>
        {'ghbdtn'.split('').map((k, i) => <Kbd key={i}>{k.toUpperCase()}</Kbd>)}
        <Badge code="en" size="sm" />
      </span>
    ),
  },
  {
    title: 'Press Space',
    text: 'The word is checked in every language you use, and the one where it makes the most sense wins.',
    visual: <span className="step-space" aria-hidden><Kbd>Space</Kbd></span>,
  },
  {
    title: 'Fixed, and the keyboard switched',
    text: 'The word is replaced and your keyboard switches too, so the next word comes out right.',
    visual: (
      <span className="step-fixed" aria-hidden>
        <b>привет</b>
        <Badge code="ru" size="sm" />
      </span>
    ),
  },
];

export function HowItWorks() {
  return (
    <section className="section" id="how" aria-labelledby="how-title">
      <div className="page">
        <SectionHead kicker="How it works" title={<span id="how-title">You keep typing. <em>It keeps up.</em></span>} />
        <ol className="steps">
          {STEPS.map((s, i) => (
            <Reveal key={s.title} className="step" delay={i * .08}>
              <li>
                <div className="step-visual">{s.visual}</div>
                <p className="step-num">Step {i + 1}</p>
                <h3>{s.title}</h3>
                <p>{s.text}</p>
              </li>
            </Reveal>
          ))}
        </ol>
      </div>
    </section>
  );
}

/** A line of text with the badge at the cursor, cycling through a few languages. */
function CursorBadgeVisual() {
  const lines = [
    { code: 'en', text: 'See you at' },
    { code: 'he', text: 'נתראה ב' },
    { code: 'ru', text: 'Увидимся в' },
    { code: 'el', text: 'Τα λέμε στις' },
    { code: 'ar', text: 'نراك في' },
  ];
  const ref = useRef<HTMLDivElement>(null);
  const inView = useInView(ref, { amount: .5 });
  const [i, setI] = useState(0);
  useEffect(() => {
    if (!inView) return;
    const t = setInterval(() => setI(n => (n + 1) % lines.length), 1800);
    return () => clearInterval(t);
  }, [inView, lines.length]);
  const l = lines[i];
  return (
    <div className="tile-visual cursor-visual" ref={ref} aria-hidden>
      <div className="cursor-field">
        <AnimatePresence mode="popLayout" initial={false}>
          <motion.span
            key={l.code}
            className="cursor-text"
            dir="auto"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: .35, ease: [.2, .8, .2, 1] }}
          >
            {l.text}
            <span className="cursor-caret">
              <span className="cursor-badge"><Badge code={l.code} size="sm" /></span>
            </span>
          </motion.span>
        </AnimatePresence>
      </div>
    </div>
  );
}

// From the app's own description of what it does.
const CONTEXT: [string, string, string?][] = [
  ['הוא אוכל far', 'הוא אוכל כשר'],
  ['it is not far', 'it is not far', 'left alone'],
  ['ha jh akuo', 'יש חי שלום', 'all three fixed'],
];

const SMALL = [
  {
    icon: BookX,
    title: 'A “Never fix” list',
    text: 'Slang, names, codes: words you never want changed. Add them in the app, or pick one from your recent fixes. It can also learn from your undos.',
  },
  {
    icon: ShieldCheck,
    title: 'Stays on your PC',
    text: 'Words are checked on your computer, and what you type is never saved or sent anywhere. The app goes online only to check for updates.',
  },
  {
    icon: KeyRound,
    title: 'Off in password boxes',
    text: 'Always. And you can turn it off for any app you like, such as games or remote desktop.',
  },
  {
    icon: AppWindow,
    title: 'Uses your own shortcut',
    text: 'It switches keyboards with your Windows shortcut (Alt+Shift, Ctrl+Shift or Win+Space), so Windows always stays in sync.',
  },
  {
    icon: AudioLines,
    title: 'Hear the language',
    text: 'Optional: it says the language’s name when the keyboard changes, in that language when Windows has a voice for it.',
  },
];

export function Features() {
  return (
    <section className="section" id="features" aria-labelledby="features-title">
      <div className="page">
        <SectionHead kicker="Details" title={<span id="features-title">Careful, <em>and out of your way.</em></span>} />

        <div className="tiles">
          <Reveal className="tile tile-undo">
            <div className="tile-visual undo-visual" aria-hidden>
              <div className="undo-row">
                <FixPair from="привет" to="ghbdtn" />
              </div>
              <div className="undo-keys">
                <Kbd>Backspace</Kbd>
                <span className="undo-or">or</span>
                <Kbd>Ctrl</Kbd><span className="undo-plus">+</span><Kbd>Z</Kbd>
              </div>
            </div>
            <h3>Undo with one key</h3>
            <p>
              Meant it? Press <Kbd>Backspace</Kbd> right after a fix, or <Kbd>Ctrl</Kbd>+<Kbd>Z</Kbd> within
              5 seconds, and the word comes back exactly as you typed it.
            </p>
          </Reveal>

          <Reveal className="tile tile-cursor" delay={.08}>
            <CursorBadgeVisual />
            <h3>Your language, at the cursor</h3>
            <p>A small colored badge next to the text cursor always shows which keyboard is on.</p>
          </Reveal>

          <Reveal className="tile tile-context" delay={.04}>
            <div className="context-body">
              <h3>It reads the words around it</h3>
              <p>
                When a word makes sense on both keyboards, the words before it decide. And when you realize
                too late, the words just before it that were typed on the same wrong keyboard are fixed too.
              </p>
            </div>
            <table className="context-table">
              <thead>
                <tr><th scope="col">You type</th><th scope="col">You get</th></tr>
              </thead>
              <tbody>
                {CONTEXT.map(([typed, got, note]) => (
                  <tr key={typed}>
                    <td><span className="context-typed" dir="auto">{typed}</span></td>
                    <td>
                      <b dir="auto">{got}</b>
                      {note && <span className="context-note">{note}</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Reveal>
        </div>

        <ul className="small-features">
          {SMALL.map((f, i) => (
            <Reveal key={f.title} delay={i * .05}>
              <li>
                <span className="small-icon"><f.icon size={20} strokeWidth={1.8} aria-hidden /></span>
                <h3>{f.title}</h3>
                <p>{f.text}</p>
              </li>
            </Reveal>
          ))}
        </ul>
      </div>
    </section>
  );
}
