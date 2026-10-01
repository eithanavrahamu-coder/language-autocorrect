import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { ArrowDownToLine, LoaderCircle, X } from 'lucide-react';
import { byCode } from '../demo/keyboard';
import { dismissDownload, onDownloadClick, useDownloadStage } from '../download';
import { otherPlatform, setPlatform, usePlatform } from '../platform';
import { DOWNLOADS, downloadMeta } from '../site';
import './bits.css';

/** The colored language badge the app shows next to the text cursor. */
export function Badge({ code, size = 'md' }: { code: string; size?: 'sm' | 'md' | 'lg' }) {
  const l = byCode(code);
  return (
    <span className={`badge badge-${size}`} style={{ background: l.color }} title={l.name}>
      {l.badge}
    </span>
  );
}

export function Kbd({ children }: { children: ReactNode }) {
  return <kbd className="kbd">{children}</kbd>;
}

/** A word as typed on the wrong keyboard, crossed out, then what it became: like the app's "Recent fixes". */
export function FixPair({ from, to }: { from: string; to: string }) {
  return (
    <span className="fixpair">
      <s>{from}</s>
      <span className="fixpair-arrow" aria-label="becomes">→</span>
      <b dir="auto">{to}</b>
    </span>
  );
}

/**
 * The download for the visitor's own computer (Windows or Mac), with a way to get the other one. `showMeta` adds the
 * version, size and what it runs on.
 */
export function DownloadButton({ big = false, showMeta = true }: { big?: boolean; showMeta?: boolean }) {
  const starting = useDownloadStage() === 'starting';
  const platform = usePlatform();
  const d = DOWNLOADS[platform];
  const Icon = starting ? LoaderCircle : ArrowDownToLine;
  return (
    <div className={`download${big ? ' download-big' : ''}`}>
      <motion.a
        className={`btn btn-primary${starting ? ' is-starting' : ''}`}
        href={d.url}
        download
        onClick={onDownloadClick}
        aria-disabled={starting || undefined}
        whileHover={{ y: -1 }}
        whileTap={{ scale: .97 }}
        transition={{ type: 'spring', stiffness: 500, damping: 30 }}
      >
        <Icon size={big ? 20 : 18} strokeWidth={2.2} className={starting ? 'spin' : undefined} aria-hidden />
        {starting ? 'Starting download…' : `Download for ${d.name}`}
      </motion.a>
      {showMeta && (
        <>
          <p className="download-meta">
            Free · {downloadMeta(platform)}
            {d.beta && <span className="download-beta" title="New and still being tested">Beta</span>}
          </p>
          <button type="button" className="download-other" onClick={() => setPlatform(otherPlatform(platform))}>
            {platform === 'mac' ? 'On Windows? Get the Windows app' : 'On a Mac? Get the Mac app (beta)'}
          </button>
        </>
      )}
    </div>
  );
}

/** The note at the bottom of the window after a download click, until the browser has had time to show the file. */
export function DownloadNotice() {
  const stage = useDownloadStage();
  const platform = usePlatform();
  const d = DOWNLOADS[platform];
  return (
    <div className="download-notice-wrap" role="status">
      <AnimatePresence>
        {stage !== 'idle' && (
          <motion.div
            className="download-notice"
            initial={{ opacity: 0, y: 16, scale: .98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, transition: { duration: .18 } }}
            transition={{ type: 'spring', stiffness: 420, damping: 34 }}
          >
            {stage === 'starting' ? (
              <>
                <LoaderCircle size={22} strokeWidth={2.2} className="download-notice-icon spin" aria-hidden />
                <div>
                  <p className="download-notice-title">Your download is starting…</p>
                  <p>
                    It’s {d.size ?? 'a big file'}, so your browser can take a few
                    seconds to show it. No need to click again.
                  </p>
                </div>
              </>
            ) : (
              <>
                <ArrowDownToLine size={22} strokeWidth={2.2} className="download-notice-icon" aria-hidden />
                <div>
                  <p className="download-notice-title">Look for it in your browser’s downloads</p>
                  <p>
                    When it’s finished, open <b>{d.file}</b>
                    {platform === 'mac' && <>, drag Language Autocorrect into Applications,</>} and follow the{' '}
                    <a href="#install" onClick={dismissDownload}>install steps</a>.
                  </p>
                </div>
              </>
            )}
            <button type="button" className="download-notice-close" onClick={dismissDownload} aria-label="Close">
              <X size={18} aria-hidden />
            </button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export function SectionHead({ kicker, title, children }: { kicker: string; title: ReactNode; children?: ReactNode }) {
  return (
    <Reveal className="section-head">
      <p className="kicker">{kicker}</p>
      <h2>{title}</h2>
      {children && <p className="section-lede">{children}</p>}
    </Reveal>
  );
}

/** Fades content up as it scrolls into view (once). */
export function Reveal({ children, className, delay = 0 }: { children: ReactNode; className?: string; delay?: number }) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 18 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: true, margin: '0px 0px -12% 0px' }}
      transition={{ duration: .7, ease: [.2, .8, .2, 1], delay }}
    >
      {children}
    </motion.div>
  );
}

export function GitHubMark({ size = 18 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 16 16" fill="currentColor" aria-hidden>
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
    </svg>
  );
}
