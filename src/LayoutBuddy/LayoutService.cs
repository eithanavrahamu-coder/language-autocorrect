using System;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

/// <summary>Reads and switches the keyboard layout of the foreground window.</summary>
internal static class LayoutService
{
    private const int LangHebrew = 0x0D;
    private const int LangEnglish = 0x09;

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

    public static IntPtr? FindInstalled(Lang lang)
    {
        int n = Native.GetKeyboardLayoutList(0, null);
        if (n <= 0) return null;
        var list = new IntPtr[n];
        Native.GetKeyboardLayoutList(n, list);
        foreach (var hkl in list)
            if (FromHkl(hkl) == lang) return hkl;
        return null;
    }

    /// <summary>Asks the foreground window to switch to the given language. Returns false if it isn't installed.</summary>
    public static bool Switch(Lang lang)
    {
        var hkl = FindInstalled(lang);
        if (hkl == null) return false;
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        return Native.PostMessage(fg, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl.Value);
    }
}
