import { AbsoluteFill, Html5Audio, staticFile } from 'remotion';
import './fonts.ts';
import { Background } from './parts/Background.tsx';
import { Apps } from './scenes/Apps.tsx';
import { Hero } from './scenes/Hero.tsx';
import { Languages } from './scenes/Languages.tsx';
import { Outro } from './scenes/Outro.tsx';
import { Undo } from './scenes/Undo.tsx';

/** The whole 20-second video. Each scene shows itself only during its part (see timeline.ts). */
export function Promo() {
  return (
    <AbsoluteFill>
      <Background />
      <Hero />
      <Languages />
      <Undo />
      <Apps />
      <Outro />
      {/* Made by scripts/sound.ts from the same timeline (`npm run sound`). */}
      <Html5Audio src={staticFile('soundtrack.wav')} />
    </AbsoluteFill>
  );
}
