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
        if (focusChanged) FocusChanged?.Invoke();

        // UI Automation is slower, so ask less often (or right away when focus moves).
        bool askUia = focusChanged || _tick % 4 == 0;
        if (askUia) _isPassword = IsPasswordFocused();

        var caret = CaretLocator.Find(gti, focus, useUia: askUia || prev.Caret == null);
        if (caret == null && !askUia && prev.Caret != null && !focusChanged)
            caret = prev.Caret; // keep last UIA result between UIA polls

        _snapshot = new FocusSnapshot(fg, focus, ProcessName(pid), LayoutService.FromHkl(Native.GetKeyboardLayout(tid)),
            caret, _isPassword);
    }

    private static bool IsPasswordFocused()
    {
        try
        {
            return AutomationElement.FocusedElement?.Current.IsPassword == true;
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
