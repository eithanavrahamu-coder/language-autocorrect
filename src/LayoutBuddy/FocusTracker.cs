using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

internal sealed record FocusSnapshot(IntPtr Window, IntPtr FocusWindow, string? Process, Lang? Layout, Rectangle? Caret, bool IsPassword);

/// <summary>
/// Polls the foreground window on a background thread: which app, which layout,
/// where the caret is, and whether a password box has focus.
/// </summary>
internal sealed class FocusTracker : IDisposable
{
    private readonly Thread _thread;
    private readonly Dictionary<uint, string> _processNames = new();
    private readonly WebViewContent _webView = new();
    private volatile bool _stop;
    private volatile FocusSnapshot _snapshot = new(IntPtr.Zero, IntPtr.Zero, null, null, null, false);
    private int _tick;
    private bool _isPassword;

    public FocusSnapshot Snapshot => _snapshot;

    /// <summary>Raised (on the tracker thread) when the foreground window or focused control changes.</summary>
    public event Action? FocusChanged;

    public FocusTracker()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "LayoutBuddy focus" };
        _thread.SetApartmentState(ApartmentState.MTA);
    }

    public void Start() => _thread.Start();

    public void Dispose()
    {
        _stop = true;
        _thread.Join(TimeSpan.FromSeconds(1));
    }

    private void Loop()
    {
        while (!_stop)
        {
            try { Poll(); }
            catch (Exception ex) { Log.Write("Focus poll error: " + ex.Message); }
            Thread.Sleep(60);
        }
    }

    private void Poll()
    {
        _tick++;
        var fg = Native.GetForegroundWindow();
        uint tid = Native.GetWindowThreadProcessId(fg, out uint pid);
        var gti = new Native.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.GUITHREADINFO>() };
        Native.GetGUIThreadInfo(tid, ref gti);
        var focus = gti.hwndFocus != IntPtr.Zero ? gti.hwndFocus : fg;

        var prev = _snapshot;
        bool focusChanged = fg != prev.Window || focus != prev.FocusWindow;
        // Only a different window resets the word being typed. Embedded apps (WhatsApp, Teams...) can move focus
        // between their inner parts on their own; clicks and Tab already reset the word.
        if (fg != prev.Window) FocusChanged?.Invoke();

        // UI Automation is slower, so ask less often (or right away when focus moves).
        bool askUia = focusChanged || _tick % 4 == 0;
        AutomationElement? element = null;
        bool fetched = false;
        AutomationElement? Element()
        {
            if (!fetched) element = FocusedElement(fg, focus, search: askUia);
            fetched = true;
            return element;
        }
        if (askUia) _isPassword = IsPassword(Element());

        var caret = CaretLocator.Find(gti, focus, askUia || prev.Caret == null ? Element : null);
        if (caret == null && !askUia && prev.Caret != null && !focusChanged)
            caret = prev.Caret; // keep last UIA result between UIA polls

        var hkl = LayoutService.CurrentHkl();
        var process = ProcessName(pid);
        if (focusChanged) LogFocusOnce(process, focus, hkl);
        _snapshot = new FocusSnapshot(fg, focus, process, LayoutService.TypedLanguage(hkl),
            caret, _isPassword);
    }

    private readonly HashSet<string> _loggedApps = new();

    /// <summary>Writes one line per app to the log (process, focused window class, keyboard) to help diagnose apps.</summary>
    private void LogFocusOnce(string? process, IntPtr focus, IntPtr hkl)
    {
        if (string.IsNullOrEmpty(process) || !_loggedApps.Add(process)) return;
        var lang = LayoutService.FromHkl(hkl);
        Log.Write($"App '{process}': typing window class '{Native.ClassName(focus)}', keyboard 0x{(long)hkl:X8} ({lang?.ToString() ?? "unsupported"})");
    }

    /// <summary>The focused element through UI Automation (slower, so not on every poll).</summary>
    private AutomationElement? FocusedElement(IntPtr fg, IntPtr focus, bool search)
    {
        AutomationElement? el;
        try
        {
            el = AutomationElement.FocusedElement;
            // Apps like the new WhatsApp: their web page is only reachable through a WebView2 helper window.
            if (WebViewContent.IsHost(focus) && el?.Current.FrameworkId != "Chrome")
                return _webView.FocusedElement(fg, search) ?? el;
        }
        catch
        {
            return null;
        }
        return el;
    }

    private static bool IsPassword(AutomationElement? el)
    {
        try
        {
            return el?.Current.IsPassword == true;
        }
        catch
        {
            return false;
        }
    }

    private string? ProcessName(uint pid)
    {
        if (pid == 0) return null;
        if (_processNames.TryGetValue(pid, out var name)) return name;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            name = p.ProcessName;
        }
        catch
        {
            name = "";
        }
        if (_processNames.Count > 500) _processNames.Clear();
        _processNames[pid] = name;
        return name;
    }
}
