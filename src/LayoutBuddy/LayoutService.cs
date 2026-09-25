using System;
using System.Linq;
using System.Threading.Tasks;
using LayoutBuddy.Engine;
using Microsoft.Win32;

namespace LayoutBuddy;

/// <summary>Reads and switches the keyboard layout of the foreground window.</summary>
internal static class LayoutService
{
    private const int LangHebrew = 0x0D;
    private const int LangEnglish = 0x09;

    private enum SwitchHotkey { AltShift, CtrlShift, WinSpace }

    public static Lang? FromHkl(IntPtr hkl) => ((int)((long)hkl & 0x3FF)) switch
    {
        LangHebrew => Lang.Hebrew,
        LangEnglish => Lang.English,
        _ => null,
    };

    public static IntPtr CurrentHkl()
    {
        var fg = Native.GetForegroundWindow();
        uint tid = Native.GetWindowThreadProcessId(fg, out _);
        return Native.GetKeyboardLayout(tid);
    }

    public static Lang? Current() => FromHkl(CurrentHkl());

    private static IntPtr[] Installed()
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        if (n <= 0) return [];
        var list = new IntPtr[n];
        Native.GetKeyboardLayoutList(n, list);
        return list;
    }

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
        if (Current() == target) return;
        var hotkey = UsableHotkey();
        if (hotkey == null)
        {
            PostSwitch(target);
            return;
        }

        switch (hotkey.Value)
        {
            case SwitchHotkey.AltShift: input.Chord(Native.VK_LMENU, Native.VK_LSHIFT); break;
            case SwitchHotkey.CtrlShift: input.Chord(Native.VK_LCONTROL, Native.VK_LSHIFT); break;
            case SwitchHotkey.WinSpace: input.Chord(Native.VK_LWIN, Native.VK_SPACE); break;
        }

        // Safety net: if the hotkey didn't take effect, switch directly.
        _ = Task.Delay(400).ContinueWith(_ =>
        {
            if (Current() is Lang now && now != target) PostSwitch(target);
        });
    }

    /// <summary>A hotkey can be used only with exactly one English and one Hebrew layout and no Shift/Alt/Win held down.</summary>
    private static SwitchHotkey? UsableHotkey()
    {
        var layouts = Installed();
        if (layouts.Length != 2 || layouts.Any(h => FromHkl(h) == null)) return null;
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
        if (n != 2) return $"{n} layouts installed – switching directly";
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
        return Native.PostMessage(fg, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl.Value);
    }
}
