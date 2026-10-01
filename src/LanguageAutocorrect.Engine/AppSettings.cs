using System.Text.Json;
using System.Text.Json.Serialization;

namespace LanguageAutocorrect.Engine;

public sealed class AppSettings
{
    public bool ShowIndicator { get; set; } = true;

    /// <summary>Show each fix at the cursor: the word glows, and a small card shows what was typed.</summary>
    public bool ShowFixes { get; set; } = true;

    /// <summary>How many fix cards have shown the "Backspace to undo" tip.</summary>
    public int UndoTipsShown { get; set; }

    /// <summary>The user has undone a fix, so the tip isn't needed anymore.</summary>
    public bool UndoTipDone { get; set; }

    /// <summary>Whether the next fix card shows the undo tip: the first few, then now and then, until the user has undone a fix.</summary>
    public bool TakeUndoTip()
    {
        if (UndoTipDone || (UndoTipsShown >= 3 && TotalFixes % 20 != 0)) return false;
        UndoTipsShown++;
        return true;
    }

    /// <summary>Say the language out loud when the keyboard changes. Off by default (offered in setup).</summary>
    public bool VoiceEnabled { get; set; }

    /// <summary>Version of the settings file, for one-time changes to existing users' settings.</summary>
    public int SettingsVersion { get; set; }

    private const int CurrentSettingsVersion = 2;
    public bool AutoCorrectEnabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Sensitivity Sensitivity { get; set; } = Sensitivity.Medium;

    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Language codes the user types in (see <see cref="Languages"/>); null = every supported language
    /// with a keyboard installed in Windows. English is always included.
    /// </summary>
    public List<string>? Languages { get; set; }

    /// <summary>The languages to check, given the ones with keyboards installed.</summary>
    public List<Lang> EnabledLanguages(IEnumerable<Lang> installed)
    {
        var chosen = Languages == null
            ? installed
            : Languages.Select(LanguageAutocorrect.Engine.Languages.FromCode).OfType<LanguageInfo>().Select(l => l.Lang);
        return chosen.Prepend(Lang.English).Distinct().ToList();
    }

    public List<string> ExcludedApps { get; set; } = ["mstsc", "KeePass", "KeePassXC", "1Password", "Bitwarden"];

    /// <summary>Add a word to the never-fix list automatically after it is undone enough times. Off by default.</summary>
    public bool LearnFromUndos { get; set; }

    /// <summary>How many times a word must be undone before it is never auto-corrected again.</summary>
    public int UndosToBlock { get; set; } = 3;

    /// <summary>Word -> number of times its auto-correction was undone.</summary>
    public Dictionary<string, int> NeverFixUndoCounts { get; set; } = new();

    /// <summary>
    /// Once a word is undone <see cref="UndosToAsk"/> times, a card by the text asks whether to stop fixing it, instead
    /// of the word going on the never-fix list by itself (<see cref="LearnFromUndos"/>). Off by default (offered in setup).
    /// </summary>
    public bool AskAfterUndos { get; set; }

    /// <summary>How many times a word must be undone before the app asks whether to stop fixing it.</summary>
    public int UndosToAsk { get; set; } = 5;

    /// <summary>Word -> undos counted toward that question since it was last answered.</summary>
    public Dictionary<string, int> UndoAskCounts { get; set; } = new();

    /// <summary>Look for a newer version once a day (only the download page's version number is read).</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>When the app last looked for a newer version (UTC).</summary>
    public DateTime? UpdateCheckedAt { get; set; }

    /// <summary>The newest version the tray has already told the user about, so each one is announced once.</summary>
    public string? UpdateAnnounced { get; set; }

    /// <summary>
    /// The version an update replaced, until the window has shown what's new since then (see <see cref="WhatsNew"/>).
    /// </summary>
    public string? WhatsNewFrom { get; set; }

    /// <summary>
    /// The version that ran last (the Mac app, which is updated by dragging a new copy over it, notices an update by
    /// it). Null until then.
    /// </summary>
    public string? LastRunVersion { get; set; }

    public long TotalFixes { get; set; }
    public string? TodayDate { get; set; }
    public int TodayFixes { get; set; }

    /// <summary>Counts one auto-correction (resets the daily count on a new day).</summary>
    public void CountFix(DateTime now, int words = 1)
    {
        var today = now.ToString("yyyy-MM-dd");
        if (TodayDate != today)
        {
            TodayDate = today;
            TodayFixes = 0;
        }
        TodayFixes += words;
        TotalFixes += words;
    }

    public int FixesToday(DateTime now) => TodayDate == now.ToString("yyyy-MM-dd") ? TodayFixes : 0;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public const string FolderName = "LanguageAutocorrect";

    /// <summary>Folders used under the app's earlier names, newest first.</summary>
    public static readonly string[] LegacyFolderNames = ["TypeLanguageCorrector4000", "LayoutBuddy"];

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName, "settings.json");

    /// <summary>Copies settings from the app's most recent earlier name the first time the new name runs.</summary>
    public static void MigrateLegacy()
    {
        try
        {
            if (File.Exists(DefaultPath)) return;
            foreach (var folder in LegacyFolderNames)
            {
                var legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, "settings.json");
                if (!File.Exists(legacy)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
                File.Copy(legacy, DefaultPath);
                return;
            }
        }
        catch (Exception)
        {
            // Not worth failing over; the user just starts with defaults.
        }
    }

    /// <summary>Loads settings; falls back to defaults if the file is missing or unreadable.</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
                if (s != null)
                {
                    s.ExcludedApps ??= [];
                    s.NeverFixUndoCounts ??= new();
                    s.UndoAskCounts ??= new();
                    s.UndosToBlock = Math.Clamp(s.UndosToBlock, 1, 10);
                    s.UndosToAsk = Math.Clamp(s.UndosToAsk, 1, 10);
                    if (s.SettingsVersion < 2) s.VoiceEnabled = false; // voice became opt-in
                    s.SettingsVersion = CurrentSettingsVersion;
                    return s;
                }
            }
        }
        catch (Exception)
        {
            // Corrupt or old-format file: start fresh.
        }
        return new AppSettings();
    }

    public void Save(string path)
    {
        SettingsVersion = CurrentSettingsVersion;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
        File.Move(tmp, path, overwrite: true);
    }

    public bool IsExcluded(string? processName) =>
        !string.IsNullOrEmpty(processName) &&
        ExcludedApps.Any(a => string.Equals(a.Trim().Replace(".exe", "", StringComparison.OrdinalIgnoreCase),
            processName, StringComparison.OrdinalIgnoreCase));
}
