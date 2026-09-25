using LayoutBuddy.Engine;

namespace LayoutBuddy.Engine.Tests;

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

    private static TypingSession NewSession(int undosToBlock = 3) =>
        new(Shared.Detector, new NeverFixList(undosToBlock: undosToBlock));

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
