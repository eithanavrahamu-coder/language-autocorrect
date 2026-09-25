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

    public List<string> ExcludedApps { get; set; } = ["mstsc", "KeePass", "KeePassXC", "1Password", "Bitwarden"];

    /// <summary>How many times a word must be undone before it is never auto-corrected again.</summary>
    public int UndosToBlock { get; set; } = 3;

    /// <summary>Word -> number of times its auto-correction was undone.</summary>
    public Dictionary<string, int> NeverFixUndoCounts { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LayoutBuddy", "settings.json");

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
