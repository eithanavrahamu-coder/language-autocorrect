using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LayoutBuddy;

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

    public void Send()
    {
        if (_inputs.Count == 0) return;
        var arr = _inputs.ToArray();
        _inputs.Clear();
        Native.SendInput((uint)arr.Length, arr, Marshal.SizeOf<Native.INPUT>());
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
