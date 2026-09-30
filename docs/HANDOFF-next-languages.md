# Handoff: add the next wave of languages to Language Autocorrect

You are continuing work on **Language Autocorrect** (made by Eithan Avraham), a Windows tray app that notices
words typed on the wrong keyboard layout (e.g. `ghbdtn` → `привет`, `akuo` → `שלום`) and fixes them when the
user presses Space or Enter, then switches the keyboard.

Repository: `eithanavrahamu-coder/language-autocorrect` (branch `main`). Clone it, read `README.md`, then this file.

## Wave 3 (versions 3.5.0–3.9.0): Spanish, Portuguese, Turkish, Italian, Urdu

Done, one commit each. Keyboard maps were converted from Microsoft's own layout files (kbdlayout.info, `.klc`
downloads) rather than typed by hand. What the engine gained on the way:

- **Latin-script languages.** Most of their words type the same as English and are left alone ("same text");
  only words with accents or extra letters differ. Their accuracy is measured on those words only, so the
  samples are small (Italian 57 words).
- **Accents on Shift** (Spanish ¨, Portuguese ^ and `, Italian é): `ShiftKeyboard` may contain dead keys and
  letters that aren't capitals; `KeyMap.ShiftTypesLetter` counts both, but never a plain capital.
- **`JoinsWithApostrophe`** (English, French, Italian): don't = don + 't, l'uomo = l' + uomo. Only short
  forms ranked in the top 1000 count, because the lists also hold junk pieces like 'a.
- **`DottedI`** (Turkish): i/İ and ı/I. All case changes go through `LanguageInfo.ToUpper/ToLower`.
- **Punctuation that is really a letter:** a word isn't "known as typed" if it starts with punctuation, or has
  ; [ ] \ ` = inside or right after it, where the other keyboard types a letter (".ok" is Turkish çok, "per;"
  is Italian però, "ma;ana" is mañana).
- **Capitals on punctuation keys:** Shift + a key that types a letter in one of the user's languages starts a
  capitalized word (">ok" becomes Çok, ":bpym" becomes Жизнь).
- **One-letter words** are fixed only if two keys typed them and they are among the most common (Portuguese é).
- Urdu is Beta (a small word list: ~8,700 words).

Known gaps: Italian è is a single key ([ on the English keyboard) and can't be fixed on its own; words
starting with ¿ or ¡ are skipped.

Next candidates (word lists exist in FrequencyWords): Hindi, Bengali (medium work: InScript keyboards type many
letters with Shift), Romanian, Hungarian, Czech, Slovak, Swedish, Finnish, Norwegian, Danish (easy, like Spanish).

## Status (version 3.3.0)

All eight languages below are done, one commit each. Notes for whoever continues:

- **Word lists.** Georgian's FrequencyWords list is mostly Bulgarian/Macedonian/Russian subtitles in a broken
  encoding (cp1251 read as a Georgian 8-bit encoding); those entries were filtered out, leaving ~10,400 real words.
  Kazakh (~4,700) and Armenian (~6,900) lists are small; rarer words rely on the letter model. Serbian is mostly
  transliterated from Latin-script subtitles (forms without diacritics and English words dropped). Thai mojibake
  (`เธ...`) was removed.
- **Shift letters.** `LanguageInfo.ShiftKeyboard` holds what Shift types where it types letters of its own
  (Georgian QWERTY, Korean, Thai). Such keys are kept in the key sequence as the US shifted character (`T`, `:`).
  Languages without a `ShiftKeyboard` behave exactly as before.
- **Korean** (`LanguageInfo.JoinsSyllables`, `Hangul.cs`): jamo are composed like the IME does. On Windows,
  `LayoutService.TypedLanguage` reads the IME mode with `WM_IME_CONTROL`/`IMC_GETCONVERSIONMODE`; the Korean keyboard
  in English mode counts as English, and switching presses `VK_HANGUL`. Before deleting a Korean word, `VK_HANGUL` is
  pressed twice to finish the syllable the IME is still composing. **None of this could be tried on a real Windows
  machine** – if Korean misbehaves, check the log and these three places first. If the mode can't be read, Korean is
  treated as unsupported in that window (no fixes) rather than guessed.
- **Thai** (`LanguageInfo.WithoutSpaces`): `LanguageModel.Rank` splits text that isn't a dictionary entry into the
  fewest known words and ranks it by the rarest one.
- Belarusian is still missing (no FrequencyWords list).

## Your task

Add these languages, in this order. Commit and push after each language (or each small group) so work is never lost.

1. **Bulgarian** (`bg`), **Serbian Cyrillic** (`sr`), **Macedonian** (`mk`), **Kazakh** (`kk`) – Cyrillic, same approach as Russian.
2. **Georgian** (`ka`), **Armenian** (`hy`) – own alphabets, same approach.
3. **Korean** (`ko`) – needs extra work, see "Special cases".
4. **Thai** (`th`) – needs extra work, see "Special cases".

Belarusian has no word list in the source below; skip it.

## How the code is organized

- `src/LanguageAutocorrect.Engine/` – plain .NET 10 library, no Windows calls, fully unit tested. **Most of your work is here.**
  - `Languages.cs` – **the language registry.** One `LanguageInfo` per language: enum value, ISO code, English and
    native name, 2-letter badge, badge color, Windows primary language id, alphabet (lowercase letters), whether it
    has upper/lower case, right-to-left flag, and `Keyboard`: what each of the 47 physical keys types, in the order
    ``` ` 1 2 3 4 5 6 7 8 9 0 - = q w e r t y u i o p [ ] \ a s d f g h j k l ; ' z x c v b n m , . / ```
    (US key names). A token `~` + combining accent + spacing accent marks a dead key (see Greek/French/German).
    Like Windows, a dead key joins only some letters (´ + e = é, but ´ + m = "´m", two characters): the built-in
    maps join only into Latin-1, Greek and Cyrillic letters, and the app reads the exact pairs from the user's own
    layout (`LayoutService.ReadJoins`). A fix deletes as many characters as `Render` says were typed, so this matters.
  - `KeyMap.cs` – renders physical keys in a language (handles dead keys, multi-letter keys like Arabic `لا`,
    capital first letter), and `ToUsKeys` (text → keys, used by tests).
  - `LanguageModel.cs` – loads `Data/<code>.txt` (word + count per line, most frequent first), word ranks and a
    character trigram model.
  - `WrongLayoutDetector.cs` – decides if keys typed in language A are really a word in language B.
  - `TypingSession.cs` – the typing state machine (current word, earlier words, sentence context, undo).
- `src/LanguageAutocorrect/` – the Windows app (WinForms + WebView2 pages in `UI/`). It builds on Linux
  (`EnableWindowsTargeting`) but can only run on Windows. `LayoutService.cs` maps Windows keyboards to languages
  (`FromHkl` → `Languages.FromWindowsLangId`) and also reads the user's real layouts with `ToUnicodeEx` at runtime,
  overriding the static `Keyboard` strings – so a slightly wrong static map is survivable, but get it right anyway
  because the tests use it.
- `src/LanguageAutocorrect/UI/app.html` and `setup.html` – the app window and installer. They list languages from the
  registry automatically; you only need to update the **sample data** at the bottom of each file (used for browser
  previews) if you want the new languages to show in previews.
- `tests/LanguageAutocorrect.Engine.Tests/` – xUnit. `MultiLanguageTests.cs` is the template for new languages.

## Recipe for a "simple" language (all of wave 1 and 2)

1. **Word list.** Download from FrequencyWords (Hermit Dave, CC BY-SA 4.0 – already credited in the README):
   `https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/<code>/<code>_50k.txt`
   (for `kk` and `hy` only `<code>_full.txt` exists – keep the top 50,000 lines).
   Filter to words made only of the language's alphabet, lowercase, NFC-normalized, deduplicated, format `word count`,
   and save as `src/LanguageAutocorrect.Engine/Data/<code>.txt`. (All `Data/*.txt` files are embedded automatically.)
   **Serbian:** the subtitle corpus is largely in Latin script. Keep Cyrillic words; you may also transliterate Latin
   Serbian words to Cyrillic (the mapping is 1:1: lj→љ, nj→њ, dž→џ, etc.) to get a bigger list. Check the result.
2. **Registry entry** in `Languages.cs`: add the enum value to `Lang` and a `LanguageInfo`. Pick a distinct badge color.
   Keyboard maps to use (standard Windows layouts):
   - Bulgarian: Windows' default "Bulgarian" is the old BDS/typewriter layout; there is also "Bulgarian (Phonetic)".
     Use the default one for the static map (runtime reading handles the others).
   - Serbian Cyrillic, Macedonian, Kazakh, Georgian, Armenian: the standard Windows layout for each.
     Look each one up carefully (e.g. Microsoft's keyboard layout pages at
     `learn.microsoft.com/globalization/windows-keyboard-layouts`) – verify every key.
3. **Windows language id.** `Languages.FromWindowsLangId` matches the *primary* language id only.
   **Serbian and Croatian/Bosnian share primary id `0x1A`** – Serbian Cyrillic must be told apart from Latin layouts
   by the full LANGID / keyboard layout id. Extend `LanguageInfo` (e.g. an optional set of full LANGIDs or layout ids)
   and `LayoutService.FromHkl` accordingly, and make sure Croatian keyboards are *not* treated as Serbian Cyrillic.
   Macedonian `0x2F`, Bulgarian `0x02`, Kazakh `0x3F`, Georgian `0x37`, Armenian `0x2B`.
4. **Tests** in `MultiLanguageTests.cs`: add 2+ words to `FixesWordTypedOnEnglishKeyboard`, 1 to
   `FixesEnglishTypedOnOtherKeyboard`, and the language to `NewLanguages` (the accuracy test). Required per language:
   **wrongly changed < 2 %** and **caught > 80 %** (current languages get ~0.1 % and ~97 %). Print the numbers with
   `dotnet test --logger "console;verbosity=detailed"`. If a language falls short, tune only in ways that keep every
   other language's numbers where they are.
5. **Closely related languages together:** Russian/Ukrainian/Bulgarian/Serbian/Macedonian/Kazakh share most keys and
   many words. Check that with several Cyrillic languages enabled at once, the right one wins
   (`TypingSession.BestFix` picks the highest `Detection.Score`). Add a test like `PicksTheRightLanguageAmongSeveral`.
6. `README.md`: add the language to the **Languages** line.

## Special cases

**Korean.** Two differences from everything else:
- Letters (jamo) combine into syllable blocks as you type (`ㅇ+ㅏ+ㄴ` → `안`). `KeyMap.Render` must compose jamo into
  syllables (standard 2-set/Dubeolsik rules: initial/medial/final, double finals, a final moving to the next syllable
  when a vowel follows). `안녕` is typed with keys `dkssud`. Backspace deletes one jamo, not one key's worth of text –
  `TypingSession` already stops tracking a word on Backspace when keys aren't "simple"; make sure Korean is treated
  that way. The number of characters to delete when fixing is the rendered length (syllables), which the code already
  uses.
- On Windows, Korean is an **IME**: the keyboard layout stays Korean while the user toggles Hangul/English mode
  (Right Alt / 한/영 key). So "typed in the wrong mode" is an IME conversion-mode question, not a layout switch.
  You will need: detection of the Korean IME's current mode (`ImmGetConversionStatus` via `ImmGetContext` /
  `ImmGetDefaultIMEWnd` + `WM_IME_CONTROL`), treating "Korean layout + English mode" as English, and switching by
  toggling the mode (send `VK_HANGUL`, or set the conversion status) instead of the layout. Keep this behind clean
  functions in `LayoutService`.

**Thai.** Thai doesn't put spaces between words, so "check the word on Space" sees a whole phrase. Evaluate the
phrase by splitting it into dictionary words (longest-match segmentation over the Thai word list) and score how much
of it is covered by real words, versus how much of the English reading is real words. Thai also has characters on
Shift that are common – check how `TypingSession` treats Shift (it currently only allows Shift on the first letter
of languages with case) and adapt for Thai.

Do Korean and Thai **after** wave 1 and 2 are merged and pushed. If either turns out much bigger than expected,
stop, push what works (behind the language being off by default), and write down what's left.

## Rules

- **Don't break existing languages.** All existing tests must keep passing – especially the Hebrew tests and the
  accuracy numbers. Run `dotnet test tests/LanguageAutocorrect.Engine.Tests` before every commit.
- Build the Windows app too: `dotnet build src/LanguageAutocorrect` must succeed with no warnings.
- The .NET 10 SDK may not be installed: `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0`.
- Settings must stay backward compatible (`AppSettings` is loaded from users' existing JSON files).
- Voice is off by default; don't change defaults.
- Bump `<Version>` in `src/LanguageAutocorrect/LanguageAutocorrect.csproj` (currently 3.2.2) when you're done – e.g. 3.3.0.
- Every push to `main` triggers GitHub Actions (`.github/workflows/build.yml`), which runs the tests on Windows and
  produces the `.exe` as an artifact named **LanguageAutocorrect**. Check that the run succeeds and give the owner
  the run's link (Actions → Build → the run → Artifacts).
- Keep code in the existing style: small focused classes, comments only where the *why* isn't obvious.

## Talking to the owner

The owner, Eithan, is not a programmer. Explain results in plain language: what now works, how to get it
(the Actions link), and what to try (e.g. "add the Bulgarian keyboard in Windows, type `ghbdtn`..."). Be honest
about anything you couldn't verify – nobody can run the app on Windows from the build environment; only the unit
tests and the Windows CI build verify it.
