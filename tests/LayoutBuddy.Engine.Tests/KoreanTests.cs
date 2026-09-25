using LayoutBuddy.Engine;

namespace LayoutBuddy.Engine.Tests;

public class KoreanTests
{
    private static WrongLayoutDetector D => Shared.Detector;
    private static readonly DateTime T0 = new(2026, 1, 1);

    [Theory]
    [InlineData("dkssud", "안녕")]
    [InlineData("dlTdj", "있어")]         // ㅆ is Shift+T
    [InlineData("rhk", "과")]             // ㅗ+ㅏ = ㅘ
    [InlineData("dmlwk", "의자")]
    [InlineData("ekfrdl", "닭이")]        // ㄹ+ㄱ = ㄺ
    [InlineData("ekfrl", "달기")]         // ...and its ㄱ moves on when a vowel follows
    [InlineData("dkswdk", "앉아")]
    [InlineData("skfk", "나라")]          // ㄹ moves to the next syllable
    [InlineData("zzz", "ㅋㅋㅋ")]         // letters that don't join stay letters
    [InlineData("hello", "ㅗ디ㅣㅐ")]
    public void JoinsLettersIntoSyllables(string keys, string korean)
    {
        Assert.Equal(korean, KeyMap.Render(keys, Lang.Korean));
        Assert.Equal(keys, KeyMap.ToUsKeys(korean, Lang.Korean));
    }

    [Theory]
    [InlineData("안녕")]
    [InlineData("감사합니다")]
    [InlineData("있어요")]
    [InlineData("사랑해")]
    [InlineData("못")]
    public void FixesKoreanTypedInEnglishMode(string word)
    {
        var keys = KeyMap.ToUsKeys(word, Lang.Korean);
        var d = D.Evaluate(keys, Lang.English, Lang.Korean, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(word, d.Replacement);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("thanks")]
    [InlineData("computer")]
    public void FixesEnglishTypedInKoreanMode(string word)
    {
        var d = D.Evaluate(word, Lang.Korean, Lang.English, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(word, d.Replacement);
    }

    [Theory]
    [InlineData("zzz")]   // ㅋㅋㅋ, laughing
    [InlineData("gg")]    // ㅎㅎ
    [InlineData("bb")]    // ㅠㅠ, crying
    public void ChatLettersAreLeftAlone(string keys) =>
        Assert.False(D.Evaluate(keys, Lang.Korean, Lang.English, Sensitivity.High).ShouldFix);

    [Fact]
    public void SessionFixesKoreanWithShiftedLetters()
    {
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Korean] };
        foreach (var (c, shift) in new[] { ('d', false), ('l', false), ('t', true), ('d', false), ('j', false) })
            s.OnKey(KeyInput.Word(c, shift), Lang.English, true, Sensitivity.Medium, T0);
        var a = Assert.IsType<FixWord>(s.OnKey(KeyInput.Space, Lang.English, true, Sensitivity.Medium, T0));
        Assert.Equal("있어", a.Text);
        Assert.Equal(Lang.Korean, a.Layout);
        Assert.Equal("dlTdj".Length, a.Backspaces);
    }

    [Fact]
    public void FixingKoreanDeletesSyllablesNotKeys()
    {
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Korean] };
        foreach (var c in "hello") s.OnKey(KeyInput.Word(c), Lang.Korean, true, Sensitivity.Medium, T0);
        var a = Assert.IsType<FixWord>(s.OnKey(KeyInput.Space, Lang.Korean, true, Sensitivity.Medium, T0));
        Assert.Equal("hello", a.Text);
        Assert.Equal("ㅗ디ㅣㅐ".Length, a.Backspaces);
    }

    [Fact]
    public void BackspaceInKoreanStopsTrackingTheWord()
    {
        // Backspace deletes one letter of the syllable being typed, or a whole syllable already typed.
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Korean] };
        foreach (var c in "hellp") s.OnKey(KeyInput.Word(c), Lang.Korean, true, Sensitivity.Medium, T0);
        s.OnKey(KeyInput.Backspace, Lang.Korean, true, Sensitivity.Medium, T0);
        s.OnKey(KeyInput.Word('o'), Lang.Korean, true, Sensitivity.Medium, T0);
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Space, Lang.Korean, true, Sensitivity.Medium, T0));
    }

    [Fact]
    public void RecognizesKoreanKeyboard() => Assert.Equal(Lang.Korean, Languages.FromWindowsLangId(0x0412)?.Lang);
}
