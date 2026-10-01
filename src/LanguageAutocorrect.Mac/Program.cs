using System;
using AppKit;
using Foundation;
using WebKit;

namespace LanguageAutocorrect.Mac;

internal static class Program
{
    // NSApplication holds its delegate weakly: keep it alive here.
    private static MacApp? _app;

    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Fatal: " + e.ExceptionObject);
        MacApp.SelfCheck = Array.IndexOf(args, "--selfcheck") >= 0;
        // Also makes code after an await (the update check) carry on on the main thread.
        NSApplication.Init();
        _app = new MacApp();
        NSApplication.SharedApplication.Delegate = _app;
        NSApplication.SharedApplication.Run();
    }
}

/// <summary>
/// "What's new" after an update shows only the website's release notes for this update; any other link opens in the
/// browser.
/// </summary>
internal sealed class WhatsNewNavigation(Uri page, Action<bool> loaded) : WKNavigationDelegate
{
    public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
    {
        var url = navigationAction.Request.Url?.AbsoluteString;
        if (url == null || IsOurPage(url))
        {
            decisionHandler(WKNavigationActionPolicy.Allow);
            return;
        }
        decisionHandler(WKNavigationActionPolicy.Cancel);
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps) Links.Open(u.AbsoluteUri);
    }

    public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation) => loaded(true);

    public override void DidFailProvisionalNavigation(WKWebView webView, WKNavigation navigation, NSError error)
    {
        Log.Write("What's new didn't load: " + error.LocalizedDescription);
        loaded(false);
    }

    private bool IsOurPage(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == page.Host &&
        u.AbsolutePath.StartsWith(page.AbsolutePath, StringComparison.Ordinal) && u.Query.Contains("in=app");
}
