using System.Text.Json;

namespace LanguageAutocorrect.Engine;

/// <summary>
/// The download page's <c>version.json</c>: the newest version and the file to download, written by the website build
/// next to the app it publishes.
/// </summary>
/// <param name="File">The download, relative to the page (e.g. "LanguageAutocorrect.exe").</param>
/// <param name="Size">The download's size in bytes, or 0 if unknown.</param>
public sealed record UpdateManifest(Version Version, string File, long Size)
{
    /// <summary>Reads version.json; null if it isn't a usable one.</summary>
    public static UpdateManifest? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var version = ParseVersion(root.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null);
            var file = root.TryGetProperty("file", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            long size = root.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var n) ? n : 0;
            // Only a plain file name next to the page: never another site or folder.
            if (version == null || string.IsNullOrWhiteSpace(file) || file.IndexOfAny(['/', '\\', ':']) >= 0 || file.Contains("..")) return null;
            return new UpdateManifest(version, file, Math.Max(0, size));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"3.10.0" or "3.10.0+abc123" (a build's product version) → 3.10.0; null if it isn't a version.</summary>
    public static Version? ParseVersion(string? text)
    {
        var plain = text?.Split('+')[0].Trim().TrimStart('v', 'V');
        if (!Version.TryParse(plain, out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }

    /// <summary>True if this is a later version than <paramref name="current"/> (compared as numbers: 3.10 is after 3.9).</summary>
    public bool IsNewerThan(string current) => ParseVersion(current) is not { } now || Version > now;
}
