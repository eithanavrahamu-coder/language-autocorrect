namespace LanguageAutocorrect.Engine;

/// <summary>
/// Words the user has undone. A word is only blocked from auto-correction after it has
/// been undone <see cref="UndosToBlock"/> times, so a single accidental undo doesn't block it.
/// Thread-safe.
/// </summary>
public sealed class NeverFixList
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _counts;
    private int _undosToBlock;

    public NeverFixList(IDictionary<string, int>? counts = null, int undosToBlock = 3)
    {
        _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (counts != null)
            foreach (var (k, v) in counts)
                if (!string.IsNullOrWhiteSpace(k) && v > 0) _counts[Normalize(k)] = v;
        _undosToBlock = Math.Max(1, undosToBlock);
    }

    public int UndosToBlock
    {
        get { lock (_lock) return _undosToBlock; }
        set { lock (_lock) _undosToBlock = Math.Max(1, value); }
    }

    public static string Normalize(string word) => word.Trim().ToLowerInvariant();

    public bool IsBlocked(string word)
    {
        lock (_lock)
            return _counts.TryGetValue(Normalize(word), out var c) && c >= _undosToBlock;
    }

    public int UndoCount(string word)
    {
        lock (_lock)
            return _counts.TryGetValue(Normalize(word), out var c) ? c : 0;
    }

    /// <summary>Records an undo. Returns true if this undo caused the word to become blocked.</summary>
    public bool RecordUndo(string word)
    {
        var key = Normalize(word);
        if (key.Length == 0) return false;
        lock (_lock)
        {
            int before = _counts.TryGetValue(key, out var c) ? c : 0;
            _counts[key] = before + 1;
            return before < _undosToBlock && before + 1 >= _undosToBlock;
        }
    }

    /// <summary>Blocks a word immediately (manual add from settings).</summary>
    public void Block(string word)
    {
        var key = Normalize(word);
        if (key.Length == 0) return;
        lock (_lock) _counts[key] = Math.Max(_undosToBlock, _counts.GetValueOrDefault(key));
    }

    public void Remove(string word)
    {
        lock (_lock) _counts.Remove(Normalize(word));
    }

    public void Clear()
    {
        lock (_lock) _counts.Clear();
    }

    /// <summary>Replaces all counts (used to roll back a cancelled settings dialog).</summary>
    public void Restore(IDictionary<string, int> counts, int undosToBlock)
    {
        lock (_lock)
        {
            _counts.Clear();
            foreach (var (k, v) in counts) _counts[k] = v;
            _undosToBlock = Math.Max(1, undosToBlock);
        }
    }

    /// <summary>Words that are currently blocked, sorted.</summary>
    public IReadOnlyList<string> BlockedWords()
    {
        lock (_lock)
            return _counts.Where(kv => kv.Value >= _undosToBlock).Select(kv => kv.Key).OrderBy(k => k).ToList();
    }

    public Dictionary<string, int> Snapshot()
    {
        lock (_lock) return new Dictionary<string, int>(_counts);
    }
}
