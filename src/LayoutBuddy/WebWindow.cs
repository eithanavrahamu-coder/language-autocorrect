using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace LayoutBuddy;

/// <summary>A window that shows one of the embedded HTML pages and talks to it with JSON messages.</summary>
internal abstract class WebWindow : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly string _page;
    private readonly string _userDataFolder;
    private bool _ready;

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected WebWindow(string page, string userDataFolder, Size logicalClientSize)
    {
        _page = page;
        _userDataFolder = userDataFolder;
        Text = AppInfo.Name;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = IsDarkMode ? Color.FromArgb(0x16, 0x16, 0x15) : Color.FromArgb(0xFB, 0xFA, 0xF8);
        _web.DefaultBackgroundColor = BackColor;
        Controls.Add(_web);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { /* default icon */ }
        LogicalClientSize = logicalClientSize;
    }

    protected Size LogicalClientSize { get; }

    public static bool IsDarkMode
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }
    }

    /// <summary>True if the WebView2 runtime is installed (it ships with Windows 11 and current Windows 10).</summary>
    public static bool RuntimeAvailable
    {
        get
        {
            try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
            catch { return false; }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        float scale = DeviceDpi / 96f;
        ClientSize = new Size((int)(LogicalClientSize.Width * scale), (int)(LogicalClientSize.Height * scale));
        StyleTitleBar();
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            Directory.CreateDirectory(_userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
            await _web.EnsureCoreWebView2Async(env);
            var core = _web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.WebMessageReceived += OnWebMessage;
            core.NewWindowRequested += (_, a) => a.Handled = true;
            core.NavigateToString(LoadPage(_page));
        }
        catch (Exception ex)
        {
            Log.Write("WebView2 failed: " + ex);
            MessageBox.Show(this, AppInfo.Name + " couldn't open its window:\n" + ex.Message, AppInfo.Name);
            Close();
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var msg = doc.RootElement;
            var type = msg.GetProperty("type").GetString() ?? "";
            if (type == "ready") _ready = true;
            OnMessage(type, msg);
        }
        catch (Exception ex)
        {
            Log.Write("UI message error: " + ex);
        }
    }

    /// <summary>Called on the UI thread for every message from the page ("ready" first).</summary>
    protected abstract void OnMessage(string type, JsonElement msg);

    /// <summary>Sends a message to the page (ignored until the page is ready).</summary>
    public void Post(object message)
    {
        if (!_ready || IsDisposed || _web.CoreWebView2 == null) return;
        _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, Json));
    }

    protected Task RunOnUi(Action a)
    {
        var tcs = new TaskCompletionSource();
        BeginInvoke(() =>
        {
            try { a(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private static string LoadPage(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LayoutBuddy.UI.{name}")
            ?? throw new InvalidOperationException("Missing page " + name);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    // Match the Windows title bar to the page (Windows 11; ignored elsewhere).
    private void StyleTitleBar()
    {
        try
        {
            int dark = IsDarkMode ? 1 : 0;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            int color = BackColor.R | (BackColor.G << 8) | (BackColor.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref color, sizeof(int));
        }
        catch { /* older Windows */ }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
