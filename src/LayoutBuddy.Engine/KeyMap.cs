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

    private readonly record struct Token(string Text, char Combining, bool Dead);

    private static volatile Dictionary<Lang, Token[]> _maps = BuildDefaults();

    private static Dictionary<Lang, Token[]> BuildDefaults() =>
        Languages.All.ToDictionary(l => l.Lang, l => Parse(l.Keyboard.Split(' ')));

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
    public static void SetKeyboard(Lang lang, IReadOnlyList<string> tokens)
    {
        var copy = new Dictionary<Lang, Token[]>(_maps) { [lang] = Parse(tokens) };
        _maps = copy;
    }

    /// <summary>Restores the built-in keyboards (used by tests).</summary>
    public static void ResetKeyboards() => _maps = BuildDefaults();

    public static bool IsWordKey(char usKey) => PhysicalKeys.IndexOf(char.ToLowerInvariant(usKey)) >= 0;

    private static Token? Lookup(Token[] map, char key)
    {
        int i = PhysicalKeys.IndexOf(key);
        return i < 0 ? null : map[i];
    }

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
        if (capitalizeFirst && Languages.Get(lang).HasCase && s.Length > 0)
            s = char.ToUpperInvariant(s[0]) + s[1..];
        return s;
    }

    /// <summary>True if every key types exactly one character (no dead keys or multi-letter keys like Arabic لا).</summary>
    public static bool IsSimple(string usKeys, Lang lang)
    {
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
        int KeyFor(string s) => Array.FindIndex(map, t => !t.Dead && t.Text == s);
        var sb = new StringBuilder();
        foreach (var ch in text.Normalize(NormalizationForm.FormC))
        {
            // A key that types this exact character (French é, German ö)...
            int i = KeyFor(ch.ToString());
            if (i >= 0) { sb.Append(PhysicalKeys[i]); continue; }

            // ...or a dead key followed by the base letter (Greek ΄ + α = ά).
            var parts = ch.ToString().Normalize(NormalizationForm.FormD);
            int b = parts.Length == 2 ? KeyFor(parts[0].ToString()) : -1;
            int d = parts.Length == 2 ? Array.FindIndex(map, t => t.Dead && t.Combining == parts[1]) : -1;
            if (b >= 0 && d >= 0)
            {
                sb.Append(PhysicalKeys[d]).Append(PhysicalKeys[b]);
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }
}
