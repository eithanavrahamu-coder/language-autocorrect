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
    private bool _capitalized;
    private Lang? _wordLayout;

    /// <summary>Words already finished with a space, oldest first, as they appear on screen now.</summary>
    private readonly List<(string Keys, Lang Layout, bool Settled, bool Cap)> _history = new();

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

    /// <summary>The languages the user types in (a wrong-layout word is checked against each of them).</summary>
    public IReadOnlyList<Lang> Languages { get; set; } = [Lang.English, Lang.Hebrew];

    public string CurrentKeys => _keys.ToString();

    public void Reset()
    {
        _keys.Clear();
        _tainted = false;
        _capitalized = false;
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
                if (key.Shifted && ShiftTypesLetter(key.UsChar, layout))
                {
                    // Georgian, Thai and Korean type letters with Shift (თ, ธ, ㅆ): remember the key as shifted.
                    _keys.Append(KeyMap.Shifted(key.UsChar));
                    return PassThrough.Instance;
                }
                if (key.Shifted)
                {
                    // A capital first letter is fine (sentence start, German nouns); Shift anywhere else isn't.
                    if (_keys.Length == 0 && TypesCapital(key.UsChar, layout)) _capitalized = true;
                    else _tainted = true;
                }
                _keys.Append(char.ToLowerInvariant(key.UsChar));
                return PassThrough.Instance;

            case KeyKind.Backspace:
                if (_keys.Length > 0)
                {
                    // Accent keys and multi-letter keys don't delete one key per Backspace: stop tracking.
                    if (_wordLayout is Lang wl && !KeyMap.IsSimple(_keys.ToString(), wl)) { Reset(); _history.Clear(); }
                    else
                    {
                        _keys.Length--;
                        if (_keys.Length == 0) Reset();
                    }
                }
                else if (_history.Count > 0)
                {
                    // Deleted the space: go back into the previous word.
                    var prev = _history[^1];
                    _history.RemoveAt(_history.Count - 1);
                    _keys.Append(prev.Keys);
                    _wordLayout = prev.Layout;
                    _capitalized = prev.Cap;
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

    /// <summary>On this keyboard, or on the keyboard of a language the word may really be in.</summary>
    private bool ShiftTypesLetter(char usKey, Lang layout) =>
        KeyMap.ShiftTypesLetter(usKey, layout) || Languages.Any(l => KeyMap.ShiftTypesLetter(usKey, l));

    /// <summary>
    /// On this keyboard, or – on a keyboard with capitals – on the keyboard of a language the word may really be in
    /// (Shift+. types > in English, Ç in Turkish).
    /// </summary>
    private bool TypesCapital(char usKey, Lang layout) =>
        KeyMap.TypesCasedLetter(usKey, layout)
        || LayoutBuddy.Engine.Languages.Get(layout).HasCase && Languages.Any(l => KeyMap.TypesCasedLetter(usKey, l));

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
        _history.Add((_keys.ToString(), _wordLayout.Value, settled, _capitalized));
        if (_history.Count > MaxEarlierWords + 4) _history.RemoveAt(0);
    }

    /// <summary>
    /// Looks at up to 3 previous words in this sentence. Returns how many more of them are in
    /// <paramref name="target"/> than in <paramref name="layout"/>'s language (0 for the first word).
    /// </summary>
    private int SentenceContext(Lang layout, Lang target)
    {
        int score = 0;
        for (int i = _history.Count - 1, seen = 0; i >= 0 && seen < 3; i--, seen++)
        {
            var w = _history[i];
            var lang = _detector.LanguageOf(w.Keys, w.Layout, Languages);
            if (lang == target) score++;
            else if (lang == layout) score--;
        }
        return score;
    }

    /// <summary>Checks the word against every other language and returns the best fix, if any.</summary>
    private Detection? BestFix(string keys, Lang layout, Sensitivity sensitivity)
    {
        Detection? best = null;
        foreach (var target in Languages)
        {
            if (target == layout) continue;
            // A capitalized word going to a language without capitals (Hebrew, Arabic) is probably a name.
            // So is one with a shifted key, unless the language types letters with Shift (Georgian).
            var info = LayoutBuddy.Engine.Languages.Get(target);
            if (!info.HasCase && (_capitalized || (info.ShiftKeyboard == null && keys.Any(KeyMap.IsShiftedKey)))) continue;
            var d = _detector.Evaluate(keys, layout, target, sensitivity, SentenceContext(layout, target));
            if (d.ShouldFix && (best == null || d.Score > best.Score)) best = d;
        }
        return best;
    }

    private FixWord? TryFix(Lang layout, Sensitivity sensitivity, KeyKind boundary, DateTime now)
    {
        if (_keys.Length == 0 || _tainted || _wordLayout != layout) return null;
        var keys = _keys.ToString();
        var typed = KeyMap.Render(keys, layout, _capitalized);
        if (NeverFix.IsBlocked(typed)) return null;

        var d = BestFix(keys, layout, sensitivity);
        if (d == null) return null;

        // Also fix the words just before this one that were typed in the same wrong layout.
        int take = 0;
        for (int i = _history.Count - 1; i >= 0 && take < MaxEarlierWords; i--)
        {
            var w = _history[i];
            if (w.Settled || w.Layout != layout) break;
            if (NeverFix.IsBlocked(KeyMap.Render(w.Keys, layout))) break;
            if (!_detector.IsPlausibleWrongLayout(w.Keys, layout, d.TargetLang)) break;
            take++;
        }
        var earlier = _history.GetRange(_history.Count - take, take);

        var all = earlier.Select(w => (Keys: w.Keys, Cap: w.Cap)).Append((Keys: keys, Cap: _capitalized)).ToList();
        string typedAll = string.Join(" ", all.Select(w => KeyMap.Render(w.Keys, layout, w.Cap)));
        string fixedAll = string.Join(" ", all.Select(w => KeyMap.Render(w.Keys, d.TargetLang, w.Cap)));

        // Update history to what is now on screen; fixed words are settled (never re-fixed).
        _history.RemoveRange(_history.Count - take, take);
        foreach (var w in all) _history.Add((w.Keys, d.TargetLang, true, w.Cap));
        if (boundary != KeyKind.Space) _history.Clear();

        var correction = new Correction(keys, typedAll, fixedAll, layout, d.TargetLang, boundary == KeyKind.Space)
        {
            TriggerTyped = typed,
            WordCount = all.Count,
        };
        if (boundary == KeyKind.Space) Undo.Arm(correction, now);
        else Undo.Disarm();
        return new FixWord(correction, typedAll.Length, fixedAll, d.TargetLang, boundary);
    }
}
