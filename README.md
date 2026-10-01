# Language Autocorrect

*Made by Eithan Avraham*

An app for Windows (and, in beta, for Mac) for people who type in more than one language. It notices words typed on
the wrong keyboard and fixes them.

**Languages:** English, Hebrew, Russian, Arabic, Ukrainian, Persian, Greek, French (AZERTY), German (QWERTZ),
Bulgarian, Serbian (Cyrillic), Macedonian, Kazakh, Georgian, Armenian, Korean, Thai, Spanish, Portuguese, Turkish, Italian and Urdu.
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
- **Language indicator** – a small colored badge (`EN`, `עב`, `РУ`, `ΕΛ`...) next to the text cursor. It can be turned
  off in setup or in Settings.
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
  and choose *Never fix*. Words you put on the list yourself stay there until you remove them, whatever the numbers
  below are set to.
- **Ask about words you keep undoing** (off by default, offered in setup): once you've undone the same word several
  times (1–10, default 5), a small card by your text asks *Stop fixing akuo?* **Stop fixing** adds it to the list;
  **Keep fixing** starts the count again. Ignored, the card asks again at the word's next undo.
- **Learn from undos** (off by default): when on, a word you undo several times (1–10, default 3) is added to the list
  without asking. Turning one of these two on turns the other off.

Auto-correct is always off in password boxes, in the app's own window, and in the apps listed in Settings.

## Installing and uninstalling

Download `LanguageAutocorrect.exe` from the website, **https://language-autocorrect.world** (its Download button gets
it from the repository's **Releases**; or take it from the latest run on the **Actions** tab → *Build* → *Artifacts*)
and run it.
Setup takes four short steps, each with a colorful side that shows what it's about: a welcome with a typing demo
(click **Try it now** to type in it yourself; the app's own engine fixes the words, before anything is installed),
your languages (the ones already in Windows are picked), a few preferences (already set), and done, with tips and
a reminder to add any keyboard Windows doesn't have yet. Everything is pre-chosen, so *Get started → Next → Install*
is enough.

- **Install** – copies the app to `%LocalAppData%\Programs\Language Autocorrect`, adds Start menu (and optionally desktop)
  shortcuts, and registers it in **Settings → Apps → Installed apps**. No administrator rights needed.
  Running a newer version later offers **Update** and keeps your settings; running the same version offers to open it.
- **Run without installing** – runs it from where it is. `LanguageAutocorrect.exe --portable` skips the setup window.

Uninstall from **Settings → Apps → Installed apps → Language Autocorrect → Uninstall** (you can choose to keep your settings).

### Updates

Once a day the app asks the download page for the newest version number (`version.json`, published by the website
build next to the app). When there's a newer one, the tray says so once, and the app window offers **Update now**
(on Home, and in Settings → Updates, which also has **Check for updates** and a switch for the daily check; the tray
menu has it too). Updating downloads the new version, checks it's the app at that version, and runs it with
`--update`: it closes the running copy, installs over it keeping all settings, and opens again. Nothing about you is
sent. A copy run without installing opens the download page instead.

After an update (automatic, or by running a newer setup), the next time the window opens a small **What's new** window
shows a page for each version since the one that was replaced, oldest first, with **Next** and **Close**. The pages come
from the website's [release notes](https://language-autocorrect.world/release-notes/), so nothing is kept on the PC;
the app only remembers the old version number until the pages have been shown (without internet it tries again next
time). Settings → Updates → **Release notes** opens the same page in the browser.

The app window and setup use the Microsoft Edge WebView2 Runtime, which comes with Windows 11 and current Windows 10.
Run `LanguageAutocorrect.exe --selfcheck` to check keyboard layouts, voices and detection.
Settings and log: `%AppData%\LanguageAutocorrect\`.

## Mac (beta)

The Mac app (macOS 14 or newer, Apple silicon and Intel) is a menu bar app with the same engine and the same app window
(Home, Never fix, Settings). The website's Download button offers it to Mac visitors as `LanguageAutocorrect.dmg`:
open it, drag Language Autocorrect to Applications and open it from there. It isn't signed with a paid Apple developer
account, so the first time macOS says it can't check it: click **Done**, then **System Settings → Privacy & Security →
Open Anyway**. Then the app asks for the **Accessibility** permission (System Settings → Privacy & Security →
Accessibility), which macOS requires before an app can see and type keys in other apps; its window shows a card until
it's given. After an update macOS may want it again (remove the app from that list with − and allow it again).

- The menu bar icon shows the keyboard's language as a colored badge (gray while paused or waiting for permission);
  its menu opens the window, pauses auto-correct, checks for updates and quits.
- It reads the keyboards added in System Settings → Keyboard → Text Input, with their real layouts (like the Windows
  app), and switches between them directly. Undo is Backspace right after a fix or **⌘Z**.
- Not on the Mac yet: Korean, the badge next to the cursor, the fix cards and the card that asks about words you keep
  undoing. Voice, Never fix, learning from undos, sensitivity, apps to leave alone and opening at login are there.
- Updates: it checks `version.json` once a day like the Windows app, but a new version is downloaded from the website
  and dragged over the old one (settings stay). After that, the window shows What's new.
- Settings and log: `~/Library/Application Support/LanguageAutocorrect/`. Dragging the app to the Trash leaves them.
- `LanguageAutocorrect --selfcheck` (the program inside the app) starts it, checks keyboards, detection, the menu bar
  icon and the window, prints the results and quits; the build runs it on a Mac.

## Building

Requires the .NET 10 SDK.

```
dotnet test tests/LanguageAutocorrect.Engine.Tests
dotnet publish src/LanguageAutocorrect -c Release -p:PublishSingleFile=true -o publish
```

The Mac app needs the .NET macOS workload (`dotnet workload install macos`). `dotnet build src/LanguageAutocorrect.Mac`
checks its code on any computer; the app itself is built on a Mac with Xcode, which the Build workflow does (`mac` job:
build, sign ad hoc, `--selfcheck`, disk image). Pushing a branch named `try/…` runs both builds without releasing
anything.

The download page is in `website/` (React, built with Vite; needs Node.js). `npm run dev` there shows it locally.
It reads the languages and the version from the app's code, and every build on `main` publishes it to GitHub Pages
together with the app it just built. `npm run screenshots` retakes the app screenshots it shows.
The first build of each version also makes a GitHub release with both apps, which the Download button links to
(it offers the one for the visitor's computer, worked out in the browser), so GitHub counts the downloads; [language-autocorrect.world/downloads/](https://language-autocorrect.world/downloads/)
shows the count (updates the app installs itself come from the site's own copy and aren't counted).

The app was previously called LayoutBuddy and then Type Language Corrector 4000; installing this version replaces
installs under those names and keeps their settings.

- `src/LanguageAutocorrect.Engine` – detection, undo and never-fix logic (plain .NET, unit tested).
- `src/LanguageAutocorrect` – the Windows app (keyboard hook, caret tracking, indicator, voice, tray, installer).
- `src/LanguageAutocorrect/UI` – the app window and setup window (HTML pages shown with WebView2). Open them directly in a
  browser to preview with sample data (`app.html?page=words`, `app.html?paused=1`, `app.html?update=available`,
  `app.html?mac=1&permission=0`, `setup.html?screen=languages`, `setup.html?mode=update`, `setup.html?mode=uninstall`).
- `src/LanguageAutocorrect.Mac` – the Mac app (event tap, keyboards, menu bar icon, voice, and the same app window in a
  web view).
- `video` – a 20-second promo video of the app, with sound effects (see `video/README.md`).
- `motion-video` – a 54-second motion-graphics video (the problem, the core feature, how to use it, the download) with
  its own synthesized music and sound effects (see `motion-video/README.md`).

## Privacy

What you type is checked on your computer and never saved or sent anywhere. The app goes online only for updates and the
"What's new" pages, and the website has no cookies or tracking, just a count of downloads that GitHub keeps. The full
policy, with the contact address, is at
[language-autocorrect.world/privacy/](https://language-autocorrect.world/privacy/) (`website/src/privacy/Privacy.tsx`;
keep it true whenever the app starts saving or sending something new).

## Credits

Word frequency lists: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, based on OpenSubtitles data, licensed CC BY-SA 4.0.
The copies in `src/LanguageAutocorrect.Engine/Data` are changed: filtered to each language's alphabet, lowercased,
deduplicated, some cut to the 50,000 most common words, and broken entries removed. The other libraries and fonts, with
their license texts, are listed on the [privacy and credits page](https://language-autocorrect.world/privacy/#credits).

## License

MIT – see [LICENSE](LICENSE). The word lists in `src/LanguageAutocorrect.Engine/Data` are not covered by it: they keep their
own license, CC BY-SA 4.0 (see Credits).
