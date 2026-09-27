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

namespace LanguageAutocorrect;

/// <summary>A window that shows one of the embedded HTML pages (or a page on the website) and talks to it with JSON messages.</summary>
internal abstract class WebWindow : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly string? _page;
    private readonly string _userDataFolder;
    private bool _ready;

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <param name="page">The embedded page to show; null for a window that opens its own (see <see cref="Open"/>).</param>
    protected WebWindow(string? page, string userDataFolder, Size logicalClientSize)
    {
        _page = page;
        _userDataFolder = userDataFolder;
        Text = AppInfo.Name;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = IsDarkMode ? Color.FromArgb(0x19, 0x19, 0x1A) : Color.FromArgb(0xFA, 0xF9, 0xF7);
        _web.DefaultBackgroundColor = BackColor;
        Controls.Add(_web);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { /* default icon */ }
        LogicalClientSize = logicalClientSize;
    }

    protected Size LogicalClientSize { get; }

    /// <summary>
    /// No Windows title bar: the page draws its own buttons, and sends "drag" to move the window (see
    /// <see cref="BeginDrag"/>). The subclass also sets <see cref="Form.FormBorderStyle"/> to None.
    /// </summary>
    protected virtual bool Frameless => false;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!Frameless) return cp;
            const int WS_MAXIMIZEBOX = 0x10000, WS_MINIMIZEBOX = 0x20000, WS_SYSMENU = 0x80000;
            cp.Style |= WS_MINIMIZEBOX | WS_SYSMENU; // minimizes from the taskbar button; Alt+Space menu
            cp.Style &= ~WS_MAXIMIZEBOX;             // double-clicking the top edge doesn't maximize
            return cp;
        }
    }

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
        var size = new Size((int)(LogicalClientSize.Width * scale), (int)(LogicalClientSize.Height * scale));
        // Without a frame the window is all client area (setting ClientSize would add room for a title bar).
        if (Frameless) Size = size;
        else ClientSize = size;
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
            Open(core);
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
            if (type == "drag" && Frameless)
            {
                BeginDrag(new PointF(msg.GetProperty("x").GetSingle(), msg.GetProperty("y").GetSingle()));
                return;
            }
            OnMessage(type, msg);
        }
        catch (Exception ex)
        {
            Log.Write("UI message error: " + ex);
        }
    }

    /// <summary>Shows the page, once WebView2 is ready.</summary>
    protected virtual void Open(CoreWebView2 core) => core.NavigateToString(LoadPage(_page!));

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
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LanguageAutocorrect.UI.{name}")
            ?? throw new InvalidOperationException("Missing page " + name);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    private Timer? _drag;

    /// <summary>
    /// Moves the window with the mouse until the button is let go (for a frameless window's top edge). The page holds
    /// the mouse while its button is down, so Windows' own window dragging can't take over; this follows the cursor.
    /// </summary>
    /// <param name="grab">Where the page was pressed, in page pixels from the window's top left corner.</param>
    protected void BeginDrag(PointF grab)
    {
        if (_drag != null || !LeftButtonDown()) return;
        float scale = DeviceDpi / 96f;
        var offset = new Size((int)Math.Round(grab.X * scale), (int)Math.Round(grab.Y * scale));
        _drag = new Timer { Interval = 8 };
        _drag.Tick += (_, _) =>
        {
            var to = Cursor.Position - offset;
            if (to != Location) Location = to;
            if (LeftButtonDown()) return;
            _drag.Dispose();
            _drag = null;
        };
        _drag.Start();
    }

    // The button as it is now (Control.MouseButtons only knows about clicks on this thread's windows, not the page's).
    private static bool LeftButtonDown() => Native.IsDown(SystemInformation.MouseButtonsSwapped ? 0x02 : 0x01);

    // Match the Windows title bar to the page (Windows 11; ignored elsewhere). A frameless window gets rounded
    // corners and a shadow instead.
    private void StyleTitleBar()
    {
        try
        {
            int dark = IsDarkMode ? 1 : 0;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            if (Frameless)
            {
                int round = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
                int enabled = 2; // DWMWA_NCRENDERING_POLICY = DWMNCRP_ENABLED, so DWM draws the shadow
                DwmSetWindowAttribute(Handle, 2, ref enabled, sizeof(int));
                var margins = new Margins { Bottom = 1 };
                DwmExtendFrameIntoClientArea(Handle, ref margins);
                return;
            }
            int color = BackColor.R | (BackColor.G << 8) | (BackColor.B << 16);
            DwmSetWindowAttribute(Handle, 35, ref color, sizeof(int));
        }
        catch { /* older Windows */ }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
