using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace LayoutBuddy;

/// <summary>
/// Apps that show a web page through Microsoft's WebView2 without giving it a window of its own (the new WhatsApp,
/// other WinUI apps) report no text cursor and no focused text box on their window. The page can still be reached
/// through a helper window of the WebView2 process the app started, lying over the app; this finds the focused
/// element there. Used from the focus tracker's thread only.
/// </summary>
internal sealed class WebViewContent
{
    private IntPtr _app, _page;
    private AutomationElement? _focused;
    private DateTime _nextSearch;

    /// <summary>Typing goes to a WebView2 part inside the app's own window.</summary>
    public static bool IsHost(IntPtr focusWindow) => Native.ClassName(focusWindow) == "Chrome_WidgetWin_0";

    /// <summary>
    /// The focused element of the web page shown in <paramref name="app"/>. Looking for it takes a while, so with
    /// <paramref name="search"/> false only the one found before is checked.
    /// </summary>
    public AutomationElement? FocusedElement(IntPtr app, bool search)
    {
        if (app != _app)
        {
            _app = app;
            _page = IntPtr.Zero;
            _focused = null;
            _nextSearch = default;
        }
        try
        {
            if (_focused != null && _focused.Current.HasKeyboardFocus) return _focused;
            _focused = null;
            if (!search || DateTime.UtcNow < _nextSearch) return null;

            if (!Native.IsWindow(_page)) _page = FindPage(app);
            if (_page != IntPtr.Zero)
                _focused = AutomationElement.FromHandle(_page).FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.HasKeyboardFocusProperty, true));
        }
        catch (Exception)
        {
            _focused = null;
        }
        // Nothing found (the page isn't there yet, or nothing in it has focus): look again in a while.
        if (_focused == null) _nextSearch = DateTime.UtcNow.AddSeconds(1);
        return _focused;
    }

    /// <summary>The web page's window: shown by a process the app started, over the app's window.</summary>
    private static IntPtr FindPage(IntPtr app)
    {
        Native.GetWindowThreadProcessId(app, out uint appPid);
        if (!Native.GetWindowRect(app, out var appRect)) return IntPtr.Zero;
        var children = ChildProcesses(appPid);
        if (children.Count == 0) return IntPtr.Zero;

        IntPtr found = IntPtr.Zero;
        Native.EnumWindows((w, _) =>
        {
            Native.GetWindowThreadProcessId(w, out uint pid);
            if (!children.Contains(pid) || !Native.IsWindowVisible(w) || !Native.GetWindowRect(w, out var r)) return true;
            if (r.Left >= appRect.Right || r.Right <= appRect.Left || r.Top >= appRect.Bottom || r.Bottom <= appRect.Top) return true;
            found = Native.FindWindowEx(w, IntPtr.Zero, "Chrome_RenderWidgetHostHWND", null);
            return found == IntPtr.Zero;
        }, IntPtr.Zero);
        return found;
    }

    private static HashSet<uint> ChildProcesses(uint parent)
    {
        var result = new HashSet<uint>();
        var snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return result;
        try
        {
            var entry = new Native.PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<Native.PROCESSENTRY32W>() };
            for (bool ok = Native.Process32FirstW(snapshot, ref entry); ok; ok = Native.Process32NextW(snapshot, ref entry))
                if (entry.th32ParentProcessID == parent) result.Add(entry.th32ProcessID);
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }
        return result;
    }
}
