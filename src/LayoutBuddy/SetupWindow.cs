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

/// <summary>The install / update / uninstall window.</summary>
internal sealed class SetupWindow : WebWindow
{
    private readonly string _mode;
    private bool _busy;

    public SetupResult Result { get; private set; } = SetupResult.Cancelled;

    /// <param name="mode">"install", "update" or "uninstall".</param>
    public SetupWindow(string mode) : base("setup.html", Installer.SetupUiDataDir, new Size(500, 680))
    {
        _mode = mode;
        Text = mode == "uninstall" ? "Uninstall " + AppInfo.Name : AppInfo.Name + " Setup";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
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
                    languages = Languages.All.Select(l => new
                    {
                        code = l.Code,
                        name = l.Name,
                        nativeName = l.NativeName,
                        badge = l.Badge,
                        color = l.Color,
                        installed = installed.Contains(l.Lang),
                        selected = l.Lang == Lang.English || installed.Contains(l.Lang),
                        locked = l.Lang == Lang.English,
                    }),
                });
                break;

            case "install":
                bool startup = msg.TryGetProperty("startWithWindows", out var s) && s.GetBoolean();
                bool desktop = msg.TryGetProperty("desktopShortcut", out var d) && d.GetBoolean();
                List<string>? langs = msg.TryGetProperty("languages", out var ls) && ls.ValueKind == JsonValueKind.Array
                    ? ls.EnumerateArray().Select(x => x.GetString()).OfType<string>().ToList()
                    : null;
                RunWork(p => Installer.Install(_mode == "install" ? startup : null, desktop && _mode == "install", langs, p),
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
                Installer.SignalShow();
                Close();
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
