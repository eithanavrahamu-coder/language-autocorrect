namespace LanguageAutocorrect.Engine;

/// <summary>
/// "What's new" after an update. The pages are on the website (release-notes/, one per version), so the app keeps
/// nothing but the version it was updated from, until they've been shown.
/// </summary>
public static class WhatsNew
{
    /// <summary>
    /// The version to show what's new since, after <paramref name="current"/> was installed over
    /// <paramref name="previous"/>. Keeps the oldest one not yet shown, so updates in a row show every version in
    /// between. Null when nothing is new: a fresh install, the same version again, or an older one.
    /// </summary>
    /// <param name="pending">The one remembered from an earlier update, if its pages haven't been shown yet.</param>
    public static string? From(string? pending, string? previous, string current)
    {
        if (UpdateManifest.ParseVersion(current) is not { } now) return null;
        return new[] { pending, previous }
            .Select(UpdateManifest.ParseVersion)
            .OfType<Version>()
            .Where(v => v < now)
            .Order()
            .FirstOrDefault()?.ToString();
    }

    /// <summary>True if there are pages to show since <paramref name="from"/>.</summary>
    public static bool Due(string? from, string current) =>
        UpdateManifest.ParseVersion(from) is { } f && UpdateManifest.ParseVersion(current) is { } now && f < now;

    /// <summary>The release notes after <paramref name="from"/> up to <paramref name="current"/>, as pages for the app's window.</summary>
    public static Uri PageUrl(string website, string from, string current) =>
        new(new Uri(website), $"release-notes/?from={UpdateManifest.ParseVersion(from)}&to={UpdateManifest.ParseVersion(current)}&in=app");
}
