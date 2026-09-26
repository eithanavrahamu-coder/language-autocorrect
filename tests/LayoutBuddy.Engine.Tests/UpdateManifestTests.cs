using LayoutBuddy.Engine;

namespace LayoutBuddy.Engine.Tests;

public class UpdateManifestTests
{
    [Fact]
    public void ReadsTheDownloadPagesVersionFile()
    {
        var m = UpdateManifest.Parse("""{ "version": "3.11.0", "file": "LanguageAutocorrect.exe", "size": 87258853 }""");
        Assert.NotNull(m);
        Assert.Equal(new Version(3, 11, 0), m.Version);
        Assert.Equal("LanguageAutocorrect.exe", m.File);
        Assert.Equal(87258853, m.Size);
    }

    [Fact]
    public void SizeIsOptional()
    {
        var m = UpdateManifest.Parse("""{ "version": "3.11.0", "file": "LanguageAutocorrect.exe", "size": null }""");
        Assert.Equal(0, m!.Size);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "file": "LanguageAutocorrect.exe" }""")]
    [InlineData("""{ "version": "soon", "file": "LanguageAutocorrect.exe" }""")]
    [InlineData("""{ "version": "3.11.0" }""")]
    [InlineData("""{ "version": "3.11.0", "file": "https://example.com/other.exe" }""")]
    [InlineData("""{ "version": "3.11.0", "file": "../other.exe" }""")]
    [InlineData("""{ "version": "3.11.0", "file": "folder\\other.exe" }""")]
    public void RejectsAnythingElse(string json) => Assert.Null(UpdateManifest.Parse(json));

    [Theory]
    [InlineData("3.11.0", "3.10.0", true)]
    [InlineData("3.10.0", "3.9.0", true)]      // numbers, not text: 10 is after 9
    [InlineData("4.0.0", "3.10.2", true)]
    [InlineData("3.10.1", "3.10.0+fc370c0", true)] // a build's product version carries the commit
    [InlineData("3.10.0", "3.10.0+fc370c0", false)]
    [InlineData("3.10.0", "3.10.0", false)]
    [InlineData("3.9.0", "3.10.0", false)]
    public void ComparesVersions(string latest, string current, bool newer)
    {
        var m = UpdateManifest.Parse($$"""{ "version": "{{latest}}", "file": "LanguageAutocorrect.exe" }""");
        Assert.Equal(newer, m!.IsNewerThan(current));
    }
}
