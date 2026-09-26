using System;
using System.Collections.Generic;
using System.Linq;
using LayoutBuddy.Engine;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LayoutBuddy;

internal enum SetupResult { Cancelled, Installed, Portable, Uninstalled }

/// <summary>
/// The install / update / uninstall window: a colorful side that shows what each step is about, and the choices
/// next to it. Everything is chosen for the user already, so installing is a few clicks on the same button.
/// </summary>
internal sealed class SetupWindow : WebWindow
{
    /// <summary>
    /// A word per language for the typing demo, shown typed on the English keyboard and then fixed. Each is one the
    /// engine's tests check it fixes (MultiLanguageTests), or one of the website's checked examples.
    /// </summary>
    private static readonly Dictionary<Lang, string> DemoWords = new()
    {
        [Lang.Hebrew] = "שלום", [Lang.Russian] = "привет", [Lang.Arabic] = "مرحبا", [Lang.Ukrainian] = "привіт",
        [Lang.Persian] = "سلام", [Lang.Greek] = "καλημέρα", [Lang.French] = "aller", [Lang.German] = "schön",
        [Lang.Bulgarian] = "здравей", [Lang.Serbian] = "здраво", [Lang.Macedonian] = "здраво", [Lang.Kazakh] = "сәлем",
        [Lang.Georgian] = "გამარჯობა", [Lang.Armenian] = "բարև", [Lang.Korean] = "안녕", [Lang.Thai] = "สวัสดี",
        [Lang.Spanish] = "mañana", [Lang.Portuguese] = "não", [Lang.Turkish] = "güzel", [Lang.Italian] = "città",
        [Lang.Urdu] = "شکریہ",
    };

    private readonly string _mode;
    private bool _busy;

    public SetupResult Result { get; private set; } = SetupResult.Cancelled;

    /// <param name="mode">"install", "update" or "uninstall".</param>
    public SetupWindow(string mode) : base("setup.html", Installer.SetupUiDataDir, new Size(960, 640))
    {
        _mode = mode;
        Text = mode == "uninstall" ? "Uninstall " + AppInfo.Name : AppInfo.Name + " Setup";
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
    }

    protected override bool Frameless => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_MAXIMIZEBOX = 0x10000, WS_MINIMIZEBOX = 0x20000, WS_SYSMENU = 0x80000;
            var cp = base.CreateParams;
            cp.Style |= WS_MINIMIZEBOX | WS_SYSMENU; // minimizes from the taskbar button; Alt+Space menu
            cp.Style &= ~WS_MAXIMIZEBOX;             // double-clicking the top edge doesn't maximize
            return cp;
        }
    }

    protected override void OnMessage(string type, JsonElement msg)
    {
        switch (type)
        {
            case "ready":
                var installed = LayoutService.InstalledLanguages();
                Post(new
                {
                    type = "init",
                    mode = _mode,
                    version = Installer.CurrentVersion,
                    installedVersion = Installer.InstalledVersion,
                    installedState = InstalledState(),
                    switchKeys = SwitchKeys(),
                    languages = Languages.All.Select(l => new
                    {
                        code = l.Code,
                        name = l.Name,
                        nativeName = l.NativeName,
                        badge = l.Badge,
                        color = l.Color,
                        beta = l.Beta,
                        installed = installed.Contains(l.Lang),
                        selected = l.Lang == Lang.English || installed.Contains(l.Lang),
                        locked = l.Lang == Lang.English,
                        demo = Demo(l),
                    }),
                });
                break;

            case "install":
                bool startup = msg.TryGetProperty("startWithWindows", out var s) && s.GetBoolean();
                bool desktop = msg.TryGetProperty("desktopShortcut", out var d) && d.GetBoolean();
                bool voice = msg.TryGetProperty("voice", out var v) && v.GetBoolean();
                List<string>? langs = msg.TryGetProperty("languages", out var ls) && ls.ValueKind == JsonValueKind.Array
                    ? ls.EnumerateArray().Select(x => x.GetString()).OfType<string>().ToList()
                    : null;
                bool fresh = _mode == "install";
                RunWork(p => Installer.Install(fresh ? startup : null, desktop && fresh, langs, fresh ? voice : null, p),
                    SetupResult.Installed);
                break;

            case "uninstall":
                bool keep = msg.TryGetProperty("keepSettings", out var k) && k.GetBoolean();
                RunWork(p => Installer.Uninstall(keep, p), SetupResult.Uninstalled);
                break;

            case "runPortable":
                Result = SetupResult.Portable;
                Close();
                break;

            case "open":
                Installer.OpenInstalled();
                Close();
                break;

            case "drag":
                BeginDrag(new PointF(msg.GetProperty("x").GetSingle(), msg.GetProperty("y").GetSingle()));
                break;

            case "openLanguageSettings":
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:regionlanguage") { UseShellExecute = true }); }
                catch (Exception ex) { Log.Write("Open settings failed: " + ex.Message); }
                break;

            case "close":
                Close();
                break;
        }
    }

    /// <summary>How the installed copy compares to this one: "older", "same", "newer", or "unknown".</summary>
    private static string InstalledState()
    {
        if (!Version.TryParse(Installer.InstalledVersion, out var installed) ||
            !Version.TryParse(Installer.CurrentVersion, out var current)) return "unknown";
        int c = installed.CompareTo(current);
        return c < 0 ? "older" : c == 0 ? "same" : "newer";
    }

    /// <summary>The keys of the user's Windows shortcut for switching keyboards, e.g. ["Alt", "Shift"].</summary>
    private static string[]? SwitchKeys()
    {
        try
        {
            var method = LayoutService.DescribeSwitchMethod();
            return method.Contains(' ') ? null : method.Split('+');
        }
        catch (Exception ex)
        {
            Log.Write("Reading the switch shortcut failed: " + ex.Message);
            return null;
        }
    }

    /// <summary>The demo word for a language and the English-keyboard keys that type it, if it has one.</summary>
    private static object? Demo(LanguageInfo language)
    {
        if (!DemoWords.TryGetValue(language.Lang, out var word)) return null;
        var keys = KeyMap.ToUsKeys(word, language.Lang);
        return KeyMap.Render(keys, language.Lang) == word ? new { word, keys } : null;
    }

    private void RunWork(Action<Action<double, string>> work, SetupResult success)
    {
        _busy = true;
        Task.Run(() =>
        {
            string? error = null;
            try
            {
                work((value, step) => BeginInvoke(() => Post(new { type = "progress", value, step })));
            }
            catch (Exception ex)
            {
                Log.Write("Setup failed: " + ex);
                error = ex.Message;
            }
            BeginInvoke(() =>
            {
                _busy = false;
                if (error == null) Result = success;
                Post(new { type = "done", error });
            });
        });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy) e.Cancel = true; // don't close halfway through
        base.OnFormClosing(e);
    }
}
