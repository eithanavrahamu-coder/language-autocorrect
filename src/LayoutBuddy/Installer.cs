using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LayoutBuddy.Engine;
using Microsoft.Win32;

namespace LayoutBuddy;

/// <summary>
/// Per-user install (no admin needed): copies the exe to %LocalAppData%\Programs\LayoutBuddy,
/// adds shortcuts, and registers an entry under Settings → Apps with an Uninstall button.
/// </summary>
internal static class Installer
{
    private const string AppName = "LayoutBuddy";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LayoutBuddy";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ExitEventName = @"Local\LayoutBuddy.Exit";
    public const string ShowEventName = @"Local\LayoutBuddy.Show";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static string InstalledExe => Path.Combine(InstallDir, AppName + ".exe");

    /// <summary>WebView2 data for the setup window, kept out of the install folder so it can be deleted.</summary>
    public static string SetupUiDataDir => Path.Combine(Path.GetTempPath(), "LayoutBuddySetup");

    private static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

    private static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");

    private static string RoamingDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    private static string LocalDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string CurrentVersion => Application.ProductVersion.Split('+')[0];

    public static string? InstalledVersion
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(UninstallKey);
                return k?.GetValue("DisplayVersion") as string;
            }
            catch { return null; }
        }
    }

    public static bool IsRunningInstalledCopy =>
        Environment.ProcessPath is { } p &&
        string.Equals(Path.GetFullPath(p), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalled => File.Exists(InstalledExe);

    /// <summary>Installs or updates. <paramref name="startWithWindows"/> null keeps the current setting.</summary>
    public static void Install(bool? startWithWindows, bool desktopShortcut, Action<double, string> progress)
    {
        progress(0.1, "Closing LayoutBuddy");
        StopRunningInstances();
        Pause();

        progress(0.3, "Copying files");
        Directory.CreateDirectory(InstallDir);
        CopyWithRetry(Environment.ProcessPath!, InstalledExe);
        Pause();

        progress(0.5, "Creating shortcuts");
        CreateShortcut(StartMenuShortcut);
        if (desktopShortcut) CreateShortcut(DesktopShortcut);
        Pause();

        progress(0.7, "Adding to Settings → Apps");
        Register();
        if (startWithWindows is bool startup)
        {
            var settings = AppSettings.Load(AppSettings.DefaultPath);
            settings.StartWithWindows = startup;
            settings.Save(AppSettings.DefaultPath);
        }
        // Point "start with Windows" at the installed copy (the app re-applies this on start too).
        if (AppSettings.Load(AppSettings.DefaultPath).StartWithWindows) SetRunKey(InstalledExe);
        Pause();

        progress(0.9, "Starting LayoutBuddy");
        Process.Start(new ProcessStartInfo(InstalledExe, "--background") { UseShellExecute = true, WorkingDirectory = InstallDir });
        Pause();
        progress(1, "Done");
    }

    public static void Uninstall(bool keepSettings, Action<double, string> progress)
    {
        progress(0.15, "Closing LayoutBuddy");
        StopRunningInstances();
        Pause();

        progress(0.4, "Removing shortcuts");
        TryDo(() =>
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(AppName) != null) run.DeleteValue(AppName);
        });
        TryDo(() => File.Delete(StartMenuShortcut));
        TryDo(() => { if (File.Exists(DesktopShortcut)) File.Delete(DesktopShortcut); });
        Pause();

        progress(0.65, "Removing from Settings → Apps");
        TryDo(() => Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false));
        Pause();

        progress(0.85, keepSettings ? "Cleaning up" : "Removing settings");
        TryDo(() => { if (Directory.Exists(LocalDataDir)) Directory.Delete(LocalDataDir, recursive: true); });
        if (!keepSettings)
            TryDo(() => { if (Directory.Exists(RoamingDataDir)) Directory.Delete(RoamingDataDir, recursive: true); });
        Pause();
        progress(1, "Done");
    }

    /// <summary>
    /// The program can't delete itself while running: start a hidden cmd that waits for us to exit, then removes
    /// the install folder and temp files. Call right before exiting after an uninstall.
    /// </summary>
    public static void ScheduleSelfDelete()
    {
        var extractDir = Path.Combine(Path.GetTempPath(), ".net", AppName);
        TryDo(() => Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 4 > nul & rmdir /s /q \"{InstallDir}\" & rmdir /s /q \"{extractDir}\" & rmdir /s /q \"{SetupUiDataDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        }));
    }

    /// <summary>Asks a running copy to open its window.</summary>
    public static bool SignalShow()
    {
        if (!EventWaitHandle.TryOpenExisting(ShowEventName, out var ev)) return false;
        Native.AllowSetForegroundWindow(-1);
        using (ev) ev.Set();
        return true;
    }

    /// <summary>Asks running copies to exit (and closes them if they don't).</summary>
    private static void StopRunningInstances()
    {
        if (EventWaitHandle.TryOpenExisting(ExitEventName, out var ev))
            using (ev) ev.Set();

        int self = Environment.ProcessId;
        foreach (var p in Process.GetProcessesByName(AppName).Where(p => p.Id != self))
        {
            using (p)
            {
                try
                {
                    if (!p.WaitForExit(3000))
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Stopping old instance failed: " + ex.Message);
                }
            }
        }
    }

    private static void CopyWithRetry(string from, string to)
    {
        if (string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase)) return;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(from, to, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(500); // the old copy may take a moment to release the file
            }
        }
    }

    private static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", CurrentVersion);
        key.SetValue("Publisher", "Eithan Avraham");
        key.SetValue("DisplayIcon", InstalledExe + ",0");
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
    }

    private static void SetRunKey(string exe)
    {
        TryDo(() =>
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            run.SetValue(AppName, $"\"{exe}\" --background");
        });
    }

    private static void CreateShortcut(string path)
    {
        TryDo(() =>
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = InstalledExe;
            link.WorkingDirectory = InstallDir;
            link.IconLocation = InstalledExe + ",0";
            link.Description = "Hebrew/English keyboard helper";
            link.Save();
        });
    }

    private static void Pause() => Thread.Sleep(250); // lets the progress bar read naturally

    private static void TryDo(Action a)
    {
        try { a(); }
        catch (Exception ex) { Log.Write("Installer step failed: " + ex.Message); }
    }

    // ---- Fallbacks when the WebView2 runtime is missing ----

    public static SetupResult InstallWithMessageBoxes()
    {
        string question = IsInstalled
            ? "Update the installed LayoutBuddy to this version?\n\n(Your settings are kept.)"
            : "Install LayoutBuddy on this computer?\n\nIt will be added to the Start menu and to Settings → Apps.\n\n" +
              "Choose No to just run it without installing.";
        if (MessageBox.Show(question, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return SetupResult.Portable;
        try
        {
            Install(IsInstalled ? null : true, false, (_, _) => { });
            return SetupResult.Installed;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Installing failed:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return SetupResult.Cancelled;
        }
    }

    public static SetupResult UninstallWithMessageBoxes(bool quiet)
    {
        if (!quiet && MessageBox.Show("Remove LayoutBuddy and its settings from this computer?", AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return SetupResult.Cancelled;
        Uninstall(false, (_, _) => { });
        return SetupResult.Uninstalled;
    }
}
