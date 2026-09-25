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
/// Tracks the word being typed and decides when to auto-correct or undo.
/// Pure logic, no Windows calls, so it can be unit tested.
/// </summary>
public sealed class TypingSession
{
    private readonly WrongLayoutDetector _detector;
    private readonly StringBuilder _keys = new();
    private bool _tainted;
    private Lang? _wordLayout;

    public TypingSession(WrongLayoutDetector detector, NeverFixList neverFix, UndoTracker? undo = null)
    {
        _detector = detector;
        NeverFix = neverFix;
        Undo = undo ?? new UndoTracker();
    }

    public NeverFixList NeverFix { get; }
    public UndoTracker Undo { get; }
    public string CurrentKeys => _keys.ToString();

    public void Reset()
    {
        _keys.Clear();
        _tainted = false;
        _wordLayout = null;
    }

    /// <summary>Resets the word and cancels any pending undo (focus change, mouse click...).</summary>
    public void ResetAll()
    {
        Reset();
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
                bool blocked = NeverFix.RecordUndo(toUndo.Typed);
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
                if (_keys.Length > 0) _keys.Length--;
                if (_keys.Length == 0) Reset();
                return PassThrough.Instance;

            case KeyKind.Space:
            case KeyKind.Enter:
                var action = autoCorrect ? TryFix(layout, sensitivity, key.Kind, now) : null;
                Reset();
                return (TypingAction?)action ?? PassThrough.Instance;

            default:
                Reset();
                return PassThrough.Instance;
        }
    }

    private FixWord? TryFix(Lang layout, Sensitivity sensitivity, KeyKind boundary, DateTime now)
    {
        if (_keys.Length == 0 || _tainted || _wordLayout != layout) return null;
        var keys = _keys.ToString();
        var typed = KeyMap.Render(keys, layout);
        if (NeverFix.IsBlocked(typed)) return null;

        var d = _detector.Evaluate(keys, layout, sensitivity);
        if (!d.ShouldFix) return null;

        var correction = new Correction(keys, d.Typed, d.Replacement, layout, d.TargetLang, boundary == KeyKind.Space);
        if (boundary == KeyKind.Space) Undo.Arm(correction, now);
        else Undo.Disarm();
        return new FixWord(correction, keys.Length, d.Replacement, d.TargetLang, boundary);
    }
}
