using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect;

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
    private readonly FixCardForm _card = new();
    private readonly FixFlash _flash;
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
    // A language the badge shows right away (after a fix or a click), before Windows reports the switch.
    private (Lang Lang, DateTime Until)? _expected;

    // Updates: the first look a couple of minutes after start, then every hour whether a daily check is due.
    private readonly Timer _updateTimer = new() { Interval = 2 * 60 * 1000 };
    private readonly ToolStripMenuItem _updateItem;
    private UpdateManifest? _update;            // a newer version, once found
    private string _updateStatus = "idle";      // idle, checking, latest, available, downloading, installing, error
    private double _updateProgress;
    private string? _updateError;
    private bool _balloonIsUpdate;              // clicking the balloon on screen opens the window to update

    public TrayApp(WrongLayoutDetector detector, bool showWindow, bool justUpdated = false)
    {
        _settings = AppSettings.Load(_settingsPath);
        _neverFix = new NeverFixList(_settings.NeverFixUndoCounts, _settings.UndosToBlock);
        _context = new MonitorContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, false);

        // Force the indicator's handle so we can marshal calls to the UI thread through it.
        _ = _indicator.Handle;
        _ = _card.Handle;
        _flash = new FixFlash();
        _indicator.Clicked += OnBadgeClicked;

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
        _monitor.BeforeFix = fix =>
        {
            if (_settings.ShowFixes) _flash.BeforeFix(fix, _focus.Snapshot);
        };

        _autoItem = new ToolStripMenuItem("Auto-correct", null, (_, _) => Change(() => _settings.AutoCorrectEnabled = !_settings.AutoCorrectEnabled));
        var open = new ToolStripMenuItem("Open " + AppInfo.Name, null, (_, _) => ShowMain()) { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        _updateItem = new ToolStripMenuItem("Check for updates", null, (_, _) => OnUpdateMenu());
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            open,
            new ToolStripSeparator(),
            _autoItem,
            _updateItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);

        _tray = new NotifyIcon { Text = AppInfo.Name, ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMain(); };
        _tray.BalloonTipClicked += (_, _) => { if (_balloonIsUpdate) ShowMain("home"); };
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

        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = 60 * 60 * 1000;
            if (UpdateCheckDue()) _ = CheckForUpdatesAsync(manual: false);
        };
        _updateTimer.Start();

        if (justUpdated)
        {
            var text = "You now have version " + Installer.CurrentVersion + ". Your languages, word list and settings are just as you left them.";
            ShowBalloon(AppInfo.Name + " was updated", text, update: false);
            _pendingToast = "Updated to version " + Installer.CurrentVersion;
            // The download it was installed from has closed by now.
            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => Updater.CleanUp());
        }

        if (showWindow) _indicator.BeginInvoke(() => ShowMain());
    }

    // ---------------- main window ----------------

    private string? _pendingToast;

    private void ShowMain(string? page = null)
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
        if (page != null) _main.ShowPage(page);
        if (_pendingToast != null)
        {
            _main.Toast(_pendingToast);
            _pendingToast = null;
        }
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
            showFixes = _settings.ShowFixes,
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
                beta = info.Beta,
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
            checkForUpdates = _settings.CheckForUpdates,
            update = new
            {
                status = _updateStatus,
                version = _update?.Version.ToString(),
                progress = _updateProgress,
                error = _updateError,
                // A copy run without installing gets the new version from the download page instead.
                installed = Installer.IsRunningInstalledCopy,
                @checked = _settings.UpdateCheckedAt is { } at ? WhenChecked(at.ToLocalTime(), now) : null,
            },
        };
    }

    /// <summary>"today at 14:32", "yesterday at 09:10" or "on 3 Sep".</summary>
    private static string WhenChecked(DateTime at, DateTime now) =>
        at.Date == now.Date ? "today at " + at.ToString("HH:mm")
        : at.Date == now.Date.AddDays(-1) ? "yesterday at " + at.ToString("HH:mm")
        : "on " + at.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);

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
            case "update.check":
                _ = CheckForUpdatesAsync(manual: true);
                return;
            case "update.install":
                StartUpdate();
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
            case "showFixes": _settings.ShowFixes = value.GetBoolean(); break;
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
            case "checkForUpdates":
                _settings.CheckForUpdates = value.GetBoolean();
                if (UpdateCheckDue()) _ = CheckForUpdatesAsync(manual: false);
                break;
        }
    }

    // ---------------- updates ----------------

    private bool UpdateCheckDue() =>
        _settings.CheckForUpdates && (_settings.UpdateCheckedAt is not { } at || DateTime.UtcNow - at > TimeSpan.FromHours(20));

    /// <summary>
    /// Asks the download page for its newest version. A check the user asked for reports what it found (or why it
    /// couldn't look); the daily one stays quiet unless there's something new, which the tray announces once.
    /// </summary>
    private async System.Threading.Tasks.Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateStatus is "checking" or "downloading" or "installing") return;
        var before = _updateStatus;
        _updateStatus = "checking";
        _updateError = null;
        PushState();
        try
        {
            _update = await Updater.CheckAsync();
            _settings.UpdateCheckedAt = DateTime.UtcNow;
            _updateStatus = _update != null ? "available" : manual ? "latest" : "idle";
            if (_update is { } found && !manual && _settings.UpdateAnnounced != found.Version.ToString())
            {
                _settings.UpdateAnnounced = found.Version.ToString();
                ShowBalloon("Update available", $"{AppInfo.Name} {found.Version} is ready. Click here to update.", update: true);
            }
            Save();
        }
        catch (Exception ex)
        {
            Log.Write("Update check failed: " + ex.Message);
            if (manual)
            {
                _updateStatus = "error";
                _updateError = ex switch
                {
                    System.Net.Http.HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound } =>
                        "The download page isn't available right now. Try again later.",
                    System.IO.InvalidDataException => ex.Message,
                    _ => "Couldn't reach the download page. Check your internet connection and try again.",
                };
            }
            else
            {
                _updateStatus = before is "latest" or "error" ? "idle" : before;
            }
        }
        SyncMenu();
        PushState();
    }

    /// <summary>Downloads the newer version and hands over to it: it closes this copy, installs itself and starts again.</summary>
    private async void StartUpdate()
    {
        if (_update is not { } update || _updateStatus is "checking" or "downloading" or "installing") return;
        if (!Installer.IsRunningInstalledCopy)
        {
            OpenUrl(AppInfo.Website); // running without installing: there's nothing to update in place
            return;
        }
        _updateStatus = "downloading";
        _updateProgress = 0;
        _updateError = null;
        SyncMenu();
        PushState();
        try
        {
            int shown = 0;
            var progress = new Progress<double>(p =>
            {
                _updateProgress = p;
                if ((int)(p * 100) < shown + 3) return; // every few percent is plenty
                shown = (int)(p * 100);
                PushState();
            });
            var file = await Updater.DownloadAsync(update, progress);
            _updateStatus = "installing";
            PushState();
            Log.Write("Installing version " + update.Version);
            Updater.Install(file);
        }
        catch (Exception ex)
        {
            Log.Write("Update failed: " + ex);
            _updateStatus = "error";
            _updateError = ex is System.IO.InvalidDataException
                ? ex.Message
                : "The download didn't finish. Check your internet connection and try again.";
            SyncMenu();
            PushState();
        }
    }

    private void OnUpdateMenu()
    {
        if (_update != null)
        {
            ShowMain("home");
            StartUpdate();
            return;
        }
        ShowMain("settings");
        _ = CheckForUpdatesAsync(manual: true);
    }

    private void ShowBalloon(string title, string text, bool update)
    {
        _balloonIsUpdate = update;
        _tray.ShowBalloonTip(6000, title, text, ToolTipIcon.Info);
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

        var badge = layout;
        if (_expected is { } expected)
        {
            if (layout == expected.Lang || DateTime.UtcNow > expected.Until) _expected = null;
            else badge = expected.Lang;
        }
        if (_settings.ShowIndicator && badge != null && snap.Caret is { } caret && !ownApp)
            _indicator.ShowAt(caret, badge.Value);
        else if (_indicator.Visible)
            _indicator.Hide();
    }

    /// <summary>Shows <paramref name="lang"/> on the badge now; Windows reports the keyboard switch a little later.</summary>
    private void ExpectLayout(Lang lang)
    {
        _expected = (lang, DateTime.UtcNow.AddMilliseconds(700));
        if (_indicator.Visible && _focus.Snapshot.Caret is { } caret) _indicator.ShowAt(caret, lang);
    }

    /// <summary>A click on the badge switches the keyboard to the next of the user's languages.</summary>
    private void OnBadgeClicked()
    {
        var languages = _session.Languages;
        var current = _expected?.Lang ?? _focus.Snapshot.Layout ?? LayoutService.Current();
        if (current == null || languages.Count < 2) return;
        var next = languages[(languages.ToList().IndexOf(current.Value) + 1) % languages.Count];
        var send = new InputSender();
        LayoutService.AppendSwitch(send, next);
        send.Send();
        ExpectLayout(next);
    }

    private void OnFixed(FixWord fix, string? app)
    {
        _settings.CountFix(DateTime.Now, fix.Correction.WordCount);
        _recent.Insert(0, new RecentFix(DateTime.Now, fix.Correction.Typed, fix.Correction.Replacement, FriendlyAppName(app)));
        if (_recent.Count > 12) _recent.RemoveAt(_recent.Count - 1);
        ExpectLayout(fix.Layout);
        if (_settings.ShowFixes)
        {
            // The card waits for the word to be found on screen, to sit right above it.
            var card = new FixCardText(fix.Correction.Typed, fix.Correction.Replacement, UndoTip: _settings.TakeUndoTip());
            var line = _focus.Snapshot.Caret;
            _flash.WhenLocated(word =>
            {
                if (line is { } l) _card.ShowFix(card, l, word);
            });
        }
        Save();
        PushState();
    }

    private void OnUndone(UndoFix undo)
    {
        // An undone fix doesn't count and leaves the recent list.
        _settings.TotalFixes = Math.Max(0, _settings.TotalFixes - undo.Correction.WordCount);
        _settings.TodayFixes = Math.Max(0, _settings.TodayFixes - undo.Correction.WordCount);
        if (_recent.Count > 0 && _recent[0].Fixed == undo.Correction.Replacement) _recent.RemoveAt(0);
        _settings.UndoTipDone = true;
        _flash.Cancel();
        ExpectLayout(undo.Layout);
        if (_settings.ShowFixes && _focus.Snapshot.Caret is { } line)
            _card.ShowFix(new FixCardText(undo.Correction.Typed, undo.Correction.Replacement, Undone: true), line, null);
        Save();
        PushState();
        if (undo.NowBlocked)
        {
            ShowBalloon(AppInfo.Name,
                $"\"{undo.Correction.TriggerTyped}\" won't be auto-corrected anymore. You can change this in the Never fix list.",
                update: false);
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

    private void SyncMenu()
    {
        _autoItem.Checked = _settings.AutoCorrectEnabled;
        _updateItem.Text = _update != null ? "Update to version " + _update.Version : "Check for updates";
        _updateItem.Enabled = _updateStatus is not ("checking" or "downloading" or "installing");
    }

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
        // Drawn at the exact size the tray shows, so the label stays sharp instead of being shrunk from 32×32.
        using var bmp = BadgeArt.TrayIcon(lang, Math.Max(16, SystemInformation.SmallIconSize.Width));
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
        _updateTimer.Stop();
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
        _card.Close();
        _flash.Dispose();
        Save();
        base.ExitThreadCore();
    }
}
