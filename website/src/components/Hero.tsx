import { motion } from 'motion/react';
import { useEffect, useState } from 'react';
import { ArrowDownToLine, LoaderCircle } from 'lucide-react';
import icon from '../assets/icon-128.png';
import { onDownloadClick, useDownloadStage } from '../download';
import { DOWNLOAD_URL, REPO_URL } from '../site';
import { DownloadButton, FixPair, GitHubMark } from './bits';
import { TypingDemo } from './TypingDemo';
import './Hero.css';

const NAV = [
  ['How it works', '#how'],
  ['Languages', '#languages'],
  ['Install', '#install'],
  ['Questions', '#faq'],
];

export function Header() {
  const [scrolled, setScrolled] = useState(false);
  const starting = useDownloadStage() === 'starting';
  const DownloadIcon = starting ? LoaderCircle : ArrowDownToLine;
  useEffect(() => {
    const on = () => setScrolled(window.scrollY > 8);
    on();
    window.addEventListener('scroll', on, { passive: true });
    return () => window.removeEventListener('scroll', on);
  }, []);

  return (
    <header className={`header${scrolled ? ' is-scrolled' : ''}`}>
      <div className="page header-inner">
        <a className="brand" href="#top" aria-label="Language Autocorrect, back to top">
          <img src={icon} alt="" width={30} height={30} />
          <span>Language Autocorrect</span>
        </a>
        <nav className="nav" aria-label="Sections">
          {NAV.map(([label, href]) => <a key={href} href={href}>{label}</a>)}
        </nav>
        <div className="header-actions">
          <a className="header-github" href={REPO_URL} aria-label="Source code on GitHub">
            <GitHubMark />
          </a>
          <a
            className={`btn btn-primary header-download${starting ? ' is-starting' : ''}`}
            href={DOWNLOAD_URL}
            download
            onClick={onDownloadClick}
            aria-disabled={starting || undefined}
          >
            <DownloadIcon size={16} strokeWidth={2.4} className={starting ? 'spin' : undefined} aria-hidden />
            Download
          </a>
        </div>
      </div>
    </header>
  );
}

const rise = (delay: number) => ({
  initial: { opacity: 0, y: 14 },
  animate: { opacity: 1, y: 0 },
  transition: { duration: .8, ease: [.2, .8, .2, 1] as const, delay },
});

export function Hero() {
  return (
    <section className="hero" id="top">
      <div className="page hero-inner">
        <motion.p className="hero-pill" {...rise(0)}>
          <span className="hero-pill-dot" aria-hidden />
          Free for Windows 10 and 11
        </motion.p>
        <motion.h1 {...rise(.06)}>
          Type in all your languages without watching the keyboard.
        </motion.h1>
        <motion.p className="hero-lede" {...rise(.14)}>
          Language Autocorrect notices words typed on the wrong keyboard, like{' '}
          <FixPair from="ghbdtn" to="привет" /> or <FixPair from="akuo" to="שלום" />, and fixes them the
          moment you press Space. Then it switches the keyboard for you.
        </motion.p>
        <motion.div {...rise(.22)}>
          <DownloadButton big />
        </motion.div>
        <motion.div className="hero-demo" {...rise(.34)}>
          <TypingDemo />
        </motion.div>
      </div>
    </section>
  );
}
