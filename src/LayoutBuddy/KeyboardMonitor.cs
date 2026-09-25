using System;
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

    public event Action<FixWord>? Fixed;
    public event Action<UndoFix>? Undone;

    public bool IsRunning => _kbHook != IntPtr.Zero;

    public KeyboardMonitor(TypingSession session, Func<MonitorContext> context)
    {
        _session = session;
        _context = context;
        _kbProc = KeyboardProc;
        _mouseProc = MouseProc;
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
                    if (HandleKey((int)k.vkCode)) return 1;
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

    /// <summary>Returns true to swallow the key.</summary>
    private bool HandleKey(int vk)
    {
        if (IsModifier(vk)) return false;

        if (_resetRequested)
        {
            _resetRequested = false;
            _session.ResetAll();
        }

        var layout = LayoutService.Current();
        if (layout == null)
        {
            _session.ResetAll();
            return false;
        }

        var ctx = _context();
        var input = Classify(vk);
        var action = _session.OnKey(input, layout.Value, ctx.AutoCorrect && !ctx.Blocked, ctx.Sensitivity, DateTime.UtcNow);

        switch (action)
        {
            case FixWord fix:
            {
                var send = new InputSender()
                    .Tap(Native.VK_BACK, fix.Backspaces)
                    .Text(fix.Text)
                    .Tap(fix.Boundary == KeyKind.Enter ? Native.VK_RETURN : Native.VK_SPACE);
                LayoutService.AppendSwitch(send, fix.Layout);
                send.Send();
                Fixed?.Invoke(fix);
                return true;
            }

            case UndoFix undo:
            {
                var send = new InputSender()
                    .ReleaseModifiers()
                    .Tap(Native.VK_BACK, undo.Backspaces)
                    .Text(undo.Text);
                LayoutService.AppendSwitch(send, undo.Layout);
                send.Send();
                Undone?.Invoke(undo);
                return true;
            }

            default:
                return false;
        }
    }

    private static KeyInput Classify(int vk)
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

        bool shift = Native.IsDown(Native.VK_SHIFT);
        if (vk >= 'A' && vk <= 'Z')
        {
            bool caps = Native.IsToggled(Native.VK_CAPITAL);
            return KeyInput.Word((char)('a' + (vk - 'A')), shifted: shift || caps);
        }

        char oem = vk switch
        {
            0xBA => ';',
            0xDE => '\'',
            0xBC => ',',
            0xBE => '.',
            0xBF => '/',
            _ => '\0',
        };
        if (oem != '\0' && !shift) return KeyInput.Word(oem);
        return KeyInput.Other;
    }
}
