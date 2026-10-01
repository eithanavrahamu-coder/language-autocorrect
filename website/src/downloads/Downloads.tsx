import { useEffect, useState } from 'react';
import { CircleAlert, LoaderCircle, RotateCw } from 'lucide-react';
import '../components/bits.css';
import { Footer } from '../components/Install';
import { SubpageHeader } from '../components/SubpageHeader';
import { REPO_URL } from '../site';
import './Downloads.css';

/** `mac`: of the downloads, the Mac app's (its disk image); the rest are the Windows app's. */
type Version = { version: string; released: Date; downloads: number; mac: number };

type GitHubRelease = {
  tag_name: string;
  draft: boolean;
  published_at: string | null;
  assets: { name: string; download_count: number }[];
};

const RELEASES_API = `${REPO_URL.replace('https://github.com/', 'https://api.github.com/repos/')}/releases`;
const DAY = 24 * 60 * 60 * 1000;

class GitHubError extends Error {}

/** Every released version, newest first. GitHub sends at most 100 at a time. */
async function fetchVersions(signal: AbortSignal): Promise<Version[]> {
  const releases: GitHubRelease[] = [];
  for (let page = 1; page <= 20; page++) {
    const res = await fetch(`${RELEASES_API}?per_page=100&page=${page}`, { signal, cache: 'no-cache' });
    if (!res.ok) throw new GitHubError(problem(res));
    const batch: GitHubRelease[] = await res.json();
    releases.push(...batch);
    if (batch.length < 100) break;
  }
  return releases
    .filter(r => !r.draft && r.published_at)
    .map(r => ({
      version: r.tag_name.replace(/^v/, ''),
      released: new Date(r.published_at!),
      downloads: r.assets.reduce((sum, a) => sum + a.download_count, 0),
      mac: r.assets.filter(a => a.name.endsWith('.dmg')).reduce((sum, a) => sum + a.download_count, 0),
    }))
    .sort((a, b) => b.released.getTime() - a.released.getTime());
}

function problem(res: Response) {
  if ((res.status === 403 || res.status === 429) && res.headers.get('x-ratelimit-remaining') === '0') {
    const reset = Number(res.headers.get('x-ratelimit-reset'));
    return 'GitHub answers 60 times an hour for each internet connection, and that’s used up. ' +
      (reset ? `Try again after ${time(new Date(reset * 1000))}.` : 'Try again in a while.');
  }
  return `GitHub answered with an error (${res.status}). Try again in a minute.`;
}

const count = (n: number) => n.toLocaleString('en-US');
const time = (d: Date) => d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
const day = (d: Date) => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
const plural = (n: number, word: string) => `${count(n)} ${word}${n === 1 ? '' : 's'}`;

/** "Windows 120 · Mac 14", once there's a Mac app (from version 3.17.0). */
/** Version 3.17.0 and later have a Mac app too. */
const hasMacApp = (version: string) => { const [a, b] = version.split('.').map(Number); return a > 3 || (a === 3 && b >= 17); };

const split = (downloads: number, mac: number) => `Windows ${count(downloads - mac)} · Mac ${count(mac)}`;

/** "about 12 a day" while a version was the newest, once it's been out a day. */
function rate(downloads: number, from: Date, until: Date) {
  const days = (until.getTime() - from.getTime()) / DAY;
  if (days < 1 || !downloads) return null;
  const perDay = downloads / days;
  return perDay >= 1 ? `about ${count(Math.round(perDay))} a day` : `about ${count(Math.round(perDay * 7))} a week`;
}

/**
 * The download count (/downloads/), for the owner. The site's Download button links to each version's GitHub release
 * (the website build creates them), and GitHub counts how many times each release's file is downloaded. This page
 * asks GitHub for those numbers when it opens: the one place the site contacts another server, which the privacy
 * policy says.
 */
export default function Downloads() {
  const [data, setData] = useState<{ versions: Version[]; at: Date } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [checking, setChecking] = useState(true);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    fetchVersions(controller.signal)
      .then(versions => {
        setData({ versions, at: new Date() });
        setError(null);
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        setError(e instanceof GitHubError ? e.message : 'Couldn’t reach GitHub. Check the internet connection and try again.');
      })
      .finally(() => { if (!controller.signal.aborted) setChecking(false); });
    return () => controller.abort();
  }, [attempt]);

  const checkAgain = () => {
    setChecking(true);
    setAttempt(a => a + 1);
  };

  return (
    <>
      <a className="skip" href="#main">Skip to content</a>
      <SubpageHeader />
      <main id="main" className="page dl-main">
        <div className="dl-intro">
          <p className="kicker">Downloads</p>
          <h1>How many downloads</h1>
          <p className="dl-lede">
            How many times Language Autocorrect has been downloaded from this site, as counted by GitHub. The numbers
            come straight from GitHub each time you open this page.
          </p>
        </div>

        <div aria-live="polite">
          {data ? (
            <Counts versions={data.versions} at={data.at} checking={checking} error={error} onCheck={checkAgain} />
          ) : error ? (
            <div className="dl-card dl-state">
              <CircleAlert size={22} strokeWidth={2.2} className="dl-state-icon" aria-hidden />
              <div>
                <p className="dl-state-title">Couldn’t get the numbers</p>
                <p>{error}</p>
                <button type="button" className="btn btn-quiet dl-again" onClick={checkAgain} disabled={checking}>
                  <RotateCw size={16} strokeWidth={2.2} className={checking ? 'spin' : undefined} aria-hidden /> Try again
                </button>
              </div>
            </div>
          ) : (
            <div className="dl-card dl-state">
              <LoaderCircle size={22} strokeWidth={2.2} className="dl-state-icon spin" aria-hidden />
              <p className="dl-state-title">Asking GitHub…</p>
            </div>
          )}
        </div>

        <section className="dl-notes" aria-labelledby="dl-notes-title">
          <h2 id="dl-notes-title">What’s counted</h2>
          <ul>
            <li>
              Each click on <b>Download</b> on this site adds one to the count of the version the site offers then.
              Downloading the app from its page on GitHub counts too. Someone who downloads it twice counts twice.
            </li>
            <li>Updates the app installs by itself aren’t counted.</li>
            <li>From version 3.17.0 there’s also a Mac app; its downloads are counted with the Windows app’s and shown apart.</li>
            <li>
              Counting started with version 3.15.3, on September 29, 2026. Downloads before then weren’t counted
              anywhere.
            </li>
            <li>
              GitHub gives only the number: not who downloaded the app or where from. See the{' '}
              <a href="../privacy/#website">privacy policy</a>.
            </li>
          </ul>
        </section>
      </main>
      <Footer home="../" />
    </>
  );
}

function Counts({ versions, at, checking, error, onCheck }: {
  versions: Version[];
  at: Date;
  checking: boolean;
  error: string | null;
  onCheck: () => void;
}) {
  const check = (
    <div className="dl-check">
      <span>{error ? error : `Checked at ${time(at)}`}</span>
      <button type="button" className="btn btn-quiet dl-again" onClick={onCheck} disabled={checking}>
        <RotateCw size={16} strokeWidth={2.2} className={checking ? 'spin' : undefined} aria-hidden /> Check again
      </button>
    </div>
  );

  if (!versions.length) {
    return (
      <div className="dl-card dl-state dl-state-col">
        <p className="dl-state-title">No downloads counted yet</p>
        <p>Counting starts once the website’s next build puts the app on GitHub as a release.</p>
        {check}
      </div>
    );
  }

  const total = versions.reduce((sum, v) => sum + v.downloads, 0);
  const totalMac = versions.reduce((sum, v) => sum + v.mac, 0);
  const hasMac = versions.some(v => hasMacApp(v.version));
  const most = Math.max(1, ...versions.map(v => v.downloads));
  const [newest] = versions;
  return (
    <>
      <div className="dl-card dl-summary">
        <div className="dl-total">
          <p className="dl-total-number">{count(total)}</p>
          <p className="dl-total-label">{total === 1 ? 'download' : 'downloads'} in total</p>
          {hasMac && <p className="dl-total-label">{split(total, totalMac)}</p>}
        </div>
        <dl className="dl-facts">
          <div>
            <dt>Newest version ({newest.version})</dt>
            <dd>{plural(newest.downloads, 'download')} since {day(newest.released)}</dd>
          </div>
          <div>
            <dt>Versions counted</dt>
            <dd>{count(versions.length)}</dd>
          </div>
        </dl>
        {check}
      </div>

      <h2 className="dl-list-title">Each version</h2>
      <ol className="dl-list">
        {versions.map((v, i) => {
          const until = i ? versions[i - 1].released : at;
          const perDay = rate(v.downloads, v.released, until);
          return (
            <li key={v.version} className="dl-row">
              <div className="dl-row-head">
                <a className="dl-version" href={`../release-notes/#v${v.version}`}>Version {v.version}</a>
                <span className="dl-count">{count(v.downloads)}</span>
              </div>
              <div className="dl-bar" aria-hidden>
                <span style={{ width: `${(v.downloads / most) * 100}%` }} />
              </div>
              <p className="dl-when">
                {i ? `${day(v.released)} – ${day(until)}` : `Newest, since ${day(v.released)}`}
                {perDay && ` · ${perDay}`}
                {hasMacApp(v.version) && ` · ${split(v.downloads, v.mac)}`}
              </p>
            </li>
          );
        })}
      </ol>
    </>
  );
}
