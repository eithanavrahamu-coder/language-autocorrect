using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class ThaiTests
{
    private static WrongLayoutDetector D => Shared.Detector;
    private static readonly DateTime T0 = new(2026, 1, 1);

    [Theory]
    [InlineData("สวัสดี")]
    [InlineData("ขอบคุณ")]
    [InlineData("สวัสดีครับ")]        // Thai has no spaces: a phrase is checked word by word
    [InlineData("ขอบคุณมาก")]
    [InlineData("ไม่เป็นไรครับ")]
    [InlineData("เธอ")]               // ธ is Shift+T
    public void FixesThaiTypedOnEnglishKeyboard(string text)
    {
        var keys = KeyMap.ToUsKeys(text, Lang.Thai);
        Assert.Equal(text, KeyMap.Render(keys, Lang.Thai));
        var d = D.Evaluate(keys, Lang.English, Lang.Thai, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(text, d.Replacement);
    }

    [Theory]
    [InlineData("สวัสดีครับ")]
    [InlineData("ขอบคุณมาก")]
    [InlineData("ฉันไม่รู้ว่าเธอพูดอะไร")]
    public void LeavesThaiTypedOnThaiKeyboard(string text) =>
        Assert.False(D.Evaluate(KeyMap.ToUsKeys(text, Lang.Thai), Lang.Thai, Lang.English, Sensitivity.High).ShouldFix);

    [Theory]
    [InlineData("hello")]
    [InlineData("thanks")]
    public void FixesEnglishTypedOnThaiKeyboard(string word)
    {
        var d = D.Evaluate(word, Lang.Thai, Lang.English, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(word, d.Replacement);
    }

    [Fact]
    public void SessionKeepsThaiLettersTypedWithShift()
    {
        // เธอ = G, Shift+T, V.
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Thai] };
        Assert.IsType<PassThrough>(Type(s, [('g', false), ('t', true), ('v', false)], Lang.Thai));
        s.ResetAll();
        var a = Assert.IsType<FixWord>(Type(s, [('g', false), ('t', true), ('v', false)], Lang.English));
        Assert.Equal("เธอ", a.Text);
        Assert.Equal("gTv", a.Correction.Typed);
        Assert.Equal(Lang.Thai, a.Layout);
    }

    [Fact]
    public void RecognizesThaiKeyboard() => Assert.Equal(Lang.Thai, Languages.FromWindowsLangId(0x041E)?.Lang);

    private static TypingAction Type(TypingSession s, (char Key, bool Shift)[] keys, Lang layout)
    {
        foreach (var (k, shift) in keys) s.OnKey(KeyInput.Word(k, shift), layout, true, Sensitivity.Medium, T0);
        return s.OnKey(KeyInput.Space, layout, true, Sensitivity.Medium, T0);
    }
}
