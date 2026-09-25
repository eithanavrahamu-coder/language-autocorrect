namespace LayoutBuddy.Engine;

public enum Lang { English, Hebrew }

/// <summary>
/// Maps physical keys (identified by the character they produce on a US layout)
/// to the character the same key produces on the Hebrew (Standard) layout.
/// </summary>
public static class KeyMap
{
    // US key -> Hebrew character on the same physical key.
    private static readonly Dictionary<char, char> UsToHe = new()
    {
        ['q'] = '/', ['w'] = '\'', ['e'] = 'ק', ['r'] = 'ר', ['t'] = 'א', ['y'] = 'ט',
        ['u'] = 'ו', ['i'] = 'ן', ['o'] = 'ם', ['p'] = 'פ',
        ['a'] = 'ש', ['s'] = 'ד', ['d'] = 'ג', ['f'] = 'כ', ['g'] = 'ע', ['h'] = 'י',
        ['j'] = 'ח', ['k'] = 'ל', ['l'] = 'ך', [';'] = 'ף', ['\''] = ',',
        ['z'] = 'ז', ['x'] = 'ס', ['c'] = 'ב', ['v'] = 'ה', ['b'] = 'נ', ['n'] = 'מ',
        ['m'] = 'צ', [','] = 'ת', ['.'] = 'ץ', ['/'] = '.',
    };

    private static readonly Dictionary<char, char> HeToUs =
        UsToHe.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Keys that can be part of a word (they map to a Hebrew letter or an English letter).</summary>
    public static bool IsWordKey(char usKey) => UsToHe.ContainsKey(char.ToLowerInvariant(usKey));

    /// <summary>Renders a sequence of US keys as it would appear when typed in <paramref name="lang"/>.</summary>
    public static string Render(string usKeys, Lang lang)
    {
        if (lang == Lang.English) return usKeys;
        var chars = new char[usKeys.Length];
        for (int i = 0; i < usKeys.Length; i++)
            chars[i] = UsToHe.TryGetValue(char.ToLowerInvariant(usKeys[i]), out var h) ? h : usKeys[i];
        return new string(chars);
    }

    /// <summary>Converts Hebrew text back to the US keys that type it (for tests and tools).</summary>
    public static string ToUsKeys(string hebrew)
    {
        var chars = new char[hebrew.Length];
        for (int i = 0; i < hebrew.Length; i++)
            chars[i] = HeToUs.TryGetValue(hebrew[i], out var u) ? u : hebrew[i];
        return new string(chars);
    }
}
