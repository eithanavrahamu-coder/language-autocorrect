import { Composition } from 'remotion';
import { DURATION, Promo } from './Promo';

export function Root() {
  return <Composition id="Promo" component={Promo} durationInFrames={DURATION} fps={30} width={1920} height={1080} />;
}
