using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class NeverFixListTests
{
    [Fact]
    public void NotBlockedUntilThreshold()
    {
        var list = new NeverFixList(undosToBlock: 3);
        Assert.False(list.RecordUndo("akuo"));
        Assert.False(list.IsBlocked("akuo"));
        Assert.False(list.RecordUndo("akuo"));
        Assert.False(list.IsBlocked("akuo"));
        Assert.True(list.RecordUndo("akuo"));
        Assert.True(list.IsBlocked("akuo"));
        Assert.False(list.RecordUndo("akuo")); // already blocked, not "newly" blocked
    }

    [Fact]
    public void CaseInsensitiveAndRemovable()
    {
        var list = new NeverFixList(undosToBlock: 1);
        list.RecordUndo("Akuo");
        Assert.True(list.IsBlocked("akuo"));
        list.Remove("AKUO");
        Assert.False(list.IsBlocked("akuo"));
    }

    [Fact]
    public void ManualBlockAndClear()
    {
        var list = new NeverFixList();
        list.Block("xyz");
        Assert.True(list.IsBlocked("xyz"));
        Assert.Equal(["xyz"], list.BlockedWords());
        list.Clear();
        Assert.Empty(list.BlockedWords());
    }

    [Fact]
    public void RaisingThresholdUnblocks()
    {
        var list = new NeverFixList(new Dictionary<string, int> { ["abc"] = 1 }, undosToBlock: 1);
        Assert.True(list.IsBlocked("abc"));
        list.UndosToBlock = 3;
        Assert.False(list.IsBlocked("abc"));
    }
}

public class UndoTrackerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1);
    private static readonly Correction C = new("akuo", "akuo", "שלום", Lang.English, Lang.Hebrew, true);

    [Fact]
    public void BackspaceRightAwayUndoes()
    {
        var u = new UndoTracker();
        u.Arm(C, T0);
        Assert.Same(C, u.OnKey(UndoKey.Backspace, T0.AddMilliseconds(500)));
    }

    [Fact]
    public void LateBackspaceDoesNotUndo()
    {
        var u = new UndoTracker();
        u.Arm(C, T0);
        Assert.Null(u.OnKey(UndoKey.Backspace, T0.AddSeconds(3)));
    }

    [Fact]
    public void CtrlZHasLongerWindow()
    {
        var u = new UndoTracker();
        u.Arm(C, T0);
        Assert.Same(C, u.OnKey(UndoKey.CtrlZ, T0.AddSeconds(4)));
    }

    [Fact]
    public void OtherKeyFirstCancelsUndo()
    {
        var u = new UndoTracker();
        u.Arm(C, T0);
        Assert.Null(u.OnKey(null, T0.AddMilliseconds(100)));
        Assert.Null(u.OnKey(UndoKey.Backspace, T0.AddMilliseconds(200)));
    }
}

public class TypingSessionTests
{
    private static readonly DateTime T0 = new(2026, 1, 1);

    private static TypingSession NewSession(int undosToBlock = 3, bool learn = true) =>
        new(Shared.Detector, new NeverFixList(undosToBlock: undosToBlock)) { LearnFromUndos = learn };

    private static TypingAction TypeWords(TypingSession s, string text, Lang layout, DateTime at)
    {
        TypingAction last = PassThrough.Instance;
        foreach (var c in text)
            last = s.OnKey(c == ' ' ? KeyInput.Space : KeyInput.Word(c), layout, true, Sensitivity.Medium, at);
        return last;
    }

    private static TypingAction Type(TypingSession s, string keys, KeyInput boundary, Lang layout, DateTime at)
    {
        foreach (var c in keys) s.OnKey(KeyInput.Word(c), layout, true, Sensitivity.Medium, at);
        return s.OnKey(boundary, layout, true, Sensitivity.Medium, at);
    }

    [Fact]
    public void FixesOnSpace()
    {
        var s = NewSession();
        var a = Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0));
        Assert.Equal(4, a.Backspaces);
        Assert.Equal("שלום", a.Text);
        Assert.Equal(Lang.Hebrew, a.Layout);
        Assert.True(a.Swallow);
    }

    [Fact]
    public void BackspaceDuringWordIsTracked()
    {
        var s = NewSession();
        foreach (var c in "akuox") s.OnKey(KeyInput.Word(c), Lang.English, true, Sensitivity.Medium, T0);
        s.OnKey(KeyInput.Backspace, Lang.English, true, Sensitivity.Medium, T0);
        var a = Assert.IsType<FixWord>(s.OnKey(KeyInput.Space, Lang.English, true, Sensitivity.Medium, T0));
        Assert.Equal("שלום", a.Text);
    }

    [Fact]
    public void ShiftedWordIsNotFixed()
    {
        var s = NewSession();
        s.OnKey(KeyInput.Word('a', shifted: true), Lang.English, true, Sensitivity.Medium, T0);
        foreach (var c in "kuo") s.OnKey(KeyInput.Word(c), Lang.English, true, Sensitivity.Medium, T0);
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Space, Lang.English, true, Sensitivity.Medium, T0));
    }

    [Fact]
    public void DisabledAutoCorrectPassesThrough()
    {
        var s = NewSession();
        foreach (var c in "akuo") s.OnKey(KeyInput.Word(c), Lang.English, false, Sensitivity.Medium, T0);
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Space, Lang.English, false, Sensitivity.Medium, T0));
    }

    [Fact]
    public void ImmediateBackspaceUndoesButDoesNotBlockUntilThirdTime()
    {
        var s = NewSession(undosToBlock: 3);
        for (int i = 1; i <= 3; i++)
        {
            var t = T0.AddMinutes(i);
            Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, t));
            var undo = Assert.IsType<UndoFix>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, t.AddMilliseconds(300)));
            Assert.Equal(5, undo.Backspaces);           // "שלום" + space
            Assert.Equal("akuo ", undo.Text);
            Assert.Equal(Lang.English, undo.Layout);
            Assert.Equal(i == 3, undo.NowBlocked);
        }
        // Blocked now: no more fixes for this word.
        Assert.IsType<PassThrough>(Type(s, "akuo", KeyInput.Space, Lang.English, T0.AddMinutes(10)));
    }

    [Fact]
    public void BackspaceAfterTypingMoreIsNotAnUndo()
    {
        // The reported problem: correcting a typo after an auto-correct must not count as an undo.
        var s = NewSession(undosToBlock: 1);
        Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0));
        s.OnKey(KeyInput.Word('s'), Lang.Hebrew, true, Sensitivity.Medium, T0.AddMilliseconds(200));
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, T0.AddMilliseconds(400)));
        Assert.False(s.NeverFix.IsBlocked("akuo"));
        Assert.Equal(0, s.NeverFix.UndoCount("akuo"));
    }

    [Fact]
    public void SlowBackspaceIsNotAnUndo()
    {
        var s = NewSession(undosToBlock: 1);
        Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0));
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, T0.AddSeconds(2)));
        Assert.Equal(0, s.NeverFix.UndoCount("akuo"));
    }

    [Fact]
    public void CtrlZUndoes()
    {
        var s = NewSession();
        Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0));
        Assert.IsType<UndoFix>(s.OnKey(KeyInput.CtrlZ, Lang.Hebrew, true, Sensitivity.Medium, T0.AddSeconds(3)));
        Assert.Equal(1, s.NeverFix.UndoCount("akuo"));
    }

    [Fact]
    public void EnterFixIsNotUndoable()
    {
        var s = NewSession();
        var a = Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Enter, Lang.English, T0));
        Assert.Equal(KeyKind.Enter, a.Boundary);
        Assert.IsType<PassThrough>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, T0.AddMilliseconds(100)));
    }

    [Fact]
    public void UndoDoesNotBlockWhenLearningIsOff()
    {
        var s = NewSession(undosToBlock: 1, learn: false);
        Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0));
        var undo = Assert.IsType<UndoFix>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, T0.AddMilliseconds(100)));
        Assert.False(undo.NowBlocked);
        Assert.False(s.NeverFix.IsBlocked("akuo"));
        Assert.IsType<FixWord>(Type(s, "akuo", KeyInput.Space, Lang.English, T0.AddMinutes(1)));
    }

    [Fact]
    public void FixesEarlierWordsTypedInTheSameWrongLayout()
    {
        // "ha" (יש) is not fixed on its own; "jh" (חי) is, because "ha" reads as Hebrew. Both get fixed together.
        var s = NewSession();
        Assert.IsType<PassThrough>(TypeWords(s, "ha ", Lang.English, T0));
        var a = Assert.IsType<FixWord>(TypeWords(s, "jh ", Lang.English, T0));
        Assert.Equal("יש חי", a.Text);
        Assert.Equal("ha jh".Length, a.Backspaces);
        Assert.Equal(2, a.Correction.WordCount);
        Assert.Equal("jh", a.Correction.TriggerTyped);
    }

    [Fact]
    public void EarlierWordsFixedWhenFirstWordsWereUncertain()
    {
        // Short uncertain words are left alone at first, then fixed once a clear word shows up.
        var s = new TypingSession(Shared.Detector, new NeverFixList());
        foreach (var c in "jh ")
            s.OnKey(c == ' ' ? KeyInput.Space : KeyInput.Word(c), Lang.English, true, Sensitivity.Low, T0);
        TypingAction last = PassThrough.Instance;
        foreach (var c in "akuo ")
            last = s.OnKey(c == ' ' ? KeyInput.Space : KeyInput.Word(c), Lang.English, true, Sensitivity.Low, T0);
        var a = Assert.IsType<FixWord>(last);
        Assert.Equal("חי שלום", a.Text);
    }

    [Fact]
    public void RealEnglishWordsBeforeAreNotTouched()
    {
        var s = NewSession();
        var a = Assert.IsType<FixWord>(TypeWords(s, "hello world akuo ", Lang.English, T0));
        Assert.Equal("שלום", a.Text);
        Assert.Equal(4, a.Backspaces);
    }

    [Fact]
    public void AlreadyFixedWordsAreNotFixedAgain()
    {
        var s = NewSession();
        Assert.IsType<FixWord>(TypeWords(s, "akuo ", Lang.English, T0));
        // Now typing in Hebrew (layout switched); next wrong word must only fix itself.
        TypeWords(s, "hello ", Lang.Hebrew, T0.AddSeconds(5)); // English typed in Hebrew layout -> fixed
        var a = Assert.IsType<FixWord>(TypeWords(s, "akuo ", Lang.English, T0.AddSeconds(10)));
        Assert.Equal("שלום", a.Text);
    }

    [Fact]
    public void UndoRestoresAllFixedWords()
    {
        var s = NewSession();
        Assert.IsType<FixWord>(TypeWords(s, "ha akuo ", Lang.English, T0));
        var u = Assert.IsType<UndoFix>(s.OnKey(KeyInput.Backspace, Lang.Hebrew, true, Sensitivity.Medium, T0.AddMilliseconds(200)));
        Assert.Equal("יש שלום".Length + 1, u.Backspaces);
        Assert.Equal("ha akuo ", u.Text);
    }

    [Fact]
    public void BackspaceIntoPreviousWordKeepsTracking()
    {
        var s = NewSession();
        TypeWords(s, "hello akuk", Lang.English, T0);
        s.OnKey(KeyInput.Backspace, Lang.English, true, Sensitivity.Medium, T0);
        var a = Assert.IsType<FixWord>(TypeWords(s, "o ", Lang.English, T0));
        Assert.Equal("שלום", a.Text);
    }

    [Fact]
    public void WordThatFitsTheSentenceIsFixed()
    {
        // "הוא אוכל far": "far" is English, but after two Hebrew words it means "כשר".
        var s = NewSession();
        TypeWords(s, KeyMap.ToUsKeys("הוא אוכל") + " ", Lang.Hebrew, T0);
        var a = Assert.IsType<FixWord>(TypeWords(s, "far ", Lang.English, T0));
        Assert.Equal("כשר", a.Text);
    }

    [Fact]
    public void SameWordInAnEnglishSentenceStays()
    {
        var s = NewSession();
        Assert.IsType<PassThrough>(TypeWords(s, "it is not far ", Lang.English, T0));
    }

    [Fact]
    public void FirstWordAloneStays()
    {
        var s = NewSession();
        Assert.IsType<PassThrough>(TypeWords(s, "far ", Lang.English, T0));
    }

    [Fact]
    public void OneHebrewWordIsNotEnoughToOverrideACommonEnglishWord()
    {
        var s = NewSession();
        TypeWords(s, KeyMap.ToUsKeys("אוכל") + " ", Lang.Hebrew, T0);
        Assert.IsType<PassThrough>(TypeWords(s, "far ", Lang.English, T0));
    }

    [Fact]
    public void EnglishWordAfterHebrewSentenceWorksBothWays()
    {
        // "I love my" typed correctly, then Hebrew layout by mistake: "גםע" (dog typed in Hebrew) -> dog.
        var s = NewSession();
        TypeWords(s, "i love my ", Lang.English, T0);
        var a = Assert.IsType<FixWord>(TypeWords(s, "dog ", Lang.Hebrew, T0));
        Assert.Equal("dog", a.Text);
    }

    [Fact]
    public void LayoutChangeMidWordResets()
    {
        var s = NewSession();
        s.OnKey(KeyInput.Word('a'), Lang.Hebrew, true, Sensitivity.Medium, T0);
        foreach (var c in "kuo") s.OnKey(KeyInput.Word(c), Lang.English, true, Sensitivity.Medium, T0);
        Assert.Equal("kuo", s.CurrentKeys);
    }
}

public class AppSettingsTests
{
    [Fact]
    public void RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var s = new AppSettings { Sensitivity = Sensitivity.High, UndosToBlock = 4 };
            s.NeverFixUndoCounts["akuo"] = 2;
            s.Save(path);
            var l = AppSettings.Load(path);
            Assert.Equal(Sensitivity.High, l.Sensitivity);
            Assert.Equal(4, l.UndosToBlock);
            Assert.Equal(2, l.NeverFixUndoCounts["akuo"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BadFileGivesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, "{ not json");
        try { Assert.True(AppSettings.Load(path).AutoCorrectEnabled); }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("mstsc", true)]
    [InlineData("MSTSC", true)]
    [InlineData("notepad", false)]
    public void Exclusions(string proc, bool excluded) =>
        Assert.Equal(excluded, new AppSettings().IsExcluded(proc));
}

public class VoiceSettingTests
{
    [Fact]
    public void VoiceIsOffByDefault() => Assert.False(new AppSettings().VoiceEnabled);

    [Fact]
    public void OldSettingsTurnVoiceOffOnce()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ \"VoiceEnabled\": true }");
            var s = AppSettings.Load(path);
            Assert.False(s.VoiceEnabled);

            // Turned back on by the user: stays on.
            s.VoiceEnabled = true;
            s.Save(path);
            Assert.True(AppSettings.Load(path).VoiceEnabled);
        }
        finally { File.Delete(path); }
    }
}
