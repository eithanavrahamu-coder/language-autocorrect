import { useEffect, useState } from 'react';
import { AnimatePresence, MotionConfig, motion, type Variants } from 'motion/react';
import { ArrowRight, Sparkles } from 'lucide-react';
import '../components/bits.css';
import { Footer } from '../components/Install';
import { SubpageHeader } from '../components/SubpageHeader';
import { RELEASES, compareVersions, formatDate, type Release } from './notes';
import './ReleaseNotes.css';

const params = new URLSearchParams(window.location.search);

export default function ReleaseNotes() {
  return (
    <MotionConfig reducedMotion="user">
      {params.get('in') === 'app' ? <WhatsNew from={params.get('from')} to={params.get('to')} /> : <AllReleases />}
    </MotionConfig>
  );
}

function Title({ release, as: Heading }: { release: Release; as: 'h1' | 'h2' }) {
  const Icon = release.icon;
  return (
    <div className="rn-title">
      <span className="rn-icon"><Icon size={20} strokeWidth={2} aria-hidden /></span>
      <Heading>{release.title}</Heading>
    </div>
  );
}

/** The page on the site: every version, newest first. */
function AllReleases() {
  useEffect(() => {
    // A link to one version (#v3.14.0): the list is drawn after the page loads, so go there now.
    const id = decodeURIComponent(window.location.hash.slice(1));
    if (id) document.getElementById(id)?.scrollIntoView({ behavior: 'instant' });
  }, []);

  return (
    <>
      <a className="skip" href="#main">Skip to content</a>
      <SubpageHeader />
      <main id="main" className="page rn-main">
        <div className="rn-intro">
          <p className="kicker">Release notes</p>
          <h1>What’s new</h1>
          <p className="rn-lede">
            Every version of Language Autocorrect, newest first. The app updates itself, and after each update it
            shows you the pages for the versions you just got.
          </p>
        </div>
        <ol className="rn-list">
          {RELEASES.map((r, i) => (
            <li key={r.version} id={`v${r.version}`} className="rn-entry">
              <div className="rn-meta">
                <a className="rn-version" href={`#v${r.version}`}>Version {r.version}</a>
                <time dateTime={r.date}>{formatDate(r.date)}</time>
                {i === 0 && <span className="rn-latest">Latest</span>}
              </div>
              <article className="rn-card">
                <div className="rn-art" aria-hidden><r.Art /></div>
                <div className="rn-text">
                  <Title release={r} as="h2" />
                  <div className="rn-body">{r.text}</div>
                </div>
              </article>
            </li>
          ))}
        </ol>
      </main>
      <Footer home="../" />
    </>
  );
}

type WebViewWindow = Window & { chrome?: { webview?: { postMessage(message: unknown): void } } };

/** Closes the app's window; opened in a browser instead, goes to the whole list. */
function close() {
  const host = (window as WebViewWindow).chrome?.webview;
  if (host) host.postMessage({ type: 'close' });
  else window.location.href = './';
}

const slide: Variants = {
  enter: (dir: number) => ({ opacity: 0, x: dir * 36 }),
  center: { opacity: 1, x: 0, transition: { duration: .32, ease: [.2, .8, .2, 1] } },
  exit: (dir: number) => ({ opacity: 0, x: dir * -36, transition: { duration: .16, ease: 'easeIn' } }),
};

/**
 * The app's "What's new" window after an update (?in=app&from=3.12.0&to=3.15.0): one page for each version after
 * `from` up to `to`, oldest first.
 */
function WhatsNew({ from, to }: { from: string | null; to: string | null }) {
  const pages = RELEASES
    .filter(r => (!from || compareVersions(r.version, from) > 0) && (!to || compareVersions(r.version, to) <= 0))
    .reverse();
  const [[index, dir], setPage] = useState([0, 1]);
  const last = pages.length - 1;
  const go = (i: number) => { if (i >= 0 && i <= last && i !== index) setPage([i, i > index ? 1 : -1]); };

  useEffect(() => {
    document.documentElement.classList.add('in-app');
    document.title = 'What’s new';
  }, []);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close();
      else if (e.key === 'ArrowRight') go(index + 1);
      else if (e.key === 'ArrowLeft') go(index - 1);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  });

  if (!pages.length) {
    return (
      <motion.div className="wn wn-none" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}>
        <span className="rn-icon"><Sparkles size={20} aria-hidden /></span>
        <h1>{to ? `You now have version ${to}` : 'Language Autocorrect was updated'}</h1>
        <p>Your languages, word list and settings are just as you left them.</p>
        <div className="wn-buttons">
          <a className="btn btn-quiet" href="./">See the release notes</a>
          <button type="button" className="btn btn-primary" onClick={close} autoFocus>Done</button>
        </div>
      </motion.div>
    );
  }

  const r = pages[index];
  return (
    <motion.div className="wn" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: .35 }}>
      <div className="wn-top">
        <span className="wn-kicker"><Sparkles size={16} strokeWidth={2.2} aria-hidden /> What’s new</span>
        {pages.length > 1 && <span className="wn-count">{index + 1} of {pages.length}</span>}
      </div>
      <div className="wn-stage">
        <AnimatePresence mode="wait" initial={false} custom={dir}>
          <motion.article
            key={r.version}
            className="wn-page"
            custom={dir}
            variants={slide}
            initial="enter"
            animate="center"
            exit="exit"
          >
            <div className="rn-art wn-art" aria-hidden><r.Art /></div>
            <div className="wn-text">
              <p className="wn-version">Version {r.version} · {formatDate(r.date)}</p>
              <Title release={r} as="h1" />
              <div className="rn-body">{r.text}</div>
            </div>
          </motion.article>
        </AnimatePresence>
      </div>
      <div className="wn-foot">
        {pages.length > 1 && (
          <div className="wn-dots">
            {pages.map((p, i) => (
              <button
                key={p.version}
                type="button"
                className={i === index ? 'is-on' : undefined}
                aria-label={`Version ${p.version}`}
                aria-current={i === index || undefined}
                onClick={() => go(i)}
              />
            ))}
          </div>
        )}
        <a className="wn-all" href="./">All release notes</a>
        <div className="wn-buttons">
          <button type="button" className="btn btn-quiet" onClick={close}>Close</button>
          <button type="button" className="btn btn-primary" onClick={() => (index < last ? go(index + 1) : close())} autoFocus>
            {index < last ? <>Next <ArrowRight size={16} strokeWidth={2.4} aria-hidden /></> : 'Done'}
          </button>
        </div>
      </div>
    </motion.div>
  );
}
