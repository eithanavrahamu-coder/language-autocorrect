namespace LayoutBuddy.Engine;

public enum Lang { English, Hebrew, Russian, Arabic, Ukrainian, Persian, Greek, French, German, Bulgarian, Serbian, Macedonian, Kazakh }

/// <summary>Everything the app knows about one supported language.</summary>
public sealed record LanguageInfo(
    Lang Lang,
    string Code,          // ISO 639-1, also the word-list file name
    string Name,          // English name
    string NativeName,
    string Badge,         // short label for the cursor indicator and tray icon
    string Color,         // badge color, #RRGGBB
    int WindowsLangId,    // primary language id of the Windows keyboard layout
    string Alphabet,      // lowercase letters that can appear in words
    bool HasCase,
    bool RightToLeft,
    string Keyboard)      // what each physical key types (see KeyMap.PhysicalKeys), space separated
{
    private readonly HashSet<char> _letters = new(Alphabet);

    /// <summary>
    /// Full Windows language ids (LANGIDs) to match instead of the primary id, for a language that shares its
    /// primary id with others (Serbian Cyrillic shares 0x1A with Croatian, Bosnian and Serbian Latin).
    /// </summary>
    public IReadOnlyList<int>? WindowsFullLangIds { get; init; }

    public bool IsLetter(char c) => _letters.Contains(HasCase ? char.ToLowerInvariant(c) : c);
}

public static class Languages
{
    // Physical key order (US names):
    //   ` 1 2 3 4 5 6 7 8 9 0 - =   q w e r t y u i o p [ ] \   a s d f g h j k l ; '   z x c v b n m , . /
    // A token starting with "~" is a dead key: "~" + combining accent + the accent shown when nothing follows.
    private const string Acute = "~́´";
    private const string Circumflex = "~̂^";
    private const string Tonos = "~́΄";
    private const string Apostrophe = "~́'";

    public static readonly IReadOnlyList<LanguageInfo> All =
    [
        new(Lang.English, "en", "English", "English", "EN", "#2563EB", 0x09,
            "abcdefghijklmnopqrstuvwxyz'", true, false,
            "` 1 2 3 4 5 6 7 8 9 0 - = q w e r t y u i o p [ ] \\ a s d f g h j k l ; ' z x c v b n m , . /"),

        new(Lang.Hebrew, "he", "Hebrew", "עברית", "עב", "#16A34A", 0x0D,
            "אבגדהוזחטיכךלמםנןסעפףצץקרשת'", false, true,
            "; 1 2 3 4 5 6 7 8 9 0 - = / ' ק ר א ט ו ן ם פ ] [ \\ ש ד ג כ ע י ח ל ך ף , ז ס ב ה נ מ צ ת ץ ."),

        new(Lang.Russian, "ru", "Russian", "Русский", "РУ", "#DC2626", 0x19,
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя", true, false,
            "ё 1 2 3 4 5 6 7 8 9 0 - = й ц у к е н г ш щ з х ъ \\ ф ы в а п р о л д ж э я ч с м и т ь б ю ."),

        new(Lang.Arabic, "ar", "Arabic", "العربية", "ع", "#0D9488", 0x01,
            "ءآأؤإئابةتثجحخدذرزسشصضطظعغفقكلمنهوىي", false, true,
            "ذ 1 2 3 4 5 6 7 8 9 0 - = ض ص ث ق ف غ ع ه خ ح ج د \\ ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ"),

        new(Lang.Ukrainian, "uk", "Ukrainian", "Українська", "УК", "#CA8A04", 0x22,
            "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя'", true, false,
            "' 1 2 3 4 5 6 7 8 9 0 - = й ц у к е н г ш щ з х ї ґ ф і в а п р о л д ж є я ч с м и т ь б ю ."),

        new(Lang.Persian, "fa", "Persian", "فارسی", "فا", "#9333EA", 0x29,
            "ءآأؤئابتثجحخدذرزسشصضطظعغفقلمنهوپچژکگی", false, true,
            "` 1 2 3 4 5 6 7 8 9 0 - = ض ص ث ق ف غ ع ه خ ح ج چ پ ش س ی ب ل ا ت ن م ک گ ظ ط ز ر ذ د ئ و . /"),

        new(Lang.Greek, "el", "Greek", "Ελληνικά", "ΕΛ", "#0284C7", 0x08,
            "αβγδεζηθικλμνξοπρσςτυφχψωάέήίόύώϊϋΐΰ", true, false,
            $"` 1 2 3 4 5 6 7 8 9 0 - = ; ς ε ρ τ υ θ ι ο π [ ] \\ α σ δ φ γ η ξ κ λ {Tonos} ' ζ χ ψ ω β ν μ , . /"),

        new(Lang.French, "fr", "French", "Français", "FR", "#4F46E5", 0x0C,
            "abcdefghijklmnopqrstuvwxyzàâæçéèêëîïôœùûüÿ'", true, false,
            $"² & é \" ' ( - è _ ç à ) = a z e r t y u i o p {Circumflex} $ * q s d f g h j k l m ù w x c v b n , ; : !"),

        new(Lang.German, "de", "German", "Deutsch", "DE", "#EA580C", 0x07,
            "abcdefghijklmnopqrstuvwxyzäöüß", true, false,
            $"{Circumflex} 1 2 3 4 5 6 7 8 9 0 ß {Acute} q w e r t z u i o p ü + # a s d f g h j k l ö ä y x c v b n m , . -"),

        // Windows' default "Bulgarian" layout (BDS); Phonetic and Typewriter are read from Windows at runtime.
        new(Lang.Bulgarian, "bg", "Bulgarian", "Български", "БГ", "#047857", 0x02,
            "абвгдежзийклмнопрстуфхцчшщъьюяѝ", true, false,
            "( 1 2 3 4 5 6 7 8 9 0 - . , у е и ш щ к с д з ц ; „ ь я а о ж г т н в м ч ю й ъ э ф х п р л б"),

        new(Lang.Serbian, "sr", "Serbian (Cyrillic)", "Српски", "СР", "#BE123C", 0x1A,
            "абвгдђежзијклљмнњопрстћуфхцчџш", true, false,
            $"` 1 2 3 4 5 6 7 8 9 0 {Apostrophe} + љ њ е р т з у и о п ш ђ ж а с д ф г х ј к л ч ћ ѕ џ ц в б н м , . -")
        {
            WindowsFullLangIds = [0x0C1A, 0x1C1A, 0x281A, 0x301A],
        },

        new(Lang.Macedonian, "mk", "Macedonian", "Македонски", "МК", "#B45309", 0x2F,
            "абвгдѓежзѕијклљмнњопрстќуфхцчџшѝѐ", true, false,
            "ѝ 1 2 3 4 5 6 7 8 9 0 - = љ њ е р т ѕ у и о п ш ѓ ж а с д ф г х ј к л ч ќ з џ ц в б н м , . /"),

        new(Lang.Kazakh, "kk", "Kazakh", "Қазақша", "ҚЗ", "#0E7490", 0x3F,
            "аәбвгғдеёжзийкқлмнңоөпрстуұүфхһцчшщъыіьэюя", true, false,
            "( \" ә і ң ғ , . ү ұ қ ө һ й ц у к е н г ш щ з х ъ \\ ф ы в а п р о л д ж э я ч с м и т ь б ю №"),
    ];

    private static readonly Dictionary<Lang, LanguageInfo> ByLang = All.ToDictionary(l => l.Lang);

    public static LanguageInfo Get(Lang lang) => ByLang[lang];

    public static LanguageInfo? FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <param name="langId">The Windows language id (LANGID) of a keyboard layout, or just its primary id.</param>
    public static LanguageInfo? FromWindowsLangId(int langId) =>
        All.FirstOrDefault(l => l.WindowsFullLangIds?.Contains(langId) ?? l.WindowsLangId == (langId & 0x3FF));
}
