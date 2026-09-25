import { motion } from 'motion/react';
import { LANGUAGES } from '../demo/keyboard';
import { LANGUAGE_COUNT } from '../site';
import { Badge, SectionHead } from './bits';
import './Languages.css';

export function Languages() {
  return (
    <section className="section" id="languages" aria-labelledby="languages-title">
      <div className="page">
        <SectionHead
          kicker="Languages"
          title={<span id="languages-title">{LANGUAGE_COUNT} languages. <em>Use as many as you like.</em></span>}
        >
          Pick yours once in setup. Languages whose keyboard is already on your PC are picked for you. The app
          reads your actual Windows keyboards, so variants like Canadian French or Swiss German work too.
        </SectionHead>

        <motion.ul
          className="langs"
          initial="hidden"
          whileInView="shown"
          viewport={{ once: true, margin: '0px 0px -10% 0px' }}
          variants={{ shown: { transition: { staggerChildren: .025 } } }}
        >
          {LANGUAGES.map(l => (
            <motion.li
              key={l.code}
              className="lang"
              variants={{ hidden: { opacity: 0, y: 12 }, shown: { opacity: 1, y: 0 } }}
              transition={{ duration: .5, ease: [.2, .8, .2, 1] }}
              whileHover="hover"
            >
              <motion.span
                variants={{ hover: { scale: 1.08, rotate: -3 } }}
                transition={{ type: 'spring', stiffness: 500, damping: 18 }}
              >
                <Badge code={l.code} size="lg" />
              </motion.span>
              <span className="lang-names">
                <span className="lang-native" lang={l.code} dir="auto">{l.nativeName}</span>
                <span className="lang-name">
                  {l.code === 'en' ? 'Always on' : l.name}
                  {l.beta && <span className="lang-beta" title="Newer and less tested">Beta</span>}
                </span>
              </span>
            </motion.li>
          ))}
        </motion.ul>

        <dl className="lang-notes">
          <div>
            <dt>Korean</dt>
            <dd>The Korean keyboard types both Hangul and English, so the app fixes a word by switching the 한/영 mode.</dd>
          </div>
          <div>
            <dt>Thai</dt>
            <dd>Thai is written without spaces, so a whole phrase is checked by splitting it into words.</dd>
          </div>
          <div>
            <dt>Beta</dt>
            <dd>Newer and less tested so far: a smaller word list, or not yet tried on every Windows keyboard.</dd>
          </div>
        </dl>
      </div>
    </section>
  );
}
