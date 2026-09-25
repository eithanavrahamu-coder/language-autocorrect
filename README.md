# LayoutBuddy

A small Windows tray app for people who type in Hebrew and English:

- **Language indicator** – a small `EN` / `עב` badge next to the text cursor.
- **Voice** – says "English" / "עברית" when the layout changes.
- **Auto-correct** – notices a word typed in the wrong layout (e.g. `akuo` → `שלום`, `יקךךם` → `hello`) and fixes it when you press Space or Enter, switching the layout for you.

## Undo and "words never to fix"

- To undo a correction, press **Backspace right after it** (within about 1.5 seconds, before typing anything else), or **Ctrl+Z** within 5 seconds.
- Backspace pressed later, or after typing something else, is treated as normal editing and is **not** an undo.
- A word goes on the "never fix" list only after you undo it **3 times** (changeable in Settings, 1–10). A notification tells you when that happens.
- Settings → "Words never to fix" shows the list; you can remove words, clear it, or add words by hand.

Auto-correct is always off in password boxes and in the apps listed in Settings.

## Running

Download `LayoutBuddy.exe` (from the latest run on the repository's **Actions** tab → *Build* → *Artifacts*) and run it. It lives in the tray near the clock; right-click for options, double-click for Settings.
Run `LayoutBuddy.exe --selfcheck` to check keyboard layouts, voices and detection.

Settings and log: `%AppData%\LayoutBuddy\`.

## Building

Requires the .NET 10 SDK.

```
dotnet test tests/LayoutBuddy.Engine.Tests
dotnet publish src/LayoutBuddy -c Release -p:PublishSingleFile=true -o publish
```

- `src/LayoutBuddy.Engine` – detection, undo and never-fix logic (plain .NET, unit tested).
- `src/LayoutBuddy` – the Windows app (keyboard hook, caret tracking, indicator, voice, tray, settings).

## Credits

Word frequency lists: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, based on OpenSubtitles data, licensed CC BY-SA 4.0.
