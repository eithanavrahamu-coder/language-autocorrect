import { useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import homeLight from '../assets/screens/home-light.webp';
import homeDark from '../assets/screens/home-dark.webp';
import wordsLight from '../assets/screens/words-light.webp';
import wordsDark from '../assets/screens/words-dark.webp';
import settingsLight from '../assets/screens/settings-light.webp';
import settingsDark from '../assets/screens/settings-dark.webp';
import setupLight from '../assets/screens/setup-light.webp';
import setupDark from '../assets/screens/setup-dark.webp';
import { SectionHead } from './bits';
import './Screens.css';

// Real screenshots of the app's windows (scripts/screenshots.mjs), sample data included. Both windows draw their own
// top edge, so they have no title bar.
const TABS = [
  { id: 'home', label: 'Home', light: homeLight, dark: homeDark, w: 960, h: 640,
    alt: 'The Home page: a colorful side playing back the latest fix, auto-correct switched on, counts of fixed words, and recent fixes.' },
  { id: 'words', label: 'Never fix', light: wordsLight, dark: wordsDark, w: 960, h: 640,
    alt: 'The Never fix page: a list of words that are never auto-corrected, and the option to learn from undos.' },
  { id: 'settings', label: 'Settings', light: settingsLight, dark: settingsDark, w: 960, h: 640,
    alt: 'Settings: a card for each language, the chosen ones in their color, and the same languages listed on the colorful side.' },
  { id: 'setup', label: 'Setup', light: setupLight, dark: setupDark, w: 960, h: 640,
    alt: 'The setup window: a colorful side showing a word typed on the wrong keyboard being fixed, and a Get started button.' },
];

export function Screens() {
  const [active, setActive] = useState(TABS[0].id);
  const tab = TABS.find(t => t.id === active)!;

  return (
    <section className="section" id="app" aria-labelledby="app-title">
      <div className="page">
        <SectionHead kicker="The app" title={<span id="app-title">A calm little window, <em>one click away.</em></span>}>
          Click the tray icon, or open it from the Start menu, to see recent fixes, your Never fix list and settings.
        </SectionHead>

        <div className="screens-tabs" role="tablist" aria-label="App pages">
          {TABS.map(t => (
            <button
              key={t.id}
              type="button"
              role="tab"
              id={`tab-${t.id}`}
              aria-selected={t.id === active}
              aria-controls="screens-panel"
              className={`screens-tab${t.id === active ? ' is-active' : ''}`}
              onClick={() => setActive(t.id)}
            >
              {t.id === active && (
                <motion.span className="screens-tab-bg" layoutId="screens-tab" transition={{ type: 'spring', stiffness: 500, damping: 38 }} />
              )}
              <span className="screens-tab-label">{t.label}</span>
            </button>
          ))}
        </div>

        <div className="screens-stage">
          <motion.div
            className="win"
            layout
            style={{ width: `min(100%, ${tab.w}px)` }}
            transition={{ type: 'spring', stiffness: 260, damping: 32 }}
            id="screens-panel"
            role="tabpanel"
            aria-labelledby={`tab-${tab.id}`}
          >
            <div className="win-body" style={{ aspectRatio: `${tab.w} / ${tab.h}` }}>
              <AnimatePresence initial={false} mode="popLayout">
                <motion.picture
                  key={tab.id}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  transition={{ duration: .25 }}
                >
                  <source srcSet={tab.dark} media="(prefers-color-scheme: dark)" />
                  <img src={tab.light} alt={tab.alt} width={tab.w} height={tab.h} loading="lazy" decoding="async" />
                </motion.picture>
              </AnimatePresence>
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
}
