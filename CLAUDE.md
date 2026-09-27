# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Language Autocorrect is a Windows tray app. When a word is typed on the wrong keyboard layout (`ghbdtn` → `привет`), it
fixes the word on Space/Enter and switches the keyboard. The code still uses the old internal name **LayoutBuddy**
(projects, namespaces, `RootNamespace`), but the product name, exe name (`LanguageAutocorrect.exe`) and `AppInfo` use
"Language Autocorrect". Also read `README.md` for user-facing behavior and `docs/HANDOFF-next-languages.md` for
language-adding history and the per-language recipe.

## Commands

.NET 10 SDK (the app targets `net10.0-windows`; it builds anywhere thanks to `EnableWindowsTargeting` but only runs
on Windows):

```
dotnet test tests/LayoutBuddy.Engine.Tests
dotnet test tests/LayoutBuddy.Engine.Tests --filter "FullyQualifiedName~MultiLanguageTests.FixesWordTypedOnEnglishKeyboard"
dotnet test tests/LayoutBuddy.Engine.Tests --logger "console;verbosity=detailed"   # prints per-language accuracy numbers
dotnet build src/LayoutBuddy                                                     # must build with no warnings
dotnet publish src/LayoutBuddy -c Release -p:PublishSingleFile=true -o publish   # single self-contained exe
```

`LanguageAutocorrect.exe --selfcheck` checks keyboards, voices and detection on a real machine; `--portable` skips setup.

Website (`website/`, React + Vite, Node 24): `npm run dev`, `npm run build`, `npm run lint` (oxlint),
`npm run screenshots` (retakes the app screenshots with Playwright). Promo video (`video/`, Remotion; also needs
ffmpeg): `npm run studio`, `npm run render`, `npm run typecheck`.

## Architecture

**`src/LayoutBuddy.Engine`**: a plain .NET library with no Windows calls. All detection logic lives here, and it is
the only unit-tested part.
- `Languages.cs` is the language registry: one `LanguageInfo` per language, including `Keyboard`, which says what each of
  47 physical keys types, in US-key order (`` ` 1 2 … = q w … / ``). A `~` token marks a dead key. Optional init
  properties turn on per-language behavior: `ShiftKeyboard`, `JoinsSyllables` (Korean), `WithoutSpaces` (Thai),
  `JoinsWithApostrophe`, `DottedI` (Turkish) and `Beta`. Keys are always carried as **US key characters**
  and only rendered into a language by `KeyMap`.
- `Data/<code>.txt` holds FrequencyWords lists (`word count`, most frequent first, CC BY-SA 4.0). Every file is embedded
  automatically. `LanguageModel` builds word ranks plus a character trigram model from them.
- `WrongLayoutDetector` decides whether keys typed in layout A are really a word in language B.
  `TypingSession.OnKey` is the typing state machine: it tracks the current word and earlier words for sentence
  context, and returns `PassThrough`, `FixWord` or `UndoFix`. When several languages match, `BestFix` picks the
  highest `Detection.Score`.
- `UndoTracker` (Backspace right after a fix / Ctrl+Z), `NeverFixList`, and `AppSettings` (JSON in
  `%AppData%\LanguageAutocorrect\`) all live here too. **Settings must stay backward compatible** with users' existing
  JSON files.

**`src/LayoutBuddy`**: the WinForms app.
- `Program.Main` dispatches on arguments: `--uninstall`, `--update` (run by the updater: install over the existing
  copy silently), or setup when the exe isn't the installed copy. Otherwise it starts `TrayApp`.
- `KeyboardMonitor` runs global low-level keyboard and mouse hooks on their own thread and feeds each key to
  `TypingSession`. On a fix, it sends Backspaces + text via `InputSender`, then switches the keyboard after a short
  delay (`ScheduleSwitch`).
- `LayoutService` maps Windows HKLs to `Lang`. It reads the user's real layouts with `ToUnicodeEx` at runtime (these
  override the static `Keyboard` maps) and switches by pressing the user's own hotkey (Alt+Shift / Ctrl+Shift /
  Win+Space). Korean is handled as IME mode, not a layout switch.
- `TrayApp` is the controller. It implements `IAppController` (`BuildState` / `HandleAction`) for the app window.
- `WebWindow` hosts the UI pages in WebView2: `MainWindow` shows `UI/app.html` and `SetupWindow` shows `UI/setup.html`.
  Both pages are embedded resources that talk to C# through `chrome.webview.postMessage`. Open them in a normal
  browser to preview with the mock data at the bottom of each file (`app.html?page=words`, `setup.html?screen=languages`,
  `setup.html?mode=update`). Languages come from the registry; only that sample data needs manual updating.
- `Installer` copies the running exe to `%LocalAppData%\Programs\Language Autocorrect`, then writes shortcuts, the
  uninstall registry entry and the run key. It also migrates installs from the old names (LayoutBuddy, Type Language
  Corrector 4000).
- `Updater` reads `AppInfo.Website + "version.json"` once a day, downloads the exe it lists, and runs it with
  `--update`.

**`website/`**: before `dev`/`build`, `scripts/prepare.mjs` **parses the C# in `Languages.cs`** (a hand-written reader
for the `new(Lang.X, …)` entries and their `{ … }` property blocks) and reads `<Version>` from the csproj. It writes
`src/generated/app-info.json`, the demo word lists and `public/version.json`. When you change the shape of a
`LanguageInfo` entry, keep that parser working. `video/scripts/data.ts` reuses it, reading from the **last commit**.

## Release flow

Every push to `main` runs `.github/workflows/build.yml`:
1. Tests run and the single-file exe is published (Windows runner), then uploaded as the `LanguageAutocorrect` artifact.
2. The website is built with `DOWNLOAD_BYTES` set, the exe is copied next to it, and the result is deployed to GitHub
   Pages at https://language-autocorrect.world (custom domain set in the repo's Pages settings, Source = GitHub Actions).

So every push to `main` updates the public download. **Bump `<Version>` in `src/LayoutBuddy/LayoutBuddy.csproj` for
any app change**, or installed copies won't be offered the update (the site's `version.json` comes from that number).

## Working with the owner

The owner is not a programmer. Explain results in plain language: what now works, what to try, and what couldn't be
verified. Only unit tests and the Windows CI build verify changes; the app's hooks, IME and UI can't be exercised from
here. Commit and push after each separate piece of work (for example, one commit per language). When done, give the
link to the successful Actions run.

When adding a language, follow `docs/HANDOFF-next-languages.md`. Add tests in `MultiLanguageTests.cs`: wrongly
changed must stay under 2 % and caught above 80 %. Don't move any existing language's numbers, and check that related
languages (e.g. the Cyrillic ones) still pick the right winner when several are on together. Voice stays off by
default.
