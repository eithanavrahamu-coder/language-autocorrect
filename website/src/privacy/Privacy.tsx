import type { ReactNode } from 'react';
import { MotionConfig } from 'motion/react';
import { CloudOff, Cookie, HardDrive, type LucideIcon } from 'lucide-react';
import '../components/bits.css';
import { Footer } from '../components/Install';
import { SubpageHeader } from '../components/SubpageHeader';
import { CONTACT_EMAIL, REPO_URL } from '../site';
import './Privacy.css';

/** When this page last changed what it says. Update it with every such change, and mention it in the release notes. */
const UPDATED = { date: '2026-10-02', text: 'October 2, 2026' };

const SUMMARY: [LucideIcon, string, string][] = [
  [HardDrive, 'Your typing stays on your computer', 'Words are checked on your computer. What you type is never saved or sent anywhere.'],
  [CloudOff, 'Nothing is collected', 'No account, no statistics, no crash reports, no ads. The app goes online only to check for updates.'],
  [Cookie, 'No cookies or tracking', 'This site sets no cookies and loads nothing from Google or any other company.'],
];

const Mail = () => <a href={`mailto:${CONTACT_EMAIL}`}>{CONTACT_EMAIL}</a>;

function Section({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section className="pv-section" aria-labelledby={id}>
      <h2 id={id}>{title}</h2>
      {children}
    </section>
  );
}

export default function Privacy() {
  return (
    <MotionConfig reducedMotion="user">
      <a className="skip" href="#main">Skip to content</a>
      <SubpageHeader />
      <main id="main" className="page pv-main">
        <div className="pv-intro">
          <p className="kicker">Privacy</p>
          <h1>Privacy policy</h1>
          <p className="pv-lede">
            Language Autocorrect is free, with no ads and no account. This page says exactly what the app (for
            Windows and for Mac) and this website do with information, in plain words.
          </p>
          <p className="pv-updated">Last updated <time dateTime={UPDATED.date}>{UPDATED.text}</time></p>
        </div>

        <ul className="pv-summary">
          {SUMMARY.map(([Icon, title, text]) => (
            <li key={title}>
              <span className="pv-summary-icon"><Icon size={20} strokeWidth={2} aria-hidden /></span>
              <h3>{title}</h3>
              <p>{text}</p>
            </li>
          ))}
        </ul>

        <div className="pv-body">
          <Section id="app" title="The app">
            <h3>What it looks at</h3>
            <p>
              To notice a word typed on the wrong keyboard, the app watches the keys you press, like any autocorrect.
              It keeps the word you’re typing and at most the 12 words before it, in memory only, and forgets them as
              you type on. It also notes which app you’re typing in and which keyboard is on, so it can leave alone
              the apps you turned it off for and switch the keyboard for you.
            </p>
            <p>
              On a Mac, macOS asks you first: the app needs your permission under{' '}
              <b>System Settings → Privacy &amp; Security → Accessibility</b>, which is what lets any app see the keys
              you press in other apps and type a fixed word for you. The app uses it only for that, and you can turn
              it off there at any time.
            </p>
            <p>
              It never changes anything in password boxes (on a Mac, macOS keeps what you type in them from every app).
              It also leaves Remote Desktop and the password managers KeePass, KeePassXC, 1Password and Bitwarden alone,
              and you can add any other app in its settings.
            </p>
            <p>
              On Windows, to draw the short flash on a word it fixed, it looks at the pixels of that one line on the
              screen, just before and just after the fix. That picture stays in memory for a moment and is never saved.
              The Mac app doesn’t do this.
            </p>

            <h3>What it keeps on your computer</h3>
            <p>
              In your user folder (on Windows <code>%AppData%\LanguageAutocorrect</code>, on a Mac{' '}
              <code>~/Library/Application Support/LanguageAutocorrect</code>) the app keeps:
            </p>
            <ul>
              <li>your settings, such as your languages and the apps it’s turned off for;</li>
              <li>
                your “Never fix” list, and the words you’ve undone and how many times, so it can learn to stop fixing
                them or ask you whether to;
              </li>
              <li>how many words it has fixed, today and in total;</li>
              <li>the version that last ran, so it can show what’s new after an update;</li>
              <li>
                a small log for finding problems: times, the names of the apps you typed in, keyboard switches and
                errors. It never contains what you type, and it starts over when it reaches half a megabyte.
              </li>
            </ul>
            <p>
              On Windows, the app’s own window keeps a browser cache in <code>%LocalAppData%\LanguageAutocorrect</code>;
              on a Mac it keeps none. The list of recent fixes on its home page is kept in memory only and is gone when
              the app closes.
            </p>
            <p>
              None of this is ever sent to anyone. On Windows, uninstalling the app deletes all of it, unless you choose
              to keep your settings for later. On a Mac, moving the app to the Trash leaves that folder, so your
              settings are there if you install it again; delete the folder to remove them too.
            </p>

            <h3>What it sends over the internet</h3>
            <ul>
              <li>
                <b>Update checks.</b> Once a day, the app asks this website for the number of the newest version. On
                Windows, when you install an update, the app downloads it from here; on a Mac you download it yourself
                from this site. Each request carries the app’s name and version number and, like any visit to a website,
                your IP address, which this site’s host can see (see <a href="#website">The website</a>). You can turn
                update checks off in the app’s settings.
              </li>
              <li>
                <b>What’s new.</b> After an update, the app shows this website’s release notes for the new version.
              </li>
              <li>
                <b>Links you click</b> in the app open in your browser.
              </li>
            </ul>
            <p>
              That’s all. The app sends no usage statistics or crash reports, has no ads or account, and has no ID that
              tells your copy apart from anyone else’s.
            </p>

            <h3>Parts of Windows and macOS it uses</h3>
            <p>
              Spoken keyboard names (off unless you turn them on) use the voices built into Windows or macOS, which run
              on your computer. On Windows, the app’s windows are shown by Microsoft Edge WebView2, which is part of
              Windows and is covered by{' '}
              <a href="https://privacy.microsoft.com/privacystatement">Microsoft’s privacy statement</a>. On a Mac they’re
              shown by WebKit, which is part of macOS and is covered by{' '}
              <a href="https://www.apple.com/legal/privacy/">Apple’s privacy policy</a>.
            </p>
          </Section>

          <Section id="website" title="The website">
            <ul>
              <li>
                <b>Hosting.</b> This site is hosted on GitHub Pages by GitHub, Inc. (part of Microsoft).{' '}
                <a href="https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages#data-collection">
                  GitHub records visitors’ IP addresses
                </a>{' '}
                for security, under the{' '}
                <a href="https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement">
                  GitHub privacy statement
                </a>
                . I can’t see those records.
              </li>
              <li>
                <b>Downloads are counted.</b> The Download button gets the app from{' '}
                <a href={`${REPO_URL}/releases`}>its releases on GitHub</a>, and GitHub counts how many times each
                version is downloaded, for Windows and for Mac. That number is all I see: not who downloaded it or where from. As with the site,
                GitHub sees your IP address, under the same privacy statement. Updates the app installs by itself come
                from this site and aren’t counted.
              </li>
              <li>
                <b>The right download for your computer.</b> To show the Windows or the Mac download, the page looks at
                what your browser says about your computer. That happens in your browser: it isn’t sent anywhere or
                kept.
              </li>
              <li>
                <b>No cookies or tracking.</b> The site sets no cookies, stores nothing in your browser, and has no
                statistics, ads or social media buttons.
              </li>
              <li>
                <b>Nothing from other companies.</b> The site’s fonts, pictures and code are all stored on the site
                itself, so visiting it doesn’t contact Google Fonts or any other service. The one exception is the
                page that shows the download count (<a href="../downloads/">/downloads/</a>), which asks GitHub for the
                numbers when it’s opened.
              </li>
              <li>
                <b>The typing demo</b> works inside your browser. What you type in it isn’t sent anywhere.
              </li>
              <li>
                <b>Links to GitHub</b> (the source code, reporting a problem) take you to GitHub, where GitHub’s privacy
                statement applies. Problem reports there are public, so don’t put anything private in them.
              </li>
            </ul>
          </Section>

          <Section id="email" title="Emailing me">
            <p>
              If you email <Mail />, I use your address and message only to reply to you. I never share them or add you
              to a mailing list. The mailbox is Gmail, so{' '}
              <a href="https://policies.google.com/privacy">Google’s privacy policy</a> covers how it’s stored.
            </p>
          </Section>

          <Section id="rights" title="Your rights">
            <p>
              Because neither the app nor this site collects information about you, there’s nothing about you for me
              to show you, correct or delete. What the app keeps is on your own computer, and you can delete it (see <a href="#app">The app</a>). For
              what GitHub or Google keep, see their privacy statements. If you have a question or a concern, email{' '}
              <Mail />. You can also complain to the data protection authority where you live.
            </p>
          </Section>

          <Section id="changes" title="Changes">
            <p>
              If this ever changes, for example if the app starts sending something new, this page will say so first,
              with a new date, and the release notes will mention it.
            </p>
            <ul>
              <li>
                <b>October 2, 2026:</b> there’s now a Mac app, which asks for the Accessibility permission and keeps its
                files in <code>~/Library/Application Support/LanguageAutocorrect</code> (see <a href="#app">The app</a>).
                The website shows the download for your computer, worked out in your browser (see{' '}
                <a href="#website">The website</a>).
              </li>
              <li>
                <b>September 29, 2026:</b> the Download button now gets the app from GitHub, which counts the
                downloads (see <a href="#website">The website</a>).
              </li>
            </ul>
          </Section>

          <Section id="who" title="Who’s responsible">
            <p>
              Eithan Avraham, who makes Language Autocorrect. Email: <Mail />.
            </p>
          </Section>

          <Credits />
        </div>
      </main>
      <Footer home="../" />
    </MotionConfig>
  );
}

const MIT_HOLDERS = [
  'Copyright (c) 2026 Eithan Avraham (Language Autocorrect)',
  'Copyright (c) Meta Platforms, Inc. and affiliates (React, React DOM, Scheduler)',
  'Copyright (c) 2024 Motion B.V. (Motion)',
  'Copyright (c) 2018 Framer B.V. (Framer Motion)',
  'Copyright (c) 2013-present Cole Bemis (Feather icons, used by Lucide)',
  'Copyright (c) .NET Foundation and Contributors (.NET, Windows Forms, .NET for macOS)',
];

/** Everything the app and the site are made of that someone else made, with the notices their licenses ask for. */
function Credits() {
  return (
    <Section id="credits" title="Credits and licenses">
      <p>
        Language Autocorrect is open source, under the <a href={`${REPO_URL}/blob/main/LICENSE`}>MIT License</a>.
        It’s built with other people’s work:
      </p>
      <ul>
        <li>
          <b>Word lists:</b> <a href="https://github.com/hermitdave/FrequencyWords">FrequencyWords</a> by Hermit Dave,
          made from <a href="https://www.opensubtitles.org/">OpenSubtitles</a> subtitles, licensed{' '}
          <a href="https://creativecommons.org/licenses/by-sa/4.0/">CC BY-SA 4.0</a>. The app uses changed copies:
          only words written in each language’s own alphabet, in lowercase, with duplicates merged, some lists cut to
          their most common 50,000 words, and broken entries removed. The changed lists are shared under the same
          license, in <a href={`${REPO_URL}/tree/main/src/LanguageAutocorrect.Engine/Data`}>the source code</a>.
        </li>
        <li>
          <b>Fonts on this site:</b> <a href="https://github.com/simpals/onest">Onest</a>, copyright 2021 The Onest
          Project Authors, and <a href="https://github.com/ateliertriay/bricolage">Bricolage Grotesque</a>, copyright
          2022 The Bricolage Grotesque Project Authors, both under the{' '}
          <a href="https://openfontlicense.org/open-font-license-official-text/">SIL Open Font License 1.1</a>.
        </li>
        <li>
          <b>This site’s code:</b> <a href="https://react.dev/">React</a> (MIT License),{' '}
          <a href="https://motion.dev/">Motion</a> (MIT License) and <a href="https://lucide.dev/">Lucide</a> icons
          (ISC License, and MIT License for the ones that come from Feather).
        </li>
        <li>
          <b>The app’s code:</b> <a href="https://dotnet.microsoft.com/">.NET</a> and Windows Forms (MIT License,
          with the parts listed in <a href="https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT">
          .NET’s third-party notices</a>) and the Microsoft Edge WebView2 SDK (BSD license). The Mac app uses{' '}
          <a href="https://github.com/dotnet/macios">.NET for macOS</a> (MIT License).
        </li>
      </ul>

      <details className="pv-licenses">
        <summary>Full license texts</summary>

        <h4>MIT License</h4>
        <pre>{`${MIT_HOLDERS.join('\n')}

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.`}</pre>

        <h4>ISC License (Lucide)</h4>
        <pre>{`Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.`}</pre>

        <h4>BSD License (Microsoft Edge WebView2 SDK)</h4>
        <pre>{`Copyright (C) Microsoft Corporation. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:

   * Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
   * The name of Microsoft Corporation, or the names of its contributors
may not be used to endorse or promote products derived from this
software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.`}</pre>
      </details>
    </Section>
  );
}
