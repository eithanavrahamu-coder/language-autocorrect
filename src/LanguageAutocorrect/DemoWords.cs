using System.Collections.Generic;
using System.Linq;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect;

/// <summary>
/// A word per language for the typing demos (the setup window, and the app's Home before anything was fixed), shown
/// typed on the English keyboard and then fixed. Each is one the engine's tests check it fixes (MultiLanguageTests),
/// or one of the website's checked examples.
/// </summary>
internal static class DemoWords
{
    private static readonly Dictionary<Lang, string> Words = new()
    {
        [Lang.Hebrew] = "שלום", [Lang.Russian] = "привет", [Lang.Arabic] = "مرحبا", [Lang.Ukrainian] = "привіт",
        [Lang.Persian] = "سلام", [Lang.Greek] = "καλημέρα", [Lang.French] = "aller", [Lang.German] = "schön",
        [Lang.Bulgarian] = "здравей", [Lang.Serbian] = "здраво", [Lang.Macedonian] = "здраво", [Lang.Kazakh] = "сәлем",
        [Lang.Georgian] = "გამარჯობა", [Lang.Armenian] = "բարև", [Lang.Korean] = "안녕", [Lang.Thai] = "สวัสดี",
        [Lang.Spanish] = "mañana", [Lang.Portuguese] = "não", [Lang.Turkish] = "güzel", [Lang.Italian] = "città",
        [Lang.Urdu] = "شکریہ",
    };

    // Only the words the English keyboard's keys really type back.
    private static readonly Dictionary<Lang, object> Demos = Words
        .Select(w => (w.Key, w.Value, Keys: KeyMap.ToUsKeys(w.Value, w.Key)))
        .Where(w => KeyMap.Render(w.Keys, w.Key) == w.Value)
        .ToDictionary(w => w.Key, w => (object)new { word = w.Value, keys = w.Keys });

    /// <summary>The demo word for a language and the English-keyboard keys that type it, if it has one.</summary>
    public static object? For(LanguageInfo language) => Demos.GetValueOrDefault(language.Lang);
}
