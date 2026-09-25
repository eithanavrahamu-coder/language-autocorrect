using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

internal sealed class TrayApp : ApplicationContext, IAppController
{
    private sealed record RecentFix(DateTime Time, string Typed, string Fixed, string? App);

    private readonly string _settingsPath = AppSettings.DefaultPath;
    private readonly AppSettings _settings;
    private readonly NeverFixList _neverFix;
    private readonly TypingSession _session;
    private readonly WrongLayoutDetector _detector;
    private readonly KeyboardMonitor _monitor;
    private readonly FocusTracker _focus = new();
    private readonly VoiceAnnouncer _voice = new();
    private readonly IndicatorForm _indicator = new();
    private readonly NotifyIcon _tray;
    private readonly Timer _uiTimer = new() { Interval = 60 };
    private readonly ToolStripMenuItem _autoItem;
    private readonly List<RecentFix> _recent = new();
    private readonly System.Threading.EventWaitHandle _exitEvent, _showEvent;
    private readonly System.Threading.RegisteredWaitHandle _exitWait, _showWait;
    private volatile MonitorContext _context;
    private Lang? _lastLayout;
    private IntPtr _lastWindow;
    private IntPtr _trayIconHandle;
    private MainWindow? _main;
    private IReadOnlyList<Lang> _installedLanguages = [];
    private int _tickCount;

    public TrayApp(WrongLayoutDetector detector, bool showWindow)
    {
        _settings = AppSettings.Load(_settingsPath);
        _neverFix = new NeverFixList(_settings.NeverFixUndoCounts, _settings.UndosToBlock);
        _context = new MonitorContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, false);

        // Force the indicator's handle so we can marshal calls to the UI thread through it.
        _ = _indicator.Handle;

        _detector = detector;
        _session = new TypingSession(detector, _neverFix) { LearnFromUndos = _settings.LearnFromUndos };
        RefreshLanguages();
        _monitor = new KeyboardMonitor(_session, () => _context);
        _monitor.Fixed += fix =>
        {
            var app = _focus.Snapshot.Process;
            _indicator.BeginInvoke(() => OnFixed(fix, app));
        };
        _monitor.Undone += undo => _indicator.BeginInvoke(() => OnUndone(undo));

        _autoItem = new ToolStripMenuItem("Auto-correct", null, (_, _) => Change(() => _settings.AutoCorrectEnabled = !_settings.AutoCorrectEnabled));
        var open = new ToolStripMenuItem("Open " + AppInfo.Name, null, (_, _) => ShowMain()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            open,
            new ToolStripSeparator(),
            _autoItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);

        _tray = new NotifyIcon { Text = AppInfo.Name, ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
        UpdateTrayIcon(LayoutService.Current() ?? Lang.English);
        SyncMenu();

        _focus.FocusChanged += _monitor.RequestReset;
        _focus.Start();
        _monitor.Start();
        if (!_monitor.IsRunning)
            MessageBox.Show(AppInfo.Name + " could not install its keyboard hook. Auto-correct will not work.", AppInfo.Name);

        // Other processes (installer, a second launch) signal these.
        _exitEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.ExitEventName);
        _exitWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(_exitEvent,
            (_, _) => _indicator.BeginInvoke(ExitThread), null, -1, executeOnlyOnce: true);
        _showEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.ShowEventName);
        _showWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => _indicator.BeginInvoke(ShowMain), null, -1, executeOnlyOnce: false);

        _uiTimer.Tick += (_, _) => OnTick();
        _uiTimer.Start();
        StartupRegistration.Apply(_settings.StartWithWindows);

        if (showWindow) _indicator.BeginInvoke(ShowMain);
    }

    // ---------------- main window ----------------

    private void ShowMain()
    {
        if (!WebWindow.RuntimeAvailable)
        {
            if (MessageBox.Show(AppInfo.Name + "'s window needs the Microsoft Edge WebView2 Runtime, which isn't installed.\n\n" +
                                "Open the download page?", AppInfo.Name, MessageBoxButtons.YesNo) == DialogResult.Yes)
                OpenUrl("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
            return;
        }
        if (_main == null || _main.IsDisposed)
        {
            _main = new MainWindow(this);
            _main.FormClosed += (_, _) => _main = null;
            _main.Show();
        }
        else
        {
            if (_main.WindowState == FormWindowState.Minimized) _main.WindowState = FormWindowState.Normal;
            _main.Show();
        }
        _main.Activate();
    }

    private void PushState() => _main?.PushState();

    public object BuildState()
    {
        var now = DateTime.Now;
        var layout = _focus.Snapshot.Layout ?? _lastLayout;
        return new
        {
            version = Installer.CurrentVersion,
            autoCorrect = _settings.AutoCorrectEnabled,
            showIndicator = _settings.ShowIndicator,
            voice = _settings.VoiceEnabled,
            sensitivity = _settings.Sensitivity.ToString(),
            startWithWindows = _settings.StartWithWindows,
            learnFromUndos = _settings.LearnFromUndos,
            undosToBlock = _neverFix.UndosToBlock,
            neverFix = _neverFix.BlockedWords(),
            excludedApps = _settings.ExcludedApps,
            layout = layout is Lang l ? LanguageJson(Languages.Get(l)) : null,
            languages = Languages.All.Select(info => new
            {
                code = info.Code,
                name = info.Name,
                nativeName = info.NativeName,
                badge = info.Badge,
                color = info.Color,
                enabled = _settings.EnabledLanguages(_installedLanguages).Contains(info.Lang),
                installed = _installedLanguages.Contains(info.Lang),
                locked = info.Lang == Lang.English,
            }),
            fixesToday = _settings.FixesToday(now),
            fixesTotal = _settings.TotalFixes,
            nativeVoices = _session.Languages
                .Where(l => l != Lang.English && VoiceAnnouncer.VoiceFor(l) == null)
                .Select(l => Languages.Get(l).Name),
            recent = _recent.Select(r => new { time = r.Time.ToString("HH:mm"), typed = r.Typed, @fixed = r.Fixed, app = r.App }),
        };
    }

    public void HandleAction(string type, JsonElement msg)
    {
        switch (type)
        {
            case "set":
                ApplySetting(msg.GetProperty("key").GetString() ?? "", msg.GetProperty("value"));
                break;
            case "neverFix.add":
                var word = msg.GetProperty("word").GetString()?.Trim();
                if (!string.IsNullOrEmpty(word)) _neverFix.Block(word);
                break;
            case "neverFix.remove":
                _neverFix.Remove(msg.GetProperty("word").GetString() ?? "");
                break;
            case "neverFix.clear":
                _neverFix.Clear();
                break;
            case "recent.neverFix":
                int i = msg.GetProperty("index").GetInt32();
                if (i >= 0 && i < _recent.Count)
                    foreach (var w in _recent[i].Typed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        _neverFix.Block(w);
                break;
            case "excluded.add":
                var app = msg.GetProperty("app").GetString()?.Trim();
                if (!string.IsNullOrEmpty(app) && !_settings.IsExcluded(app)) _settings.ExcludedApps.Add(app);
                break;
            case "excluded.remove":
                var name = msg.GetProperty("app").GetString();
                _settings.ExcludedApps.RemoveAll(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
                break;
            case "testVoice":
                _ = _voice.SpeakAsync(_focus.Snapshot.Layout ?? _lastLayout ?? Lang.English);
                return;
            case "language.set":
                SetLanguage(msg.GetProperty("code").GetString(), msg.GetProperty("on").GetBoolean());
                break;
            case "openLanguageSettings":
                OpenUrl("ms-settings:regionlanguage");
                return;
            case "openAppsSettings":
                OpenUrl("ms-settings:appsfeatures");
                return;
        }
        SyncMenu();
        Save();
    }

    private void ApplySetting(string key, JsonElement value)
    {
        switch (key)
        {
            case "autoCorrect": _settings.AutoCorrectEnabled = value.GetBoolean(); break;
            case "showIndicator": _settings.ShowIndicator = value.GetBoolean(); break;
            case "voice": _settings.VoiceEnabled = value.GetBoolean(); break;
            case "learnFromUndos":
                _settings.LearnFromUndos = value.GetBoolean();
                _session.LearnFromUndos = _settings.LearnFromUndos;
                break;
            case "undosToBlock":
                _settings.UndosToBlock = Math.Clamp(value.GetInt32(), 1, 10);
                _neverFix.UndosToBlock = _settings.UndosToBlock;
                break;
            case "sensitivity":
                if (Enum.TryParse<Sensitivity>(value.GetString(), out var s)) _settings.Sensitivity = s;
                break;
            case "startWithWindows":
                _settings.StartWithWindows = value.GetBoolean();
                StartupRegistration.Apply(_settings.StartWithWindows);
                break;
        }
    }

    // ---------------- events ----------------

    private static object LanguageJson(LanguageInfo info) =>
        new { code = info.Code, name = info.Name, badge = info.Badge, color = info.Color };

    /// <summary>Re-reads installed keyboards and the user's chosen languages; loads word lists in the background.</summary>
    private void RefreshLanguages()
    {
        _installedLanguages = LayoutService.InstalledLanguages();
        LayoutService.LoadInstalledKeyboards();
        // Only languages whose keyboard is installed can be switched to.
        var enabled = _settings.EnabledLanguages(_installedLanguages).Where(_installedLanguages.Contains).ToList();
        _session.Languages = enabled;
        System.Threading.Tasks.Task.Run(() =>
        {
            try { _detector.Preload(enabled); }
            catch (Exception ex) { Log.Write("Loading word lists failed: " + ex.Message); }
        });
    }

    private void SetLanguage(string? code, bool on)
    {
        var info = Languages.FromCode(code);
        if (info == null || info.Lang == Lang.English) return;
        var list = _settings.EnabledLanguages(_installedLanguages).Select(l => Languages.Get(l).Code).ToList();
        list.Remove(info.Code);
        if (on) list.Add(info.Code);
        _settings.Languages = list;
        RefreshLanguages();
    }

    private void OnTick()
    {
        // Notice keyboards added or removed in Windows (every ~5 seconds).
        if (++_tickCount % 80 == 0 && !LayoutService.InstalledLanguages().SequenceEqual(_installedLanguages))
        {
            RefreshLanguages();
            PushState();
        }

        var snap = _focus.Snapshot;
        bool ownApp = string.Equals(snap.Process, AppInfo.Id, StringComparison.OrdinalIgnoreCase);
        bool blocked = ownApp || snap.IsPassword || _settings.IsExcluded(snap.Process);
        var ctx = new MonitorContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, blocked);
        if (ctx != _context) _context = ctx;

        var layout = snap.Layout;
        if (layout != null && layout != _lastLayout)
        {
            // Speak only when the layout changes inside the same window (not when switching apps).
            if (_lastLayout != null && snap.Window == _lastWindow && _settings.VoiceEnabled && !ownApp)
                _voice.Announce(layout.Value);
            _lastLayout = layout;
            UpdateTrayIcon(layout.Value);
            PushState();
        }
        _lastWindow = snap.Window;

        if (_settings.ShowIndicator && layout != null && snap.Caret is { } caret && !ownApp)
            _indicator.ShowAt(caret, layout.Value);
        else if (_indicator.Visible)
            _indicator.Hide();
    }

    private void OnFixed(FixWord fix, string? app)
    {
        _settings.CountFix(DateTime.Now, fix.Correction.WordCount);
        _recent.Insert(0, new RecentFix(DateTime.Now, fix.Correction.Typed, fix.Correction.Replacement, FriendlyAppName(app)));
        if (_recent.Count > 12) _recent.RemoveAt(_recent.Count - 1);
        Save();
        PushState();
    }

    private void OnUndone(UndoFix undo)
    {
        // An undone fix doesn't count and leaves the recent list.
        _settings.TotalFixes = Math.Max(0, _settings.TotalFixes - undo.Correction.WordCount);
        _settings.TodayFixes = Math.Max(0, _settings.TodayFixes - undo.Correction.WordCount);
        if (_recent.Count > 0 && _recent[0].Fixed == undo.Correction.Replacement) _recent.RemoveAt(0);
        Save();
        PushState();
        if (undo.NowBlocked)
        {
            _tray.ShowBalloonTip(4000, AppInfo.Name,
                $"\"{undo.Correction.TriggerTyped}\" won't be auto-corrected anymore. You can change this in the Never fix list.",
                ToolTipIcon.Info);
        }
    }

    private static string? FriendlyAppName(string? process)
    {
        if (string.IsNullOrEmpty(process)) return null;
        return process.ToLowerInvariant() switch
        {
            "chrome" => "Chrome",
            "msedge" => "Edge",
            "firefox" => "Firefox",
            "winword" => "Word",
            "excel" => "Excel",
            "powerpnt" => "PowerPoint",
            "outlook" or "olk" => "Outlook",
            "ms-teams" or "teams" => "Teams",
            "notepad" => "Notepad",
            "whatsapp" or "whatsapp.root" => "WhatsApp",
            "code" => "VS Code",
            _ => char.ToUpperInvariant(process[0]) + process[1..],
        };
    }

    // ---------------- helpers ----------------

    private void Change(Action change)
    {
        change();
        SyncMenu();
        Save();
        PushState();
    }

    private void SyncMenu() => _autoItem.Checked = _settings.AutoCorrectEnabled;

    private void Save()
    {
        try
        {
            _settings.NeverFixUndoCounts = _neverFix.Snapshot();
            _settings.Save(_settingsPath);
        }
        catch (Exception ex)
        {
            Log.Write("Saving settings failed: " + ex.Message);
        }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Write("Open failed: " + ex.Message); }
    }

    private void UpdateTrayIcon(Lang lang)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(IndicatorForm.ColorOf(lang));
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(0, 1, 10, 10, 180, 90);
            path.AddArc(22, 1, 10, 10, 270, 90);
            path.AddArc(22, 21, 10, 10, 0, 90);
            path.AddArc(0, 21, 10, 10, 90, 90);
            path.CloseFigure();
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillPath(bg, path);
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(Languages.Get(lang).Badge, font, Brushes.White, new RectangleF(0, 0, 32, 32), fmt);
        }
        var handle = bmp.GetHicon();
        var old = _trayIconHandle;
        _tray.Icon = Icon.FromHandle(handle);
        _trayIconHandle = handle;
        if (old != IntPtr.Zero) Native.DestroyIcon(old);
        _tray.Text = AppInfo.Name + " – " + Languages.Get(lang).Name;
    }

    protected override void ExitThreadCore()
    {
        _uiTimer.Stop();
        _exitWait.Unregister(null);
        _showWait.Unregister(null);
        _exitEvent.Dispose();
        _showEvent.Dispose();
        _main?.Close();
        _monitor.Dispose();
        _focus.Dispose();
        _voice.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _indicator.Close();
        Save();
        base.ExitThreadCore();
    }
}
