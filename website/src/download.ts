import { useSyncExternalStore } from 'react';
import type { MouseEvent } from 'react';

/**
 * What the download buttons and the note at the bottom of the page show after a click. A page can't see when the
 * browser's own download begins, so the stages run on a timer: GitHub takes a few seconds before it sends a file
 * this big, and without a sign that something is happening people click again and get the file twice.
 */
export type DownloadStage = 'idle' | 'starting' | 'started';

const STARTING_MS = 7000;
const HIDE_MS = 30000;

let stage: DownloadStage = 'idle';
let timers: ReturnType<typeof setTimeout>[] = [];
const listeners = new Set<() => void>();

function setStage(next: DownloadStage) {
  stage = next;
  listeners.forEach(l => l());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function useDownloadStage() {
  return useSyncExternalStore(subscribe, () => stage, () => stage);
}

/** For every download link: the first click downloads, more clicks while it's starting are ignored. */
export function onDownloadClick(e: MouseEvent) {
  if (stage === 'starting') {
    e.preventDefault();
    return;
  }
  timers.forEach(clearTimeout);
  setStage('starting');
  timers = [setTimeout(() => setStage('started'), STARTING_MS), setTimeout(() => setStage('idle'), HIDE_MS)];
}

export function dismissDownload() {
  timers.forEach(clearTimeout);
  setStage('idle');
}
