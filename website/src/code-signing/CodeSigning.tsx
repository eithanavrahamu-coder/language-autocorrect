import type { ReactNode } from 'react';
import { MotionConfig } from 'motion/react';
import { GitBranch, Hourglass, ShieldCheck, UserCheck, type LucideIcon } from 'lucide-react';
import '../components/bits.css';
import { Footer } from '../components/Install';
import { SubpageHeader } from '../components/SubpageHeader';
import { CONTACT_EMAIL, REPO_URL } from '../site';
// Laid out like the privacy policy, with its styles.
import '../privacy/Privacy.css';
import './CodeSigning.css';

/** When this page last changed what it says. */
const UPDATED = { date: '2026-10-06', text: 'October 6, 2026' };

const SUMMARY: [LucideIcon, string, string][] = [
  [GitBranch, 'Built from the public code', 'Every download is built from this project’s open source code, on GitHub’s own build machines.'],
  [UserCheck, 'Approved by hand', 'Nothing is signed automatically. Each release is approved by the project’s approver first.'],
  [ShieldCheck, 'Signed by SignPath Foundation', 'The signature shows the file is the one that was built, and that it hasn’t been changed since.'],
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

/**
 * The code signing policy that SignPath Foundation asks open source projects to publish before it signs their releases
 * (https://signpath.org/terms.html): the sentence crediting SignPath word for word, who has which role, a link to the
 * privacy policy, and how releases are built. The site's footer and the GitHub releases link here.
 */
export default function CodeSigning() {
  return (
    <MotionConfig reducedMotion="user">
      <a className="skip" href="#main">Skip to content</a>
      <SubpageHeader />
      <main id="main" className="page pv-main">
        <div className="pv-intro">
          <p className="kicker">Code signing</p>
          <h1>Code signing policy</h1>
          <p className="pv-lede">
            A digital signature shows who published an app and that nobody has changed it since. This page says how the
            Windows app is built and signed, and who decides what gets signed.
          </p>
          <p className="cs-credit">
            Free code signing provided by <a href="https://signpath.io/">SignPath.io</a>, certificate by{' '}
            <a href="https://signpath.org/">SignPath Foundation</a>
          </p>
          <p className="pv-updated">Last updated <time dateTime={UPDATED.date}>{UPDATED.text}</time></p>
        </div>

        <div className="cs-status" role="note">
          <Hourglass size={20} strokeWidth={2} aria-hidden />
          <p>
            <b>Not signed yet.</b> Language Autocorrect has applied to SignPath Foundation for code signing. Until
            that’s approved, the download is unsigned, which is one reason Chrome and Windows warn about it (the{' '}
            <a href="../#install">install steps</a> show what to click). This page will say from which version on the
            app is signed.
          </p>
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
          <Section id="what" title="What is signed">
            <p>
              The Windows app, <code>LanguageAutocorrect.exe</code>: the file you download from this site, and the
              file the app downloads when it updates itself. Only files built from this project’s own source code are
              signed, and nothing else is signed with this certificate.
            </p>
            <p>
              The Mac app (beta) isn’t part of this. A Mac looks for a signature from Apple, which needs a paid Apple
              developer account, so the Mac app is signed only “ad hoc” and macOS asks you to allow it the first time,
              as the install steps show.
            </p>
          </Section>

          <Section id="build" title="How a release is built and signed">
            <ul>
              <li>
                The source code is public, on <a href={REPO_URL}>GitHub</a>, under the{' '}
                <a href={`${REPO_URL}/blob/main/LICENSE`}>MIT License</a>.
              </li>
              <li>
                Every release is built from that code by the project’s GitHub Actions workflow,{' '}
                <a href={`${REPO_URL}/blob/main/.github/workflows/build.yml`}><code>build.yml</code></a>, on GitHub’s
                build machines. No release is built on anyone’s own computer, and nothing is added to it that isn’t
                built from the code.
              </li>
              <li>
                The workflow sends the app it built to SignPath. SignPath checks that it’s Language Autocorrect, by the
                product name and version written into the file, and signs it only after the approver has approved that
                release.
              </li>
              <li>The signed file is what the website offers for download and what the app installs as an update.</li>
            </ul>
          </Section>

          <Section id="team" title="Team roles">
            <p>Language Autocorrect is made by one person, so for now one person has every role:</p>
            <ul>
              <li>
                <b>Committers and reviewers:</b>{' '}
                <a href="https://github.com/eithanavrahamu-coder">Eithan Avraham (eithanavrahamu-coder)</a>. I’m the
                only one who can change the source code. Changes anyone else suggests (pull requests) are taken in only
                after I’ve reviewed them.
              </li>
              <li>
                <b>Approvers:</b>{' '}
                <a href="https://github.com/eithanavrahamu-coder">Eithan Avraham (eithanavrahamu-coder)</a>, who approves
                every signing request by hand.
              </li>
            </ul>
            <p>
              Everyone with one of these roles signs in to GitHub and SignPath with two-step verification. If more
              people join, this page will list them and their roles.
            </p>
          </Section>

          <Section id="privacy" title="Privacy">
            <p>
              The app never sends what you type anywhere. It goes online only for updates: once a day it asks this
              website for the newest version number (you can turn that off), and after an update it shows what’s new
              from here. The <a href="../privacy/">privacy policy</a> says exactly what the app and this website do
              with information, including the parts of Windows, macOS and GitHub they use and those companies’
              privacy statements.
            </p>
          </Section>

          <Section id="check" title="Checking a signature">
            <p>
              On a signed version, right-click <code>LanguageAutocorrect.exe</code>, choose <b>Properties</b> and open
              the <b>Digital Signatures</b> tab: the signer is <b>SignPath Foundation</b>. Windows also names SignPath
              Foundation as the publisher when you open the app, instead of “Unknown publisher”.
            </p>
          </Section>

          <Section id="contact" title="Questions and problems">
            <p>
              If you think a file signed for Language Autocorrect wasn’t built from this project’s code, or does
              something it shouldn’t, email me at <Mail />. You can also tell{' '}
              <a href="https://signpath.org/">SignPath Foundation</a>.
            </p>
          </Section>
        </div>
      </main>
      <Footer home="../" />
    </MotionConfig>
  );
}
