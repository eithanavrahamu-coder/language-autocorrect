using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect;

/// <summary>
/// Finds and downloads newer versions from the download page, which lists its newest version in version.json.
/// Installing is done by running the download with <c>--update</c>: it replaces the installed copy and starts it again,
/// keeping all settings.
/// </summary>
internal static class Updater
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Id}/{Installer.CurrentVersion}");
        return http;
    }

    /// <summary>Where downloads wait until they're installed.</summary>
    private static string DownloadDir => Path.Combine(Path.GetTempPath(), AppInfo.Id + "Update");

    /// <summary>The newest version if it's newer than this one, otherwise null. Throws when the page can't be read.</summary>
    public static async Task<UpdateManifest?> CheckAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        // A fresh copy, not one a proxy or the page's cache kept from before a release.
        var url = new Uri(new Uri(AppInfo.Website), "version.json?t=" + DateTime.UtcNow.Ticks);
        using var response = await Http.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();
        var manifest = UpdateManifest.Parse(await response.Content.ReadAsStringAsync(cts.Token))
            ?? throw new InvalidDataException("The download page's version file couldn't be read.");
        return manifest.IsNewerThan(Installer.CurrentVersion) ? manifest : null;
    }

    /// <summary>
    /// Downloads the new version and checks it's the app, at the expected version (InvalidDataException if not).
    /// Returns the file.
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateManifest update, IProgress<double>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(DownloadDir);
        var file = Path.Combine(DownloadDir, $"{AppInfo.Id}-{update.Version}.exe");
        var part = file + ".part";

        using (var response = await Http.GetAsync(new Uri(new Uri(AppInfo.Website), update.File), HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? update.Size;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(part);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report((double)done / total);
            }
        }

        if (update.Size > 0 && new FileInfo(part).Length != update.Size)
            throw new InvalidDataException("The download was incomplete. Try again.");
        var info = FileVersionInfo.GetVersionInfo(part);
        if (info.ProductName != AppInfo.Name || UpdateManifest.ParseVersion(info.ProductVersion) is not { } got || got < update.Version)
            throw new InvalidDataException("The downloaded file isn't the expected version of " + AppInfo.Name + ".");
        File.Move(part, file, overwrite: true);
        return file;
    }

    /// <summary>Starts the downloaded version to install itself over this one (it closes this copy first).</summary>
    public static void Install(string file) =>
        Process.Start(new ProcessStartInfo(file, "--update") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file)! });

    /// <summary>Removes downloads that were already installed (in the background; a file still running is left).</summary>
    public static void CleanUp()
    {
        Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(DownloadDir)) return;
                foreach (var f in Directory.GetFiles(DownloadDir))
                    try { File.Delete(f); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            catch (Exception ex)
            {
                Log.Write("Cleaning up updates failed: " + ex.Message);
            }
        });
    }
}
