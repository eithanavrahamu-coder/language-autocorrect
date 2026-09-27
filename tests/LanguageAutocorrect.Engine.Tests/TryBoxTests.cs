using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class TryBoxTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TryBox Box(params Lang[] others) =>
        new(Shared.Detector, [Lang.English, .. others], Sensitivity.Medium);

    /// <summary>Types keys as US-keyboard characters: a space is Space, a capital is Shift + the key.</summary>
    private static TypingAction Type(TryBox box, string keys, DateTime? now = null)
    {
        TypingAction last = PassThrough.Instance;
        foreach (var c in keys)
        {
            var key = c == ' ' ? KeyInput.Space
                : char.IsUpper(c) ? KeyInput.Word(char.ToLowerInvariant(c), shifted: true)
                : KeyInput.Word(c);
            last = box.Press(key, now ?? T0);
        }
        return last;
    }

    [Fact]
    public void StartsEmptyOnEnglish()
    {
        var box = Box(Lang.Hebrew);
        Assert.Equal("", box.Text);
        Assert.Equal(Lang.English, box.Layout);
    }

    [Fact]
    public void FixesWordAndSwitchesKeyboard()
    {
        var box = Box(Lang.Hebrew);
        Type(box, "aku");
        Assert.Equal("aku", box.Text);
        var fix = Assert.IsType<FixWord>(Type(box, "o "));
        Assert.Equal("שלום", fix.Text);
        Assert.Equal("שלום ", box.Text);
        Assert.Equal(Lang.Hebrew, box.Layout);
    }

    [Fact]
    public void KeepsTypingOnTheNewKeyboard()
    {
        var box = Box(Lang.Russian);
        Type(box, "ghbdtn rfr ");
        Assert.Equal("привет как ", box.Text);
        Assert.Equal(Lang.Russian, box.Layout);
    }

    [Fact]
    public void BackspaceRightAfterAFixPutsTheWordBack()
    {
        var box = Box(Lang.Hebrew);
        Type(box, "akuo ");
        Assert.IsType<UndoFix>(box.Press(KeyInput.Backspace, T0.AddMilliseconds(300)));
        Assert.Equal("akuo ", box.Text);
        Assert.Equal(Lang.English, box.Layout);
    }

    [Fact]
    public void CtrlZUndoesAFewSecondsLater()
    {
        var box = Box(Lang.Hebrew);
        Type(box, "akuo ");
        Assert.IsType<UndoFix>(box.Press(KeyInput.CtrlZ, T0.AddSeconds(3)));
        Assert.Equal("akuo ", box.Text);
    }

    [Fact]
    public void RealWordsAreLeftAlone()
    {
        var box = Box(Lang.Hebrew);
        Assert.IsType<PassThrough>(Type(box, "Hello there "));
        Assert.Equal("Hello there ", box.Text);
        Assert.Equal(Lang.English, box.Layout);
    }

    [Fact]
    public void BackspaceDeletesLettersThenGoesBackIntoTheWordBefore()
    {
        var box = Box(Lang.Hebrew);
        Type(box, "hi ab");
        box.Press(KeyInput.Backspace, T0);
        Assert.Equal("hi a", box.Text);
        box.Press(KeyInput.Backspace, T0);
        box.Press(KeyInput.Backspace, T0);
        Assert.Equal("hi", box.Text);
        box.Press(KeyInput.Backspace, T0);
        Assert.Equal("h", box.Text);
    }
}
