namespace LanguageAutocorrect.Engine;

/// <summary>
/// Counts undos for the question "Stop fixing this word?" (<see cref="AppSettings.AskAfterUndos"/>). Kept apart from
/// <see cref="NeverFixList"/>, whose counts block a word by themselves. Once a word has been undone
/// <see cref="UndosToAsk"/> times it asks, and at every undo after that until the question is answered.
/// Thread-safe.
/// </summary>
public sealed class UndoQuestion
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _counts;
    private int _undosToAsk;

    public UndoQuestion(IDictionary<string, int>? counts = null, int undosToAsk = 5)
    {
        _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (counts != null)
            foreach (var (k, v) in counts)
                if (!string.IsNullOrWhiteSpace(k) && v > 0) _counts[NeverFixList.Normalize(k)] = v;
        _undosToAsk = Math.Max(1, undosToAsk);
    }

    public int UndosToAsk
    {
        get { lock (_lock) return _undosToAsk; }
        set { lock (_lock) _undosToAsk = Math.Max(1, value); }
    }

    public int UndoCount(string word)
    {
        lock (_lock)
            return _counts.TryGetValue(NeverFixList.Normalize(word), out var c) ? c : 0;
    }

    /// <summary>Records an undo. Returns true if it's time to ask.</summary>
    public bool RecordUndo(string word)
    {
        var key = NeverFixList.Normalize(word);
        if (key.Length == 0) return false;
        lock (_lock)
        {
            int count = _counts.GetValueOrDefault(key) + 1;
            _counts[key] = count;
            return count >= _undosToAsk;
        }
    }

    /// <summary>The question was answered, either way: the word's count starts again.</summary>
    public void Answered(string word)
    {
        lock (_lock) _counts.Remove(NeverFixList.Normalize(word));
    }

    public Dictionary<string, int> Snapshot()
    {
        lock (_lock) return new Dictionary<string, int>(_counts);
    }
}
