using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AppSettings.MigrateLegacy();
        Application.ThreadException += (_, e) => Log.Write("UI error: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Fatal: " + e.ExceptionObject);

        if (args.Contains("--uninstall"))
        {
            RunUninstall(quiet: args.Contains("--quiet"));
            return;
        }

        bool selfCheck = args.Contains("--selfcheck");
        if (!selfCheck && !args.Contains("--portable") && !Installer.IsRunningInstalledCopy)
        {
            var result = RunSetup(Installer.IsInstalled ? "update" : "install");
            if (result != SetupResult.Portable) return;
        }

        using var mutex = new Mutex(true, @"Local\" + AppInfo.Id + ".SingleInstance", out bool isFirst);
        if (!isFirst && !selfCheck)
        {
            // Already running (e.g. opened again from the Start menu): bring its window up.
            if (!Installer.SignalShow())
                MessageBox.Show(AppInfo.Name + " is already running (look for its icon near the clock).", AppInfo.Name);
            return;
        }

        WrongLayoutDetector detector;
        try
        {
            detector = WrongLayoutDetector.LoadDefault();
        }
        catch (Exception ex)
        {
            Log.Write("Loading dictionaries failed: " + ex);
            MessageBox.Show(AppInfo.Name + " could not load its dictionaries:\n" + ex.Message, AppInfo.Name);
            return;
        }

        if (selfCheck)
        {
            MessageBox.Show(SelfCheck(detector), AppInfo.Name + " self-check");
            return;
        }

        Log.Write("Started v" + Installer.CurrentVersion);
        // Open the window on a manual launch; stay in the tray when started with Windows.
        bool showWindow = !args.Contains("--background");
        Application.Run(new TrayApp(detector, showWindow));
    }

    private static SetupResult RunSetup(string mode)
    {
        if (!WebWindow.RuntimeAvailable) return Installer.InstallWithMessageBoxes();
        using var window = new SetupWindow(mode);
        Application.Run(window);
        return window.Result;
    }

    private static void RunUninstall(bool quiet)
    {
        SetupResult result;
        if (quiet || !WebWindow.RuntimeAvailable)
        {
            result = Installer.UninstallWithMessageBoxes(quiet);
        }
        else
        {
            using var window = new SetupWindow("uninstall");
            Application.Run(window);
            result = window.Result;
        }
        if (result == SetupResult.Uninstalled) Installer.ScheduleSelfDelete();
    }

    private static string SelfCheck(WrongLayoutDetector detector)
    {
        var sb = new StringBuilder();
        void Line(bool ok, string text) => sb.AppendLine((ok ? "✔ " : "✘ ") + text);

        Line(LayoutService.FindInstalled(Lang.English) != null, "English keyboard layout installed");
        Line(LayoutService.FindInstalled(Lang.Hebrew) != null, "Hebrew keyboard layout installed");
        sb.AppendLine("   Switch shortcut: " + LayoutService.DescribeSwitchMethod());
        var d = detector.Evaluate("akuo", Lang.English, Sensitivity.Medium);
        Line(d.ShouldFix && d.Replacement == "שלום", $"Detection: akuo → {d.Replacement}");
        Line(VoiceAnnouncer.EnglishVoice != null, "English voice available");
        Line(VoiceAnnouncer.HebrewVoice != null, "Hebrew voice available (optional – otherwise says \"Hebrew\" in English)");
        Line(WebWindow.RuntimeAvailable, "WebView2 runtime (for the app window)");
        Line(Installer.IsInstalled, "Installed in " + Installer.InstallDir);
        sb.AppendLine();
        sb.AppendLine($"Settings: {AppSettings.DefaultPath}");
        return sb.ToString();
    }
}
