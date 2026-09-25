using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

internal sealed class TrayApp : ApplicationContext
{
    private readonly string _settingsPath = AppSettings.DefaultPath;
    private readonly AppSettings _settings;
    private readonly NeverFixList _neverFix;
    private readonly KeyboardMonitor _monitor;
    private readonly FocusTracker _focus = new();
    private readonly VoiceAnnouncer _voice = new();
    private readonly IndicatorForm _indicator = new();
    private readonly NotifyIcon _tray;
    private readonly Timer _uiTimer = new() { Interval = 60 };
    private readonly ToolStripMenuItem _autoItem, _indicatorItem, _voiceItem;
    private volatile MonitorContext _context;
    private Lang? _lastLayout;
    private IntPtr _lastWindow;
    private IntPtr _trayIconHandle;
    private SettingsForm? _settingsForm;
    private readonly System.Threading.EventWaitHandle _exitEvent;
    private readonly System.Threading.RegisteredWaitHandle _exitWait;

    public TrayApp(WrongLayoutDetector detector)
    {
        _settings = AppSettings.Load(_settingsPath);
        _neverFix = new NeverFixList(_settings.NeverFixUndoCounts, _settings.UndosToBlock);
        _context = new MonitorContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, false);

        // Force the indicator's handle so we can marshal calls to the UI thread through it.
        _ = _indicator.Handle;

        _monitor = new KeyboardMonitor(new TypingSession(detector, _neverFix), () => _context);
        _monitor.Undone += undo => _indicator.BeginInvoke(() => OnUndone(undo));

        _autoItem = new ToolStripMenuItem("Auto-correct", null, (_, _) => Toggle(s => s.AutoCorrectEnabled = !s.AutoCorrectEnabled));
        _indicatorItem = new ToolStripMenuItem("Show indicator", null, (_, _) => Toggle(s => s.ShowIndicator = !s.ShowIndicator));
        _voiceItem = new ToolStripMenuItem("Voice", null, (_, _) => Toggle(s => s.VoiceEnabled = !s.VoiceEnabled));
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _autoItem, _indicatorItem, _voiceItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);

        _tray = new NotifyIcon { Text = "LayoutBuddy", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowSettings();
        UpdateTrayIcon(LayoutService.Current() ?? Lang.English);
        SyncMenu();

        _focus.FocusChanged += _monitor.RequestReset;
        _focus.Start();
        _monitor.Start();
        if (!_monitor.IsRunning)
            MessageBox.Show("LayoutBuddy could not install its keyboard hook. Auto-correct will not work.", "LayoutBuddy");

        // The installer/uninstaller signals this to close us.
        _exitEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, Installer.ExitEventName);
        _exitWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(_exitEvent,
            (_, _) => _indicator.BeginInvoke(ExitThread), null, -1, executeOnlyOnce: true);

        _uiTimer.Tick += (_, _) => OnTick();
        _uiTimer.Start();
        StartupRegistration.Apply(_settings.StartWithWindows);
    }

    private void OnTick()
    {
        var snap = _focus.Snapshot;
        bool blocked = snap.IsPassword || _settings.IsExcluded(snap.Process);
        var ctx = new MonitorContext(_settings.AutoCorrectEnabled, _settings.Sensitivity, blocked);
        if (ctx != _context) _context = ctx;

        var layout = snap.Layout;
        if (layout != null && layout != _lastLayout)
        {
            // Speak only when the layout changes inside the same window (not when switching apps).
            if (_lastLayout != null && snap.Window == _lastWindow && _settings.VoiceEnabled)
                _voice.Announce(layout.Value);
            _lastLayout = layout;
            UpdateTrayIcon(layout.Value);
        }
        _lastWindow = snap.Window;

        bool ownWindow = _settingsForm != null && snap.Window == _settingsForm.Handle;
        if (_settings.ShowIndicator && layout != null && snap.Caret is { } caret && !ownWindow)
            _indicator.ShowAt(caret, layout.Value);
        else if (_indicator.Visible)
            _indicator.Hide();
    }

    private void OnUndone(UndoFix undo)
    {
        _settings.NeverFixUndoCounts = _neverFix.Snapshot();
        Save();
        if (undo.NowBlocked)
        {
            _tray.ShowBalloonTip(4000, "LayoutBuddy",
                $"\"{undo.Correction.Typed}\" won't be auto-corrected anymore (you undid it {_neverFix.UndosToBlock} times). " +
                "You can change this in Settings.", ToolTipIcon.Info);
        }
    }

    private void Toggle(Action<AppSettings> change)
    {
        change(_settings);
        SyncMenu();
        Save();
    }

    private void SyncMenu()
    {
        _autoItem.Checked = _settings.AutoCorrectEnabled;
        _indicatorItem.Checked = _settings.ShowIndicator;
        _voiceItem.Checked = _settings.VoiceEnabled;
    }

    private void ShowSettings()
    {
        if (_settingsForm != null)
        {
            _settingsForm.Activate();
            return;
        }
        var backup = _neverFix.Snapshot();
        int backupUndos = _neverFix.UndosToBlock;
        using var form = new SettingsForm(_settings, _neverFix);
        _settingsForm = form;
        try
        {
            if (form.ShowDialog() == DialogResult.OK)
            {
                form.ApplyTo(_settings);
                _neverFix.UndosToBlock = _settings.UndosToBlock;
                StartupRegistration.Apply(_settings.StartWithWindows);
                SyncMenu();
                Save();
            }
            else
            {
                _neverFix.Restore(backup, backupUndos);
            }
        }
        finally
        {
            _settingsForm = null;
        }
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

    private void UpdateTrayIcon(Lang lang)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(lang == Lang.Hebrew ? IndicatorForm.HebrewColor : IndicatorForm.EnglishColor);
            g.FillRectangle(bg, 0, 2, 32, 28);
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(lang == Lang.Hebrew ? "עב" : "EN", font, Brushes.White, new RectangleF(0, 0, 32, 32), fmt);
        }
        var handle = bmp.GetHicon();
        var old = _trayIconHandle;
        _tray.Icon = Icon.FromHandle(handle);
        _trayIconHandle = handle;
        if (old != IntPtr.Zero) Native.DestroyIcon(old);
        _tray.Text = lang == Lang.Hebrew ? "LayoutBuddy – Hebrew" : "LayoutBuddy – English";
    }

    protected override void ExitThreadCore()
    {
        _uiTimer.Stop();
        _exitWait.Unregister(null);
        _exitEvent.Dispose();
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
