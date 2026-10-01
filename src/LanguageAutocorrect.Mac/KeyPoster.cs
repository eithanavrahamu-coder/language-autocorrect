using System;
using System.Collections.Generic;

namespace LanguageAutocorrect.Mac;

/// <summary>Builds and posts key presses, tagged so the app's own key watcher lets them through.</summary>
internal sealed unsafe class KeyPoster
{
    /// <summary>Marks the app's own key presses (in their "user data" field).</summary>
    public const long Marker = 0x4C41_4D43;

    // Its own key state, so keys the user is holding (Shift, ⌘) don't change what's typed.
    private static readonly IntPtr Source = Native.CGEventSourceCreate(Native.PrivateState);

    private readonly List<IntPtr> _events = new();

    public KeyPoster Tap(ushort keycode, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            Add(Native.CGEventCreateKeyboardEvent(Source, keycode, true));
            Add(Native.CGEventCreateKeyboardEvent(Source, keycode, false));
        }
        return this;
    }

    /// <summary>Types <paramref name="text"/> as it is, whatever keyboard is on.</summary>
    public KeyPoster Text(string text)
    {
        fixed (char* p = text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                {
                    Tap(InputSources.SpaceKey);
                    continue;
                }
                int n = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
                foreach (bool down in new[] { true, false })
                {
                    var ev = Native.CGEventCreateKeyboardEvent(Source, 0, down);
                    if (ev == IntPtr.Zero) continue;
                    Native.CGEventKeyboardSetUnicodeString(ev, (nuint)n, p + i);
                    Add(ev);
                }
                i += n - 1;
            }
        }
        return this;
    }

    /// <summary>The user's own key press again, as it was (with Shift, for Shift+Return).</summary>
    public KeyPoster Again(IntPtr ev)
    {
        var copy = Native.CGEventCreateCopy(ev);
        if (copy == IntPtr.Zero) return this;
        Native.CGEventSetIntegerValueField(copy, Native.SourceUserDataField, Marker);
        _events.Add(copy);
        return this;
    }

    private void Add(IntPtr ev)
    {
        if (ev == IntPtr.Zero) return;
        Native.CGEventSetFlags(ev, 0);
        Native.CGEventSetIntegerValueField(ev, Native.SourceUserDataField, Marker);
        _events.Add(ev);
    }

    /// <summary>Posts everything added so far, in order.</summary>
    public void Post()
    {
        foreach (var ev in _events)
        {
            Native.CGEventPost(Native.HidEventTap, ev);
            Native.CFRelease(ev);
        }
        _events.Clear();
    }
}
