using LayoutBuddy.Engine;
using Xunit.Abstractions;

namespace LayoutBuddy.Engine.Tests;

public class DetectorTests(ITestOutputHelper output)
{
    private static WrongLayoutDetector D => Shared.Detector;

    [Theory]
    [InlineData("akuo", "שלום")]
    [InlineData("t,v", "אתה")]
    [InlineData("nv", "מה")]
    [InlineData("ak", "של")]
    [InlineData("vhuo", "היום")]
    [InlineData("rcv", "רבה")]
    [InlineData("tbh", "אני")]
    [InlineData("nxsv", "מסדה")]
    public void FixesHebrewTypedInEnglish(string keys, string expected)
    {
        var d = D.Evaluate(keys, Lang.English, Sensitivity.Medium);
        Assert.True(d.ShouldFix, d.Reason);
        Assert.Equal(expected, d.Replacement);
        Assert.Equal(Lang.Hebrew, d.TargetLang);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("world")]
    [InlineData("the")]
    [InlineData("a")]
    [InlineData("hello,")]
    [InlineData("end.")]
    [InlineData("www.google.com")]
    [InlineData("don't")]
    [InlineData("ok")]
    [InlineData("is")]
    public void LeavesEnglishAlone(string keys)
    {
        var d = D.Evaluate(keys, Lang.English, Sensitivity.High);
        Assert.False(d.ShouldFix, $"{keys} -> {d.Replacement}: {d.Reason}");
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("world")]
    [InlineData("thanks")]
    [InlineData("computer")]
    public void FixesEnglishTypedInHebrew(string keys)
    {
        var d = D.Evaluate(keys, Lang.Hebrew, Sensitivity.Medium);
        Assert.True(d.ShouldFix, d.Reason);
        Assert.Equal(keys, d.Replacement);
    }

    [Theory]
    [InlineData("שלום")]
    [InlineData("של")]
    [InlineData("מה")]
    [InlineData("שלום.")]
    [InlineData("תודה")]
    public void LeavesHebrewAlone(string word)
    {
        var d = D.Evaluate(Shared.Keys(word), Lang.Hebrew, Sensitivity.High);
        Assert.False(d.ShouldFix, $"{word} -> {d.Replacement}: {d.Reason}");
    }

    [Fact]
    public void Accuracy()
    {
        var en = Shared.English.Value;
        var he = Shared.Hebrew.Value;
        var enWords = ReadWords(Lang.English).Skip(50).Take(20000).Where((_, i) => i % 10 == 0).ToList();
        var heWords = ReadWords(Lang.Hebrew).Skip(50).Take(20000).Where((_, i) => i % 10 == 0).ToList();

        foreach (var s in Enum.GetValues<Sensitivity>())
        {
            // Correct-layout words must not be touched.
            int enFalse = enWords.Count(w => D.Evaluate(w, Lang.English, s).ShouldFix);
            int heFalse = heWords.Count(w => D.Evaluate(KeyMap.ToUsKeys(w), Lang.Hebrew, s).ShouldFix);
            // Wrong-layout words should be fixed.
            int heCaught = heWords.Count(w => D.Evaluate(KeyMap.ToUsKeys(w), Lang.English, s).ShouldFix);
            int enCaught = enWords.Count(w => D.Evaluate(w, Lang.Hebrew, s).ShouldFix);

            double falseRate = (enFalse + heFalse) / (double)(enWords.Count + heWords.Count);
            double catchRate = (heCaught + enCaught) / (double)(enWords.Count + heWords.Count);
            output.WriteLine($"{s}: wrongly changed {falseRate:P2} (en {enFalse}, he {heFalse}); " +
                             $"caught {catchRate:P1} (he {heCaught}/{heWords.Count}, en {enCaught}/{enWords.Count})");

            Assert.True(falseRate < 0.02, $"{s} false rate {falseRate:P2}");
            Assert.True(catchRate > (s == Sensitivity.Low ? 0.75 : 0.85), $"{s} catch rate {catchRate:P1}");
        }
        _ = en; _ = he;
    }

    private static IEnumerable<string> ReadWords(Lang lang)
    {
        var name = lang == Lang.English ? "LayoutBuddy.Data.en.txt" : "LayoutBuddy.Data.he.txt";
        using var s = typeof(LanguageModel).Assembly.GetManifestResourceStream(name)!;
        using var r = new StreamReader(s);
        var list = new List<string>();
        string? line;
        while ((line = r.ReadLine()) != null) list.Add(line[..line.IndexOf(' ')]);
        return list;
    }
}
