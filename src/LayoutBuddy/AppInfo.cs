namespace LayoutBuddy;

internal static class AppInfo
{
    /// <summary>The name people see.</summary>
    public const string Name = "Language Autocorrect";

    /// <summary>The download page. Its version.json names the newest version, for update checks.</summary>
    public const string Website = "https://eithanavrahamu-coder.github.io/language-auto/";

    /// <summary>Used for the exe, folders, registry keys and system object names.</summary>
    public const string Id = LayoutBuddy.Engine.AppSettings.FolderName;

    /// <summary>The app's earlier names (id, display name), for upgrading old installs.</summary>
    public static readonly (string Id, string Name)[] Legacy =
    [
        ("TypeLanguageCorrector4000", "Type Language Corrector 4000"),
        ("LayoutBuddy", "LayoutBuddy"),
    ];
}
