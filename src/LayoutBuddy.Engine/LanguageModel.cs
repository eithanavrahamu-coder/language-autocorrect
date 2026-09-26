using System.Reflection;

namespace LayoutBuddy.Engine;

/// <summary>
/// Word-frequency dictionary plus a character trigram model for one language.
/// </summary>
public sealed class LanguageModel
{
    private readonly Dictionary<string, int> _ranks;
    private readonly Dictionary<string, double> _trigram = new();
    private readonly Dictionary<string, double> _bigram = new();
    private readonly Dictionary<char, double> _unigram = new();
    private readonly Dictionary<string, double> _context2 = new();
    private readonly Dictionary<char, double> _context1 = new();
    private double _total;
    private readonly int _longestWord;

    public Lang Lang { get; }
    public LanguageInfo Info { get; }

    private LanguageModel(Lang lang, IEnumerable<(string Word, long Count)> words)
    {
        Lang = lang;
        Info = Languages.Get(lang);
        _ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        int rank = 0;
        foreach (var (word, count) in words)
        {
            rank++;
            if (!_ranks.ContainsKey(word)) _ranks[word] = rank;
            _longestWord = Math.Max(_longestWord, word.Length);
            Train(word, 1.0 + Math.Log10(Math.Max(1, count)));
        }
    }

    public int WordCount => _ranks.Count;

    public static LanguageModel Load(Lang lang)
    {
        var name = $"LayoutBuddy.Data.{Languages.Get(lang).Code}.txt";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing resource {name}");
        using var reader = new StreamReader(stream);
        return new LanguageModel(lang, Parse(reader));
    }

    public static LanguageModel FromWords(Lang lang, IEnumerable<(string, long)> words) => new(lang, words);

    private static IEnumerable<(string, long)> Parse(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            int sp = line.IndexOf(' ');
            if (sp <= 0) continue;
            long.TryParse(line.AsSpan(sp + 1), out var count);
            yield return (line[..sp], count);
        }
    }

    public bool IsLetter(char c) => Info.IsLetter(c);

    /// <summary>
    /// Strips leading/trailing punctuation. Returns null if punctuation remains inside the word.
    /// </summary>
    public string? Core(string text)
    {
        int start = 0, end = text.Length;
        while (start < end && (!IsLetter(text[start]) || text[start] == '\'')) start++;
        while (end > start && (!IsLetter(text[end - 1]) || text[end - 1] == '\'')) end--;
        if (start == end) return null;
        for (int i = start; i < end; i++)
            if (!IsLetter(text[i])) return null;
        return Normalize(text[start..end]);
    }

    /// <summary>Splits on any non-letter, returning the letter runs (lowercased for English).</summary>
    public IReadOnlyList<string> Segments(string text)
    {
        var result = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && !IsLetter(text[i])) i++;
            int s = i;
            while (i < text.Length && IsLetter(text[i])) i++;
            if (i > s)
            {
                var seg = text[s..i].Trim('\'');
                if (seg.Length > 0) result.Add(Normalize(seg));
            }
        }
        return result;
    }

    private string Normalize(string s) => Info.ToLower(s).Normalize(System.Text.NormalizationForm.FormC);

    /// <summary>
    /// Frequency rank (1 = most common), or null if not in the dictionary. In a language written without spaces
    /// (Thai), text typed before Space is often several words: it ranks as the rarest of the words it splits into.
    /// </summary>
    public int? Rank(string core) =>
        _ranks.TryGetValue(core, out var r) ? r
        : Info.WithoutSpaces ? PhraseRank(core)
        : Info.JoinsWithApostrophe ? JoinedRank(core)
        : null;

    /// <summary>
    /// A word joined to a short form by an apostrophe (don + 't, l' + uomo), which the word lists keep apart:
    /// ranks as the rarer of the two, or null if the word isn't known or the short form isn't a common one
    /// (the lists also hold junk pieces like 'a).
    /// </summary>
    private int? JoinedRank(string core)
    {
        int i = core.IndexOf('\'');
        if (i <= 0 || i == core.Length - 1) return null;
        int? Joined(string word, string shortForm) =>
            _ranks.TryGetValue(shortForm, out var rs) && rs <= 1000
            && _ranks.TryGetValue(word, out var rw) && rw <= KnownRankLimit(word.Length)
                ? Math.Max(rs, rw) : null;
        return Joined(core[..i], core[i..]) ?? Joined(core[(i + 1)..], core[..(i + 1)]);
    }

    /// <summary>
    /// Splits text into the fewest known words (then preferring common ones) and returns the rarest word's rank,
    /// or null if the text can't be split into known words.
    /// </summary>
    private int? PhraseRank(string text)
    {
        var lookup = _ranks.GetAlternateLookup<ReadOnlySpan<char>>();
        int n = text.Length;
        var words = new int[n + 1];   // fewest words covering text[..i]
        var rarest = new int[n + 1];  // ...and the rarest of them
        Array.Fill(words, int.MaxValue);
        words[0] = 0;
        for (int i = 0; i < n; i++)
        {
            if (words[i] == int.MaxValue) continue;
            for (int j = i + 1; j <= Math.Min(n, i + _longestWord); j++)
            {
                if (!lookup.TryGetValue(text.AsSpan(i, j - i), out var r) || r > KnownRankLimit(j - i)) continue;
                int count = words[i] + 1, worst = Math.Max(rarest[i], r);
                if (count < words[j] || (count == words[j] && worst < rarest[j]))
                {
                    words[j] = count;
                    rarest[j] = worst;
                }
            }
        }
        return words[n] == int.MaxValue ? null : rarest[n];
    }

    /// <summary>
    /// A word counts as "known" if it is in the dictionary with a rank good enough for its length
    /// (short words must be common, because the lists contain lots of short junk tokens).
    /// </summary>
    public bool IsKnown(string core)
    {
        var rank = Rank(core);
        return rank != null && rank <= KnownRankLimit(Length(core));
    }

    /// <summary>A word's length in letters (a Korean syllable counts as the letters typed for it: 못 = ㅁㅗㅅ).</summary>
    public int Length(string core) => Info.JoinsSyllables ? Hangul.Decompose(core).Length : core.Length;

    public static int KnownRankLimit(int length) => length switch
    {
        1 => 60,
        2 => 3000,
        3 => 15000,
        _ => int.MaxValue,
    };

    /// <summary>Average log-probability per character under the trigram model.</summary>
    public double AvgLogProb(string core)
    {
        string s = "^^" + core + "$";
        double sum = 0;
        for (int i = 2; i < s.Length; i++)
            sum += Math.Log(Prob(s[i - 2], s[i - 1], s[i]));
        return sum / (s.Length - 2);
    }

    private void Train(string word, double weight)
    {
        string s = "^^" + word + "$";
        for (int i = 2; i < s.Length; i++)
        {
            char a = s[i - 2], b = s[i - 1], c = s[i];
            Add(_trigram, new string([a, b, c]), weight);
            Add(_context2, new string([a, b]), weight);
            Add(_bigram, new string([b, c]), weight);
            Add(_context1, b, weight);
            Add(_unigram, c, weight);
            _total += weight;
        }
    }

    private double Prob(char a, char b, char c)
    {
        double p3 = Ratio(_trigram, new string([a, b, c]), _context2, new string([a, b]));
        double p2 = Ratio(_bigram, new string([b, c]), _context1, b);
        double p1 = _unigram.TryGetValue(c, out var u) ? u / _total : 0;
        return 0.6 * p3 + 0.3 * p2 + 0.09 * p1 + 0.01 / 40;
    }

    private static double Ratio<TK1, TK2>(Dictionary<TK1, double> num, TK1 k1, Dictionary<TK2, double> den, TK2 k2)
        where TK1 : notnull where TK2 : notnull
        => den.TryGetValue(k2, out var d) && d > 0 && num.TryGetValue(k1, out var n) ? n / d : 0;

    private static void Add<TK>(Dictionary<TK, double> d, TK key, double w) where TK : notnull
        => d[key] = d.TryGetValue(key, out var v) ? v + w : w;
}
