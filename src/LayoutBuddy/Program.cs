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

        if (args.Contains("--uninstall"))
        {
            Installer.Uninstall(quiet: args.Contains("--quiet"));
            return;
        }

        bool selfCheck = args.Contains("--selfcheck");
        if (!selfCheck && !args.Contains("--portable") && !Installer.IsRunningInstalledCopy && Installer.OfferInstall())
            return;

        using var mutex = new Mutex(true, @"Local\LayoutBuddy.SingleInstance", out bool isFirst);
        if (!isFirst && !selfCheck)
        {
            MessageBox.Show("LayoutBuddy is already running (look for its icon near the clock).", "LayoutBuddy");
            return;
        }

        Application.ThreadException += (_, e) => Log.Write("UI error: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Fatal: " + e.ExceptionObject);

        WrongLayoutDetector detector;
        try
        {
            detector = WrongLayoutDetector.LoadDefault();
        }
        catch (Exception ex)
        {
            Log.Write("Loading dictionaries failed: " + ex);
            MessageBox.Show("LayoutBuddy could not load its dictionaries:\n" + ex.Message, "LayoutBuddy");
            return;
        }

        if (selfCheck)
        {
            MessageBox.Show(SelfCheck(detector), "LayoutBuddy self-check");
            return;
        }

        Log.Write("Started v" + Application.ProductVersion);
        Application.Run(new TrayApp(detector));
    }

    private static string SelfCheck(WrongLayoutDetector detector)
    {
        var sb = new StringBuilder();
        void Line(bool ok, string text) => sb.AppendLine((ok ? "✔ " : "✘ ") + text);

        Line(LayoutService.FindInstalled(Lang.English) != null, "English keyboard layout installed");
        Line(LayoutService.FindInstalled(Lang.Hebrew) != null, "Hebrew keyboard layout installed");
        var d = detector.Evaluate("akuo", Lang.English, Sensitivity.Medium);
        Line(d.ShouldFix && d.Replacement == "שלום", $"Detection: akuo → {d.Replacement}");
        Line(VoiceAnnouncer.EnglishVoice != null, "English voice available");
        Line(VoiceAnnouncer.HebrewVoice != null, "Hebrew voice available (optional – otherwise says \"Hebrew\" in English)");
        var s = AppSettings.Load(AppSettings.DefaultPath);
        sb.AppendLine();
        sb.AppendLine($"Settings: {AppSettings.DefaultPath}");
        sb.AppendLine($"Words never fixed: {new NeverFixList(s.NeverFixUndoCounts, s.UndosToBlock).BlockedWords().Count}");
        return sb.ToString();
    }
}
