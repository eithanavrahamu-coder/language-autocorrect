import type { ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import icon from '../assets/icon-128.png';
import { setPlatform, usePlatform, type Platform } from '../platform';
import { DOWNLOADS, LANGUAGE_COUNT, REPO_URL } from '../site';
import { DownloadButton, GitHubMark, Kbd, Reveal, SectionHead } from './bits';
import './Install.css';

/** One install step: [title, what to do]. */
type Step = [string, ReactNode];

const WINDOWS_STEPS: Step[] = [
  [
    'Download the app',
    <>
      It’s a single file, <code>LanguageAutocorrect.exe</code>{DOWNLOADS.windows.size && ` (${DOWNLOADS.windows.size})`}.
      If your browser asks whether to keep it, choose <b>Keep</b>.
    </>,
  ],
  [
    'Open it',
    <>
      <p>
        Windows may show <b>“Windows protected your PC”</b>, because the app is new and isn’t signed with a
        paid certificate. Click <b>More info</b>, then <b>Run anyway</b>.
      </p>
      <div className="smartscreen" aria-hidden>
        <p className="smartscreen-title">Windows protected your PC</p>
        <p className="smartscreen-text">Microsoft Defender SmartScreen prevented an unrecognized app from starting.</p>
        <p className="smartscreen-link"><span className="smartscreen-step">1</span>More info</p>
        <div className="smartscreen-buttons">
          <span className="smartscreen-btn is-hint"><span className="smartscreen-step">2</span>Run anyway</span>
          <span className="smartscreen-btn">Don’t run</span>
        </div>
      </div>
    </>,
  ],
  [
    'Click Get started',
    <>
      Your languages and settings are already picked for you, so it’s <b>Next</b>, then <b>Install</b>.
      No administrator rights needed. Prefer not to install? Choose <b>Run without installing</b>.
    </>,
  ],
];

const MAC_STEPS: Step[] = [
  [
    'Download the app',
    <>
      It’s a disk image, <code>LanguageAutocorrect.dmg</code>{DOWNLOADS.mac.size && ` (${DOWNLOADS.mac.size})`}. Open
      it and drag <b>Language Autocorrect</b> into the <b>Applications</b> folder.
    </>,
  ],
  [
    'Open it',
    <>
      <p>
        The first time, macOS says it can’t check the app, because it isn’t signed with a paid Apple developer
        account. Click <b>Done</b>, open <b>System Settings → Privacy &amp; Security</b>, scroll down and click{' '}
        <b>Open Anyway</b>, then <b>Open Anyway</b> again.
      </p>
      <div className="mac-blocked" aria-hidden>
        <p className="mac-blocked-head">Security</p>
        <div className="mac-blocked-row">
          <p>“Language Autocorrect” was blocked to protect your Mac.</p>
          <span className="mac-blocked-btn">Open Anyway</span>
        </div>
      </div>
    </>,
  ],
  [
    'Allow it to fix your typing',
    <>
      macOS asks before any app can see and type keys. Click <b>Allow access</b> in the app’s window, then{' '}
      <b>Open System Settings</b>, and turn on <b>Language Autocorrect</b> under <b>Accessibility</b>. It then
      sits in the menu bar, showing your keyboard’s language.
    </>,
  ],
];

const GOOD_TO_KNOW: Record<Platform, [string, ReactNode][]> = {
  windows: [
    ['Works on', 'Windows 11, or Windows 10 version 1809 or newer (64‑bit).'],
    ['Updating', <>The app checks once a day and offers <b>Update now</b> in its window, keeping your settings. Or download the new version and open it.</>],
    ['Uninstalling', 'Settings → Apps → Installed apps → Language Autocorrect → Uninstall.'],
    ['Needs', 'Microsoft Edge WebView2, which comes with Windows 11 and up-to-date Windows 10.'],
  ],
  mac: [
    ['Works on', 'macOS 14 Sonoma or newer, on Macs with Apple silicon or Intel.'],
    ['New on the Mac', 'Everything that fixes words is there. Korean, the badge next to the cursor and the fix cards are Windows-only for now.'],
    ['Updating', 'The app checks once a day and tells you when there’s a new version. Download it and drag it over the old one; your settings stay. macOS may ask you to allow access again.'],
    ['Uninstalling', 'Quit it from its menu bar icon, then drag it from Applications to the Trash.'],
  ],
};

export function Install() {
  const platform = usePlatform();
  const steps = platform === 'mac' ? MAC_STEPS : WINDOWS_STEPS;
  return (
    <section className="section" id="install" aria-labelledby="install-title">
      <div className="page">
        <SectionHead kicker="Install" title={<span id="install-title">Up and running <em>in a minute.</em></span>} />
        <div className="install-tabs" role="tablist" aria-label="Your computer">
          {(['windows', 'mac'] as const).map(p => (
            <button
              key={p}
              type="button"
              role="tab"
              aria-selected={p === platform}
              aria-controls="install-panel"
              className={p === platform ? 'is-on' : undefined}
              onClick={() => setPlatform(p)}
            >
              {DOWNLOADS[p].name}
              {DOWNLOADS[p].beta && <span className="download-beta">Beta</span>}
            </button>
          ))}
        </div>
        <div className="install" id="install-panel" role="tabpanel" key={platform}>
          <ol className="install-steps">
            {steps.map(([title, text], i) => (
              <Reveal key={title} delay={i * .06}>
                <li>
                  <span className="install-num">{i + 1}</span>
                  <div>
                    <h3>{title}</h3>
                    {i === 1 ? text : <p>{text}</p>}
                  </div>
                </li>
              </Reveal>
            ))}
          </ol>

          <Reveal className="install-side" delay={.1}>
            <h3>Good to know</h3>
            <dl>
              {GOOD_TO_KNOW[platform].map(([term, text]) => <div key={term}><dt>{term}</dt><dd>{text}</dd></div>)}
            </dl>
          </Reveal>
        </div>
      </div>
    </section>
  );
}

const FAQ: [string, ReactNode][] = [
  ['Is it really free?', 'Yes. No ads, no account, no trial.'],
  [
    'Does it see what I type?',
    <>
      It has to look at the word you’re typing in order to fix it, but that happens on your computer. Nothing you
      type is saved or sent anywhere. The app goes online only to check for updates, which you can turn off. See
      the <a href="privacy/">privacy policy</a>.
    </>,
  ],
  [
    'It changed a word I meant to type. What now?',
    <>
      Press <Kbd>Backspace</Kbd> right away, or <Kbd>Ctrl</Kbd>+<Kbd>Z</Kbd> (<Kbd>⌘</Kbd>+<Kbd>Z</Kbd> on a Mac)
      within 5 seconds, and the word comes back as you typed it. To keep it from ever changing that word, add it to{' '}
      <b>Never fix</b> in the app, or hover the fix under Recent fixes and choose <b>Never fix</b>.
    </>,
  ],
  [
    'Why does Windows warn me when I open it?',
    'Windows is careful with apps it hasn’t seen much of, and this one isn’t signed with a paid certificate. Click More info, then Run anyway.',
  ],
  [
    'Why does my Mac say it can’t open it?',
    'macOS is careful with apps that aren’t signed with a paid Apple developer account. Click Done, then open System Settings → Privacy & Security and click Open Anyway.',
  ],
  [
    'Why does the Mac app ask for Accessibility access?',
    'macOS lets an app see the keys you press in other apps, and type a fixed word for you, only with that permission. What you type still stays on your Mac.',
  ],
  [
    'Can I mix more than two languages?',
    `Yes, any of the ${LANGUAGE_COUNT}, as many as you like. When a word could belong to several, the one where it makes the most sense wins.`,
  ],
  [
    'Can I still switch keyboards myself?',
    'Of course. On Windows the app switches the keyboard by pressing your own shortcut (Alt+Shift, Ctrl+Shift or Win+Space), and on a Mac it switches the way the input menu does, so your shortcut keeps working as before.',
  ],
  [
    'Where does it work?',
    'In the apps you type in: browsers, chats, email and documents. It’s always off in password boxes, and you can turn it off for any app in Settings.',
  ],
  ['Is there a Mac or phone version?', 'There’s a Mac version, new and in beta, for macOS 14 or newer. There’s no phone version.'],
];

export function Faq() {
  return (
    <section className="section" id="faq" aria-labelledby="faq-title">
      <div className="page faq-layout">
        <SectionHead kicker="Questions" title={<span id="faq-title">Questions, <em>answered.</em></span>}>
          Something else? Ask on{' '}
          <a href={`${REPO_URL}/issues`}>GitHub</a>.
        </SectionHead>
        <div className="faq">
          {FAQ.map(([q, a]) => (
            <details key={q}>
              <summary>
                {q}
                <ChevronDown size={20} className="faq-chevron" aria-hidden />
              </summary>
              <div className="faq-answer"><p>{a}</p></div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

export function FinalCta() {
  return (
    <section className="section final" aria-labelledby="final-title">
      <div className="page">
        <Reveal className="final-card">
          <div className="final-glow" aria-hidden />
          <img src={icon} alt="" width={88} height={88} className="final-icon" />
          <h2 id="final-title">Stop retyping words.</h2>
          <p>Free for Windows and Mac. Set up in a minute, then forget it’s there.</p>
          <DownloadButton big />
        </Reveal>
      </div>
    </section>
  );
}

/** @param home the way back to the site's front page from the page it's on ('../' on the release notes) */
export function Footer({ home = '' }: { home?: string }) {
  return (
    <footer className="footer">
      <div className="page footer-inner">
        <div className="footer-brand">
          <img src={icon} alt="" width={28} height={28} />
          <div>
            <p className="footer-name">Language Autocorrect</p>
            <p>Made by Eithan Avraham</p>
          </div>
        </div>
        <div className="footer-links">
          <a href={`${home}release-notes/`}>Release notes</a>
          <a href={`${home}privacy/`}>Privacy</a>
          <a href={REPO_URL}><GitHubMark size={16} /> Source code</a>
          <a href={`${REPO_URL}/issues`}>Report a problem</a>
        </div>
        <p className="footer-credit">
          Word lists: <a href="https://github.com/hermitdave/FrequencyWords">FrequencyWords</a> by Hermit Dave,
          based on OpenSubtitles, licensed{' '}
          <a href="https://creativecommons.org/licenses/by-sa/4.0/">CC BY-SA 4.0</a>, filtered for the app.{' '}
          <a href={`${home}privacy/#credits`}>All credits</a>
        </p>
      </div>
    </footer>
  );
}
