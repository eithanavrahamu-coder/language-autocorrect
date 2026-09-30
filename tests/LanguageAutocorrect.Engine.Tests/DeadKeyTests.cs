using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

/// <summary>
/// Accent (dead) keys: Windows joins an accent with only some letters (´ + e = é) and types the accent and then the
/// letter for the rest (´ + m = ´m). What was checked with Windows itself (ToUnicodeEx) on each layout.
/// </summary>
public class DeadKeyTests
{
    private static readonly DateTime T0 = new(2026, 1, 1);

    [Theory]
    [InlineData("'e", Lang.Spanish, "é")]
    [InlineData("'y", Lang.Spanish, "ý")]
    [InlineData("'m", Lang.Spanish, "´m")]      // Windows has no ḿ
    [InlineData("'s", Lang.Spanish, "´s")]
    [InlineData("'l", Lang.Spanish, "´l")]
    [InlineData("'n", Lang.Spanish, "´n")]      // ...nor ń (ñ has its own key)
    [InlineData("'c", Lang.Spanish, "´c")]
    [InlineData("\"u", Lang.Spanish, "ü")]
    [InlineData("\"Y", Lang.Spanish, "¨Y")]     // ÿ joins, its capital Ÿ doesn't
    [InlineData("[a", Lang.Spanish, "à")]
    [InlineData("'o", Lang.Portuguese, "õ")]
    [InlineData("'n", Lang.Portuguese, "ñ")]
    [InlineData("'v", Lang.Portuguese, "~v")]   // Windows has no ṽ
    [InlineData("'e", Lang.Portuguese, "~e")]   // ...nor ẽ
    [InlineData("=n", Lang.German, "´n")]
    [InlineData("=E", Lang.German, "É")]
    [InlineData("`o", Lang.German, "ô")]
    [InlineData("`s", Lang.German, "^s")]
    [InlineData("[y", Lang.French, "^y")]
    [InlineData(";a", Lang.Greek, "ά")]
    [InlineData(";k", Lang.Greek, "΄κ")]
    [InlineData("-g", Lang.Serbian, "ѓ")]
    [InlineData("-k", Lang.Serbian, "ќ")]
    [InlineData("-a", Lang.Serbian, "'а")]
    public void AccentJoinsOnlyTheLettersWindowsJoins(string keys, Lang lang, string typed) =>
        Assert.Equal(typed, KeyMap.Render(keys, lang));

    [Theory]
    [InlineData("''e", Lang.Spanish, "´´e")]
    [InlineData("'[e", Lang.Spanish, "´`e")]
    [InlineData("''o", Lang.Portuguese, "~~o")]
    public void TwoAccentKeysTypeBothAccents(string keys, Lang lang, string typed) =>
        Assert.Equal(typed, KeyMap.Render(keys, lang));

    [Theory]
    [InlineData("i'm", Lang.Spanish, "i´m", "i'm")]
    [InlineData("i've", Lang.Portuguese, "i~ve", "i've")]
    public void FixDeletesWhatIsOnScreen(string keys, Lang layout, string onScreen, string word)
    {
        // An English contraction typed on a keyboard where ' is an accent key.
        var s = new TypingSession(Shared.Detector, new NeverFixList()) { Languages = [Lang.English, layout] };
        foreach (var c in keys) s.OnKey(KeyInput.Word(c), layout, true, Sensitivity.Medium, T0);
        var fix = Assert.IsType<FixWord>(s.OnKey(KeyInput.Space, layout, true, Sensitivity.Medium, T0));
        Assert.Equal(onScreen, fix.Correction.Typed);
        Assert.Equal(onScreen.Length, fix.Backspaces);
        Assert.Equal(word, fix.Text);

        // Undoing types back exactly what was there.
        var undo = Assert.IsType<UndoFix>(s.OnKey(KeyInput.Backspace, Lang.English, true, Sensitivity.Medium, T0.AddMilliseconds(200)));
        Assert.Equal(onScreen + " ", undo.Text);
    }
}

[CollectionDefinition(nameof(KeyboardChanges), DisableParallelization = true)]
public class KeyboardChanges;

/// <summary>Tests that replace a keyboard, as the app does with the user's real layouts (not run alongside others).</summary>
[Collection(nameof(KeyboardChanges))]
public class InstalledKeyboardTests
{
    [Fact]
    public void AccentsJoinAsTheInstalledKeyboardSays()
    {
        // Latin American Spanish joins ´ with c into ç; whatever Windows didn't list is typed as the accent, then the letter.
        var info = Languages.Get(Lang.Spanish);
        var joins = new Dictionary<(char Accent, char Letter), char> { [('´', 'e')] = 'é', [('´', 'c')] = 'ç' };
        try
        {
            KeyMap.SetKeyboard(Lang.Spanish, info.Keyboard.Split(' '), info.ShiftKeyboard!.Split(' '), joins);
            Assert.Equal("ç", KeyMap.Render("'c", Lang.Spanish));
            Assert.Equal("é", KeyMap.Render("'e", Lang.Spanish));
            Assert.Equal("´a", KeyMap.Render("'a", Lang.Spanish));
            Assert.Equal("´´e", KeyMap.Render("''e", Lang.Spanish));
        }
        finally
        {
            KeyMap.ResetKeyboards();
        }
        Assert.Equal("á", KeyMap.Render("'a", Lang.Spanish));
    }
}
