import { MotionConfig } from 'motion/react';
import { DownloadNotice } from './components/bits';
import { Header, Hero } from './components/Hero';
import { Features, HowItWorks } from './components/Features';
import { Languages } from './components/Languages';
import { Screens } from './components/Screens';
import { Faq, FinalCta, Footer, Install } from './components/Install';

export default function App() {
  return (
    <MotionConfig reducedMotion="user">
      <a className="skip" href="#main">Skip to content</a>
      <Header />
      <main id="main">
        <Hero />
        <HowItWorks />
        <Languages />
        <Features />
        <Screens />
        <Install />
        <Faq />
        <FinalCta />
      </main>
      <Footer />
      <DownloadNotice />
    </MotionConfig>
  );
}
