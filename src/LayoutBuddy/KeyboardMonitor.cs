using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

/// <summary>What the hook needs to know about the current situation (updated from other threads).</summary>
internal sealed record MonitorContext(bool AutoCorrect, Sensitivity Sensitivity, bool Blocked);

/// <summary>
/// Global low-level keyboard + mouse hooks on a dedicated thread. Feeds keys into a
/// <see cref="TypingSession"/> and performs the fixes/undos it asks for.
/// </summary>
internal sealed class KeyboardMonitor : IDisposable
{
    private readonly TypingSession _session;
    private readonly Func<MonitorContext> _context;
    private readonly Native.HookProc _kbProc;
    private readonly Native.HookProc _mouseProc;
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _kbHook, _mouseHook;
    private volatile bool _resetRequested;

    // The keyboard switch after a fix waits a moment (see ScheduleSwitch). Only used on the hook thread.
    private readonly Native.TimerProc _switchTimerProc;
    private (Lang Target, IntPtr Window)? _pendingSwitch;
    private IntPtr _switchTimer;

    public event Action<FixWord>? Fixed;
    public event Action<UndoFix>? Undone;

    public bool IsRunning => _kbHook != IntPtr.Zero;

    public KeyboardMonitor(TypingSession session, Func<MonitorContext> context)
    {
        _session = session;
        _context = context;
        _kbProc = KeyboardProc;
        _mouseProc = MouseProc;
        _switchTimerProc = OnSwitchTimer;
    }

    public void Start()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = Native.GetCurrentThreadId();
            var mod = Native.GetModuleHandle(null);
            _kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbProc, mod, 0);
            _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, mod, 0);
            if (_kbHook == IntPtr.Zero) Log.Write($"Keyboard hook failed: {Marshal.GetLastWin32Error()}");
            ready.Set();
            while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }
            if (_kbHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_kbHook);
            if (_mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(_mouseHook);
            _kbHook = _mouseHook = IntPtr.Zero;
        })
        {
            IsBackground = true,
            Name = "LayoutBuddy hook",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Forget the current word (called when focus moves).</summary>
    public void RequestReset() => _resetRequested = true;

    public void Dispose()
    {
        if (_threadId != 0) Native.PostThreadMessage(_threadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN)
            {
                var m = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if ((m.flags & Native.LLMHF_INJECTED) == 0) _session.ResetAll();
            }
        }
        return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && ((int)wParam == Native.WM_KEYDOWN || (int)wParam == Native.WM_SYSKEYDOWN))
        {
            var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            if ((k.flags & Native.LLKHF_INJECTED) == 0)
            {
                try
                {
                    if (HandleKey((int)k.vkCode, (int)k.scanCode, (k.flags & LLKHF_EXTENDED) != 0)) return 1;
                }
                catch (Exception ex)
                {
                    Log.Write("Hook error: " + ex);
                    _session.ResetAll();
                }
            }
        }
        return Native.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private static bool IsModifier(int vk) => vk is Native.VK_SHIFT or Native.VK_LSHIFT or Native.VK_RSHIFT
        or Native.VK_CONTROL or Native.VK_LCONTROL or Native.VK_RCONTROL
        or Native.VK_MENU or Native.VK_LMENU or Native.VK_RMENU
        or Native.VK_LWIN or Native.VK_RWIN or Native.VK_CAPITAL;

    private const uint LLKHF_EXTENDED = 0x01;

    /// <summary>Returns true to swallow the key.</summary>
    private bool HandleKey(int vk, int scanCode, bool extended)
    {
        if (IsModifier(vk)) return false;

        if (_resetRequested)
        {
            _resetRequested = false;
            _session.ResetAll();
        }

        // A keyboard switch still waiting after a fix happens before this key, so the key is typed in the new language.
        if (_pendingSwitch is { } waiting && waiting.Window != Native.GetForegroundWindow()) CancelSwitch();
        var switching = _pendingSwitch?.Target;

        var layout = switching ?? LayoutService.Current();
        if (layout == null)
        {
            _session.ResetAll();
            return false;
        }

        var ctx = _context();
        var input = Classify(vk, scanCode, extended);
        var action = _session.OnKey(input, layout.Value, ctx.AutoCorrect && !ctx.Blocked, ctx.Sensitivity, DateTime.UtcNow);

        switch (action)
        {
            case FixWord fix:
            {
                var send = new InputSender();
                TakeSwitch(send);
                // The Korean IME may still be putting the last syllable together: pressing Hangul/English twice
                // finishes it (keeping the mode), so each Backspace deletes a whole syllable.
                if (layout == Lang.Korean) send.Tap(Native.VK_HANGUL, 2);
                send.Tap(Native.VK_BACK, fix.Backspaces)
                    .Text(fix.Text)
                    .Tap(fix.Boundary == KeyKind.Enter ? Native.VK_RETURN : Native.VK_SPACE);
                ScheduleSwitch(fix.Layout, send.Send());
                Fixed?.Invoke(fix);
                return true;
            }

            case UndoFix undo:
            {
                // If the fix hasn't switched the keyboard yet, it simply doesn't.
                CancelSwitch();
                var send = new InputSender()
                    .ReleaseModifiers()
                    .Tap(Native.VK_BACK, undo.Backspaces)
                    .Text(undo.Text);
                ScheduleSwitch(undo.Layout, send.Send());
                Undone?.Invoke(undo);
                return true;
            }

            default:
            {
                if (switching == null) return false;
                // Switch first, then type this key: the user's key would otherwise arrive before the switch.
                var send = new InputSender();
                TakeSwitch(send);
                send.KeyDown(vk, scanCode, extended).Send();
                return true;
            }
        }
    }

    /// <summary>
    /// Switches the keyboard to <paramref name="target"/> shortly after a fix was typed. Some apps (WhatsApp and other
    /// apps built on web pages) handle a keyboard switch before the keystrokes still waiting for them, and lose the
    /// last letters of the fix when both are sent together. Longer fixes get a little more time.
    /// </summary>
    private void ScheduleSwitch(Lang target, int keyEvents)
    {
        CancelSwitch();
        _pendingSwitch = (target, Native.GetForegroundWindow());
        _switchTimer = Native.SetTimer(IntPtr.Zero, IntPtr.Zero, (uint)Math.Min(150, 40 + keyEvents), _switchTimerProc);
    }

    /// <summary>Adds the waiting keyboard switch, if any, to <paramref name="send"/>.</summary>
    private void TakeSwitch(InputSender send)
    {
        if (_pendingSwitch is not { } pending) return;
        CancelSwitch();
        // The user went to another window meanwhile: leave its keyboard alone.
        if (Native.GetForegroundWindow() == pending.Window) LayoutService.AppendSwitch(send, pending.Target);
    }

    private void CancelSwitch()
    {
        _pendingSwitch = null;
        if (_switchTimer != IntPtr.Zero) Native.KillTimer(IntPtr.Zero, _switchTimer);
        _switchTimer = IntPtr.Zero;
    }

    private void OnSwitchTimer(IntPtr hWnd, uint msg, IntPtr idEvent, uint time)
    {
        try
        {
            var send = new InputSender();
            TakeSwitch(send);
            send.Send();
        }
        catch (Exception ex)
        {
            Log.Write("Keyboard switch error: " + ex);
        }
    }

    /// <summary>
    /// Scan code (the key's physical position) -> the character that key types on a US keyboard.
    /// Physical position, not the virtual key, because French/German keyboards move letters around.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<int, char> PhysicalKeyByScanCode = BuildScanCodes();

    private static System.Collections.Generic.Dictionary<int, char> BuildScanCodes()
    {
        var map = new System.Collections.Generic.Dictionary<int, char> { [0x29] = '`', [0x2B] = '\\' };
        void Row(int first, string keys)
        {
            for (int i = 0; i < keys.Length; i++) map[first + i] = keys[i];
        }
        Row(0x02, "1234567890-=");
        Row(0x10, "qwertyuiop[]");
        Row(0x1E, "asdfghjkl;'");
        Row(0x2C, "zxcvbnm,./");
        return map;
    }

    /// <summary>The scan code of each physical key (used to read the user's actual keyboard layouts).</summary>
    public static System.Collections.Generic.IEnumerable<(int ScanCode, char Key)> PhysicalKeys =>
        PhysicalKeyByScanCode.Select(kv => (kv.Key, kv.Value));

    private static KeyInput Classify(int vk, int scanCode, bool extended)
    {
        bool ctrl = Native.IsDown(Native.VK_CONTROL);
        bool alt = Native.IsDown(Native.VK_MENU);
        bool win = Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);

        if (ctrl && !alt && !win && vk == 'Z') return KeyInput.CtrlZ;
        if (ctrl || alt || win) return KeyInput.Other;

        switch (vk)
        {
            case Native.VK_BACK: return KeyInput.Backspace;
            case Native.VK_SPACE: return KeyInput.Space;
            case Native.VK_RETURN: return KeyInput.Enter;
        }

        if (extended || !PhysicalKeyByScanCode.TryGetValue(scanCode, out char key)) return KeyInput.Other;
        bool shift = Native.IsDown(Native.VK_SHIFT);
        bool caps = Native.IsToggled(Native.VK_CAPITAL);
        return KeyInput.Word(key, shifted: shift || caps);
    }
}
