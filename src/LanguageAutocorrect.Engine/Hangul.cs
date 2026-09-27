using System.Text;

namespace LanguageAutocorrect.Engine;

/// <summary>
/// Korean letters (jamo) join into syllable blocks as they are typed: ㅇ+ㅏ+ㄴ = 안. This follows the standard
/// 2-set (Dubeolsik) rules the Windows Korean IME uses, including double vowels (ㅗ+ㅏ = ㅘ), double finals
/// (ㄹ+ㄱ = ㄺ) and a final moving to the next syllable when a vowel follows (안+ㅏ = 아나).
/// </summary>
public static class Hangul
{
    private const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private const string Vowels = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    private const string Finals = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ"; // index 0 = no final

    private const char First = '가', Last = '힣';

    // Pairs typed as two keys: double vowels and double finals.
    private static readonly Dictionary<(char, char), char> DoubleVowels = Pairs("ㅗㅏㅘ ㅗㅐㅙ ㅗㅣㅚ ㅜㅓㅝ ㅜㅔㅞ ㅜㅣㅟ ㅡㅣㅢ");
    private static readonly Dictionary<(char, char), char> DoubleFinals = Pairs("ㄱㅅㄳ ㄴㅈㄵ ㄴㅎㄶ ㄹㄱㄺ ㄹㅁㄻ ㄹㅂㄼ ㄹㅅㄽ ㄹㅌㄾ ㄹㅍㄿ ㄹㅎㅀ ㅂㅅㅄ");

    private static Dictionary<(char, char), char> Pairs(string triples) =>
        triples.Split(' ').ToDictionary(t => (t[0], t[1]), t => t[2]);

    public static bool IsSyllable(char c) => c >= First && c <= Last;

    /// <summary>A single letter (jamo) as the keys type it, before it joins a syllable.</summary>
    public static bool IsLetter(char c) => IsConsonant(c) || IsVowel(c);

    private static bool IsVowel(char c) => Vowels.Contains(c);
    private static bool IsConsonant(char c) => Initials.Contains(c);

    /// <summary>Joins letters into syllables the way they appear when typed one key at a time.</summary>
    public static string Compose(string letters)
    {
        var sb = new StringBuilder(letters.Length);
        char l = '\0', v = '\0', t = '\0'; // the syllable being typed: initial, vowel, final

        void Flush()
        {
            if (l != '\0' && v != '\0')
                sb.Append((char)(First + (Initials.IndexOf(l) * 21 + Vowels.IndexOf(v)) * 28 + (t == '\0' ? 0 : Finals.IndexOf(t))));
            else
            {
                if (l != '\0') sb.Append(l);
                if (v != '\0') sb.Append(v);
            }
            l = v = t = '\0';
        }

        foreach (var c in letters)
        {
            if (IsConsonant(c))
            {
                if (l != '\0' && v != '\0' && t == '\0' && Finals.Contains(c)) t = c;
                else if (t != '\0' && DoubleFinals.TryGetValue((t, c), out var tt)) t = tt;
                else
                {
                    Flush();
                    l = c;
                }
            }
            else if (IsVowel(c))
            {
                if (t != '\0')
                {
                    // The final moves to the new syllable (for a double final, only its second part).
                    var pair = DoubleFinals.FirstOrDefault(p => p.Value == t).Key;
                    char moved = pair == default ? t : pair.Item2;
                    t = pair == default ? '\0' : pair.Item1;
                    Flush();
                    l = moved;
                    v = c;
                }
                else if (v != '\0' && DoubleVowels.TryGetValue((v, c), out var vv)) v = vv;
                else if (l != '\0' && v == '\0') v = c;
                else
                {
                    Flush();
                    v = c;
                }
            }
            else
            {
                Flush();
                sb.Append(c);
            }
        }
        Flush();
        return sb.ToString();
    }

    /// <summary>Splits syllables back into the letters typed for them, one key each (안녕 = ㅇㅏㄴㄴㅕㅇ).</summary>
    public static string Decompose(string text)
    {
        var sb = new StringBuilder(text.Length * 3);
        foreach (var c in text)
        {
            if (!IsSyllable(c))
            {
                AppendKeys(sb, c);
                continue;
            }
            int i = c - First;
            sb.Append(Initials[i / (21 * 28)]);
            AppendKeys(sb, Vowels[i / 28 % 21]);
            if (i % 28 != 0) AppendKeys(sb, Finals[i % 28]);
        }
        return sb.ToString();
    }

    private static void AppendKeys(StringBuilder sb, char letter)
    {
        var pair = DoubleVowels.Concat(DoubleFinals).FirstOrDefault(p => p.Value == letter).Key;
        if (pair == default) sb.Append(letter);
        else sb.Append(pair.Item1).Append(pair.Item2);
    }
}
