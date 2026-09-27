using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class KeyMapTests
{
    [Theory]
    [InlineData("akuo", "שלום")]
    [InlineData("t,v", "אתה")]
    [InlineData("nv", "מה")]
    [InlineData("rcv", "רבה")]
    [InlineData("ahkuo", "שילום")]
    public void RendersHebrew(string keys, string hebrew)
    {
        Assert.Equal(hebrew, KeyMap.Render(keys, Lang.Hebrew));
        Assert.Equal(keys, KeyMap.ToUsKeys(hebrew));
    }

    [Fact]
    public void EnglishRenderIsIdentity() => Assert.Equal("hello", KeyMap.Render("hello", Lang.English));

    [Theory]
    [InlineData('a', true)]
    [InlineData(',', true)]
    [InlineData(';', true)]
    [InlineData('1', true)]   // French é, è, à... are on the number keys
    [InlineData('[', true)]   // Russian х
    [InlineData('+', false)]
    public void WordKeys(char c, bool expected) => Assert.Equal(expected, KeyMap.IsWordKey(c));
}
