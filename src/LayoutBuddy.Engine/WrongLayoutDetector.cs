namespace LayoutBuddy.Engine;

public enum Sensitivity { Low, Medium, High }

public sealed record Detection(bool ShouldFix, string Typed, string Replacement, Lang TargetLang, string Reason);

/// <summary>
/// Decides whether a word was typed in the wrong keyboard layout.
/// Input is the sequence of physical keys (as US-layout characters).
/// </summary>
public sealed class WrongLayoutDetector
{
    private readonly LanguageModel _en;
    private readonly LanguageModel _he;

    public WrongLayoutDetector(LanguageModel english, LanguageModel hebrew)
    {
        _en = english;
        _he = hebrew;
    }

    public static WrongLayoutDetector LoadDefault() =>
        new(LanguageModel.Load(Lang.English), LanguageModel.Load(Lang.Hebrew));

    private sealed record Params(
        int MaxAltRank,          // alt word must be at least this common when typed text is unknown
        int MaxAltRankShort,     // same, for 2-letter alt words
        double NgramMargin,      // both unknown: alt must beat typed by this many nats/char
        int MinLenNgram,         // both unknown: minimum word length
        double PopularityRatio,  // both known: alt must be this many times more popular (by rank)...
        int MaxPopularAltRank);  // ...and at least this common

    private static Params For(Sensitivity s) => s switch
    {
        Sensitivity.Low => new(20000, 300, double.PositiveInfinity, int.MaxValue, double.PositiveInfinity, 0),
        Sensitivity.High => new(50000, 1500, 0.9, 3, 15, 5000),
        _ => new(50000, 600, 1.4, 4, 40, 2000),
    };

    /// <summary>Everything we know about one key sequence in both languages.</summary>
    private readonly record struct Analysis(
        string Typed, string Alt, Lang Target, string? AltCore,
        bool TypedKnown, int? TypedRank, double TypedLp,
        bool AltKnown, int? AltRank, double AltLp);

    private Analysis Analyze(string usKeys, Lang current)
    {
        var target = current == Lang.English ? Lang.Hebrew : Lang.English;
        var typedModel = current == Lang.English ? _en : _he;
        var altModel = current == Lang.English ? _he : _en;
        string typed = KeyMap.Render(usKeys, current);
        string alt = KeyMap.Render(usKeys, target);

        var typedCore = typedModel.Core(typed);
        bool typedKnown;
        int? typedRank = null;
        double typedLp;
        if (typedCore != null)
        {
            typedKnown = typedModel.IsKnown(typedCore);
            typedRank = typedModel.Rank(typedCore);
            typedLp = typedModel.AvgLogProb(typedCore);
        }
        else
        {
            // Punctuation inside the typed text (e.g. "www.google.com", "3.5", "t,v").
            // Treat as fine if every piece looks like a real word.
            var segs = typedModel.Segments(typed);
            typedKnown = segs.Count > 0 && segs.All(s => typedModel.IsKnown(s) && s.Length >= 2);
            typedLp = double.NegativeInfinity;
        }

        var altCore = altModel.Core(alt);
        int? altRank = altCore == null ? null : altModel.Rank(altCore);
        bool altKnown = altCore != null && altModel.IsKnown(altCore);
        double altLp = altCore == null ? double.NegativeInfinity : altModel.AvgLogProb(altCore);
        return new(typed, alt, target, altCore, typedKnown, typedRank, typedLp, altKnown, altRank, altLp);
    }

    public Detection Evaluate(string usKeys, Lang current, Sensitivity sensitivity)
    {
        var a = Analyze(usKeys, current);
        Detection No(string why) => new(false, a.Typed, a.Alt, a.Target, why);
        Detection Yes(string why) => new(true, a.Typed, a.Alt, a.Target, why);

        var p = For(sensitivity);
        if (usKeys.Length < 2) return No("too short");
        if (a.AltCore == null || a.AltCore.Length < 2) return No("alt not a word shape");

        if (a.TypedKnown)
        {
            // Both are real words: the much more popular one wins.
            if (a.AltKnown && a.AltCore.Length >= 3 && a.AltRank <= p.MaxPopularAltRank &&
                a.TypedRank is int tr && a.AltRank is int ar && tr >= ar * p.PopularityRatio)
                return Yes("alt word is much more popular");
            return No("typed word is known");
        }

        int limit = a.AltCore.Length == 2 ? p.MaxAltRankShort : p.MaxAltRank;
        if (a.AltKnown && a.AltRank <= limit)
            return Yes("alt word is known");

        if (a.AltCore.Length >= p.MinLenNgram && a.AltLp > -3.2 && a.AltLp - a.TypedLp >= p.NgramMargin)
            return Yes("alt looks more like a word");

        return No("not confident");
    }

    /// <summary>
    /// Looser check used for the words just before a word we are fixing: once we know the user
    /// is typing in the wrong layout, a word only needs to look more like the other language.
    /// </summary>
    public bool IsPlausibleWrongLayout(string usKeys, Lang current)
    {
        var a = Analyze(usKeys, current);
        if (a.AltCore == null) return false;
        if (a.TypedKnown)
            return a.AltKnown && a.TypedRank is int tr && a.AltRank is int ar && tr >= ar * 3;
        if (a.AltKnown) return true;
        return a.AltCore.Length >= 2 && a.AltLp > -3.5 && a.AltLp > a.TypedLp;
    }
}
