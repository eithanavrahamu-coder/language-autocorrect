namespace LayoutBuddy.Engine;

/// <summary>A correction that was just applied and may be undone.</summary>
public sealed record Correction(string UsKeys, string Typed, string Replacement, Lang From, Lang To, bool EndedWithSpace)
{
    /// <summary>The word that triggered the fix, as typed (Typed may include earlier words too).</summary>
    public string TriggerTyped { get; init; } = Typed;

    /// <summary>How many words were fixed together.</summary>
    public int WordCount { get; init; } = 1;
}

public enum UndoKey { Backspace, CtrlZ }

/// <summary>
/// Decides whether a key press right after an auto-correction means "undo it".
/// Only an unambiguous signal counts:
///  - Backspace as the very first key, within <see cref="BackspaceWindow"/> of the correction, or
///  - Ctrl+Z as the very first key, within <see cref="CtrlZWindow"/>.
/// Any other key (or waiting too long) cancels the chance to undo, so normal editing
/// after a correction never counts as an undo.
/// </summary>
public sealed class UndoTracker
{
    public TimeSpan BackspaceWindow { get; init; } = TimeSpan.FromMilliseconds(1500);
    public TimeSpan CtrlZWindow { get; init; } = TimeSpan.FromSeconds(5);

    private Correction? _pending;
    private DateTime _armedAt;

    public Correction? Pending => _pending;

    public void Arm(Correction correction, DateTime now)
    {
        _pending = correction;
        _armedAt = now;
    }

    public void Disarm() => _pending = null;

    /// <summary>
    /// Call for the first key after a correction. Returns the correction to undo, or null.
    /// Always disarms: only the very first key can trigger an undo.
    /// </summary>
    public Correction? OnKey(UndoKey? key, DateTime now)
    {
        var pending = _pending;
        _pending = null;
        if (pending == null || key == null) return null;
        var elapsed = now - _armedAt;
        return key switch
        {
            UndoKey.Backspace when elapsed <= BackspaceWindow => pending,
            UndoKey.CtrlZ when elapsed <= CtrlZWindow => pending,
            _ => null,
        };
    }
}
