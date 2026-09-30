using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LanguageAutocorrect.Engine;
using Microsoft.Win32;

namespace LanguageAutocorrect;

/// <summary>Reads and switches the keyboard layout of the foreground window.</summary>
internal static class LayoutService
{
    private enum SwitchHotkey { AltShift, CtrlShift, WinSpace }

    private static readonly ConcurrentDictionary<IntPtr, Lang?> LanguageByHkl = new();

    /// <summary>
    /// The language of a keyboard layout, from its Windows language id (the low word of the HKL) – unless the
    /// layout clearly types another script, like a US keyboard added under Hebrew or a Latin keyboard added
    /// under Serbian (Cyrillic).
    /// </summary>
    public static Lang? FromHkl(IntPtr hkl) => LanguageByHkl.GetOrAdd(hkl, h =>
    {
        var info = Languages.FromWindowsLangId((int)((long)h & 0xFFFF));
        // The Korean keyboard itself types English letters; its IME turns them into Hangul.
        return info == null || (info.Lang != Lang.Korean && TypesOtherScript(h, info)) ? null : info.Lang;
    });

    private static bool TypesOtherScript(IntPtr hkl, LanguageInfo info)
    {
        try
        {
            var tokens = ReadKeyboard(hkl);
            return tokens != null && tokens.Count(t => info.IsLetter(t[0])) < 10;
        }
        catch (Exception ex)
        {
            Log.Write($"Checking keyboard {info.Name} failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// The window that actually receives typing. Apps like WhatsApp, Teams or Store apps show a frame window
    /// but type into an embedded part that runs on another thread (or in another process) with its own
    /// keyboard language, so the foreground window's language can be wrong.
    /// </summary>
    public static IntPtr FocusWindow()
    {
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return IntPtr.Zero;
        uint tid = Native.GetWindowThreadProcessId(fg, out _);
        var gti = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        if (Native.GetGUIThreadInfo(tid, ref gti) && gti.hwndFocus != IntPtr.Zero) return gti.hwndFocus;

        // Store apps: the frame (ApplicationFrameHost) hosts the app's CoreWindow from another process.
        if (Native.ClassName(fg) == "ApplicationFrameWindow")
        {
            var core = Native.FindWindowEx(fg, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (core != IntPtr.Zero) return core;
        }
        return fg;
    }

    public static IntPtr CurrentHkl()
    {
        var target = FocusWindow();
        uint tid = Native.GetWindowThreadProcessId(target, out _);
        var hkl = Native.GetKeyboardLayout(tid);
        if (hkl == IntPtr.Zero)
        {
            // Fall back to the foreground window's thread.
            tid = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
            hkl = Native.GetKeyboardLayout(tid);
        }
        return hkl;
    }

    /// <summary>The language typed in the focused window right now.</summary>
    public static Lang? Current() => TypedLanguage(CurrentHkl());

    /// <summary>The language a keyboard types right now: the Korean keyboard types English in its English mode.</summary>
    public static Lang? TypedLanguage(IntPtr hkl)
    {
        var lang = FromHkl(hkl);
        if (lang != Lang.Korean) return lang;
        // If the mode can't be read, don't guess: fixing English as if it were Korean would switch the mode.
        return KoreanImeMode() is int mode ? ((mode & IME_CMODE_NATIVE) != 0 ? Lang.Korean : Lang.English) : null;
    }

    /// <summary>The installed keyboard layouts, in the order Windows cycles through them.</summary>
    private static IntPtr[] Installed()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        if (n <= 0) return [];
        var list = new IntPtr[n];
        Native.GetKeyboardLayoutList(n, list);
        return list;
    }

    /// <summary>Supported languages that have a keyboard installed in Windows.</summary>
    public static IReadOnlyList<Lang> InstalledLanguages() =>
        Installed().Select(FromHkl).OfType<Lang>().Distinct().ToList();

    public static IntPtr? FindInstalled(Lang lang)
    {
        foreach (var hkl in Installed())
            if (FromHkl(hkl) == lang) return hkl;
        return null;
    }

    /// <summary>
    /// Adds the keystrokes that switch to <paramref name="target"/> to <paramref name="input"/>, or switches directly.
    /// Pressing the user's own language hotkey keeps Windows' language state in sync, so the user's
    /// Alt+Shift / Win+Space keeps working normally afterwards. Posting WM_INPUTLANGCHANGEREQUEST
    /// changes the layout behind Windows' back and can make the next hotkey press seem to do nothing.
    /// </summary>
    public static void AppendSwitch(InputSender input, Lang target)
    {
        var current = CurrentHkl();
        if (FromHkl(current) == Lang.Korean && target is Lang.Korean or Lang.English)
        {
            // Korean and English are two modes of the Korean keyboard: press Hangul/English.
            if (TypedLanguage(current) is Lang now && now != target) input.Tap(Native.VK_HANGUL);
            return;
        }
        if (FromHkl(current) == target) return;

        // Without an English keyboard, English is typed with the Korean keyboard's English mode.
        var keyboard = target == Lang.English && FindInstalled(Lang.English) == null && FindInstalled(Lang.Korean) != null
            ? Lang.Korean
            : target;

        // The hotkey moves to the next layout in the list; press it as many times as needed.
        var layouts = Installed();
        int from = Array.IndexOf(layouts, current);
        int to = Array.FindIndex(layouts, h => FromHkl(h) == keyboard);
        var hotkey = UsableHotkey();
        if (hotkey == null || from < 0 || to < 0 || layouts.Length > 4)
        {
            PostSwitch(keyboard);
        }
        else
        {
            int presses = (to - from + layouts.Length) % layouts.Length;
            for (int i = 0; i < presses; i++)
            {
                switch (hotkey.Value)
                {
                    case SwitchHotkey.AltShift: input.Chord(Native.VK_LMENU, Native.VK_LSHIFT); break;
                    case SwitchHotkey.CtrlShift: input.Chord(Native.VK_LCONTROL, Native.VK_LSHIFT); break;
                    case SwitchHotkey.WinSpace: input.Chord(Native.VK_LWIN, Native.VK_SPACE); break;
                }
            }

            // Safety net: if the hotkey didn't land on the right layout, switch directly.
            _ = Task.Delay(400).ContinueWith(_ =>
            {
                if (FromHkl(CurrentHkl()) is Lang now && now != keyboard) PostSwitch(keyboard);
            });
        }

        // The Korean keyboard comes back in whichever mode it was left in.
        if (keyboard == Lang.Korean) _ = Task.Delay(600).ContinueWith(_ => SetKoreanMode(hangul: target == Lang.Korean));
    }

    // ---------------- the Korean IME's Hangul and English modes ----------------

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeoutMs, out IntPtr result);

    private const uint WM_IME_CONTROL = 0x283;
    private const int IMC_GETCONVERSIONMODE = 0x1, IMC_SETCONVERSIONMODE = 0x2;
    private const int IME_CMODE_NATIVE = 0x1; // Hangul mode
    private const uint SMTO_ABORTIFHUNG = 0x2;

    /// <summary>The Korean IME's conversion mode in the window being typed into, or null if it can't be read.</summary>
    private static int? KoreanImeMode()
    {
        var ime = ImmGetDefaultIMEWnd(FocusWindow());
        if (ime == IntPtr.Zero) return null;
        var ok = SendMessageTimeout(ime, WM_IME_CONTROL, IMC_GETCONVERSIONMODE, IntPtr.Zero, SMTO_ABORTIFHUNG, 50, out var mode);
        return ok == IntPtr.Zero ? null : (int)mode;
    }

    /// <summary>Puts the Korean IME of the window being typed into in Hangul or English mode.</summary>
    private static void SetKoreanMode(bool hangul)
    {
        if (FromHkl(CurrentHkl()) != Lang.Korean || KoreanImeMode() is not int mode) return;
        int wanted = hangul ? mode | IME_CMODE_NATIVE : mode & ~IME_CMODE_NATIVE;
        if (wanted == mode) return;
        var ime = ImmGetDefaultIMEWnd(FocusWindow());
        SendMessageTimeout(ime, WM_IME_CONTROL, IMC_SETCONVERSIONMODE, wanted, SMTO_ABORTIFHUNG, 100, out _);
    }

    /// <summary>A hotkey can be used only when no Shift/Alt/Win is held down.</summary>
    private static SwitchHotkey? UsableHotkey()
    {
        // (Ctrl is released by the caller before an undo, so it isn't checked here.)
        if (Native.IsDown(Native.VK_SHIFT) || Native.IsDown(Native.VK_MENU)
            || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN))
            return null;
        return ConfiguredHotkey();
    }

    private static SwitchHotkey ConfiguredHotkey()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Toggle");
            var v = (k?.GetValue("Language Hotkey") ?? k?.GetValue("Hotkey")) as string;
            return v switch
            {
                null or "1" => SwitchHotkey.AltShift,
                "2" => SwitchHotkey.CtrlShift,
                _ => SwitchHotkey.WinSpace, // "3" = none assigned, "4" = grave accent
            };
        }
        catch
        {
            return SwitchHotkey.AltShift;
        }
    }

    public static string DescribeSwitchMethod()
    {
        int n = Installed().Length;
        if (n > 4) return $"{n} layouts installed – switching directly";
        return ConfiguredHotkey() switch
        {
            SwitchHotkey.AltShift => "Alt+Shift",
            SwitchHotkey.CtrlShift => "Ctrl+Shift",
            _ => "Win+Space",
        };
    }

    /// <summary>Asks the foreground window to switch layouts directly.</summary>
    public static bool PostSwitch(Lang lang)
    {
        var hkl = FindInstalled(lang);
        if (hkl == null) return false;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        // Ask both the window being typed into and its frame (embedded apps may listen on either).
        var focus = FocusWindow();
        bool ok = Native.PostMessage(fg, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl.Value);
        if (focus != IntPtr.Zero && focus != fg)
            ok |= Native.PostMessage(focus, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl.Value);
        return ok;
    }

    // ---------------- reading the user's real keyboards ----------------

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    private const uint MAPVK_VSC_TO_VK = 1;
    private const uint DontChangeKeyboardState = 0x4;

    /// <summary>
    /// Asks Windows what each physical key types on the user's installed layouts and teaches the engine,
    /// so variants (Canadian French, Swiss German, Persian Standard...) are handled exactly.
    /// </summary>
    public static void LoadInstalledKeyboards()
    {
        foreach (var hkl in Installed())
        {
            // The Korean keyboard types English letters; its Hangul letters come from the IME.
            if (FromHkl(hkl) is not Lang lang || lang == Lang.Korean) continue;
            try
            {
                var tokens = ReadKeyboard(hkl);
                // Georgian and similar keyboards type letters of their own with Shift.
                var shifted = tokens != null && Languages.Get(lang).ShiftKeyboard != null ? ReadKeyboard(hkl, shift: true) : null;
                if (tokens != null) KeyMap.SetKeyboard(lang, tokens, shifted, JoinsFor(hkl, lang));
            }
            catch (Exception ex)
            {
                Log.Write($"Reading keyboard {lang} failed: {ex.Message}");
            }
        }
    }

    /// <summary>Each keyboard's accent joins, read once (see <see cref="ReadJoins"/>).</summary>
    private static readonly ConcurrentDictionary<IntPtr, IReadOnlyDictionary<(char Accent, char Letter), char>?> JoinsByHkl = new();

    /// <summary>What the keyboard's accent keys join with, or null (the engine's usual rule) if it can't be read.</summary>
    private static IReadOnlyDictionary<(char Accent, char Letter), char>? JoinsFor(IntPtr hkl, Lang lang)
    {
        try
        {
            return JoinsByHkl.GetOrAdd(hkl, ReadJoins);
        }
        catch (Exception ex)
        {
            Log.Write($"Reading the accent keys of keyboard {lang} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Which letters each accent (dead) key joins with on this keyboard. Windows joins ´ then e into é, but types ´ then
    /// m as "´m", and the letters differ from keyboard to keyboard (Latin American Spanish joins ´ then c into ç). A fix
    /// deletes as many characters as were typed, so this has to be exact. Found by pressing each accent key and then each
    /// key, the way Windows does while typing: without <see cref="DontChangeKeyboardState"/>, so the accent carries over
    /// to the next key. A space after every try clears any accent still waiting, so nothing is left pending.
    /// </summary>
    private static IReadOnlyDictionary<(char Accent, char Letter), char>? ReadJoins(IntPtr hkl)
    {
        var plain = new byte[256];
        var shifted = new byte[256];
        shifted[Native.VK_SHIFT] = 0x80;
        var buf = new StringBuilder(8);
        int Press(uint vk, uint sc, bool shift, uint flags)
        {
            buf.Clear();
            return ToUnicodeEx(vk, sc, shift ? shifted : plain, buf, buf.Capacity, flags, hkl);
        }
        void ClearAccent() => Press(Native.VK_SPACE, 0x39, false, 0);

        // Each key by itself, with and without Shift: an accent key, or a key that types one character.
        var accents = new List<(uint Vk, uint Sc, bool Shift, char Accent)>();
        var letters = new List<(uint Vk, uint Sc, bool Shift, char Letter)>();
        foreach (var (scanCode, _) in KeyboardMonitor.PhysicalKeys)
        {
            uint sc = (uint)scanCode;
            uint vk = MapVirtualKeyEx(sc, MAPVK_VSC_TO_VK, hkl);
            if (vk == 0) continue;
            foreach (bool shift in new[] { false, true })
            {
                int n = Press(vk, sc, shift, DontChangeKeyboardState);
                if (n < 0 && buf.Length > 0 && !accents.Exists(a => a.Accent == buf[0])) accents.Add((vk, sc, shift, buf[0]));
                else if (n == 1) letters.Add((vk, sc, shift, buf[0]));
            }
        }
        if (accents.Count == 0) return null;

        var joins = new Dictionary<(char Accent, char Letter), char>();
        ClearAccent();
        foreach (var a in accents)
        {
            foreach (var l in letters)
            {
                // One character back means Windows joined them (or typed just one of them): either way, one on screen.
                if (Press(a.Vk, a.Sc, a.Shift, 0) < 0 && Press(l.Vk, l.Sc, l.Shift, 0) == 1) joins[(a.Accent, l.Letter)] = buf[0];
                ClearAccent();
            }
        }
        return joins;
    }

    private static string[]? ReadKeyboard(IntPtr hkl, bool shift = false)
    {
        var byKey = KeyboardMonitor.PhysicalKeys.ToDictionary(p => p.Key, p => p.ScanCode);
        var tokens = new string[KeyMap.PhysicalKeys.Length];
        var state = new byte[256];
        if (shift) state[Native.VK_SHIFT] = 0x80;
        var buf = new StringBuilder(8);
        int letters = 0;

        for (int i = 0; i < KeyMap.PhysicalKeys.Length; i++)
        {
            uint sc = (uint)byKey[KeyMap.PhysicalKeys[i]];
            uint vk = MapVirtualKeyEx(sc, MAPVK_VSC_TO_VK, hkl);
            buf.Clear();
            int n = vk == 0 ? 0 : ToUnicodeEx(vk, sc, state, buf, buf.Capacity, DontChangeKeyboardState, hkl);
            if (n < 0)
            {
                // Dead key: Windows gives the accent itself; clear the pending state and record a combining accent.
                string spacing = buf.Length > 0 ? buf.ToString(0, 1) : "´";
                buf.Clear();
                ToUnicodeEx(vk, sc, state, buf, buf.Capacity, DontChangeKeyboardState, hkl);
                char? combining = CombiningFor(spacing[0]);
                tokens[i] = combining is char c ? $"~{c}{spacing}" : spacing;
            }
            else if (n > 0)
            {
                var text = buf.ToString(0, n);
                tokens[i] = text.Contains(' ') ? "�" : text;
                if (char.IsLetter(text[0])) letters++;
            }
            else
            {
                tokens[i] = "�"; // this key types nothing on this layout
            }
        }
        return shift || letters >= 20 ? tokens : null; // sanity check
    }

    private static char? CombiningFor(char spacing) => spacing switch
    {
        '´' or '΄' or '\'' => '́',
        '`' => '̀',
        '^' => '̂',
        '¨' => '̈',
        '~' => '̃',
        'ˇ' => '̌',
        '¸' => '̧',
        _ => null,
    };
}
