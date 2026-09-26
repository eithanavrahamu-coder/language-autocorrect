# Promo video

`LanguageAutocorrect-promo.mp4`: a 20-second video of the app (1920×1080, 60 fps, with sound), made with
[Remotion](https://www.remotion.dev) from the app's own look, examples and language list. Needs Node.js and ffmpeg.

```
npm install
npm run studio    # preview it in the browser, with a timeline to scrub
npm run render    # make LanguageAutocorrect-promo.mp4 again
```

- `src/timeline.ts` – every moment of the video, in seconds. Change timings here.
- `src/scenes/` – the five parts: the fix, the languages, undo, every app, and the end.
- `scripts/sound.ts` – every sound effect is synthesized here (no recordings, so no licenses) and placed at the
  moments in the timeline, then saved as `public/soundtrack.wav` at a normal loudness (-16 LUFS).
- `npm run stills -- 2.5 8.4` saves single frames (at those seconds) to `out/`, to look at.

The languages (badges and colors) are read from the app's code, through the website's `scripts/prepare.mjs`.
