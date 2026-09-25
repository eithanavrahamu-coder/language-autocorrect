using LayoutBuddy.Engine;
using Xunit.Abstractions;

namespace LayoutBuddy.Engine.Tests;

public class MultiLanguageTests(ITestOutputHelper output)
{
    private static WrongLayoutDetector D => Shared.Detector;
    private static readonly DateTime T0 = new(2026, 1, 1);

    [Theory]
    [InlineData(Lang.Russian, "привет")]
    [InlineData(Lang.Russian, "спасибо")]
    [InlineData(Lang.Ukrainian, "привіт")]
    [InlineData(Lang.Ukrainian, "дякую")]
    [InlineData(Lang.Arabic, "مرحبا")]
    [InlineData(Lang.Arabic, "شكرا")]
    [InlineData(Lang.Persian, "سلام")]
    [InlineData(Lang.Persian, "ممنون")]
    [InlineData(Lang.Greek, "γεια")]
    [InlineData(Lang.Greek, "καλημέρα")]
    [InlineData(Lang.French, "aller")]
    [InlineData(Lang.French, "été")]
    [InlineData(Lang.German, "schön")]
    [InlineData(Lang.German, "zeit")]
    public void FixesWordTypedOnEnglishKeyboard(Lang lang, string word)
    {
        var keys = KeyMap.ToUsKeys(word, lang);
        Assert.Equal(word, KeyMap.Render(keys, lang));
        var d = D.Evaluate(keys, Lang.English, lang, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(word, d.Replacement);
    }

    [Theory]
    [InlineData(Lang.Russian, "hello")]
    [InlineData(Lang.Arabic, "thanks")]
    [InlineData(Lang.Greek, "computer")]
    [InlineData(Lang.French, "what")]
    [InlineData(Lang.German, "yes")]
    public void FixesEnglishTypedOnOtherKeyboard(Lang lang, string word)
    {
        var d = D.Evaluate(word, lang, Lang.English, Sensitivity.Medium);
        Assert.True(d.ShouldFix, $"{d.Typed} -> {d.Replacement}: {d.Reason}");
        Assert.Equal(word, d.Replacement);
    }

    [Theory]
    [InlineData(Lang.French, "hello")]   // types the same on both keyboards
    [InlineData(Lang.German, "hello")]
    public void SameTextIsLeftAlone(Lang lang, string word) =>
        Assert.False(D.Evaluate(word, Lang.English, lang, Sensitivity.High).ShouldFix);

    [Fact]
    public void GreekAccentKeyCombines()
    {
        // ΄ is a dead key on ";": ";a" types ά.
        Assert.Equal("ά", KeyMap.Render(";a", Lang.Greek));
        Assert.Equal("καλημ;ερα".Length - 1, KeyMap.Render(KeyMap.ToUsKeys("καλημέρα", Lang.Greek), Lang.Greek).Length);
    }

    [Fact]
    public void ArabicLamAlefIsTwoLetters() => Assert.Equal("لا", KeyMap.Render("b", Lang.Arabic));

    [Fact]
    public void PicksTheRightLanguageAmongSeveral()
    {
        var s = new TypingSession(D, new NeverFixList())
        {
            Languages = [Lang.English, Lang.Hebrew, Lang.Russian, Lang.Arabic],
        };
        var ru = Assert.IsType<FixWord>(Type(s, "ghbdtn ", Lang.English));
        Assert.Equal("привет", ru.Text);
        Assert.Equal(Lang.Russian, ru.Layout);

        s.ResetAll();
        var he = Assert.IsType<FixWord>(Type(s, "akuo ", Lang.English));
        Assert.Equal("שלום", he.Text);
    }

    [Fact]
    public void CapitalizedGermanNounKeepsItsCapital()
    {
        // "Zeit" typed on an English keyboard shows "Yeit".
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.German] };
        s.OnKey(KeyInput.Word('y', shifted: true), Lang.English, true, Sensitivity.Medium, T0);
        var a = Assert.IsType<FixWord>(Type(s, "eit ", Lang.English));
        Assert.Equal("Zeit", a.Text);
        Assert.Equal("Yeit", a.Correction.Typed);
    }

    [Fact]
    public void CapitalizedWordIsNotTurnedIntoHebrew()
    {
        var s = new TypingSession(D, new NeverFixList());
        s.OnKey(KeyInput.Word('a', shifted: true), Lang.English, true, Sensitivity.Medium, T0);
        Assert.IsType<PassThrough>(Type(s, "kuo ", Lang.English));
    }

    [Fact]
    public void RussianSentenceContext()
    {
        // "он живёт far" is unusual; a real Russian phrase then an ambiguous word follows the sentence.
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Russian] };
        Type(s, KeyMap.ToUsKeys("я очень", Lang.Russian) + " ", Lang.Russian);
        var a = Assert.IsType<FixWord>(Type(s, KeyMap.ToUsKeys("рад", Lang.Russian) + " ", Lang.English));
        Assert.Equal("рад", a.Text);
    }

    private static TypingAction Type(TypingSession s, string text, Lang layout)
    {
        TypingAction last = PassThrough.Instance;
        foreach (var c in text)
            last = s.OnKey(c == ' ' ? KeyInput.Space : KeyInput.Word(c), layout, true, Sensitivity.Medium, T0);
        return last;
    }

    public static TheoryData<Lang> NewLanguages =>
        [Lang.Russian, Lang.Arabic, Lang.Ukrainian, Lang.Persian, Lang.Greek, Lang.French, Lang.German];

    [Theory]
    [MemberData(nameof(NewLanguages))]
    public void Accuracy(Lang lang)
    {
        var en = Words(Lang.English);
        var other = Words(lang)
            .Select(w => (Word: w, Keys: KeyMap.ToUsKeys(w, lang)))
            .Where(x => x.Keys.All(KeyMap.IsWordKey) && KeyMap.Render(x.Keys, lang) == x.Word)
            .ToList();

        // Only words that look different on the two keyboards can be fixed at all.
        var enDiff = en.Where(w => KeyMap.Render(w, lang) != w).ToList();
        var otherDiff = other.Where(x => KeyMap.Render(x.Keys, Lang.English) != x.Word).ToList();

        int enFalse = en.Count(w => D.Evaluate(w, Lang.English, lang, Sensitivity.Medium).ShouldFix);
        int otherFalse = other.Count(x => D.Evaluate(x.Keys, lang, Lang.English, Sensitivity.Medium).ShouldFix);
        int otherCaught = otherDiff.Count(x => D.Evaluate(x.Keys, Lang.English, lang, Sensitivity.Medium).ShouldFix);
        int enCaught = enDiff.Count(w => D.Evaluate(w, lang, Lang.English, Sensitivity.Medium).ShouldFix);

        double falseRate = (enFalse + otherFalse) / (double)(en.Count + other.Count);
        double catchRate = (otherCaught + enCaught) / (double)(otherDiff.Count + enDiff.Count);
        output.WriteLine($"en<->{lang}: wrongly changed {falseRate:P2} (en {enFalse}/{en.Count}, {lang} {otherFalse}/{other.Count}); " +
                         $"caught {catchRate:P1} ({lang} {otherCaught}/{otherDiff.Count}, en {enCaught}/{enDiff.Count})");

        Assert.True(falseRate < 0.02, $"false rate {falseRate:P2}");
        Assert.True(catchRate > 0.80, $"catch rate {catchRate:P1}");
    }

    private static List<string> Words(Lang lang)
    {
        var name = $"LayoutBuddy.Data.{Languages.Get(lang).Code}.txt";
        using var s = typeof(LanguageModel).Assembly.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        var list = new List<string>();
        string? line;
        while ((line = r.ReadLine()) != null) list.Add(line[..line.IndexOf(' ')]);
        return list.Skip(50).Take(20000).Where((_, i) => i % 10 == 0).ToList();
    }
}
