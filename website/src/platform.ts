import { useSyncExternalStore } from 'react';

/** The computers the app is made for. */
export type Platform = 'windows' | 'mac';

export const otherPlatform = (p: Platform): Platform => (p === 'mac' ? 'windows' : 'mac');

/**
 * Which app this visitor needs, from what the browser says about the computer it runs on. It's worked out in the
 * browser and never sent or kept anywhere. Phones, tablets and anything else get the Windows app, with the Mac app a
 * click away.
 */
function detect(): Platform {
  if (typeof navigator === 'undefined') return 'windows';
  const nav = navigator as Navigator & { userAgentData?: { platform?: string; mobile?: boolean } };
  if (nav.userAgentData?.mobile || /iPhone|iPad|iPod|Android/i.test(nav.userAgent)) return 'windows';
  const name = nav.userAgentData?.platform || nav.platform || nav.userAgent;
  // iPads say they're Macs; their touch screen gives them away.
  return /mac/i.test(name) && nav.maxTouchPoints <= 1 ? 'mac' : 'windows';
}

let current = detect();
const listeners = new Set<() => void>();

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

/** The app the page offers: the visitor's own computer's, until they pick the other one. */
export function usePlatform() {
  return useSyncExternalStore(subscribe, () => current, () => current);
}

export function setPlatform(platform: Platform) {
  current = platform;
  listeners.forEach(l => l());
}
