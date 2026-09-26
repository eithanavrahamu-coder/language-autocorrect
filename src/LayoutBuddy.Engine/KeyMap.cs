using System.Text;

namespace LayoutBuddy.Engine;

/// <summary>
/// Physical keys are identified by the character they type on a US keyboard ("q", ";", "1"...).
/// This maps them to what each language's keyboard types, including dead keys (accents that combine
/// with the next letter, like the Greek tonos or the French circumflex).
/// </summary>
public static class KeyMap
{
    /// <summary>The 47 keys that can be part of a word, in the order used by <see cref="LanguageInfo.Keyboard"/>.</summary>
    public const string PhysicalKeys = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./";

    /// <summary>
    /// The same keys with Shift held, as a US keyboard types them. A key sequence uses these for keys that
    /// type a letter of their own with Shift (Georgian Shift+T types თ, not a capital ტ).
    /// </summary>
    public const string ShiftedKeys = "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?";

    private readonly record struct Token(string Text, char Combining, bool Dead);

    private sealed record Map(Token[] Plain, Token[] Shifted);

    private static volatile Dictionary<Lang, Map> _maps = BuildDefaults();

    private static Dictionary<Lang, Map> BuildDefaults() =>
        Languages.All.ToDictionary(l => l.Lang, l => Build(l, l.Keyboard.Split(' '), l.ShiftKeyboard?.Split(' ')));

    private static Map Build(LanguageInfo info, IReadOnlyList<string> plain, IReadOnlyList<string>? shifted)
    {
        var p = Parse(plain);
        var s = shifted != null ? Parse(shifted) : p.Select((t, i) => DefaultShifted(t, i, info.HasCase)).ToArray();
        return new Map(p, s);
    }

    /// <summary>Without a shifted map: the capital of a letter, otherwise what a US keyboard types.</summary>
    private static Token DefaultShifted(Token t, int key, bool hasCase) =>
        hasCase && !t.Dead && t.Text.Length == 1 && char.ToUpperInvariant(t.Text[0]) != t.Text[0]
            ? t with { Text = t.Text.ToUpperInvariant() }
            : new Token(ShiftedKeys[key].ToString(), '\0', false);

    private static Token[] Parse(IReadOnlyList<string> tokens)
    {
        if (tokens.Count != PhysicalKeys.Length)
            throw new ArgumentException($"Expected {PhysicalKeys.Length} keys, got {tokens.Count}");
        return tokens.Select(t => t.Length >= 3 && t[0] == '~'
            ? new Token(t[2..], t[1], true)
            : new Token(t, '\0', false)).ToArray();
    }

    /// <summary>
    /// Replaces a language's keyboard with what the user's actual Windows layout types
    /// (layouts vary: Canadian French, Swiss German, Persian Standard...).
    /// </summary>
    /// <param name="tokens">One entry per <see cref="PhysicalKeys"/>, in the same format as <see cref="LanguageInfo.Keyboard"/>.</param>
    /// <param name="shifted">The same with Shift held, or null to derive it.</param>
    public static void SetKeyboard(Lang lang, IReadOnlyList<string> tokens, IReadOnlyList<string>? shifted = null)
    {
        var info = Languages.Get(lang);
        var copy = new Dictionary<Lang, Map>(_maps) { [lang] = Build(info, tokens, shifted ?? info.ShiftKeyboard?.Split(' ')) };
        _maps = copy;
    }

    /// <summary>Restores the built-in keyboards (used by tests).</summary>
    public static void ResetKeyboards() => _maps = BuildDefaults();

    public static bool IsWordKey(char usKey) => PhysicalKeys.IndexOf(char.ToLowerInvariant(usKey)) >= 0;

    private static Token? Lookup(Map map, char key)
    {
        int i = PhysicalKeys.IndexOf(key);
        if (i >= 0) return map.Plain[i];
        i = ShiftedKeys.IndexOf(key);
        return i < 0 ? null : map.Shifted[i];
    }

    /// <summary>
    /// True if Shift + this key types a letter of its own in <paramref name="lang"/> (Georgian თ, Thai ธ, Korean ㅆ)
    /// or an accent of its own (Spanish ¨), rather than a capital letter.
    /// </summary>
    public static bool ShiftTypesLetter(char usKey, Lang lang)
    {
        var info = Languages.Get(lang);
        int i = PhysicalKeys.IndexOf(char.ToLowerInvariant(usKey));
        if (info.ShiftKeyboard == null || i < 0) return false;
        var t = _maps[lang].Shifted[i];
        var plain = _maps[lang].Plain[i];
        if (t.Dead) return !plain.Dead || plain.Combining != t.Combining;
        return t.Text.Length == 1 && t.Text != plain.Text && !IsCapitalOf(t.Text, plain.Text, info)
               && (info.IsLetter(t.Text[0]) || info.JoinsSyllables && Hangul.IsLetter(t.Text[0]));
    }

    private static bool IsCapitalOf(string upper, string lower, LanguageInfo info) =>
        info.HasCase && lower.Length == 1 && upper[0] == info.ToUpper(lower[0]);

    public static bool IsShiftedKey(char c) => ShiftedKeys.IndexOf(c) >= 0;

    /// <summary>The <see cref="ShiftedKeys"/> character for a physical key.</summary>
    public static char Shifted(char usKey) => ShiftedKeys[PhysicalKeys.IndexOf(char.ToLowerInvariant(usKey))];

    /// <summary>Renders a sequence of physical keys as it would appear when typed in <paramref name="lang"/>.</summary>
    /// <param name="capitalizeFirst">Shift was held for the first key (only affects languages with capital letters).</param>
    public static string Render(string usKeys, Lang lang, bool capitalizeFirst = false)
    {
        var map = _maps[lang];
        var sb = new StringBuilder(usKeys.Length + 2);
        for (int i = 0; i < usKeys.Length; i++)
        {
            var t = Lookup(map, usKeys[i]);
            if (t == null) { sb.Append(usKeys[i]); continue; }
            var tok = t.Value;
            if (tok.Dead)
            {
                // Combine with the next key's letter if possible (΄ + α = ά), otherwise show the accent itself.
                var next = i + 1 < usKeys.Length ? Lookup(map, usKeys[i + 1]) : null;
                if (next is { Dead: false } n && n.Text.Length == 1)
                {
                    var composed = (n.Text + tok.Combining).Normalize(NormalizationForm.FormC);
                    if (composed.Length == 1)
                    {
                        sb.Append(composed);
                        i++;
                        continue;
                    }
                }
                sb.Append(tok.Text);
                continue;
            }
            sb.Append(tok.Text);
        }

        var s = sb.ToString();
        if (Languages.Get(lang).JoinsSyllables) s = Hangul.Compose(s);
        if (capitalizeFirst && Languages.Get(lang).HasCase && s.Length > 0)
            s = char.ToUpperInvariant(s[0]) + s[1..];
        return s;
    }

    /// <summary>
    /// True if every key types exactly one character (no dead keys, multi-letter keys like Arabic لا,
    /// or Korean letters joined into syllables).
    /// </summary>
    public static bool IsSimple(string usKeys, Lang lang)
    {
        if (Languages.Get(lang).JoinsSyllables) return false;
        var map = _maps[lang];
        foreach (var k in usKeys)
            if (Lookup(map, k) is { } t && (t.Dead || t.Text.Length != 1)) return false;
        return true;
    }

    /// <summary>True if the key types a letter that has upper and lower case in <paramref name="lang"/>.</summary>
    public static bool TypesCasedLetter(char usKey, Lang lang)
    {
        if (!Languages.Get(lang).HasCase) return false;
        var t = Lookup(_maps[lang], char.ToLowerInvariant(usKey));
        return t is { Dead: false } tok && tok.Text.Length == 1 && char.IsLetter(tok.Text[0])
               && char.ToUpperInvariant(tok.Text[0]) != tok.Text[0];
    }

    /// <summary>Converts text back to the physical keys that type it on <paramref name="lang"/>'s keyboard (tests and tools).</summary>
    public static string ToUsKeys(string text, Lang lang = Lang.Hebrew)
    {
        var map = _maps[lang];
        bool shiftLetters = Languages.Get(lang).ShiftKeyboard != null;
        // The last matching key, so letters win over the number row (Armenian ւ is on both 6 and U).
        int KeyFor(string s) => Array.FindLastIndex(map.Plain, t => !t.Dead && t.Text == s);
        int ShiftedKeyFor(string s) => shiftLetters ? Array.FindLastIndex(map.Shifted, t => !t.Dead && t.Text == s) : -1;
        var sb = new StringBuilder();
        text = text.Normalize(NormalizationForm.FormC);
        if (Languages.Get(lang).JoinsSyllables) text = Hangul.Decompose(text);
        foreach (var ch in text)
        {
            // A key that types this exact character (French é, German ö, Georgian Shift+T თ)...
            int i = KeyFor(ch.ToString());
            if (i >= 0) { sb.Append(PhysicalKeys[i]); continue; }
            i = ShiftedKeyFor(ch.ToString());
            if (i >= 0) { sb.Append(ShiftedKeys[i]); continue; }

            // ...or a dead key followed by the base letter (Greek ΄ + α = ά, Portuguese Shift+' then e = ê).
            var parts = ch.ToString().Normalize(NormalizationForm.FormD);
            int b = parts.Length == 2 ? KeyFor(parts[0].ToString()) : -1;
            int d = parts.Length == 2 ? Array.FindIndex(map.Plain, t => t.Dead && t.Combining == parts[1]) : -1;
            if (b >= 0 && d >= 0)
            {
                sb.Append(PhysicalKeys[d]).Append(PhysicalKeys[b]);
                continue;
            }
            d = parts.Length == 2 && shiftLetters ? Array.FindIndex(map.Shifted, t => t.Dead && t.Combining == parts[1]) : -1;
            if (b >= 0 && d >= 0)
            {
                sb.Append(ShiftedKeys[d]).Append(PhysicalKeys[b]);
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
