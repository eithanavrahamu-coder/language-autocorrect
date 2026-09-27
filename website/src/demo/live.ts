// Typing into a demo box yourself, as in the "Try it now" bar.
import { useEffect, useState } from 'react';
import type { Dispatch, KeyboardEvent as ReactKeyboardEvent } from 'react';
import { keyOf } from './keyboard';
import { evaluate } from './detect';
import type { DemoAction, DemoState } from './composer';
import { TRY_WORDS } from './scenes';

/** Typing yourself needs a real keyboard: phones type characters, not physical keys. */
export function useFinePointer() {
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

/** The visitor's own language when there's a word to try in it, otherwise Hebrew. */
export function preferredTryLanguage() {
  for (const l of navigator.languages ?? []) {
    const code = l.slice(0, 2).toLowerCase();
    if (TRY_WORDS[code]) return code;
  }
  return 'he';
}

/**
 * A key pressed in a demo box, handled as the app would: letters are physical keys that the demo keyboard turns
 * into text, Space and Enter end the word (fixing it if it was typed on the wrong keyboard), and Backspace right
 * after a fix, or Ctrl+Z, undoes it. Returns true for Enter, so the box can also send the text.
 */
export function typeKey(
  e: ReactKeyboardEvent<HTMLElement>, s: DemoState, dispatch: Dispatch<DemoAction>, languages: string[],
): boolean {
  if (e.ctrlKey && e.code === 'KeyZ') { e.preventDefault(); dispatch({ type: 'undo', now: Date.now() }); return false; }
  if (e.ctrlKey || e.metaKey || e.altKey) return false;
  if (e.key === 'Escape') { e.currentTarget.blur(); return false; }
  if (e.key === 'Backspace') { e.preventDefault(); dispatch({ type: 'backspace', now: Date.now() }); return false; }
  if (e.code === 'Space' || e.key === 'Enter') {
    e.preventDefault();
    const fix = s.tail ? null : evaluate(s.keys, s.layout, languages);
    dispatch({ type: 'commit', fix, now: Date.now() });
    return e.key === 'Enter';
  }
  const key = keyOf(e);
  if (key) { e.preventDefault(); dispatch({ type: 'key', key }); }
  return false;
}
