using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using AppKit;
using CoreGraphics;
using Foundation;
using WebKit;

namespace LanguageAutocorrect.Mac;

/// <summary>
/// A window that shows one of the app's pages (or a page of the website) and talks to it with JSON messages. The pages
/// were written for the Windows app's WebView2, so a small script gives them the same <c>chrome.webview</c> to talk
/// through. Nothing is kept: no cookies, no cache.
/// </summary>
internal sealed class PageWindow : NSObject, IWKScriptMessageHandler
{
    private const string Bridge = """
        (() => {
          const listeners = [];
          window.chrome = { webview: {
            postMessage: m => window.webkit.messageHandlers.host.postMessage(JSON.stringify(m)),
            addEventListener: (type, f) => { if (type === 'message') listeners.push(f); },
          } };
          window.__fromHost = data => listeners.forEach(f => f({ data }));
        })();
        """;

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly NSWindow _window;
    private readonly WKWebView _web;
    private readonly WKUserContentController _content;
    private readonly Dialogs _dialogs = new();
    private bool _ready;

    /// <summary>A message from the page (on the main thread): its type and the whole message.</summary>
    public event Action<string, JsonElement>? Message;
    public event Action? Closed;

    public PageWindow(string title, CGSize size, CGSize minSize, bool resizable)
    {
        _content = new WKUserContentController();
        _content.AddScriptMessageHandler(this, "host");
        _content.AddUserScript(new WKUserScript(new NSString(Bridge), WKUserScriptInjectionTime.AtDocumentStart, true));
        var config = new WKWebViewConfiguration
        {
            UserContentController = _content,
            WebsiteDataStore = WKWebsiteDataStore.NonPersistentDataStore,
        };
        _web = new WKWebView(new CGRect(CGPoint.Empty, size), config) { UIDelegate = _dialogs };

        var style = NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable;
        if (resizable) style |= NSWindowStyle.Resizable;
        _window = new NSWindow(new CGRect(CGPoint.Empty, size), style, NSBackingStore.Buffered, false)
        {
            Title = title,
            ContentView = _web,
            ContentMinSize = minSize,
        };
        _window.ReleaseWhenClosed(false);
        _window.Center();
        _window.WillClose += (_, _) =>
        {
            _ready = false;
            _content.RemoveScriptMessageHandler("host");
            Closed?.Invoke();
        };
    }

    public WKWebView Web => _web;

    /// <summary>The page has loaded and listens (it said "ready").</summary>
    public bool IsReady => _ready;

    /// <summary>Shows one of the app's own pages (UI/app.html).</summary>
    public void LoadPage(string name) => _web.LoadHtmlString(ReadPage(name), new NSUrl("about:blank"));

    public void LoadUrl(Uri url) => _web.LoadRequest(new NSUrlRequest(new NSUrl(url.AbsoluteUri)));

    public void Show()
    {
        _window.MakeKeyAndOrderFront(null);
        _window.OrderFrontRegardless();
    }

    public void Close() => _window.Close();

    [Export("userContentController:didReceiveScriptMessage:")]
    public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
    {
        try
        {
            if (message.Body is not NSString body) return;
            using var doc = JsonDocument.Parse(body.ToString());
            var msg = doc.RootElement;
            var type = msg.GetProperty("type").GetString() ?? "";
            if (type == "ready") _ready = true;
            Message?.Invoke(type, msg);
        }
        catch (Exception ex)
        {
            Log.Write("Page message error: " + ex);
        }
    }

    /// <summary>Sends a message to the page (dropped until the page is ready).</summary>
    public void Post(object message)
    {
        if (!_ready) return;
        var json = JsonSerializer.Serialize(message, Json);
        _web.EvaluateJavaScript($"window.__fromHost({json});", (_, error) =>
        {
            if (error != null) Log.Write("Page script error: " + error.LocalizedDescription);
        });
    }

    /// <summary>The page's confirm() questions ("Remove all words from the list?"), as Mac alerts.</summary>
    private sealed class Dialogs : WKUIDelegate
    {
        public override void RunJavaScriptConfirmPanel(WKWebView webView, string message, WKFrameInfo frame, Action<bool> completionHandler)
        {
            using var alert = new NSAlert { MessageText = message };
            alert.AddButton("OK");
            alert.AddButton("Cancel");
            completionHandler(alert.RunModal() == (nint)(long)NSAlertButtonReturn.First);
        }
    }

    private static string ReadPage(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LanguageAutocorrect.UI.{name}")
            ?? throw new InvalidOperationException("Missing page " + name);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
