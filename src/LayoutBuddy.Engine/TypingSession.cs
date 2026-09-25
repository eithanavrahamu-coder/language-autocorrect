using System.Text;

namespace LayoutBuddy.Engine;

public enum KeyKind
{
    /// <summary>A key that can be part of a word (letters and ; ' , . /).</summary>
    WordKey,
    Backspace,
    Space,
    Enter,
    CtrlZ,
    /// <summary>Anything else (arrows, tab, digits, shortcuts, mouse clicks): ends the current word without checking it.</summary>
    Other,
}

public readonly record struct KeyInput(KeyKind Kind, char UsChar = '\0', bool Shifted = false)
{
    public static KeyInput Word(char usChar, bool shifted = false) => new(KeyKind.WordKey, usChar, shifted);
    public static readonly KeyInput Backspace = new(KeyKind.Backspace);
    public static readonly KeyInput Space = new(KeyKind.Space);
    public static readonly KeyInput Enter = new(KeyKind.Enter);
    public static readonly KeyInput CtrlZ = new(KeyKind.CtrlZ);
    public static readonly KeyInput Other = new(KeyKind.Other);
}

public abstract record TypingAction
{
    /// <summary>True if the key that triggered this action must be swallowed.</summary>
    public virtual bool Swallow => true;
}

public sealed record PassThrough : TypingAction
{
    public static readonly PassThrough Instance = new();
    public override bool Swallow => false;
}

/// <summary>Delete <see cref="Backspaces"/> characters, switch to <see cref="Layout"/>, type <see cref="Text"/>, then re-send the boundary key.</summary>
public sealed record FixWord(Correction Correction, int Backspaces, string Text, Lang Layout, KeyKind Boundary) : TypingAction;

/// <summary>Delete <see cref="Backspaces"/> characters, switch to <see cref="Layout"/>, type <see cref="Text"/>.</summary>
public sealed record UndoFix(Correction Correction, int Backspaces, string Text, Lang Layout, bool NowBlocked) : TypingAction;

/// <summary>
/// Tracks the words being typed and decides when to auto-correct or undo.
/// Pure logic, no Windows calls, so it can be unit tested.
/// </summary>
public sealed class TypingSession
{
    /// <summary>How many earlier words (typed in the same wrong layout) can be fixed together with the current one.</summary>
    public const int MaxEarlierWords = 8;

    private readonly WrongLayoutDetector _detector;
    private readonly StringBuilder _keys = new();
    private bool _tainted;
    private Lang? _wordLayout;

    /// <summary>Words already finished with a space, oldest first, as they appear on screen now.</summary>
    private readonly List<(string Keys, Lang Layout, bool Settled)> _history = new();

    public TypingSession(WrongLayoutDetector detector, NeverFixList neverFix, UndoTracker? undo = null)
    {
        _detector = detector;
        NeverFix = neverFix;
        Undo = undo ?? new UndoTracker();
    }

    public NeverFixList NeverFix { get; }
    public UndoTracker Undo { get; }

    /// <summary>When true, undoing the same word enough times adds it to the never-fix list. Off by default.</summary>
    public bool LearnFromUndos { get; set; }

    public string CurrentKeys => _keys.ToString();

    public void Reset()
    {
        _keys.Clear();
        _tainted = false;
        _wordLayout = null;
    }

    /// <summary>Forgets everything (focus change, mouse click, Enter...).</summary>
    public void ResetAll()
    {
        Reset();
        _history.Clear();
        Undo.Disarm();
    }

    public TypingAction OnKey(KeyInput key, Lang layout, bool autoCorrect, Sensitivity sensitivity, DateTime now)
    {
        if (Undo.Pending != null)
        {
            UndoKey? undoKey = key.Kind switch
            {
                KeyKind.Backspace => UndoKey.Backspace,
                KeyKind.CtrlZ => UndoKey.CtrlZ,
                _ => null,
            };
            var toUndo = Undo.OnKey(undoKey, now);
            if (toUndo != null)
            {
                Reset();
                _history.Clear();
                bool blocked = LearnFromUndos && NeverFix.RecordUndo(toUndo.TriggerTyped);
                return new UndoFix(toUndo, toUndo.Replacement.Length + 1, toUndo.Typed + " ", toUndo.From, blocked);
            }
        }

        switch (key.Kind)
        {
            case KeyKind.WordKey:
                if (_wordLayout != null && _wordLayout != layout) Reset();
                _wordLayout = layout;
                _keys.Append(char.ToLowerInvariant(key.UsChar));
                if (key.Shifted) _tainted = true;
                return PassThrough.Instance;

            case KeyKind.Backspace:
                if (_keys.Length > 0)
                {
                    _keys.Length--;
                    if (_keys.Length == 0) Reset();
                }
                else if (_history.Count > 0)
                {
                    // Deleted the space: go back into the previous word.
                    var prev = _history[^1];
                    _history.RemoveAt(_history.Count - 1);
                    _keys.Append(prev.Keys);
                    _wordLayout = prev.Layout;
                }
                return PassThrough.Instance;

            case KeyKind.Space:
                var fix = autoCorrect ? TryFix(layout, sensitivity, KeyKind.Space, now) : null;
                if (fix == null) PushCurrentWord(settled: false);
                Reset();
                return (TypingAction?)fix ?? PassThrough.Instance;

            case KeyKind.Enter:
                var fixEnter = autoCorrect ? TryFix(layout, sensitivity, KeyKind.Enter, now) : null;
                ResetAllButUndo();
                return (TypingAction?)fixEnter ?? PassThrough.Instance;

            default:
                ResetAllButUndo();
                return PassThrough.Instance;
        }
    }

    private void ResetAllButUndo()
    {
        Reset();
        _history.Clear();
    }

    private void PushCurrentWord(bool settled)
    {
        if (_keys.Length == 0 || _wordLayout == null || _tainted)
        {
            // A double space or a word we can't reason about breaks the chain.
            _history.Clear();
            return;
        }
        _history.Add((_keys.ToString(), _wordLayout.Value, settled));
        if (_history.Count > MaxEarlierWords + 4) _history.RemoveAt(0);
    }

    /// <summary>
    /// Looks at up to 3 previous words in this sentence. Returns how many more of them are in the other
    /// language than in <paramref name="layout"/>'s language (0 for the first word).
    /// </summary>
    private int SentenceContext(Lang layout)
    {
        int score = 0;
        for (int i = _history.Count - 1, seen = 0; i >= 0 && seen < 3; i--, seen++)
        {
            var w = _history[i];
            var lang = _detector.LanguageOf(w.Keys, w.Layout);
            if (lang == null) continue;
            score += lang == layout ? -1 : 1;
        }
        return score;
    }

    private FixWord? TryFix(Lang layout, Sensitivity sensitivity, KeyKind boundary, DateTime now)
    {
        if (_keys.Length == 0 || _tainted || _wordLayout != layout) return null;
        var keys = _keys.ToString();
        var typed = KeyMap.Render(keys, layout);
        if (NeverFix.IsBlocked(typed)) return null;

        var d = _detector.Evaluate(keys, layout, sensitivity, SentenceContext(layout));
        if (!d.ShouldFix) return null;

        // Also fix the words just before this one that were typed in the same wrong layout.
        int take = 0;
        for (int i = _history.Count - 1; i >= 0 && take < MaxEarlierWords; i--)
        {
            var w = _history[i];
            if (w.Settled || w.Layout != layout) break;
            if (NeverFix.IsBlocked(KeyMap.Render(w.Keys, layout))) break;
            if (!_detector.IsPlausibleWrongLayout(w.Keys, layout)) break;
            take++;
        }
        var earlier = _history.GetRange(_history.Count - take, take);

        var allKeys = earlier.Select(w => w.Keys).Append(keys).ToList();
        string typedAll = string.Join(" ", allKeys.Select(k => KeyMap.Render(k, layout)));
        string fixedAll = string.Join(" ", allKeys.Select(k => KeyMap.Render(k, d.TargetLang)));

        // Update history to what is now on screen; fixed words are settled (never re-fixed).
        _history.RemoveRange(_history.Count - take, take);
        foreach (var k in allKeys) _history.Add((k, d.TargetLang, true));
        if (boundary != KeyKind.Space) _history.Clear();

        var correction = new Correction(keys, typedAll, fixedAll, layout, d.TargetLang, boundary == KeyKind.Space)
        {
            TriggerTyped = typed,
            WordCount = allKeys.Count,
        };
        if (boundary == KeyKind.Space) Undo.Arm(correction, now);
        else Undo.Disarm();
        return new FixWord(correction, typedAll.Length, fixedAll, d.TargetLang, boundary);
    }
}
