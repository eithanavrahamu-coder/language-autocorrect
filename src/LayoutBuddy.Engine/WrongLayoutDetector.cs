namespace LayoutBuddy.Engine;

public enum Sensitivity { Low, Medium, High }

public sealed record Detection(bool ShouldFix, string Typed, string Replacement, Lang TargetLang, string Reason)
{
    /// <summary>How strongly the replacement looks like a real word (for choosing between several languages).</summary>
    public double Score { get; init; }
}

/// <summary>
/// Decides whether a word was typed in the wrong keyboard layout.
/// Input is the sequence of physical keys (as US-layout characters).
/// </summary>
public sealed class WrongLayoutDetector
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Lang, Lazy<LanguageModel>> _models = new();

    public WrongLayoutDetector(params LanguageModel[] models)
    {
        foreach (var m in models) _models[m.Lang] = new Lazy<LanguageModel>(m);
    }

    /// <summary>A detector that loads each language's word list the first time it is needed.</summary>
    public static WrongLayoutDetector LoadDefault() => new();

    public LanguageModel Model(Lang lang) =>
        _models.GetOrAdd(lang, l => new Lazy<LanguageModel>(() => LanguageModel.Load(l))).Value;

    /// <summary>Loads word lists ahead of time so the first word typed isn't slow.</summary>
    public void Preload(IEnumerable<Lang> langs)
    {
        foreach (var l in langs) _ = Model(l);
    }

    /// <summary>The other language of the original English/Hebrew pair (used by older callers and tests).</summary>
    private static Lang DefaultTarget(Lang current) => current == Lang.English ? Lang.Hebrew : Lang.English;

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
        string Typed, string Alt, Lang Target, string? AltCore, int AltLength,
        bool TypedKnown, int? TypedRank, double TypedLp,
        bool AltKnown, int? AltRank, double AltLp);

    private Analysis Analyze(string usKeys, Lang current, Lang target)
    {
        var typedModel = Model(current);
        var altModel = Model(target);
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
            typedKnown = segs.Count > 0 && segs.All(s => typedModel.IsKnown(s) && typedModel.Length(s) >= 2);
            typedLp = double.NegativeInfinity;
        }

        var altCore = altModel.Core(alt);
        int? altRank = altCore == null ? null : altModel.Rank(altCore);
        bool altKnown = altCore != null && altModel.IsKnown(altCore);
        double altLp = altCore == null ? double.NegativeInfinity : altModel.AvgLogProb(altCore);
        int altLength = altCore == null ? 0 : altModel.Length(altCore);
        return new(typed, alt, target, altCore, altLength, typedKnown, typedRank, typedLp, altKnown, altRank, altLp);
    }

    /// <param name="context">
    /// Language of the words just before this one: positive = that many recent words are in the other language
    /// (so this word probably is too), negative = they are in the language as typed.
    /// </param>
    public Detection Evaluate(string usKeys, Lang current, Sensitivity sensitivity, int context = 0) =>
        Evaluate(usKeys, current, DefaultTarget(current), sensitivity, context);

    public Detection Evaluate(string usKeys, Lang current, Lang target, Sensitivity sensitivity, int context = 0)
    {
        var a = Analyze(usKeys, current, target);
        double score = a.AltKnown && a.AltRank is int rk ? 100 - Math.Log(rk) : a.AltLp;
        Detection No(string why) => new(false, a.Typed, a.Alt, a.Target, why);
        Detection Yes(string why) => new(true, a.Typed, a.Alt, a.Target, why) { Score = score };

        // Keyboards that share most letters (English/French/German) often type the same thing.
        if (string.Equals(a.Typed, a.Alt, StringComparison.OrdinalIgnoreCase)) return No("same text");
        if (IsNumber(a.Typed, current)) return No("number");
        if (AddsDigits(a)) return No("digits in a word");
        if (IsRepeatedLetter(a.Typed, current)) return No("repeated letter");

        // The sentence so far is in the typed language: only fix clear mistakes.
        if (context < 0 && sensitivity != Sensitivity.Low) sensitivity = Sensitivity.Low;

        var p = For(sensitivity);
        if (usKeys.Length < 2) return No("too short");
        if (a.AltCore == null || a.AltLength < 2) return No("alt not a word shape");

        // The sentence so far is in the other language ("הוא אוכל far" -> כשר):
        // a word that exists there is fixed even if it is also a real word as typed.
        // Rare dictionary entries count too (כשר is rank ~20000), but not rare short ones or ones with
        // an apostrophe, which are mostly junk in the word lists.
        // The whole result must be a clean word (no stray ' , . / around it).
        bool altClean = a.Alt.Equals(a.AltCore, StringComparison.OrdinalIgnoreCase);
        bool altIsWord = altClean && (a.AltKnown ||
            (a.AltRank <= 25000 && a.AltLength >= 3 && !a.AltCore.Contains('\'')));
        if (context >= 2 && altIsWord)
            return Yes("fits the sentence");
        if (context == 1 && altClean && a.AltKnown &&
            (!a.TypedKnown || (a.TypedRank is int t1 && a.AltRank is int r1 && t1 * 10 >= r1)))
            return Yes("fits the sentence");

        if (a.TypedKnown)
        {
            // Both are real words: the much more popular one wins.
            if (a.AltKnown && a.AltLength >= 3 && a.AltRank <= p.MaxPopularAltRank &&
                a.TypedRank is int tr && a.AltRank is int ar && tr >= ar * p.PopularityRatio)
                return Yes("alt word is much more popular");
            return No("typed word is known");
        }

        int limit = a.AltLength == 2 ? p.MaxAltRankShort : p.MaxAltRank;
        if (a.AltKnown && a.AltRank <= limit)
            return Yes("alt word is known");

        if (a.AltLength >= p.MinLenNgram && a.AltLp > -3.2 && a.AltLp - a.TypedLp >= p.NgramMargin)
            return Yes("alt looks more like a word");

        return No("not confident");
    }

    /// <summary>
    /// Numbers (times, prices) are typed on purpose, even where the other keyboard types letters on the
    /// number row (Kazakh, French).
    /// </summary>
    private bool IsNumber(string typed, Lang typedIn) =>
        typed.Any(char.IsDigit) && !typed.Any(Model(typedIn).IsLetter);

    /// <summary>
    /// Letters typed on the number row (Armenian ր is the 8 key) read as digits on other keyboards:
    /// "հավերժ" is not the English "have8=".
    /// </summary>
    private static bool AddsDigits(Analysis a) => a.Alt.Count(char.IsDigit) > a.Typed.Count(char.IsDigit);

    /// <summary>Korean chat repeats a lone letter on purpose: ㅋㅋㅋ (laughing), ㅠㅠ (crying).</summary>
    private static bool IsRepeatedLetter(string typed, Lang typedIn) =>
        Languages.Get(typedIn).JoinsSyllables && typed.Length >= 2 && Hangul.IsLetter(typed[0]) && typed.All(c => c == typed[0]);

    /// <summary>
    /// Which language a finished word is in: the typed layout if it is a word there and not in the other,
    /// the other language if the reverse, otherwise null (ambiguous or unknown).
    /// </summary>
    public Lang? LanguageOf(string usKeys, Lang typedIn) => LanguageOf(usKeys, typedIn, [DefaultTarget(typedIn)]);

    /// <summary>
    /// Which of the languages a finished word is in: the one where it is a known word, if it is clearly
    /// more common there than in the others; otherwise null (ambiguous or unknown).
    /// </summary>
    public Lang? LanguageOf(string usKeys, Lang typedIn, IEnumerable<Lang> others)
    {
        var known = new List<(Lang Lang, int Rank)>();
        foreach (var lang in others.Prepend(typedIn).Distinct())
        {
            var model = Model(lang);
            var core = model.Core(KeyMap.Render(usKeys, lang));
            if (core != null && model.IsKnown(core) && model.Rank(core) is int r) known.Add((lang, r));
        }
        if (known.Count == 0) return null;
        known.Sort((x, y) => x.Rank.CompareTo(y.Rank));
        if (known.Count == 1 || known[0].Rank * 20 <= known[1].Rank) return known[0].Lang;
        return null;
    }

    /// <summary>
    /// Looser check used for the words just before a word we are fixing: once we know the user
    /// is typing in the wrong layout, a word only needs to look more like the other language.
    /// </summary>
    public bool IsPlausibleWrongLayout(string usKeys, Lang current) =>
        IsPlausibleWrongLayout(usKeys, current, DefaultTarget(current));

    public bool IsPlausibleWrongLayout(string usKeys, Lang current, Lang target)
    {
        var a = Analyze(usKeys, current, target);
        if (string.Equals(a.Typed, a.Alt, StringComparison.OrdinalIgnoreCase)) return false;
        if (a.AltCore == null || IsNumber(a.Typed, current) || AddsDigits(a) || IsRepeatedLetter(a.Typed, current)) return false;
        if (a.TypedKnown)
            return a.AltKnown && a.TypedRank is int tr && a.AltRank is int ar && tr >= ar * 3;
        if (a.AltKnown) return true;
        return a.AltLength >= 2 && a.AltLp > -3.5 && a.AltLp > a.TypedLp;
    }
}
