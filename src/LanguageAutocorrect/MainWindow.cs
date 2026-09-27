using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace LanguageAutocorrect;

/// <summary>What the main window needs from the running app.</summary>
internal interface IAppController
{
    object BuildState();
    void HandleAction(string type, JsonElement msg);
}

/// <summary>The app's main window (Home / Never fix / Settings).</summary>
internal sealed class MainWindow : WebWindow
{
    private readonly IAppController _app;

    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Id, "WebView2");

    public MainWindow(IAppController app) : base("app.html", DataFolder, new Size(980, 680))
    {
        _app = app;
        Text = AppInfo.Name;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        float scale = DeviceDpi / 96f;
        MinimumSize = new Size((int)(800 * scale), (int)(560 * scale));
    }

    // Asked for before the page was ready to hear it.
    private string? _pendingPage, _pendingToast;
    private bool _pageReady;

    protected override void OnMessage(string type, JsonElement msg)
    {
        if (type != "ready") _app.HandleAction(type, msg);
        PushState();
        if (type != "ready") return;
        _pageReady = true;
        if (_pendingPage != null) ShowPage(_pendingPage);
        if (_pendingToast != null) Toast(_pendingToast);
        _pendingPage = _pendingToast = null;
    }

    public void PushState() => Post(new { type = "state", state = _app.BuildState() });

    public void ShowPage(string page)
    {
        if (_pageReady) Post(new { type = "show", page });
        else _pendingPage = page;
    }

    /// <summary>A short message at the bottom of the window.</summary>
    public void Toast(string text)
    {
        if (_pageReady) Post(new { type = "toast", text });
        else _pendingToast = text;
    }
}
