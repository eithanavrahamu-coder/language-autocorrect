import { Composition } from 'remotion';
import { Promo } from './Promo.tsx';
import { FPS, HEIGHT, SECONDS, WIDTH } from './timeline.ts';

export function Root() {
  return (
    <Composition id="Promo" component={Promo} durationInFrames={SECONDS * FPS} fps={FPS} width={WIDTH} height={HEIGHT} />
  );
}
