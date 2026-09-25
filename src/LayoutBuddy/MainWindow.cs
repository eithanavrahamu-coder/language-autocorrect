using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace LayoutBuddy;

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
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayoutBuddy", "WebView2");

    public MainWindow(IAppController app) : base("app.html", DataFolder, new Size(980, 680))
    {
        _app = app;
        Text = "LayoutBuddy";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        float scale = DeviceDpi / 96f;
        MinimumSize = new Size((int)(800 * scale), (int)(560 * scale));
    }

    protected override void OnMessage(string type, JsonElement msg)
    {
        if (type != "ready") _app.HandleAction(type, msg);
        PushState();
    }

    public void PushState() => Post(new { type = "state", state = _app.BuildState() });

    public void ShowPage(string page) => Post(new { type = "show", page });
}
