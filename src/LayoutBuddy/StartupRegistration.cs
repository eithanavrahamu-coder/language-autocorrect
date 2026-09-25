using System;
using Microsoft.Win32;

namespace LayoutBuddy;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.Id;

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\" --background");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName);
        }
        catch (Exception ex)
        {
            Log.Write("Startup registration failed: " + ex.Message);
        }
    }
}
