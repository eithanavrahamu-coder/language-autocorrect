# Language Autocorrect

*Made by Eithan Avraham*

A Windows app for people who type in more than one language. It notices words typed on the wrong keyboard
and fixes them.

**Languages:** English, Hebrew, Russian, Arabic, Ukrainian, Persian, Greek, French (AZERTY), German (QWERTZ),
Bulgarian, Serbian (Cyrillic), Macedonian, Kazakh, Georgian, Armenian, Korean, Thai, Spanish and Portuguese.
Pick yours once in setup (languages whose keyboard is already in Windows are pre-selected) and change them any time in
Settings → Languages. The app reads your actual Windows keyboards, so variants like Canadian French or Swiss German
work too.

- **Auto-correct** – notices words typed in the wrong layout (e.g. `ghbdtn` → `привет`, `akuo` → `שלום`, `zhqt` → `what`)
  and fixes them when you press Space or Enter, then switches the keyboard for you. With several languages on, the one
  where the word makes the most sense wins. Words just before it that were typed in the same wrong layout
  are fixed too (`ha jh akuo` → `יש חי שלום`). When both readings are real words, the words before it decide:
  `הוא אוכל far` → `הוא אוכל כשר`, while `it is not far` stays as typed. For the first word of a line, the much more
  common reading wins. A capital first letter is kept (`Yeit` → `Zeit`).
- **Korean** – the Korean keyboard types both Hangul and English (switched with the 한/영 key or Right Alt), so for
  Korean the app looks at the Hangul/English mode instead of the keyboard, and fixes a word by switching the mode
  (`dkssud` → `안녕`, `ㅗ디ㅣㅐ` → `hello`). Laughing and crying (`ㅋㅋㅋ`, `ㅠㅠ`) are left alone.
- **Thai** – Thai is written without spaces, so what is typed before Space is often a whole phrase; it is checked by
  splitting it into dictionary words (`l;ylfu` → `สวัสดี`, `-v[86I,kd` → `ขอบคุณมาก`).
- **Language indicator** – a small colored badge (`EN`, `עב`, `РУ`, `ΕΛ`...) next to the text cursor.
- **Voice** (off by default; offered in setup, or turn it on in Settings) – says the language's name when the keyboard
  changes (in that language if Windows has a voice for it).

Click the tray icon (or open it from the Start menu) for the app window: Home (recent fixes and stats),
Never fix (your word list), and Settings.

It switches the keyboard by pressing your own language shortcut (Alt+Shift, Ctrl+Shift or Win+Space), so
Windows stays in sync and your shortcut keeps working normally.

## Undo and "Never fix"

- To undo a correction, press **Backspace right after it** (within about 1.5 seconds, before typing anything else), or
  **Ctrl+Z** within 5 seconds. Backspace later, or after typing something else, is normal editing.
- **Never fix** is a list of words that are never auto-corrected. Add words in the app window, or hover a recent fix
  and choose *Never fix*.
- **Learn from undos** (off by default): when on, a word you undo several times (1–10, default 3) is added to the list.

Auto-correct is always off in password boxes, in the app's own window, and in the apps listed in Settings.

## Installing and uninstalling

Download `LanguageAutocorrect.exe` from the website, **https://eithanavrahamu-coder.github.io/language-auto/**
(or from the latest run on the repository's **Actions** tab → *Build* → *Artifacts*) and run it.
The setup window offers:

- **Install** – copies the app to `%LocalAppData%\Programs\Language Autocorrect`, adds Start menu (and optionally desktop)
  shortcuts, and registers it in **Settings → Apps → Installed apps**. No administrator rights needed.
  Running a newer version later offers **Update** and keeps your settings.
- **Run without installing** – runs it from where it is. `LanguageAutocorrect.exe --portable` skips the setup window.

Uninstall from **Settings → Apps → Installed apps → Language Autocorrect → Uninstall** (you can choose to keep your settings).

The app window and setup use the Microsoft Edge WebView2 Runtime, which comes with Windows 11 and current Windows 10.
Run `LanguageAutocorrect.exe --selfcheck` to check keyboard layouts, voices and detection.
Settings and log: `%AppData%\LanguageAutocorrect\`.

## Building

Requires the .NET 10 SDK.

```
dotnet test tests/LayoutBuddy.Engine.Tests
dotnet publish src/LayoutBuddy -c Release -p:PublishSingleFile=true -o publish
```

The download page is in `website/` (React, built with Vite; needs Node.js). `npm run dev` there shows it locally.
It reads the languages and the version from the app's code, and every build on `main` publishes it to GitHub Pages
together with the app it just built. `npm run screenshots` retakes the app screenshots it shows.

The app was previously called LayoutBuddy and then Type Language Corrector 4000; installing this version replaces
installs under those names and keeps their settings. The code still uses `LayoutBuddy` as its internal project name.

- `src/LayoutBuddy.Engine` – detection, undo and never-fix logic (plain .NET, unit tested).
- `src/LayoutBuddy` – the Windows app (keyboard hook, caret tracking, indicator, voice, tray, installer).
- `src/LayoutBuddy/UI` – the app window and setup window (HTML pages shown with WebView2). Open them directly in a
  browser to preview with sample data (`app.html?page=words`, `setup.html?mode=uninstall`).
- `video` – a 20-second promo video of the app, with sound effects (see `video/README.md`).

## Credits

Word frequency lists: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, based on OpenSubtitles data, licensed CC BY-SA 4.0.
