using System;
using System.IO;

namespace LanguageAutocorrect.Mac;

/// <summary>Where the Mac app keeps its files: ~/Library/Application Support/LanguageAutocorrect.</summary>
internal static class MacPaths
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", AppInfo.Id);

    public static string Settings => Path.Combine(Folder, "settings.json");
}

/// <summary>A small log for finding problems (never what's typed). Starts over at half a megabyte.</summary>
internal static class Log
{
    private static readonly object Lock = new();

    public static string Path => System.IO.Path.Combine(MacPaths.Folder, "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(MacPaths.Folder);
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
