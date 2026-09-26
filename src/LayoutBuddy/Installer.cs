using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LayoutBuddy.Engine;
using Microsoft.Win32;

namespace LayoutBuddy;

/// <summary>
/// Per-user install (no admin needed): copies the exe to %LocalAppData%\Programs\Language Autocorrect,
/// adds shortcuts, and registers an entry under Settings → Apps with an Uninstall button.
/// </summary>
internal static class Installer
{
    private const string AppName = AppInfo.Name;
    private const string AppId = AppInfo.Id;
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
    private const string UninstallKey = UninstallRoot + AppId;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ExitEventName = @"Local\" + AppId + ".Exit";
    public const string ShowEventName = @"Local\" + AppId + ".Show";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static string InstalledExe => Path.Combine(InstallDir, AppId + ".exe");

    /// <summary>WebView2 data for the setup window, kept out of the install folder so it can be deleted.</summary>
    public static string SetupUiDataDir => Path.Combine(Path.GetTempPath(), AppId + "Setup");

    private static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

    private static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");

    private static string RoamingDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppId);

    private static string LocalDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId);

    public static string CurrentVersion => Application.ProductVersion.Split('+')[0];

    public static string? InstalledVersion
    {
        get
        {
            try
            {
                foreach (var id in AppInfo.Legacy.Select(l => l.Id).Prepend(AppId))
                {
                    using var k = Registry.CurrentUser.OpenSubKey(UninstallRoot + id);
                    if (k?.GetValue("DisplayVersion") is string v) return v;
                }
                return null;
            }
            catch { return null; }
        }
    }

    public static bool IsRunningInstalledCopy =>
        Environment.ProcessPath is { } p &&
        string.Equals(Path.GetFullPath(p), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalled => File.Exists(InstalledExe) || LegacyInstalled;

    // ---- The app's earlier names (LayoutBuddy, Type Language Corrector 4000) ----

    private static string LegacyInstallDir((string Id, string Name) legacy) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", legacy.Name);

    private static bool LegacyInstalled => AppInfo.Legacy.Any(l =>
        File.Exists(Path.Combine(LegacyInstallDir(l), l.Id + ".exe")));

    /// <summary>Removes installs made under earlier names: shortcuts, Settings → Apps entries, startup entries and files.</summary>
    private static void RemoveLegacyInstalls()
    {
        foreach (var legacy in AppInfo.Legacy)
        {
            var (id, name) = legacy;
            TryDo(() => Registry.CurrentUser.DeleteSubKeyTree(UninstallRoot + id, throwOnMissingSubKey: false));
            TryDo(() =>
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (run?.GetValue(id) != null) run.DeleteValue(id);
            });
            foreach (var folder in new[] { Environment.SpecialFolder.Programs, Environment.SpecialFolder.DesktopDirectory })
            {
                var lnk = Path.Combine(Environment.GetFolderPath(folder), name + ".lnk");
                TryDo(() => { if (File.Exists(lnk)) File.Delete(lnk); });
            }
            var dir = LegacyInstallDir(legacy);
            TryDo(() => { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); });
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), id);
            TryDo(() => { if (Directory.Exists(local)) Directory.Delete(local, recursive: true); });
            // Settings were already copied to the new folder by AppSettings.MigrateLegacy().
            var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), id);
            TryDo(() =>
            {
                if (Directory.Exists(roaming) && File.Exists(AppSettings.DefaultPath))
                    Directory.Delete(roaming, recursive: true);
            });
        }
    }

    /// <summary>Installs or updates. <paramref name="startWithWindows"/> null keeps the current setting.</summary>
    /// <param name="languages">Language codes chosen in setup; null keeps the current choice.</param>
    /// <param name="voice">Say the language out loud; null keeps the current setting.</param>
    /// <param name="launchArgs">How the installed copy is started at the end.</param>
    public static void Install(bool? startWithWindows, bool desktopShortcut, List<string>? languages, bool? voice,
        Action<double, string> progress, string launchArgs = "--background")
    {
        progress(0.1, "Closing " + AppName);
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
        RemoveLegacyInstalls();
        AppSettings.MigrateLegacy();
        if (startWithWindows is bool || languages != null || voice is bool)
        {
            var settings = AppSettings.Load(AppSettings.DefaultPath);
            if (startWithWindows is bool startup) settings.StartWithWindows = startup;
            if (languages != null) settings.Languages = languages;
            if (voice is bool speak) settings.VoiceEnabled = speak;
            settings.Save(AppSettings.DefaultPath);
        }
        // Point "start with Windows" at the installed copy (the app re-applies this on start too).
        if (AppSettings.Load(AppSettings.DefaultPath).StartWithWindows) SetRunKey(InstalledExe);
        Pause();

        progress(0.9, "Starting " + AppName);
        Process.Start(new ProcessStartInfo(InstalledExe, launchArgs) { UseShellExecute = true, WorkingDirectory = InstallDir });
        Pause();
        progress(1, "Done");
    }

    public static void Uninstall(bool keepSettings, Action<double, string> progress)
    {
        progress(0.15, "Closing " + AppName);
        StopRunningInstances();
        Pause();

        progress(0.4, "Removing shortcuts");
        TryDo(() =>
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(AppId) != null) run.DeleteValue(AppId);
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
        var extractDir = Path.Combine(Path.GetTempPath(), ".net", AppId);
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

    /// <summary>Opens the installed app's window: asks the running copy to (waiting a moment if it's still starting), or starts it.</summary>
    public static void OpenInstalled()
    {
        int self = Environment.ProcessId;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            if (SignalShow()) return;
            var others = Process.GetProcessesByName(AppId);
            bool starting = others.Any(p => p.Id != self);
            foreach (var p in others) p.Dispose();
            if (!starting) break;
            Thread.Sleep(200);
        }
        try { Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir }); }
        catch (Exception ex) { Log.Write("Opening the app failed: " + ex.Message); }
    }

    /// <summary>Asks running copies to exit (and closes them if they don't).</summary>
    private static void StopRunningInstances()
    {
        foreach (var name in AppInfo.Legacy.Select(l => @"Local\" + l.Id + ".Exit").Prepend(ExitEventName))
            if (EventWaitHandle.TryOpenExisting(name, out var ev))
                using (ev) ev.Set();

        int self = Environment.ProcessId;
        var running = AppInfo.Legacy.Select(l => l.Id).Prepend(AppId).SelectMany(Process.GetProcessesByName);
        foreach (var p in running.Where(p => p.Id != self))
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
            run.SetValue(AppId, $"\"{exe}\" --background");
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
            ? "Update the installed " + AppName + " to this version?\n\n(Your settings are kept.)"
            : "Install " + AppName + " on this computer?\n\nIt will be added to the Start menu and to Settings → Apps.\n\n" +
              "Choose No to just run it without installing.";
        if (MessageBox.Show(question, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return SetupResult.Portable;
        try
        {
            Install(IsInstalled ? null : true, false, null, IsInstalled ? null : false, (_, _) => { });
            return SetupResult.Installed;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Installing failed:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return SetupResult.Cancelled;
        }
    }

    /// <summary>
    /// Installs this copy over the installed one without asking (the app's own updater started it with --update), then
    /// starts the new version with its window open.
    /// </summary>
    public static void UpdateSilently()
    {
        try
        {
            Install(null, false, null, null, (_, step) => Log.Write("Updating: " + step), launchArgs: "--updated");
        }
        catch (Exception ex)
        {
            Log.Write("Update failed: " + ex);
            MessageBox.Show(AppName + " couldn't be updated:\n" + ex.Message + "\n\nYou can download the new version from " +
                            AppInfo.Website, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // Leave the user with a running app.
            if (File.Exists(InstalledExe))
                TryDo(() => Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir }));
        }
    }

    public static SetupResult UninstallWithMessageBoxes(bool quiet)
    {
        if (!quiet && MessageBox.Show("Remove " + AppName + " and its settings from this computer?", AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return SetupResult.Cancelled;
        Uninstall(false, (_, _) => { });
        return SetupResult.Uninstalled;
    }
}
