using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AppKit;
using Foundation;
using LanguageAutocorrect.Engine;
using ObjCRuntime;
using ServiceManagement;

namespace LanguageAutocorrect.Mac;

/// <summary>The version of this copy, from the app's build.</summary>
internal static class AppVersion
{
    public static readonly string Current =
        UpdateManifest.ParseVersion(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
            ?.ToString() ?? "0.0.0";
}

/// <summary>
/// The Accessibility permission (System Settings → Privacy &amp; Security → Accessibility), which macOS asks for
/// before an app may see and type keys in other apps.
/// </summary>
internal static class Permission
{
    public static bool Granted => Native.AXIsProcessTrusted();

    /// <summary>Puts the app in the Accessibility list and shows macOS's own message, with a button to open it.</summary>
    public static void Ask()
    {
        try
        {
            var lib = Dlfcn.dlopen(Native.HIServices, 0);
            var key = Dlfcn.GetStringConstant(lib, "kAXTrustedCheckOptionPrompt");
            if (key == null) return;
            using var options = NSDictionary.FromObjectAndKey(NSNumber.FromBoolean(true), key);
            Native.AXIsProcessTrustedWithOptions(options.Handle);
        }
        catch (Exception ex)
        {
            Log.Write("Asking for permission failed: " + ex.Message);
        }
    }

    public static void OpenSettings() =>
        Links.Open("x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility");

    /// <summary>
    /// Input Monitoring (Privacy &amp; Security → Input Monitoring): some Macs want it too before keys can be watched.
    /// </summary>
    public static void AskInputMonitoring()
    {
        try
        {
            if (!Native.CGPreflightListenEventAccess()) Native.CGRequestListenEventAccess();
        }
        catch (Exception ex)
        {
            Log.Write("Asking for Input Monitoring failed: " + ex.Message);
        }
    }
}

/// <summary>Opening the app when the Mac starts (a login item).</summary>
internal static class LoginItem
{
    public static bool IsOn
    {
        get
        {
            try { return SMAppService.MainApp.Status == SMAppServiceStatus.Enabled; }
            catch (Exception) { return false; }
        }
    }

    public static void Apply(bool on)
    {
        try
        {
            var service = SMAppService.MainApp;
            if (on == (service.Status == SMAppServiceStatus.Enabled)) return;
            NSError? error;
            bool ok = on ? service.Register(out error) : service.Unregister(out error);
            if (!ok) Log.Write($"Login item {(on ? "on" : "off")} failed: {error?.LocalizedDescription}");
        }
        catch (Exception ex)
        {
            Log.Write("Login item failed: " + ex.Message);
        }
    }
}

/// <summary>
/// Looks for a newer version: the download page lists its newest version in version.json. A newer one is downloaded
/// from the website (the Mac app doesn't replace itself).
/// </summary>
internal static class MacUpdater
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Id}/{AppVersion.Current}");
        return http;
    }

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
        return manifest.IsNewerThan(AppVersion.Current) ? manifest : null;
    }
}

internal static class Links
{
    public static void Open(string url)
    {
        try
        {
            using var u = NSUrl.FromString(url);
            if (u != null) NSWorkspace.SharedWorkspace.OpenUrl(u);
        }
        catch (Exception ex)
        {
            Log.Write("Open failed: " + ex.Message);
        }
    }
}
