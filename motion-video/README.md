# Motion video

A 54-second motion-graphics video of Language Autocorrect, with music and sound effects, made with
[Remotion](https://www.remotion.dev) (React components rendered frame by frame). It's 1920×1080 at 30 fps.

```
npm install
npm run studio     # preview and scrub through it in the browser
npm run render     # writes out/language-autocorrect.mp4
npm run typecheck
```

`studio` and `render` first run `npm run audio` (`scripts/make-audio.mjs`), which **synthesizes every sound from
code**: the music (120 BPM, about 54 s) and 30 short effects (key clicks, the "fix" chime, whooshes, pops, clicks).
No samples are used, so there's nothing to license. The files go to `public/audio/` and aren't committed.

## What happens when

The music's bars line up with the scenes: bar *n* starts at frame 60·*n*, and the big moments land on its beats.

| Frames | Bars | Scene | File |
| --- | --- | --- | --- |
| 0–240 | 0–3 | The problem: Russian typed on the English keyboard, fixed the old way. *Every. Single. Time.* | `src/scenes/Problem.tsx` |
| 240–360 | 4–5 | The logo lands on the first drop, languages fly out of it | `src/scenes/Reveal.tsx` |
| 360–600 | 6–9 | The core feature: `ghbdtn` + Space → `привет`, the keyboard switches; six more languages flip on the beat | `src/scenes/CoreFeature.tsx` |
| 600–960 | 10–15 | How to use it: install, pick languages, just type (and Backspace undoes a fix) | `src/scenes/HowTo.tsx` |
| 960–1320 | 16–21 | *Just type.* No more Alt+Shift: a chat in three languages, 3 keyboard switches, 0 presses; all 22 languages | `src/scenes/JustType.tsx` |
| 1320–1620 | 22–26 | Download: the button is clicked, then the site's address | `src/scenes/Download.tsx` |

`src/Promo.tsx` places the scenes and the music. Every sound effect sits in the scene it belongs to (`<Sfx name at>`),
so moving an animation moves its sound with it. If you change a scene's length, change the song's sections in
`scripts/make-audio.mjs` (`SONG`) to match.

## Where things come from

- Colors, fonts (Bricolage Grotesque and Onest) and the logo match the app and the website
  (`src/LanguageAutocorrect/UI/app.html`, `website/src/index.css`, `website/scripts/icons.mjs`).
- `src/theme.ts` has a copy of the language list (badge and color) from
  `src/LanguageAutocorrect.Engine/Languages.cs`; update it when a language is added.
- Every wrong-keyboard example was checked with the app's own engine; they're the website demo's examples
  (`website/src/demo/scenes.ts`).
