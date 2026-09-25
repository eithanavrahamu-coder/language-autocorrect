using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayoutBuddy.Engine;

public sealed class AppSettings
{
    public bool ShowIndicator { get; set; } = true;
    public bool VoiceEnabled { get; set; } = true;
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
            : Languages.Select(LayoutBuddy.Engine.Languages.FromCode).OfType<LanguageInfo>().Select(l => l.Lang);
        return chosen.Prepend(Lang.English).Distinct().ToList();
    }

    public List<string> ExcludedApps { get; set; } = ["mstsc", "KeePass", "KeePassXC", "1Password", "Bitwarden"];

    /// <summary>Add a word to the never-fix list automatically after it is undone enough times. Off by default.</summary>
    public bool LearnFromUndos { get; set; }

    /// <summary>How many times a word must be undone before it is never auto-corrected again.</summary>
    public int UndosToBlock { get; set; } = 3;

    /// <summary>Word -> number of times its auto-correction was undone.</summary>
    public Dictionary<string, int> NeverFixUndoCounts { get; set; } = new();

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
                    s.UndosToBlock = Math.Clamp(s.UndosToBlock, 1, 10);
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
