using System.Globalization;
using System.Text;

namespace LanguageAutocorrect.Engine;

/// <summary>
/// The "try it now" box in the setup window: a pretend text box with a keyboard of its own (it starts on
/// English). Keys go through the same <see cref="TypingSession"/> as in any app, and the box's text changes the
/// way the app would change it on screen: a fix replaces the word and switches the keyboard, Backspace right
/// after it puts the word back.
/// </summary>
public sealed class TryBox
{
    private readonly TypingSession _session;
    private readonly Sensitivity _sensitivity;
    /// <summary>The text before the keys of the word being typed.</summary>
    private string _before = "";
    /// <summary>Physical keys of the word being typed; a shifted key as its shifted character ("G", ":").</summary>
    private readonly StringBuilder _keys = new();

    public TryBox(WrongLayoutDetector detector, IReadOnlyList<Lang> languages, Sensitivity sensitivity)
    {
        _session = new TypingSession(detector, new NeverFixList()) { Languages = languages };
        _sensitivity = sensitivity;
    }

    /// <summary>The keyboard the box is on.</summary>
    public Lang Layout { get; private set; } = Lang.English;

    public string Text => _before + KeyMap.Render(_keys.ToString(), Layout);

    /// <summary>Types a key. Returns what the app would do: <see cref="FixWord"/>, <see cref="UndoFix"/> or pass it through.</summary>
    public TypingAction Press(KeyInput key, DateTime now)
    {
        var action = _session.OnKey(key, Layout, autoCorrect: true, _sensitivity, now);
        switch (action)
        {
            case FixWord fix:
                Replace(fix.Backspaces, fix.Text, fix.Layout);
                _before += " ";
                break;
            case UndoFix undo:
                Replace(undo.Backspaces, undo.Text, undo.Layout);
                break;
            default:
                switch (key.Kind)
                {
                    case KeyKind.WordKey:
                        _keys.Append(key.Shifted ? KeyMap.Shifted(key.UsChar) : key.UsChar);
                        break;
                    case KeyKind.Backspace when _keys.Length > 0:
                        _keys.Length--;
                        break;
                    case KeyKind.Backspace when _before.Length > 0:
                        _before = _before[..StringInfo.ParseCombiningCharacters(_before)[^1]];
                        break;
                    case KeyKind.Space or KeyKind.Enter:
                        Replace(0, " ", Layout);
                        break;
                    default:
                        Replace(0, "", Layout);
                        break;
                }
                break;
        }
        return action;
    }

    /// <summary>Deletes <paramref name="backspaces"/> characters from the end, types <paramref name="text"/> and switches to <paramref name="layout"/>.</summary>
    private void Replace(int backspaces, string text, Lang layout)
    {
        var shown = Text;
        _before = shown[..Math.Max(0, shown.Length - backspaces)] + text;
        _keys.Clear();
        Layout = layout;
    }
}
