using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AppKit;
using CoreGraphics;
using Foundation;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Mac;

/// <summary>
/// The Mac app: a menu bar icon showing the keyboard's language, the key watcher that fixes words, and the app window
/// (the Windows app's page, UI/app.html, which adapts itself with <c>platform: "mac"</c>).
/// </summary>
internal sealed class MacApp : NSApplicationDelegate
{
    private sealed record RecentFix(DateTime Time, string Typed, string Fixed, string? App, Lang To);

    private AppSettings _settings = new();
    private NeverFixList _neverFix = null!;
    private UndoQuestion _question = null!;
    private TypingSession _session = null!;
    private WrongLayoutDetector _detector = null!;
    private KeyTap _tap = null!;
    private readonly Voice _voice = new();
    private readonly List<RecentFix> _recent = new();
    private IReadOnlyList<Lang> _installedLanguages = [];
    private TapContext _context = new(true, Sensitivity.Medium, false);

    private NSStatusItem _status = null!;
    private NSMenuItem _autoItem = null!, _updateItem = null!;
    private PageWindow? _window;
    private PageWindow? _whatsNew;
    private bool _whatsNewLoaded;
    private string? _pendingToast;
    private NSTimer? _timer, _updateTimer;
    private int _ticks;

    // The app in front, for the apps auto-correct is turned off in.
    private string? _frontName, _frontExe;
    private bool _ownAppInFront;
    private NSObject? _activationObserver;

    private Lang? _lastLayout;
    private bool _listenAsked;

    // Updates: the first look a couple of minutes after start, then every hour whether a daily check is due.
    private UpdateManifest? _update;
    private string _updateStatus = "idle"; // idle, checking, latest, available, error
    private string? _updateError;

    /// <summary>
    /// --selfcheck: start up, check that the parts that talk to macOS work (keyboards, detection, the menu bar icon, the
    /// window and its page), print what was found and quit. The build runs it on a Mac.
    /// </summary>
    public static bool SelfCheck { get; set; }

    public override void DidFinishLaunching(NSNotification notification)
    {
        if (!SelfCheck && RunningFromDiskImage())
        {
            Alert("Move Language Autocorrect to Applications first",
                "Drag Language Autocorrect into the Applications folder, then open it from there. " +
                "(macOS only lets it fix your typing once it's installed.)");
            NSApplication.SharedApplication.Terminate(this);
            return;
        }

        bool firstRun = !File.Exists(MacPaths.Settings);
        _settings = AppSettings.Load(MacPaths.Settings);
        if (firstRun && !SelfCheck) SetUpFirstRun();

        try
        {
            _detector = WrongLayoutDetector.LoadDefault();
        }
        catch (Exception ex)
        {
            Log.Write("Loading dictionaries failed: " + ex);
            Alert(AppInfo.Name + " could not load its dictionaries", ex.Message);
            NSApplication.SharedApplication.Terminate(this);
            return;
        }

        _neverFix = new NeverFixList(_settings.NeverFixUndoCounts, _settings.UndosToBlock);
        _question = new UndoQuestion(_settings.UndoAskCounts, _settings.UndosToAsk);
        // The card that asks about words undone again and again isn't on the Mac yet.
        _settings.AskAfterUndos = false;
        _session = new TypingSession(_detector, _neverFix, question: _question) { LearnFromUndos = _settings.LearnFromUndos };
        _tap = new KeyTap(_session, () => _context);
        // After the key watcher has handed the key back to macOS (saving and the window's update take a moment).
        _tap.Fixed += fix => CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(() => OnFixed(fix));
        _tap.Undone += undo => CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(() => OnUndone(undo));

        RefreshLanguages();
        _lastLayout = InputSources.Observe();
        BuildMenus();
        NoteFrontApp(NSWorkspace.SharedWorkspace.FrontmostApplication);
        _activationObserver = NSWorkspace.Notifications.ObserveDidActivateApplication((_, e) =>
        {
            NoteFrontApp(e.Application);
            _tap.Reset();
        });
        if (SelfCheck)
        {
            RunSelfCheck();
            return;
        }

        // After an update (a newer copy dragged over this one): what's new since the version that ran before.
        _settings.WhatsNewFrom = WhatsNew.From(_settings.WhatsNewFrom, _settings.LastRunVersion, AppVersion.Current);
        _settings.LastRunVersion = AppVersion.Current;
        // Opening at login can also be turned off in System Settings → General → Login Items.
        if (firstRun) LoginItem.Apply(true);
        else _settings.StartWithWindows = LoginItem.IsOn;
        Save();

        if (Permission.Granted) StartWatching();
        Log.Write("Started v" + AppVersion.Current);

        _timer = NSTimer.CreateRepeatingScheduledTimer(0.25, _ => OnTick());
        NSTimer.CreateScheduledTimer(120, false, t => _ = CheckForUpdatesIfDueAsync());
        _updateTimer = NSTimer.CreateRepeatingScheduledTimer(60 * 60, t => _ = CheckForUpdatesIfDueAsync());

        // Opened by hand (not at login), or there's something to do: show the window.
        if (!LaunchedAtLogin() || !Permission.Granted || firstRun || _settings.WhatsNewFrom != null) ShowWindow();
    }

    public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows)
    {
        // Opened again from Applications or Launchpad while running.
        ShowWindow();
        return true;
    }

    public override void WillTerminate(NSNotification notification)
    {
        _activationObserver?.Dispose();
        Save();
    }

    /// <summary>A new user: start with the Mac, and leave Remote Desktop alone like the Windows app does.</summary>
    private void SetUpFirstRun()
    {
        _settings.StartWithWindows = true;
        foreach (var app in new[] { "Windows App", "Microsoft Remote Desktop" })
            if (!_settings.IsExcluded(app)) _settings.ExcludedApps.Add(app);
    }

    // ---------------- watching the keyboard ----------------

    private void StartWatching()
    {
        if (_tap.IsRunning) return;
        if (_tap.Start())
        {
            PushState();
            UpdateStatusIcon();
            return;
        }
        // Allowed under Accessibility, and still not watching: this Mac wants Input Monitoring too.
        if (!_listenAsked)
        {
            _listenAsked = true;
            Log.Write("Watching the keyboard failed; asking for Input Monitoring");
            Permission.AskInputMonitoring();
        }
    }

    private void OnTick()
    {
        try
        {
            _ticks++;
            // Waiting for the Accessibility permission: start as soon as it's given (looked at every second).
            if (!_tap.IsRunning && _ticks % 4 == 0 && Permission.Granted) StartWatching();

            // Notice keyboards added or removed in System Settings (every few seconds).
            if (_ticks % 20 == 0 && InputSources.Refresh())
            {
                RefreshLanguages();
                PushState();
            }

            var layout = InputSources.Observe();
            if (layout != null && layout != _lastLayout)
            {
                // Speak only when the keyboard changes in the same app (not when switching apps).
                if (_lastLayout != null && _settings.VoiceEnabled && !_ownAppInFront) _voice.Announce(layout.Value);
                _lastLayout = layout;
                UpdateStatusIcon();
                PushState();
            }
        }
        catch (Exception ex)
        {
            Log.Write("Tick error: " + ex);
        }
    }

    private void NoteFrontApp(NSRunningApplication? app)
    {
        _frontName = app?.LocalizedName;
        _frontExe = app?.ExecutableUrl?.LastPathComponent;
        _ownAppInFront = app != null && app.ProcessIdentifier == NSProcessInfo.ProcessInfo.ProcessIdentifier;
        UpdateContext();
    }

    private void UpdateContext()
    {
        bool blocked = _ownAppInFront || _settings.IsExcluded(_frontName) || _settings.IsExcluded(_frontExe);
        _context = new TapContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, blocked);
    }

    /// <summary>Re-reads the keyboards and the chosen languages; loads word lists in the background.</summary>
    private void RefreshLanguages()
    {
        InputSources.Refresh();
        _installedLanguages = InputSources.InstalledLanguages();
        // Only languages with a keyboard can be switched to.
        var enabled = _settings.EnabledLanguages(_installedLanguages).Where(_installedLanguages.Contains).ToList();
        _session.Languages = enabled;
        Task.Run(() =>
        {
            try { _detector.Preload(enabled); }
            catch (Exception ex) { Log.Write("Loading word lists failed: " + ex.Message); }
        });
    }

    private void OnFixed(FixWord fix)
    {
        _settings.CountFix(DateTime.Now, fix.Correction.WordCount);
        _recent.Insert(0, new RecentFix(DateTime.Now, fix.Correction.Typed, fix.Correction.Replacement, _frontName, fix.Correction.To));
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
        _settings.UndoTipDone = true;
        if (undo.NowBlocked)
            Toast($"“{undo.Correction.TriggerTyped}” won't be auto-corrected anymore. You can change this in Never fix.");
        Save();
        PushState();
    }

    // ---------------- menu bar ----------------

    private void BuildMenus()
    {
        _status = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
        _autoItem = new NSMenuItem("Auto-correct", (_, _) => Change(() => _settings.AutoCorrectEnabled = !_settings.AutoCorrectEnabled));
        _updateItem = new NSMenuItem("Check for Updates…", (_, _) => OnUpdateMenu());
        var menu = new NSMenu();
        menu.AddItem(new NSMenuItem("Open " + AppInfo.Name, (_, _) => ShowWindow()));
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(_autoItem);
        menu.AddItem(_updateItem);
        menu.AddItem(NSMenuItem.SeparatorItem);
        menu.AddItem(new NSMenuItem("Quit " + AppInfo.Name, "q", (_, _) => NSApplication.SharedApplication.Terminate(this)));
        _status.Menu = menu;
        UpdateStatusIcon();
        SyncMenu();

        // The menus for while the window is open: ⌘C, ⌘V and the others work in its text boxes through them.
        var appMenu = new NSMenu();
        appMenu.AddItem(new NSMenuItem("Hide " + AppInfo.Name, "h", (_, _) => NSApplication.SharedApplication.Hide(this)));
        appMenu.AddItem(NSMenuItem.SeparatorItem);
        appMenu.AddItem(new NSMenuItem("Quit " + AppInfo.Name, "q", (_, _) => NSApplication.SharedApplication.Terminate(this)));
        var edit = new NSMenu("Edit");
        foreach (var (title, action, key) in new[]
                 {
                     ("Undo", "undo:", "z"), ("Redo", "redo:", "Z"), ("", "", ""), ("Cut", "cut:", "x"), ("Copy", "copy:", "c"),
                     ("Paste", "paste:", "v"), ("Select All", "selectAll:", "a"),
                 })
        {
            edit.AddItem(title == "" ? NSMenuItem.SeparatorItem : new NSMenuItem(title, new ObjCRuntime.Selector(action), key));
        }
        var window = new NSMenu("Window");
        window.AddItem(new NSMenuItem("Minimize", new ObjCRuntime.Selector("performMiniaturize:"), "m"));
        window.AddItem(new NSMenuItem("Close", new ObjCRuntime.Selector("performClose:"), "w"));
        var main = new NSMenu();
        foreach (var (title, sub) in new[] { (AppInfo.Name, appMenu), ("Edit", edit), ("Window", window) })
            main.AddItem(new NSMenuItem(title) { Submenu = sub });
        NSApplication.SharedApplication.MainMenu = main;
    }

    private void UpdateStatusIcon()
    {
        var lang = _lastLayout ?? Lang.English;
        bool paused = !_settings.AutoCorrectEnabled || !_tap.IsRunning;
        if (_status.Button is not { } button) return;
        button.Image = StatusBadge.Draw(lang, paused);
        button.ToolTip = !_tap.IsRunning
            ? AppInfo.Name + " – waiting for permission"
            : AppInfo.Name + " – " + Languages.Get(lang).Name + (_settings.AutoCorrectEnabled ? "" : " (paused)");
    }

    private void SyncMenu()
    {
        _autoItem.State = _settings.AutoCorrectEnabled ? NSCellStateValue.On : NSCellStateValue.Off;
        _updateItem.Title = _update != null ? $"Download Version {_update.Version}…" : "Check for Updates…";
        _updateItem.Enabled = _updateStatus != "checking";
    }

    // ---------------- the window ----------------

    private void ShowWindow(string? page = null)
    {
        if (_window == null)
        {
            _window = new PageWindow(AppInfo.Name, new CGSize(980, 680), new CGSize(800, 560), resizable: true);
            _window.Message += OnPageMessage;
            _window.Closed += () =>
            {
                _window = null;
                // Back to a menu bar app, without a Dock icon (unless What's new is still open).
                if (_whatsNew == null) NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Accessory;
            };
            _window.LoadPage("app.html");
            _pendingPage = page;
        }
        else if (page != null)
        {
            if (_window.IsReady) _window.Post(new { type = "show", page });
            else _pendingPage = page;
        }
        // While the window is open the app has a Dock icon and menus, like any app.
        NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        NSApplication.SharedApplication.Activate();
        _window.Show();
        ShowWhatsNew();
    }

    private string? _pendingPage;

    private void OnPageMessage(string type, JsonElement msg)
    {
        if (type == "ready")
        {
            PushState();
            if (SelfCheck)
            {
                FinishSelfCheck(windowLoaded: true);
                return;
            }
            if (_pendingPage != null) _window?.Post(new { type = "show", page = _pendingPage });
            _pendingPage = null;
            if (!_tap.IsRunning) _window?.Post(new { type = "show", page = "home" });
            if (_pendingToast != null) Toast(_pendingToast);
            return;
        }
        HandleAction(type, msg);
        PushState();
    }

    /// <summary>A short message at the bottom of the window (when it's next open, if it isn't).</summary>
    private void Toast(string text)
    {
        if (_window?.IsReady != true)
        {
            _pendingToast = text;
            return;
        }
        _window.Post(new { type = "toast", text });
        _pendingToast = null;
    }

    /// <summary>After an update: what's new in each version since the one that ran before, once.</summary>
    private void ShowWhatsNew()
    {
        if (_whatsNew != null || _settings.WhatsNewFrom is not { } from) return;
        if (!WhatsNew.Due(from, AppVersion.Current))
        {
            _settings.WhatsNewFrom = null;
            Save();
            return;
        }
        var url = WhatsNew.PageUrl(AppInfo.Website, from, AppVersion.Current);
        var window = _whatsNew = new PageWindow("What’s new in " + AppInfo.Name, new CGSize(760, 480), new CGSize(760, 480), resizable: false);
        _whatsNewLoaded = false;
        window.Web.NavigationDelegate = new WhatsNewNavigation(url, loaded =>
        {
            if (loaded) _whatsNewLoaded = true;
            else window.Close(); // no internet, most likely: try again the next time the window opens
        });
        window.Message += (type, _) => { if (type == "close") window.Close(); };
        window.Closed += () =>
        {
            _whatsNew = null;
            if (_window == null) NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Accessory;
            if (!_whatsNewLoaded) return;
            _settings.WhatsNewFrom = null;
            Save();
        };
        window.LoadUrl(url);
        window.Show();
    }

    private void PushState() => _window?.Post(new { type = "state", state = BuildState() });

    private object BuildState()
    {
        var now = DateTime.Now;
        var layout = _lastLayout;
        var enabled = _settings.EnabledLanguages(_installedLanguages);
        return new
        {
            platform = "mac",
            permission = _tap.IsRunning,
            version = AppVersion.Current,
            autoCorrect = _settings.AutoCorrectEnabled,
            showIndicator = _settings.ShowIndicator,
            showFixes = _settings.ShowFixes,
            voice = _settings.VoiceEnabled,
            sensitivity = _settings.Sensitivity.ToString(),
            startWithWindows = _settings.StartWithWindows,
            learnFromUndos = _settings.LearnFromUndos,
            undosToBlock = _neverFix.UndosToBlock,
            askAfterUndos = false,
            undosToAsk = _question.UndosToAsk,
            neverFix = _neverFix.BlockedWords(),
            excludedApps = _settings.ExcludedApps,
            layout = layout is Lang l ? LanguageJson(Languages.Get(l)) : null,
            languages = Languages.All.Where(info => !InputSources.Unsupported(info.Lang)).Select(info => new
            {
                code = info.Code,
                name = info.Name,
                nativeName = info.NativeName,
                badge = info.Badge,
                color = info.Color,
                beta = info.Beta,
                enabled = enabled.Contains(info.Lang),
                installed = _installedLanguages.Contains(info.Lang),
                locked = info.Lang == Lang.English,
            }),
            fixesToday = _settings.FixesToday(now),
            fixesTotal = _settings.TotalFixes,
            nativeVoices = _session.Languages
                .Where(x => x != Lang.English && Voice.VoiceFor(x) == null)
                .Select(x => Languages.Get(x).Name),
            recent = _recent.Select(r => new
            {
                time = r.Time.ToString("HH:mm"), typed = r.Typed, @fixed = r.Fixed, app = r.App,
                to = LanguageJson(Languages.Get(r.To)),
            }),
            checkForUpdates = _settings.CheckForUpdates,
            update = new
            {
                status = _updateStatus,
                version = _update?.Version.ToString(),
                progress = 0,
                error = _updateError,
                // A new version is downloaded from the website and dragged over this one.
                installed = false,
                @checked = _settings.UpdateCheckedAt is { } at ? WhenChecked(at.ToLocalTime(), now) : null,
            },
        };
    }

    private static object LanguageJson(LanguageInfo info) =>
        new { code = info.Code, name = info.Name, badge = info.Badge, color = info.Color };

    /// <summary>"today at 14:32", "yesterday at 09:10" or "on 3 Sep".</summary>
    private static string WhenChecked(DateTime at, DateTime now) =>
        at.Date == now.Date ? "today at " + at.ToString("HH:mm")
        : at.Date == now.Date.AddDays(-1) ? "yesterday at " + at.ToString("HH:mm")
        : "on " + at.ToString("d MMM", CultureInfo.InvariantCulture);

    private void HandleAction(string type, JsonElement msg)
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
                _voice.Speak(_lastLayout ?? Lang.English);
                return;
            case "language.set":
                SetLanguage(msg.GetProperty("code").GetString(), msg.GetProperty("on").GetBoolean());
                break;
            case "openLanguageSettings":
                Links.Open("x-apple.systempreferences:com.apple.Keyboard-Settings.extension");
                return;
            case "openAppsSettings":
                // "Uninstall": show the app in Applications, to drag to the Trash.
                NSWorkspace.SharedWorkspace.ActivateFileViewer([NSUrl.FromFilename(NSBundle.MainBundle.BundlePath)]);
                return;
            case "permission.ask":
                Permission.Ask();
                return;
            case "permission.open":
                Permission.OpenSettings();
                return;
            case "update.check":
                _ = CheckForUpdatesAsync(manual: true);
                return;
            case "update.install":
                Links.Open(AppInfo.Website + "#install");
                return;
            case "releaseNotes":
                Links.Open(AppInfo.Website + "release-notes/");
                return;
            case "privacy":
                Links.Open(AppInfo.Website + "privacy/");
                return;
        }
        UpdateContext();
        UpdateStatusIcon();
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
            case "learnFromUndos": _settings.LearnFromUndos = value.GetBoolean(); break;
            case "undosToBlock":
                _settings.UndosToBlock = Math.Clamp(value.GetInt32(), 1, 10);
                _neverFix.UndosToBlock = _settings.UndosToBlock;
                break;
            case "sensitivity":
                if (Enum.TryParse<Sensitivity>(value.GetString(), out var s)) _settings.Sensitivity = s;
                break;
            case "startWithWindows":
                _settings.StartWithWindows = value.GetBoolean();
                LoginItem.Apply(_settings.StartWithWindows);
                break;
            case "checkForUpdates":
                _settings.CheckForUpdates = value.GetBoolean();
                _ = CheckForUpdatesIfDueAsync();
                break;
        }
        _session.LearnFromUndos = _settings.LearnFromUndos;
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

    private void Change(Action change)
    {
        change();
        UpdateContext();
        UpdateStatusIcon();
        SyncMenu();
        Save();
        PushState();
    }

    private void Save()
    {
        try
        {
            _settings.NeverFixUndoCounts = _neverFix?.Snapshot() ?? _settings.NeverFixUndoCounts;
            _settings.UndoAskCounts = _question?.Snapshot() ?? _settings.UndoAskCounts;
            _settings.Save(MacPaths.Settings);
        }
        catch (Exception ex)
        {
            Log.Write("Saving settings failed: " + ex.Message);
        }
    }

    // ---------------- updates ----------------

    private async Task CheckForUpdatesIfDueAsync()
    {
        bool due = _settings.CheckForUpdates && (_settings.UpdateCheckedAt is not { } at || DateTime.UtcNow - at > TimeSpan.FromHours(20));
        if (due) await CheckForUpdatesAsync(manual: false);
    }

    /// <summary>
    /// Asks the download page for its newest version. A check the user asked for reports what it found (or why it
    /// couldn't look); the daily one stays quiet unless there's something new.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateStatus == "checking") return;
        var before = _updateStatus;
        _updateStatus = "checking";
        _updateError = null;
        PushState();
        try
        {
            _update = await MacUpdater.CheckAsync();
            _settings.UpdateCheckedAt = DateTime.UtcNow;
            _updateStatus = _update != null ? "available" : manual ? "latest" : "idle";
            if (_update is { } found && !manual && _settings.UpdateAnnounced != found.Version.ToString())
            {
                _settings.UpdateAnnounced = found.Version.ToString();
                Toast($"Version {found.Version} is ready to download.");
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
                    InvalidDataException => ex.Message,
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

    private void OnUpdateMenu()
    {
        if (_update != null)
        {
            Links.Open(AppInfo.Website + "#install");
            return;
        }
        ShowWindow("settings");
        _ = CheckForUpdatesAsync(manual: true);
    }

    // ---------------- --selfcheck ----------------

    private readonly List<string> _checks = new();
    private bool _checkFailed, _checkDone;

    private void Check(bool ok, string text)
    {
        _checks.Add((ok ? "ok    " : "FAIL  ") + text);
        _checkFailed |= !ok;
    }

    private void RunSelfCheck()
    {
        Check(AppVersion.Current != "0.0.0", "Version " + AppVersion.Current);
        Check(true, "Keyboards added: " + string.Join(", ", _installedLanguages.Select(l => Languages.Get(l).Name)));
        Check(InputSources.CurrentId() != null, "Keyboard on now: " + InputSources.CurrentId());
        foreach (var (ok, text) in InputSources.SelfCheck()) Check(ok, text);
        var d = _detector.Evaluate("akuo", Lang.English, Sensitivity.Medium);
        Check(d.ShouldFix && d.Replacement == "שלום", "Detection: akuo → " + d.Replacement);
        Check(_status.Button?.Image != null, "Menu bar icon");
        Check(true, "Accessibility permission: " + (Permission.Granted ? "given" : "not given (as expected on a build machine)"));
        Check(true, "Voices: English " + (Voice.VoiceFor(Lang.English)?.Name ?? "none") +
                    ", Hebrew " + (Voice.VoiceFor(Lang.Hebrew)?.Name ?? "none"));
        // The window's page says "ready" when it has loaded and can talk to the app (see OnPageMessage).
        ShowWindow();
        NSTimer.CreateScheduledTimer(45, false, t => FinishSelfCheck(windowLoaded: false));
    }

    private void FinishSelfCheck(bool windowLoaded)
    {
        if (_checkDone) return;
        _checkDone = true;
        Check(windowLoaded, windowLoaded ? "App window: the page loaded and talks to the app" : "App window: the page didn't answer");
        Console.WriteLine(string.Join(Environment.NewLine, _checks));
        Environment.Exit(_checkFailed ? 1 : 0);
    }

    // ---------------- helpers ----------------

    /// <summary>
    /// Opened straight from the downloaded disk image (or from Downloads, which macOS runs from a hidden copy): it
    /// has to be in Applications for its permission to stick.
    /// </summary>
    private static bool RunningFromDiskImage()
    {
        var path = NSBundle.MainBundle.BundlePath;
        return path.StartsWith("/Volumes/", StringComparison.Ordinal) || path.Contains("/AppTranslocation/", StringComparison.Ordinal);
    }

    /// <summary>Opened by macOS at login (as a login item), rather than by the user.</summary>
    private static bool LaunchedAtLogin()
    {
        const uint keyAEPropData = 0x70726474;           // 'prdt'
        const uint keyAELaunchedAsLogInItem = 0x6C676974; // 'lgit'
        var ev = NSAppleEventManager.SharedAppleEventManager.CurrentAppleEvent;
        // Or the Mac has only just started.
        return ev?.ParamDescriptorForKeyword(keyAEPropData)?.EnumCodeValue() == keyAELaunchedAsLogInItem
            || NSProcessInfo.ProcessInfo.SystemUptime < 180;
    }

    private static void Alert(string title, string text)
    {
        NSApplication.SharedApplication.ActivationPolicy = NSApplicationActivationPolicy.Regular;
        NSApplication.SharedApplication.Activate();
        using var alert = new NSAlert { MessageText = title, InformativeText = text };
        alert.RunModal();
    }
}
