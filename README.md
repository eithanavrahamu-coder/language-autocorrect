# Type Language Corrector 4000

*Made by Eithan Avraham*

A Windows app for people who type in Hebrew and English:

- **Auto-correct** – notices words typed in the wrong layout (e.g. `akuo` → `שלום`, `יקךךם` → `hello`) and fixes them when you
  press Space or Enter, then switches the keyboard for you. Words just before it that were typed in the same wrong layout
  are fixed too (`ha jh akuo` → `יש חי שלום`). When both readings are real words, the words before it decide:
  `הוא אוכל far` → `הוא אוכל כשר`, while `it is not far` stays as typed. For the first word of a line, the much more
  common reading wins.
- **Language indicator** – a small `EN` / `עב` badge next to the text cursor.
- **Voice** – says "English" / "עברית" when the keyboard changes.

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

Download `TypeLanguageCorrector4000.exe` (from the latest run on the repository's **Actions** tab → *Build* → *Artifacts*) and run it.
The setup window offers:

- **Install** – copies the app to `%LocalAppData%\Programs\Type Language Corrector 4000`, adds Start menu (and optionally desktop)
  shortcuts, and registers it in **Settings → Apps → Installed apps**. No administrator rights needed.
  Running a newer version later offers **Update** and keeps your settings.
- **Run without installing** – runs it from where it is. `TypeLanguageCorrector4000.exe --portable` skips the setup window.

Uninstall from **Settings → Apps → Installed apps → Type Language Corrector 4000 → Uninstall** (you can choose to keep your settings).

The app window and setup use the Microsoft Edge WebView2 Runtime, which comes with Windows 11 and current Windows 10.
Run `TypeLanguageCorrector4000.exe --selfcheck` to check keyboard layouts, voices and detection.
Settings and log: `%AppData%\TypeLanguageCorrector4000\`.

## Building

Requires the .NET 10 SDK.

```
dotnet test tests/LayoutBuddy.Engine.Tests
dotnet publish src/LayoutBuddy -c Release -p:PublishSingleFile=true -o publish
```

The app was previously called LayoutBuddy; installing this version replaces an old LayoutBuddy install and keeps its
settings. The code still uses `LayoutBuddy` as its internal project name.

- `src/LayoutBuddy.Engine` – detection, undo and never-fix logic (plain .NET, unit tested).
- `src/LayoutBuddy` – the Windows app (keyboard hook, caret tracking, indicator, voice, tray, installer).
- `src/LayoutBuddy/UI` – the app window and setup window (HTML pages shown with WebView2). Open them directly in a
  browser to preview with sample data (`app.html?page=words`, `setup.html?mode=uninstall`).

## Credits

Word frequency lists: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, based on OpenSubtitles data, licensed CC BY-SA 4.0.
