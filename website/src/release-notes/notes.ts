import type { ComponentType, ReactNode } from 'react';
import type { LucideIcon } from 'lucide-react';

/**
 * One version's release notes: versions/<version>.tsx, for example versions/3.15.0.tsx. The file name is the
 * version. This page lists them all, and the app's "What's new" window shows the ones since the version an update
 * replaced (?from=3.14.1&to=3.15.0&in=app).
 *
 * Every app version gets a file, and once published a file is never edited or deleted: it's that version's page
 * for good. Keep a version's animation to its own file (CSS under a `.v3-15-0` class, keyframes named
 * `v3-15-0-…`), so a later change can't break it.
 */
export type ReleaseNote = {
  /** When it was released: '2026-09-27'. */
  date: string;
  /** The symbol beside the title (from lucide-react). */
  icon: LucideIcon;
  title: string;
  /** A few plain sentences: what changed, and what to try. */
  text: ReactNode;
  /** The animation beside it. It sits in a panel about 300 × 280 px (and narrower on a phone). */
  Art: ComponentType;
};

export type Release = ReleaseNote & { version: string };

/** Compares versions as numbers (3.10.0 comes after 3.9.0). */
export function compareVersions(a: string, b: string) {
  const pa = a.split('.').map(Number), pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d) return d;
  }
  return 0;
}

const files = import.meta.glob<{ default: ReleaseNote }>('./versions/*.tsx', { eager: true });

/** Every version with notes, newest first. */
export const RELEASES: Release[] = Object.entries(files)
  .map(([file, m]) => ({ ...m.default, version: file.match(/([^/]+)\.tsx$/)![1] }))
  .sort((a, b) => compareVersions(b.version, a.version));

export const formatDate = (date: string) =>
  new Date(`${date}T12:00:00`).toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });
