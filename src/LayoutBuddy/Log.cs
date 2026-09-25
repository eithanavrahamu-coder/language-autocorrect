using System;
using System.IO;

namespace LayoutBuddy;

internal static class Log
{
    private static readonly object Lock = new();

    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Id, "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var fi = new FileInfo(Path);
                if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
