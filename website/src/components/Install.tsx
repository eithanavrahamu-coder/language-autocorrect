import type { ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';
import icon from '../assets/icon-128.png';
import { DOWNLOAD_SIZE, LANGUAGE_COUNT, REPO_URL } from '../site';
import { DownloadButton, GitHubMark, Kbd, Reveal, SectionHead } from './bits';
import './Install.css';

export function Install() {
  return (
    <section className="section" id="install" aria-labelledby="install-title">
      <div className="page">
        <SectionHead kicker="Install" title={<span id="install-title">Up and running <em>in a minute.</em></span>} />
        <div className="install">
          <ol className="install-steps">
            <Reveal>
              <li>
                <span className="install-num">1</span>
                <div>
                  <h3>Download the app</h3>
                  <p>
                    It’s a single file, <code>LanguageAutocorrect.exe</code>{DOWNLOAD_SIZE && ` (${DOWNLOAD_SIZE})`}.
                    If your browser asks whether to keep it, choose <b>Keep</b>.
                  </p>
                </div>
              </li>
            </Reveal>
            <Reveal delay={.06}>
              <li>
                <span className="install-num">2</span>
                <div>
                  <h3>Open it</h3>
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
                </div>
              </li>
            </Reveal>
            <Reveal delay={.12}>
              <li>
                <span className="install-num">3</span>
                <div>
                  <h3>Click Get started</h3>
                  <p>
                    Your languages and settings are already picked for you, so it’s <b>Next</b>, then <b>Install</b>.
                    No administrator rights needed. Prefer not to install? Choose <b>Run without installing</b>.
                  </p>
                </div>
              </li>
            </Reveal>
          </ol>

          <Reveal className="install-side" delay={.1}>
            <h3>Good to know</h3>
            <dl>
              <div><dt>Works on</dt><dd>Windows 11, or Windows 10 version 1809 or newer (64‑bit).</dd></div>
              <div><dt>Updating</dt><dd>The app checks once a day and offers <b>Update now</b> in its window, keeping your settings. Or download the new version and open it.</dd></div>
              <div><dt>Uninstalling</dt><dd>Settings → Apps → Installed apps → Language Autocorrect → Uninstall.</dd></div>
              <div><dt>Needs</dt><dd>Microsoft Edge WebView2, which comes with Windows 11 and up-to-date Windows 10.</dd></div>
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
    'It has to look at the word you’re typing in order to fix it, but that happens on your computer. The app never goes online, and nothing you type is sent anywhere.',
  ],
  [
    'It changed a word I meant to type. What now?',
    <>
      Press <Kbd>Backspace</Kbd> right away, or <Kbd>Ctrl</Kbd>+<Kbd>Z</Kbd> within 5 seconds, and the word comes
      back as you typed it. To keep it from ever changing that word, add it to <b>Never fix</b> in the app, or hover
      the fix under Recent fixes and choose <b>Never fix</b>.
    </>,
  ],
  [
    'Why does Windows warn me when I open it?',
    'Windows is careful with apps it hasn’t seen much of, and this one isn’t signed with a paid certificate. Click More info, then Run anyway.',
  ],
  [
    'Can I mix more than two languages?',
    `Yes, any of the ${LANGUAGE_COUNT}, as many as you like. When a word could belong to several, the one where it makes the most sense wins.`,
  ],
  [
    'Can I still switch keyboards myself?',
    'Of course. The app switches the keyboard by pressing your own Windows shortcut (Alt+Shift, Ctrl+Shift or Win+Space), so your shortcut keeps working as before.',
  ],
  [
    'Where does it work?',
    'In the apps you type in: browsers, chats, email and documents. It’s always off in password boxes, and you can turn it off for any app in Settings.',
  ],
  ['Is there a Mac or phone version?', 'No, it’s made for Windows only.'],
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
          <p>Free for Windows. Set up in a minute, then forget it’s there.</p>
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
          <a href={REPO_URL}><GitHubMark size={16} /> Source code</a>
          <a href={`${REPO_URL}/issues`}>Report a problem</a>
        </div>
        <p className="footer-credit">
          Word lists: <a href="https://github.com/hermitdave/FrequencyWords">FrequencyWords</a> by Hermit Dave,
          based on OpenSubtitles, licensed{' '}
          <a href="https://creativecommons.org/licenses/by-sa/4.0/">CC BY-SA 4.0</a>.
        </p>
      </div>
    </footer>
  );
}
