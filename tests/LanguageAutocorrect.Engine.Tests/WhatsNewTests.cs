using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class WhatsNewTests
{
    [Fact]
    public void RemembersTheVersionAnUpdateReplaced()
    {
        Assert.Equal("3.14.1", WhatsNew.From(null, "3.14.1", "3.15.0"));
        Assert.Equal("3.9.0", WhatsNew.From(null, "3.9", "3.10.0")); // compared as numbers
    }

    [Fact]
    public void KeepsTheOldestNotYetShown()
    {
        // Updated twice before the window was opened: show everything since the first one.
        Assert.Equal("3.12.0", WhatsNew.From("3.12.0", "3.14.1", "3.15.0"));
        Assert.Equal("3.12.0", WhatsNew.From("3.14.1", "3.12.0", "3.15.0"));
    }

    [Theory]
    [InlineData(null, null)]       // a fresh install
    [InlineData(null, "3.15.0")]   // the same version again
    [InlineData(null, "3.16.0")]   // an older version over a newer one
    [InlineData("3.16.0", null)]
    [InlineData("rubbish", "")]
    public void NothingNew(string? pending, string? previous) =>
        Assert.Null(WhatsNew.From(pending, previous, "3.15.0"));

    [Fact]
    public void DueOnlyWhenOlderThanThisVersion()
    {
        Assert.True(WhatsNew.Due("3.14.1", "3.15.0+abc123"));
        Assert.False(WhatsNew.Due("3.15.0", "3.15.0"));
        Assert.False(WhatsNew.Due(null, "3.15.0"));
    }

    [Fact]
    public void PageIsOnTheWebsite()
    {
        var url = WhatsNew.PageUrl("https://language-autocorrect.world/", "3.14.1", "3.15.0+abc123");
        Assert.Equal("https://language-autocorrect.world/release-notes/?from=3.14.1&to=3.15.0&in=app", url.AbsoluteUri);
    }
}
