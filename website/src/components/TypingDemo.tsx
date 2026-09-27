import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import type { KeyboardEvent as ReactKeyboardEvent } from 'react';
import { AnimatePresence, motion, useInView } from 'motion/react';
import { ArrowUp, Keyboard, Minus, Pause, Play, Square, Undo2, X } from 'lucide-react';
import { Badge, FixPair, Kbd } from './bits';
import { byCode, keyOf } from '../demo/keyboard';
import { evaluate, loadWords } from '../demo/detect';
import { currentWord, demoReducer, initialDemo } from '../demo/composer';
import { SCENES, TRY_WORDS } from '../demo/scenes';
import './TypingDemo.css';

const TRY_LANGUAGES = Object.keys(TRY_WORDS);
const spring = { type: 'spring', stiffness: 520, damping: 34 } as const;

// Deterministic, slightly uneven typing rhythm.
const keyDelay = (i: number) => 75 + ((i * 37) % 55);
const sceneDuration = (s: (typeof SCENES)[number]) =>
  450 + s.keys.length * 102 + 380 + 950 + (s.then?.length ?? 0) * 102 + 1400 + 650;

function useFinePointer() {
  const q = '(hover: hover) and (pointer: fine)';
  const [fine, setFine] = useState(() => typeof matchMedia !== 'undefined' && matchMedia(q).matches);
  useEffect(() => {
    const m = matchMedia(q);
    const on = () => setFine(m.matches);
    m.addEventListener('change', on);
    return () => m.removeEventListener('change', on);
  }, []);
  return fine;
}

function preferredTryLanguage() {
  for (const l of navigator.languages ?? []) {
    const code = l.slice(0, 2).toLowerCase();
    if (TRY_WORDS[code]) return code;
  }
  return 'he';
}

export function TypingDemo() {
  // Start with the last two examples already "sent", so the chat looks lived in.
  const [state, dispatch] = useReducer(demoReducer, SCENES[0].start, layout => ({
    ...initialDemo(layout),
    messages: SCENES.slice(-2).map((s, i) => ({ id: -1 - i, text: s.fixed, lang: s.to })),
  }));
  const [mode, setMode] = useState<'auto' | 'live'>('auto');
  const [scene, setScene] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [second, setSecond] = useState('he');
  const [wordsReady, setWordsReady] = useState<'idle' | 'loading' | 'ready' | 'failed'>('idle');
  const [focused, setFocused] = useState(false);
  const fine = useFinePointer();

  const root = useRef<HTMLDivElement>(null);
  const box = useRef<HTMLDivElement>(null);
  const inView = useInView(root, { amount: 0.35 });

  // ---------- examples playing by themselves ----------
  useEffect(() => {
    if (mode !== 'auto' || !playing || !inView) return;
    let cancelled = false;
    const sleep = (ms: number) => new Promise<void>((resolve, reject) =>
      setTimeout(() => (cancelled ? reject(new Error('stop')) : resolve()), ms));
    const type = async (keys: string) => {
      for (let i = 0; i < keys.length; i++) {
        if (keys[i] === ' ') dispatch({ type: 'commit', fix: null, now: Date.now() });
        else dispatch({ type: 'key', key: keys[i] });
        await sleep(keyDelay(i));
      }
    };
    (async () => {
      for (let i = scene; ; i = (i + 1) % SCENES.length) {
        const s = SCENES[i];
        setScene(i);
        dispatch({ type: 'reset', layout: s.start });
        await sleep(450);
        await type(s.keys);
        await sleep(380);
        dispatch({ type: 'commit', fix: { text: s.fixed, lang: s.to }, now: Date.now() });
        await sleep(950);
        if (s.then) await type(s.then);
        await sleep(1400);
        dispatch({ type: 'send' });
        await sleep(650);
      }
    })().catch(() => {});
    return () => { cancelled = true; };
    // Restarting on `scene` would restart the loop on every example; only a chip click (below) jumps.
  }, [mode, playing, inView]);

  const jumpTo = (i: number) => {
    setMode('auto');
    setScene(i);
    // Toggle `playing` so the effect restarts at the chosen example.
    setPlaying(false);
    requestAnimationFrame(() => setPlaying(true));
  };

  // ---------- try it yourself ----------
  const startLive = (code = second) => {
    setMode('live');
    setSecond(code);
    dispatch({ type: 'reset', layout: 'en' });
    setWordsReady('loading');
    loadWords(['en', code]).then(() => setWordsReady('ready'), () => setWordsReady('failed'));
    requestAnimationFrame(() => box.current?.focus());
  };
  useEffect(() => { setSecond(preferredTryLanguage()); }, []);

  const toastId = state.toast?.id;
  useEffect(() => {
    if (!toastId) return;
    const t = setTimeout(() => dispatch({ type: 'hideToast', id: toastId }), 2600);
    return () => clearTimeout(t);
  }, [toastId]);

  const commit = useCallback(() => {
    const fix = evaluate(state.keys, state.layout, ['en', second]);
    dispatch({ type: 'commit', fix: state.tail ? null : fix, now: Date.now() });
  }, [state.keys, state.layout, state.tail, second]);

  const onKeyDown = (e: ReactKeyboardEvent<HTMLDivElement>) => {
    if (mode !== 'live') return;
    if (e.ctrlKey && e.code === 'KeyZ') { e.preventDefault(); dispatch({ type: 'undo', now: Date.now() }); return; }
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.key === 'Escape') { box.current?.blur(); return; }
    if (e.key === 'Backspace') { e.preventDefault(); dispatch({ type: 'backspace', now: Date.now() }); return; }
    if (e.code === 'Space') { e.preventDefault(); commit(); return; }
    if (e.key === 'Enter') { e.preventDefault(); commit(); dispatch({ type: 'send' }); return; }
    const key = keyOf(e);
    if (key) { e.preventDefault(); dispatch({ type: 'key', key }); }
  };

  const toggleLayout = () => {
    if (mode !== 'live') return;
    dispatch({ type: 'layout', layout: state.layout === 'en' ? second : 'en' });
    box.current?.focus();
  };

  const word = currentWord(state);
  const empty = !state.words.length && !word;
  const live = mode === 'live';
  const tryWord = TRY_WORDS[second];

  return (
    <div className="demo" ref={root}>
      <div className="demo-glow" aria-hidden />
      {!live && (
        <p className="sr-only">
          Examples: {SCENES.map(s => `${byCode(s.start).name} keyboard, typed ${s.keys}, fixed to ${s.fixed}`).join('; ')}.
        </p>
      )}
      <div className="demo-window" aria-hidden={!live}>
        <div className="demo-titlebar">
          <span className="demo-title">
            <span className="demo-avatar" aria-hidden>D</span>
            Dana
          </span>
          <span className="demo-caption" aria-hidden>
            <Minus size={15} /><Square size={12} /><X size={16} />
          </span>
        </div>

        <div className="demo-messages">
          <p className="demo-day">Today</p>
          <AnimatePresence initial={false} mode="popLayout">
            {state.messages.map(m => (
              <motion.div
                key={m.id}
                className="demo-bubble-row"
                layout
                initial={{ opacity: 0, y: 24, scale: .96 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: -16, transition: { duration: .2 } }}
                transition={spring}
              >
                <span className="demo-bubble" dir="auto">{m.text}</span>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>

        <div className="demo-compose">
          <AnimatePresence>
            {state.toast && (
              <motion.div
                key={state.toast.id}
                className="demo-toast"
                role={live ? 'status' : undefined}
                initial={{ opacity: 0, y: 10, scale: .95 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 6, scale: .98, transition: { duration: .18 } }}
                transition={spring}
              >
                {state.toast.undone
                  ? <><Undo2 size={15} aria-hidden /> Kept as typed: <b dir="auto">{state.toast.to}</b></>
                  : <>
                      <FixPair from={state.toast.from} to={state.toast.to} />
                      {live && <span className="demo-toast-undo"><Kbd>Backspace</Kbd> to undo</span>}
                    </>}
              </motion.div>
            )}
          </AnimatePresence>

          {/* The box looks like a text box, so clicking it while the examples play starts "try it yourself";
              otherwise the keys typed into it were ignored and Space scrolled the page. */}
          <div
            ref={box}
            className={`demo-box${live ? ' is-live' : fine ? ' is-tryable' : ''}`}
            role="textbox"
            aria-label={live ? 'Demo text box. Type here; the demo keyboard starts on English.' : 'Example of typing being fixed'}
            aria-readonly={!live}
            tabIndex={live ? 0 : -1}
            onKeyDown={onKeyDown}
            onFocus={() => setFocused(true)}
            onBlur={() => setFocused(false)}
            onClick={() => (live ? box.current?.focus() : fine && startLive())}
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
              <span className={`demo-caret${live && !focused ? ' is-idle' : ''}`}>
                <button
                  type="button"
                  className="demo-badge"
                  onClick={toggleLayout}
                  tabIndex={-1}
                  aria-label={`Keyboard: ${byCode(state.layout).name}`}
                  style={{ cursor: live ? 'pointer' : 'default' }}
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
            {live && empty && !focused && <span className="demo-placeholder">Click here and start typing</span>}
          </div>
          <span className="demo-send" aria-hidden><ArrowUp size={17} strokeWidth={2.4} /></span>
        </div>
      </div>

      <div className="demo-controls">
        {live ? (
          <div className="demo-try">
            <div className="demo-try-langs" role="radiogroup" aria-label="Your other language">
              {TRY_LANGUAGES.map(code => (
                <button
                  key={code}
                  type="button"
                  role="radio"
                  aria-checked={second === code}
                  className={`chip${second === code ? ' is-active' : ''}`}
                  onClick={() => startLive(code)}
                >
                  <Badge code={code} size="sm" />
                  {byCode(code).name}
                </button>
              ))}
            </div>
            <p className="demo-try-hint">
              {wordsReady === 'failed'
                ? 'The word lists could not be loaded. Check your connection and pick a language again.'
                : <>
                    The demo keyboard starts on English. Type <b dir="auto">{tryWord.word}</b> as you would
                    on a {byCode(second).name} keyboard{' '}
                    <span className="demo-try-keys">{[...tryWord.keys].map((k, i) => <Kbd key={i}>{k.toUpperCase()}</Kbd>)}</span>{' '}
                    and press <Kbd>Space</Kbd>. <Kbd>Enter</Kbd> sends, and a click on the badge switches the keyboard.
                    {wordsReady === 'loading' && <span className="demo-loading"> Loading words…</span>}
                  </>}
            </p>
            <div className="demo-try-foot">
              <p>A simplified preview: the app itself also weighs how common each word is and reads the words around it.</p>
              <button type="button" className="btn btn-quiet btn-small" onClick={() => jumpTo(scene)}>
                Back to examples
              </button>
            </div>
          </div>
        ) : (
          <>
            <div className="demo-chips" role="tablist" aria-label="Examples">
              {SCENES.map((s, i) => (
                <button
                  key={s.lang + i}
                  type="button"
                  role="tab"
                  aria-selected={scene === i}
                  className={`chip${scene === i ? ' is-active' : ''}`}
                  onClick={() => jumpTo(i)}
                >
                  <Badge code={s.lang} size="sm" />
                  {byCode(s.lang).name}
                  {scene === i && playing && inView && (
                    <motion.span
                      key={`p${i}`}
                      className="chip-progress"
                      initial={{ scaleX: 0 }}
                      animate={{ scaleX: 1 }}
                      transition={{ duration: sceneDuration(s) / 1000, ease: 'linear' }}
                    />
                  )}
                </button>
              ))}
            </div>
            <div className="demo-actions">
              <button
                type="button"
                className="icon-btn"
                onClick={() => setPlaying(p => !p)}
                aria-label={playing ? 'Pause examples' : 'Play examples'}
              >
                {playing ? <Pause size={16} /> : <Play size={16} />}
              </button>
              {fine && (
                <button type="button" className="btn btn-quiet btn-small" onClick={() => startLive()}>
                  <Keyboard size={17} aria-hidden /> Try it yourself
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
