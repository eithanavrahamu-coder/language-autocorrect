using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LayoutBuddy;

/// <summary>
/// Per-user install (no admin needed): copies the exe to %LocalAppData%\Programs\LayoutBuddy,
/// adds a Start menu shortcut, and registers an entry under Settings → Apps with an Uninstall button.
/// </summary>
internal static class Installer
{
    private const string AppName = "LayoutBuddy";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LayoutBuddy";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ExitEventName = @"Local\LayoutBuddy.Exit";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppName);

    public static string InstalledExe => Path.Combine(InstallDir, AppName + ".exe");

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static bool IsRunningInstalledCopy =>
        Environment.ProcessPath is { } p &&
        string.Equals(Path.GetFullPath(p), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalled => File.Exists(InstalledExe);

    /// <summary>
    /// Called when the exe runs from somewhere other than the install folder.
    /// Returns true if the installed copy was started and this process should exit.
    /// </summary>
    public static bool OfferInstall()
    {
        string question = IsInstalled
            ? "LayoutBuddy is already installed. Replace it with this version?\n\n(Your settings are kept.)"
            : "Install LayoutBuddy on this computer?\n\n" +
              "It will be added to the Start menu and to Settings → Apps, where you can uninstall it.\n\n" +
              "Choose No to just run it without installing.";
        if (MessageBox.Show(question, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return false;

        try
        {
            StopRunningInstances();
            Directory.CreateDirectory(InstallDir);
            File.Copy(Environment.ProcessPath!, InstalledExe, overwrite: true);
            CreateShortcut();
            Register();
            Process.Start(new ProcessStartInfo(InstalledExe) { UseShellExecute = true, WorkingDirectory = InstallDir });
            MessageBox.Show("LayoutBuddy is installed and running (look for its icon near the clock).\n\n" +
                            "You can delete the file you downloaded.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            Log.Write("Install failed: " + ex);
            MessageBox.Show("Installing failed:\n" + ex.Message + "\n\nIf LayoutBuddy is running, exit it from the tray icon and try again.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>Runs when Windows calls the uninstall command (Settings → Apps → Uninstall).</summary>
    public static void Uninstall(bool quiet)
    {
        if (!quiet && MessageBox.Show("Remove LayoutBuddy and its settings from this computer?", AppName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        StopRunningInstances();

        TryDo(() =>
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(AppName) != null) run.DeleteValue(AppName);
        });
        TryDo(() => Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false));
        TryDo(() => File.Delete(ShortcutPath));
        TryDo(() => { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, recursive: true); });

        // This exe can't delete itself while running: let a hidden cmd do it after we exit.
        var extractDir = Path.Combine(Path.GetTempPath(), ".net", AppName);
        TryDo(() => Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{InstallDir}\" & rmdir /s /q \"{extractDir}\" & rmdir /s /q \"{DataDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        }));

        if (!quiet)
            MessageBox.Show("LayoutBuddy was removed.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", Application.ProductVersion.Split('+')[0]);
        key.SetValue("Publisher", AppName);
        key.SetValue("DisplayIcon", InstalledExe);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --quiet");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
    }

    private static void CreateShortcut()
    {
        TryDo(() =>
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(ShortcutPath);
            link.TargetPath = InstalledExe;
            link.WorkingDirectory = InstallDir;
            link.Description = "Hebrew/English keyboard helper";
            link.Save();
        });
    }

    private static void TryDo(Action a)
    {
        try { a(); }
        catch (Exception ex) { Log.Write("Installer step failed: " + ex.Message); }
    }
}
