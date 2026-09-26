namespace LayoutBuddy.Engine;

public enum Lang { English, Hebrew, Russian, Arabic, Ukrainian, Persian, Greek, French, German, Bulgarian, Serbian, Macedonian, Kazakh, Georgian, Armenian, Korean, Thai, Spanish, Portuguese, Turkish }

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

    /// <summary>
    /// What each physical key types with Shift, for keyboards where Shift types letters of their own rather than
    /// capitals (Georgian, Thai, Korean) or accents of their own (Spanish ¨). "�" marks a key that types nothing.
    /// </summary>
    public string? ShiftKeyboard { get; init; }

    /// <summary>Letters join into syllable blocks as they are typed (Korean, see <see cref="Hangul"/>).</summary>
    public bool JoinsSyllables { get; init; }

    /// <summary>Written without spaces between words (Thai): text typed before Space is often a whole phrase.</summary>
    public bool WithoutSpaces { get; init; }

    /// <summary>Words are joined by an apostrophe (English don't, French l'homme, Italian l'uomo).</summary>
    public bool JoinsWithApostrophe { get; init; }

    /// <summary>Less proven so far (small word list, or not yet tried on a real Windows keyboard): shown as "Beta".</summary>
    public bool Beta { get; init; }

    /// <summary>Turkish: dotted i/İ and dotless ı/I are different letters, so their capitals differ from English.</summary>
    public bool DottedI { get; init; }

    public bool IsLetter(char c) => _letters.Contains(HasCase ? ToLower(c) : c);

    public char ToUpper(char c) => DottedI && c is 'i' or 'ı' ? (c == 'i' ? 'İ' : 'I') : char.ToUpperInvariant(c);

    public char ToLower(char c) => DottedI && c is 'I' or 'İ' ? (c == 'I' ? 'ı' : 'i') : char.ToLowerInvariant(c);

    public string ToLower(string s) => DottedI ? string.Concat(s.Select(ToLower)) : s.ToLowerInvariant();
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
    private const string Grave = "~̀`";
    private const string Diaeresis = "~̈¨";
    private const string Tilde = "~̃~";

    // Syllables, plus the letters they are made of: letters that didn't join a syllable are part of the word.
    private static readonly string HangulSyllables =
        string.Concat(Enumerable.Range('가', '힣' - '가' + 1).Select(c => (char)c)) +
        "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    // Thai consonants, vowels and tone marks (not the Thai digits or the baht sign).
    private static readonly string ThaiLetters =
        string.Concat(Enumerable.Range(0x0E01, 0x0E3A - 0x0E01 + 1).Concat(Enumerable.Range(0x0E40, 0x0E4E - 0x0E40 + 1)).Select(c => (char)c));

    public static readonly IReadOnlyList<LanguageInfo> All =
    [
        new(Lang.English, "en", "English", "English", "EN", "#2563EB", 0x09,
            "abcdefghijklmnopqrstuvwxyz'", true, false,
            "` 1 2 3 4 5 6 7 8 9 0 - = q w e r t y u i o p [ ] \\ a s d f g h j k l ; ' z x c v b n m , . /")
        {
            JoinsWithApostrophe = true,
        },

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
            $"² & é \" ' ( - è _ ç à ) = a z e r t y u i o p {Circumflex} $ * q s d f g h j k l m ù w x c v b n , ; : !")
        {
            JoinsWithApostrophe = true,
        },

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
            "( \" ә і ң ғ , . ү ұ қ ө һ й ц у к е н г ш щ з х ъ \\ ф ы в а п р о л д ж э я ч с м и т ь б ю №")
        {
            Beta = true,
        },

        // Windows' default "Georgian (QWERTY)" layout, which types თ შ ჭ ღ ჟ ძ ჩ with Shift.
        new(Lang.Georgian, "ka", "Georgian", "ქართული", "ქა", "#A21CAF", 0x37,
            "აბგდევზთიკლმნოპჟრსტუფქღყშჩცძწჭხჯჰ", false, false,
            "„ 1 2 3 4 5 6 7 8 9 0 - = ქ წ ე რ ტ ყ უ ი ო პ [ ] ~ ა ს დ ფ გ ჰ ჯ კ ლ ; ' ზ ხ ც ვ ბ ნ მ , . /")
        {
            ShiftKeyboard = "“ ! @ # $ % ^ & * ( ) _ + � ჭ � ღ თ � � � � � { } | � შ � � � � ჟ � ₾ : \" ძ � ჩ � � N � < > ?",
            Beta = true,
        },

        // Windows' default "Armenian Phonetic" layout.
        new(Lang.Armenian, "hy", "Armenian", "Հայերեն", "ՀԱ", "#4D7C0F", 0x2B,
            "աբգդեզէըթժիլխծկհձղճմյնշոչպջռսվտրցւփքօֆև", true, false,
            "՝ է թ փ ձ ջ ւ և ր չ ճ - ժ ք ո ե ռ տ ը ւ ի օ պ խ ծ շ ա ս դ ֆ գ հ յ կ լ ; ՛ զ ղ ց վ բ ն մ , ․ /")
        {
            Beta = true,
        },

        // The standard 2-set (Dubeolsik) layout of the Korean IME. Words are whole syllables; the letters
        // (jamo) the keys type are joined into them.
        new(Lang.Korean, "ko", "Korean", "한국어", "한", "#475569", 0x12,
            HangulSyllables, false, false,
            "` 1 2 3 4 5 6 7 8 9 0 - = ㅂ ㅈ ㄷ ㄱ ㅅ ㅛ ㅕ ㅑ ㅐ ㅔ [ ] \\ ㅁ ㄴ ㅇ ㄹ ㅎ ㅗ ㅓ ㅏ ㅣ ; ' ㅋ ㅌ ㅊ ㅍ ㅠ ㅜ ㅡ , . /")
        {
            ShiftKeyboard = "~ ! @ # $ % ^ & * ( ) _ + ㅃ ㅉ ㄸ ㄲ ㅆ ㅛ ㅕ ㅑ ㅒ ㅖ { } | ㅁ ㄴ ㅇ ㄹ ㅎ ㅗ ㅓ ㅏ ㅣ : \" ㅋ ㅌ ㅊ ㅍ ㅠ ㅜ ㅡ < > ?",
            JoinsSyllables = true,
            Beta = true,
        },

        // Windows' default "Thai Kedmanee" layout, which types many letters with Shift (ธ, ซ, ู...).
        new(Lang.Thai, "th", "Thai", "ไทย", "ไท", "#DB2777", 0x1E,
            ThaiLetters, false, false,
            "_ ๅ / - ภ ถ ุ ึ ค ต จ ข ช ๆ ไ ำ พ ะ ั ี ร น ย บ ล ฃ ฟ ห ก ด เ ้ ่ า ส ว ง ผ ป แ อ ิ ื ท ม ใ ฝ")
        {
            ShiftKeyboard = "% + ๑ ๒ ๓ ๔ ู ฿ ๕ ๖ ๗ ๘ ๙ ๐ \" ฎ ฑ ธ ํ ๊ ณ ฯ ญ ฐ , ฅ ฤ ฆ ฏ โ ฌ ็ ๋ ษ ศ ซ . ( ) ฉ ฮ ฺ ์ ? ฒ ฬ ฦ",
            WithoutSpaces = true,
            Beta = true,
        },

        // Windows' "Spanish" layout (Spain); Latin American is read from Windows at runtime.
        // Shift types an accent of its own: ¨ (pingüino).
        new(Lang.Spanish, "es", "Spanish", "Español", "ES", "#D97706", 0x0A,
            "abcdefghijklmnopqrstuvwxyzáéíóúüñ", true, false,
            $"º 1 2 3 4 5 6 7 8 9 0 ' ¡ q w e r t y u i o p {Grave} + ç a s d f g h j k l ñ {Acute} z x c v b n m , . -")
        {
            ShiftKeyboard = $"ª ! \" · $ % & / ( ) = ? ¿ Q W E R T Y U I O P {Circumflex} * Ç A S D F G H J K L Ñ {Diaeresis} Z X C V B N M ; : _",
        },

        // Windows' "Portuguese (Brazil ABNT2)" layout; the Portugal layout is read from Windows at runtime.
        // Shift types accents of its own: ^ (você) and ` (à).
        new(Lang.Portuguese, "pt", "Portuguese", "Português", "PT", "#15803D", 0x16,
            "abcdefghijklmnopqrstuvwxyzáâãàçéêíóôõúü", true, false,
            $"' 1 2 3 4 5 6 7 8 9 0 - = q w e r t y u i o p {Acute} [ ] a s d f g h j k l ç {Tilde} z x c v b n m , . ;")
        {
            ShiftKeyboard = $"\" ! @ # $ % {Diaeresis} & * ( ) _ + Q W E R T Y U I O P {Grave} {{ }} A S D F G H J K L Ç {Circumflex} Z X C V B N M < > :",
        },

        // Windows' default "Turkish Q" layout: the I key types ı, and i is where English has '.
        new(Lang.Turkish, "tr", "Turkish", "Türkçe", "TR", "#991B1B", 0x1F,
            "abcçdefgğhıijklmnoöprsştuüvyz", true, false,
            "\" 1 2 3 4 5 6 7 8 9 0 * - q w e r t y u ı o p ğ ü , a s d f g h j k l ş i z x c v b n m ö ç .")
        {
            DottedI = true,
        },
    ];

    private static readonly Dictionary<Lang, LanguageInfo> ByLang = All.ToDictionary(l => l.Lang);

    public static LanguageInfo Get(Lang lang) => ByLang[lang];

    public static LanguageInfo? FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <param name="langId">The Windows language id (LANGID) of a keyboard layout, or just its primary id.</param>
    public static LanguageInfo? FromWindowsLangId(int langId) =>
        All.FirstOrDefault(l => l.WindowsFullLangIds?.Contains(langId) ?? l.WindowsLangId == (langId & 0x3FF));
}
