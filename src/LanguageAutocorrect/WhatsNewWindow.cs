using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using LanguageAutocorrect.Engine;
using Microsoft.Web.WebView2.Core;

namespace LanguageAutocorrect;

/// <summary>
/// "What's new" after an update: the website's release notes for each version since the one that was replaced, as
/// pages to click through (website/src/release-notes). Nothing is kept on this PC.
/// </summary>
internal sealed class WhatsNewWindow : WebWindow
{
    private readonly Uri _url;

    /// <summary>The pages loaded, so they were seen and aren't offered again.</summary>
    public bool Loaded { get; private set; }

    public WhatsNewWindow(string from) : base(null, MainWindow.DataFolder, new Size(760, 480))
    {
        _url = WhatsNew.PageUrl(AppInfo.Website, from, Installer.CurrentVersion);
        Text = "What’s new in " + AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
    }

    protected override void Open(CoreWebView2 core)
    {
        // Only these pages open here; any other link (like "all release notes") opens in the browser.
        core.NavigationStarting += (_, e) =>
        {
            if (IsOurPage(e.Uri)) return;
            e.Cancel = true;
            OpenInBrowser(e.Uri);
        };
        core.NewWindowRequested += (_, e) => OpenInBrowser(e.Uri);
        core.NavigationCompleted += (_, e) =>
        {
            if (e.IsSuccess) { Loaded = true; return; }
            // No internet, most likely: try again the next time the app's window opens.
            Log.Write($"What's new didn't load: {e.WebErrorStatus} {e.HttpStatusCode}");
            BeginInvoke(Close);
        };
        core.Navigate(_url.AbsoluteUri);
    }

    private bool IsOurPage(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == _url.Host &&
        u.AbsolutePath.StartsWith(_url.AbsolutePath, StringComparison.Ordinal) && u.Query.Contains("in=app");

    private static void OpenInBrowser(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return;
        try { Process.Start(new ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Open link failed: " + ex.Message); }
    }

    protected override void OnMessage(string type, JsonElement msg)
    {
        if (type == "close") Close();
    }
}
