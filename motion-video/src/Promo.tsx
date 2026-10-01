import { AbsoluteFill, Sequence, staticFile, useCurrentFrame } from 'remotion';
import { Audio } from '@remotion/media';
import { Backdrop } from './components/bits';
import { CoreFeature } from './scenes/CoreFeature';
import { Download } from './scenes/Download';
import { HowTo } from './scenes/HowTo';
import { JustType } from './scenes/JustType';
import { Problem } from './scenes/Problem';
import { Reveal, revealRadius } from './scenes/Reveal';
import { C } from './theme';

export const DURATION = 1620; // 54 s at 30 fps, as long as public/audio/music.wav

/** The music steps back a little while words are typed in the two drops, so the key clicks come through. */
const TYPING: [number, number][] = [[372, 472], [1022, 1236]];
const musicVolume = (f: number) => {
  const dip = Math.max(...TYPING.map(([a, b]) => Math.min(1, Math.max(0, (f - a + 8) / 8), Math.max(0, (b + 8 - f) / 8))));
  return 0.5 - 0.12 * dip;
};

/** One light background behind every scene after the problem, so scenes cross-fade over it without a jump. */
function LightBackdrop() {
  const f = useCurrentFrame();
  return (
    <AbsoluteFill style={{ clipPath: f < 23 ? `circle(${revealRadius(f)}px at 50% 50%)` : undefined }}>
      <Backdrop />
    </AbsoluteFill>
  );
}

// Scenes, in video frames (bar n of the music starts at frame 60·n):
//   0–240     the problem: typing on the wrong keyboard, fixing it by hand        bars 0–3
//   240–360   the logo lands on the first drop                                    bars 4–5
//   360–600   the core feature: Space fixes the word and switches the keyboard     bars 6–9
//   600–960   how to use it: install, pick languages, just type (and undo)         bars 10–15
//   960–1320  just type: no more Alt+Shift, then all 22 languages                  bars 16–21
//   1320–1620 download                                                             bars 22–26
export function Promo() {
  return (
    <AbsoluteFill style={{ background: C.dark }}>
      <Sequence name="Problem" durationInFrames={256}><Problem /></Sequence>
      <Sequence name="Background" from={230}><LightBackdrop /></Sequence>
      <Sequence name="Reveal" from={230} durationInFrames={130}><Reveal /></Sequence>
      <Sequence name="Core feature" from={355} durationInFrames={255}><CoreFeature /></Sequence>
      <Sequence name="How to use it" from={595} durationInFrames={367}><HowTo /></Sequence>
      <Sequence name="Just type" from={950} durationInFrames={370}><JustType /></Sequence>
      <Sequence name="Download" from={1316}><Download /></Sequence>
      <Audio src={staticFile('audio/music.wav')} volume={musicVolume} name="music" />
    </AbsoluteFill>
  );
}
