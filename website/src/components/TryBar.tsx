import { useEffect, useReducer, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Keyboard, Undo2, X } from 'lucide-react';
import { Badge, FixPair, Kbd, Reveal } from './bits';
import { byCode } from '../demo/keyboard';
import { loadWords } from '../demo/detect';
import { currentWord, demoReducer, initialDemo } from '../demo/composer';
import { preferredTryLanguage, typeKey, useFinePointer } from '../demo/live';
import { TRY_WORDS } from '../demo/scenes';
import './TypingDemo.css'; // the demo's text cursor with its badge, and the language chips
import './TryBar.css';

const TRY_LANGUAGES = Object.keys(TRY_WORDS);
const spring = { type: 'spring', stiffness: 520, damping: 34 } as const;

/** "Try it now": a text box under the install steps where visitors type on the wrong keyboard and see the fix. */
export function TryBar() {
  const fine = useFinePointer();
  const [state, dispatch] = useReducer(demoReducer, 'en', initialDemo);
  const [second, setSecond] = useState(preferredTryLanguage);
  const [started, setStarted] = useState(false);
  const [loaded, setLoaded] = useState<{ code: string; ok: boolean } | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [focused, setFocused] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  // The word lists are a few hundred KB, so they're fetched only once someone clicks into the box.
  useEffect(() => {
    if (!started) return;
    let current = true;
    loadWords(['en', second]).then(
      () => current && setLoaded({ code: second, ok: true }),
      () => current && setLoaded({ code: second, ok: false }),
    );
    return () => { current = false; };
  }, [started, second, attempt]);
  const wordsReady = loaded?.code !== second ? 'loading' : loaded.ok ? 'ready' : 'failed';

  const toastId = state.toast?.id;
  useEffect(() => {
    if (!toastId) return;
    const t = setTimeout(() => dispatch({ type: 'hideToast', id: toastId }), 2600);
    return () => clearTimeout(t);
  }, [toastId]);

  if (!fine) return null;

  const pick = (code: string) => {
    setSecond(code);
    if (loaded && !loaded.ok) { setLoaded(null); setAttempt(a => a + 1); }
    dispatch({ type: 'reset', layout: 'en' });
    box.current?.focus();
  };
  const clear = () => {
    dispatch({ type: 'reset', layout: 'en' });
    box.current?.focus();
  };
  const toggleLayout = () => {
    dispatch({ type: 'layout', layout: state.layout === 'en' ? second : 'en' });
    box.current?.focus();
  };
  const onKeyDown = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    // Keys on the Clear button inside the box are the button's own.
    if (e.target === e.currentTarget) typeKey(e, state, dispatch, ['en', second]);
  };

  const word = currentWord(state);
  const empty = !state.words.length && !word;
  const tryWord = TRY_WORDS[second];

  return (
    <Reveal className="trybar">
      <div className="trybar-head">
        <h3 className="trybar-title"><Keyboard size={20} aria-hidden /> Try it now</h3>
        <div className="trybar-status" role="status">
          <AnimatePresence>
            {state.toast && (
              <motion.p
                key={state.toast.id}
                className="trybar-toast"
                initial={{ opacity: 0, y: 8, scale: .95 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 4, scale: .98, transition: { duration: .18 } }}
                transition={spring}
              >
                {state.toast.undone
                  ? <><Undo2 size={15} aria-hidden /> Kept as typed: <b dir="auto">{state.toast.to}</b></>
                  : <>
                      <FixPair from={state.toast.from} to={state.toast.to} />
                      <span className="trybar-toast-undo"><Kbd>Backspace</Kbd> to undo</span>
                    </>}
              </motion.p>
            )}
          </AnimatePresence>
        </div>
      </div>

      <div
        ref={box}
        className="trybar-box"
        role="textbox"
        aria-label="Try it now. Type here; the keyboard here starts on English."
        aria-describedby="trybar-hint"
        tabIndex={0}
        onKeyDown={onKeyDown}
        onFocus={() => { setFocused(true); setStarted(true); }}
        onBlur={() => setFocused(false)}
        onClick={() => box.current?.focus()}
      >
        <span className="demo-line" dir="auto">
          {state.words.map(w => (
            <span key={w.id}>
              {w.from
                ? <motion.span
                    className="demo-fixed"
                    initial={{ opacity: 0, filter: 'blur(6px)' }}
                    animate={{ opacity: 1, filter: 'blur(0px)' }}
                    transition={{ duration: .35, ease: [.2, .8, .2, 1] }}
                  >{w.text}</motion.span>
                : w.text}
              {' '}
            </span>
          ))}
          <span>{word}</span>
          <span className={`demo-caret${focused ? '' : ' is-idle'}`}>
            <button
              type="button"
              className="demo-badge"
              onClick={toggleLayout}
              tabIndex={-1}
              aria-label={`Keyboard: ${byCode(state.layout).name}`}
              style={{ cursor: 'pointer' }}
            >
              <AnimatePresence mode="popLayout" initial={false}>
                <motion.span
                  key={state.layout}
                  initial={{ scale: .5, opacity: 0 }}
                  animate={{ scale: 1, opacity: 1 }}
                  exit={{ scale: .5, opacity: 0 }}
                  transition={spring}
                >
                  <Badge code={state.layout} size="sm" />
                </motion.span>
              </AnimatePresence>
            </button>
          </span>
        </span>
        {empty && !focused && <span className="trybar-placeholder">Click here and type as if your keyboard were on the wrong language</span>}
        {!empty && (
          <button type="button" className="icon-btn trybar-clear" onClick={clear} aria-label="Clear">
            <X size={16} aria-hidden />
          </button>
        )}
      </div>

      <div className="trybar-langs" role="radiogroup" aria-label="Your other language">
        {TRY_LANGUAGES.map(code => (
          <button
            key={code}
            type="button"
            role="radio"
            aria-checked={second === code}
            className={`chip${second === code ? ' is-active' : ''}`}
            onClick={() => pick(code)}
          >
            <Badge code={code} size="sm" />
            {byCode(code).name}
          </button>
        ))}
      </div>

      <p className="trybar-hint" id="trybar-hint">
        {started && wordsReady === 'failed'
          ? 'The word lists could not be loaded. Check your connection and pick a language again.'
          : <>
              The keyboard here starts on English. Type <b dir="auto">{tryWord.word}</b> as you would on
              a {byCode(second).name} keyboard{' '}
              <span className="demo-try-keys">{[...tryWord.keys].map((k, i) => <Kbd key={i}>{k.toUpperCase()}</Kbd>)}</span>{' '}
              and press <Kbd>Space</Kbd>. <Kbd>Backspace</Kbd> right after puts it back, and a click on the badge
              switches the keyboard.
              {started && wordsReady === 'loading' && <span className="trybar-loading"> Loading words…</span>}
            </>}
      </p>
      <p className="trybar-note">A simplified preview: the app itself also weighs how common each word is and reads the words around it.</p>
    </Reveal>
  );
}
