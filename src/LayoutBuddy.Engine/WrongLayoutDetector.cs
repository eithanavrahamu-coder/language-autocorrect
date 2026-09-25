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
        int RareTypedRank,       // typed word is a real but rare word...
        int CommonAltRank);      // ...and alt is this common -> still fix

    private static Params For(Sensitivity s) => s switch
    {
        Sensitivity.Low => new(20000, 300, double.PositiveInfinity, int.MaxValue, int.MaxValue, 0),
        Sensitivity.High => new(50000, 1500, 0.9, 3, 12000, 3000),
        _ => new(50000, 600, 1.4, 4, 25000, 1500),
    };

    public Detection Evaluate(string usKeys, Lang current, Sensitivity sensitivity)
    {
        var target = current == Lang.English ? Lang.Hebrew : Lang.English;
        var typedModel = current == Lang.English ? _en : _he;
        var altModel = current == Lang.English ? _he : _en;
        string typed = KeyMap.Render(usKeys, current);
        string alt = KeyMap.Render(usKeys, target);
        Detection No(string why) => new(false, typed, alt, target, why);
        Detection Yes(string why) => new(true, typed, alt, target, why);

        var p = For(sensitivity);
        if (usKeys.Length < 2) return No("too short");

        var altCore = altModel.Core(alt);
        if (altCore == null || altCore.Length < 2) return No("alt not a word shape");

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

        int? altRank = altModel.Rank(altCore);
        bool altKnown = altModel.IsKnown(altCore);
        double altLp = altModel.AvgLogProb(altCore);

        if (typedKnown)
        {
            if (typedRank > p.RareTypedRank && altRank <= p.CommonAltRank && altCore.Length >= 3)
                return Yes("typed word is rare, alt is common");
            return No("typed word is known");
        }

        int limit = altCore.Length == 2 ? p.MaxAltRankShort : p.MaxAltRank;
        if (altKnown && altRank <= limit)
            return Yes("alt word is known");

        if (altCore.Length >= p.MinLenNgram && altLp > -3.2 && altLp - typedLp >= p.NgramMargin)
            return Yes("alt looks more like a word");

        return No("not confident");
    }
}
