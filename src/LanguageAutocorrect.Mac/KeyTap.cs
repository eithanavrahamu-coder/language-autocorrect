using System;
using CoreFoundation;
using CoreGraphics;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Mac;

/// <summary>What the key watcher needs to know about the current situation.</summary>
internal sealed record TapContext(bool AutoCorrect, Sensitivity Sensitivity, bool Blocked);

/// <summary>
/// Watches the keys typed in every app (an event tap, which needs the Accessibility permission), feeds them into a
/// <see cref="TypingSession"/> and performs the fixes and undos it asks for. Runs on the main thread, where macOS
/// wants keyboards to be read and switched.
/// </summary>
internal sealed unsafe class KeyTap
{
    private readonly TypingSession _session;
    private readonly Func<TapContext> _context;
    private readonly CGEvent.CGEventTapCallback _callback;
    private CFMachPort? _port;
    private CFRunLoopSource? _source;

    public event Action<FixWord>? Fixed;
    public event Action<UndoFix>? Undone;

    public bool IsRunning => _port != null;

    public KeyTap(TypingSession session, Func<TapContext> context)
    {
        _session = session;
        _context = context;
        _callback = OnEvent;
    }

    /// <summary>Starts watching. False if macOS doesn't allow it (yet).</summary>
    public bool Start()
    {
        if (_port != null) return true;
        var mask = (CGEventMask)(Bit(CGEventType.KeyDown) | Bit(CGEventType.LeftMouseDown) | Bit(CGEventType.RightMouseDown)
                                 | Bit(CGEventType.OtherMouseDown));
        _port = CGEvent.CreateTap(CGEventTapLocation.Session, CGEventTapPlacement.HeadInsert, CGEventTapOptions.Default,
            mask, _callback, IntPtr.Zero);
        if (_port == null) return false;
        _source = _port.CreateRunLoopSource();
        CFRunLoop.Main.AddSource(_source, CFRunLoop.ModeCommon);
        CGEvent.TapEnable(_port);
        Log.Write("Watching the keyboard");
        return true;
    }

    private static ulong Bit(CGEventType type) => 1UL << (int)type;

    /// <summary>Forgets the words typed so far (another app came to the front).</summary>
    public void Reset() => _session.ResetAll();

    private IntPtr OnEvent(IntPtr proxy, CGEventType type, IntPtr ev, IntPtr info)
    {
        try
        {
            if (type is CGEventType.TapDisabledByTimeout or CGEventType.TapDisabledByUserInput)
            {
                // macOS turns a slow watcher off; turn it back on.
                if (_port != null) CGEvent.TapEnable(_port);
                return ev;
            }
            if (Native.CGEventGetIntegerValueField(ev, Native.SourceUserDataField) == KeyPoster.Marker) return ev;
            if (type == CGEventType.KeyDown) return HandleKey(ev) ? IntPtr.Zero : ev;
            // A click: the text cursor may be somewhere else now.
            _session.ResetAll();
        }
        catch (Exception ex)
        {
            Log.Write("Key error: " + ex);
            _session.ResetAll();
        }
        return ev;
    }

    /// <summary>Returns true to swallow the key.</summary>
    private bool HandleKey(IntPtr ev)
    {
        var layout = InputSources.Current();
        if (layout == null)
        {
            _session.ResetAll();
            return false;
        }

        var ctx = _context();
        int keycode = (int)Native.CGEventGetIntegerValueField(ev, Native.KeycodeField);
        var input = Classify(ev, keycode, Native.CGEventGetFlags(ev));
        var action = _session.OnKey(input, layout.Value, ctx.AutoCorrect && !ctx.Blocked, ctx.Sensitivity, DateTime.UtcNow);

        switch (action)
        {
            case FixWord fix:
                new KeyPoster()
                    .Tap(InputSources.BackspaceKey, fix.Backspaces)
                    .Text(fix.Text)
                    .Again(ev)
                    .Post();
                // The fix is typed as text, so switching right away doesn't change it; the next key is in the new language.
                InputSources.Switch(fix.Layout);
                Fixed?.Invoke(fix);
                return true;

            case UndoFix undo:
                // After ⌘Z, wait for ⌘ to be let go: Backspace with ⌘ held deletes the whole line.
                WhenKeysLetGo(() =>
                {
                    new KeyPoster()
                        .Tap(InputSources.BackspaceKey, undo.Backspaces)
                        .Text(undo.Text)
                        .Post();
                    InputSources.Switch(undo.Layout);
                });
                Undone?.Invoke(undo);
                return true;

            default:
                return false;
        }
    }

    private static void WhenKeysLetGo(Action action, int tries = 0)
    {
        const ulong held = Native.Command | Native.Control | Native.Option | Native.Shift;
        if ((Native.CGEventSourceFlagsState(Native.HidSystemState) & held) == 0 || tries >= 50)
        {
            action();
            return;
        }
        DispatchQueue.MainQueue.DispatchAfter(new DispatchTime(DispatchTime.Now, 20_000_000), () => WhenKeysLetGo(action, tries + 1));
    }

    private static KeyInput Classify(IntPtr ev, int keycode, ulong flags)
    {
        bool cmd = (flags & Native.Command) != 0, ctrl = (flags & Native.Control) != 0, opt = (flags & Native.Option) != 0;
        if (cmd && !ctrl && !opt && IsZ(ev, keycode)) return KeyInput.CtrlZ; // ⌘Z is undo on a Mac
        if (cmd || ctrl || opt) return KeyInput.Other;

        switch (keycode)
        {
            case InputSources.BackspaceKey: return KeyInput.Backspace;
            case InputSources.SpaceKey: return KeyInput.Space;
            case InputSources.ReturnKey or InputSources.EnterKey: return KeyInput.Enter;
        }

        if (!InputSources.PhysicalKey(keycode, out char key)) return KeyInput.Other;
        bool shifted = (flags & (Native.Shift | Native.CapsLock)) != 0;
        return KeyInput.Word(key, shifted);
    }

    /// <summary>The Z key, wherever the keyboard has it (French keyboards have it where US ones have W).</summary>
    private static bool IsZ(IntPtr ev, int keycode)
    {
        char* buf = stackalloc char[4];
        Native.CGEventKeyboardGetUnicodeString(ev, 4, out var length, buf);
        return length == 1 ? buf[0] is 'z' or 'Z' : keycode == InputSources.ZKey;
    }
}
