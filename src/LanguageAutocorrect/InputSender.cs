using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LanguageAutocorrect;

/// <summary>Builds and sends synthetic keystrokes (tagged so our own hook ignores them).</summary>
internal sealed class InputSender
{
    private readonly List<Native.INPUT> _inputs = new();

    public InputSender ReleaseModifiers()
    {
        foreach (var vk in new[] { Native.VK_LCONTROL, Native.VK_RCONTROL, Native.VK_CONTROL })
            if (Native.IsDown(vk)) Vk(vk, up: true);
        return this;
    }

    public InputSender Tap(int vk, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            Vk(vk, up: false);
            Vk(vk, up: true);
        }
        return this;
    }

    /// <summary>Presses <paramref name="modifier"/>, taps <paramref name="key"/>, releases both.</summary>
    public InputSender Chord(int modifier, int key)
    {
        Vk(modifier, up: false);
        Vk(key, up: false);
        Vk(key, up: true);
        Vk(modifier, up: true);
        return this;
    }

    public InputSender Text(string text)
    {
        foreach (char c in text)
        {
            if (c == ' ') { Tap(Native.VK_SPACE); continue; }
            Unicode(c, up: false);
            Unicode(c, up: true);
        }
        return this;
    }

    /// <summary>
    /// Presses (without releasing) the key at this physical position, so whichever keyboard is in use then
    /// decides what it types. The user's own key release follows.
    /// </summary>
    public InputSender KeyDown(int vk, int scanCode, bool extended)
    {
        _inputs.Add(new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                ki = new Native.KEYBDINPUT
                {
                    wVk = scanCode == 0 ? (ushort)vk : (ushort)0,
                    wScan = (ushort)scanCode,
                    dwFlags = (scanCode == 0 ? 0 : Native.KEYEVENTF_SCANCODE) | (extended ? Native.KEYEVENTF_EXTENDEDKEY : 0),
                    dwExtraInfo = Native.InjectedMarker,
                },
            },
        });
        return this;
    }

    /// <summary>Sends everything added so far and returns how many key events that was.</summary>
    public int Send()
    {
        if (_inputs.Count == 0) return 0;
        var arr = _inputs.ToArray();
        _inputs.Clear();
        Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf<Native.INPUT>());
        return arr.Length;
    }

    private void Vk(int vk, bool up) => _inputs.Add(new Native.INPUT
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT
            {
                wVk = (ushort)vk,
                dwFlags = up ? Native.KEYEVENTF_KEYUP : 0,
                dwExtraInfo = Native.InjectedMarker,
            },
        },
    });

    private void Unicode(char c, bool up) => _inputs.Add(new Native.INPUT
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            ki = new Native.KEYBDINPUT
            {
                wScan = c,
                dwFlags = Native.KEYEVENTF_UNICODE | (up ? Native.KEYEVENTF_KEYUP : 0),
                dwExtraInfo = Native.InjectedMarker,
            },
        },
    });
}
