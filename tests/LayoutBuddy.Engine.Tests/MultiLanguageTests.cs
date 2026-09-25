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
    [InlineData(Lang.Bulgarian, "здравей")]
    [InlineData(Lang.Bulgarian, "благодаря")]
    [InlineData(Lang.Serbian, "здраво")]
    [InlineData(Lang.Serbian, "хвала")]
    [InlineData(Lang.Macedonian, "здраво")]
    [InlineData(Lang.Macedonian, "благодарам")]
    [InlineData(Lang.Kazakh, "рахмет")]
    [InlineData(Lang.Kazakh, "сәлем")]
    [InlineData(Lang.Georgian, "გამარჯობა")]
    [InlineData(Lang.Georgian, "გთხოვთ")]      // თ is Shift+T
    [InlineData(Lang.Armenian, "բարև")]
    [InlineData(Lang.Armenian, "շնորհակալություն")]
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
    [InlineData(Lang.Bulgarian, "hello")]
    [InlineData(Lang.Serbian, "thanks")]
    [InlineData(Lang.Macedonian, "computer")]
    [InlineData(Lang.Kazakh, "world")]
    [InlineData(Lang.Georgian, "hello")]
    [InlineData(Lang.Armenian, "thanks")]
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

    [Theory]
    [InlineData("благодаря", Lang.Bulgarian)]
    [InlineData("хвала", Lang.Serbian)]
    [InlineData("благодарам", Lang.Macedonian)]
    [InlineData("рахмет", Lang.Kazakh)]
    [InlineData("спасибо", Lang.Russian)]
    [InlineData("дякую", Lang.Ukrainian)]
    public void PicksTheRightCyrillicLanguage(string word, Lang lang)
    {
        var s = new TypingSession(D, new NeverFixList())
        {
            Languages = [Lang.English, Lang.Russian, Lang.Ukrainian, Lang.Bulgarian, Lang.Serbian, Lang.Macedonian, Lang.Kazakh],
        };
        var a = Assert.IsType<FixWord>(Type(s, KeyMap.ToUsKeys(word, lang) + " ", Lang.English));
        Assert.Equal(word, a.Text);
        Assert.Equal(lang, a.Layout);
    }

    [Theory]
    [InlineData(0x0402, Lang.Bulgarian)]
    [InlineData(0x042F, Lang.Macedonian)]
    [InlineData(0x043F, Lang.Kazakh)]
    [InlineData(0x0C1A, Lang.Serbian)]   // Serbian (Cyrillic), Serbia and Montenegro (former)
    [InlineData(0x281A, Lang.Serbian)]   // Serbian (Cyrillic), Serbia
    [InlineData(0x1C1A, Lang.Serbian)]   // Serbian (Cyrillic), Bosnia and Herzegovina
    [InlineData(0x0437, Lang.Georgian)]
    [InlineData(0x042B, Lang.Armenian)]
    [InlineData(0x0419, Lang.Russian)]
    public void RecognizesWindowsLanguage(int langId, Lang lang) =>
        Assert.Equal(lang, Languages.FromWindowsLangId(langId)?.Lang);

    [Theory]
    [InlineData(0x041A)]   // Croatian
    [InlineData(0x241A)]   // Serbian (Latin)
    [InlineData(0x081A)]   // Serbian (Latin), Serbia and Montenegro (former)
    [InlineData(0x141A)]   // Bosnian (Latin)
    public void LatinKeyboardsSharingSerbiansIdAreNotSerbianCyrillic(int langId) =>
        Assert.Null(Languages.FromWindowsLangId(langId));

    [Theory]
    [InlineData("0900")]
    [InlineData("2345")]
    [InlineData("1990")]
    public void NumbersAreLeftAlone(string keys) =>
        Assert.False(D.Evaluate(keys, Lang.English, Lang.Kazakh, Sensitivity.High).ShouldFix);   // Kazakh types letters on the number row

    [Fact]
    public void LettersOnTheNumberRowAreNotReadAsDigits() =>
        Assert.False(D.Evaluate(KeyMap.ToUsKeys("հավերժ", Lang.Armenian), Lang.Armenian, Lang.English, Sensitivity.High).ShouldFix);

    [Fact]
    public void GeorgianLettersTypedWithShift()
    {
        // შენ = Shift+S, e, n: on the Georgian keyboard it's a normal word, on the English one it's fixed.
        Assert.Equal("Sen", KeyMap.ToUsKeys("შენ", Lang.Georgian));
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Georgian] };
        Assert.IsType<PassThrough>(Type(s, "Sen ", Lang.Georgian));
        s.ResetAll();
        var a = Assert.IsType<FixWord>(Type(s, "gTxovT ", Lang.English));
        Assert.Equal("გთხოვთ", a.Text);
        Assert.Equal("gTxovT", a.Correction.Typed);
        Assert.Equal(Lang.Georgian, a.Layout);
        s.ResetAll();
        Assert.IsType<PassThrough>(Type(s, "Thanks ", Lang.English));   // a capital T is still a capital
    }

    [Fact]
    public void ShiftedWordIsNotTurnedIntoHebrewWithGeorgianOn()
    {
        // Shift+T is Georgian თ, so the key is kept shifted; for Hebrew it still means a capital (a name).
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Hebrew, Lang.Georgian] };
        Assert.IsType<PassThrough>(Type(s, "Takuo ", Lang.English));
    }

    [Fact]
    public void ShiftInsideAWordStillStopsTrackingWithoutGeorgian()
    {
        var s = new TypingSession(D, new NeverFixList()) { Languages = [Lang.English, Lang.Russian] };
        Assert.IsType<PassThrough>(Type(s, "ghbdTn ", Lang.English));
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
        {
            int shifted = KeyMap.ShiftedKeys.IndexOf(c);
            var key = c == ' ' ? KeyInput.Space
                : shifted >= 0 ? KeyInput.Word(KeyMap.PhysicalKeys[shifted], shifted: true)
                : KeyInput.Word(c);
            last = s.OnKey(key, layout, true, Sensitivity.Medium, T0);
        }
        return last;
    }

    public static TheoryData<Lang> NewLanguages =>
        [Lang.Russian, Lang.Arabic, Lang.Ukrainian, Lang.Persian, Lang.Greek, Lang.French, Lang.German,
         Lang.Bulgarian, Lang.Serbian, Lang.Macedonian, Lang.Kazakh, Lang.Georgian, Lang.Armenian, Lang.Korean];

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
