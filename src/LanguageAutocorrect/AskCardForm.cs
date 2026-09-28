using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace LanguageAutocorrect;

/// <summary>
/// The card asking "Stop fixing akuo?" once a word has been undone enough times (AppSettings.AskAfterUndos). It comes
/// in like the fix card, above the text, and its two buttons take clicks without taking the focus from the app being
/// typed in. "Stop fixing" turns it into "akuo won't be fixed anymore" for a moment. Left alone it goes away after a
/// while (never while the pointer is on it), and the word's next undo asks again.
/// </summary>
internal sealed class AskCardForm : LayeredWindow
{
    private const double AskSeconds = 12, DoneSeconds = 2.2, SwapSeconds = 0.2, LeaveSeconds = 0.18;

    private enum Phase { In, Rest, Swap, Out }

    private readonly Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private string _word = "";
    private Picture[] _asking = []; // the question with no button, "Keep fixing" or "Stop fixing" under the pointer
    private Rectangle[] _buttons = []; // where those two are in the question's picture
    private Picture? _card; // what it shows now
    private Rectangle _body; // the card inside _card; the rest is its shadow
    private Point _at; // where _card's top-left corner goes at rest
    private (Picture Card, Point At)? _question; // the question, fading out while the answer comes in
    private Picture? _shown; // what's on screen at rest
    private Rectangle _line;
    private float _scale = 1;
    private Phase _phase;
    private double _stay; // seconds at rest before it goes
    private int _hover = -1;
    private bool _answered;

    /// <summary>A button was clicked: the word, and whether to stop fixing it.</summary>
    public event Action<string, bool>? Answered;

    public AskCardForm()
    {
        _frames.Tick += (_, _) => DrawFrame();
    }

    /// <param name="undos">How many times the word has been undone.</param>
    /// <param name="line">The caret's line (the card goes above it), if known.</param>
    public void Ask(string word, int undos, Rectangle? line)
    {
        _line = line ?? Corner();
        _scale = Native.DpiAt(_line.Location) / 96f;
        bool dark = WebWindow.IsDarkMode;
        _asking = new Picture[3];
        for (int hover = -1; hover <= 1; hover++)
            _asking[hover + 1] = CardArt.RenderAsk(word, undos, hover, _scale, dark, out _body, out _buttons);
        _word = word;
        _hover = -1;
        _answered = false;
        _question = null;
        _card = _asking[0];
        _at = FixCardForm.Place(_line, null, _body, _scale);
        Start(Phase.In, AskSeconds);
        DrawFrame();
        if (!Visible) Show();
    }

    public void Dismiss()
    {
        _frames.Stop();
        ClickThrough = true;
        if (Visible) Hide();
    }

    /// <summary>Without a caret: the bottom-right corner of the screen, like a notification.</summary>
    private static Rectangle Corner()
    {
        var area = Screen.FromHandle(Native.GetForegroundWindow()).WorkingArea;
        return new Rectangle(area.Right - 1, area.Bottom - 1, 1, 1);
    }

    private void Start(Phase phase, double? stay = null)
    {
        _phase = phase;
        if (stay is double seconds) _stay = seconds;
        _shown = null;
        _clock.Restart();
        _frames.Start();
    }

    private Rectangle OnScreen(Rectangle r) => new(_at.X + r.X, _at.Y + r.Y, r.Width, r.Height);

    private int ButtonAt(Point pointer) =>
        _answered || _phase == Phase.Out ? -1 : Array.FindIndex(_buttons, b => OnScreen(b).Contains(pointer));

    private void DrawFrame()
    {
        if (_card == null) return;
        var pointer = Cursor.Position;
        bool over = OnScreen(_body).Contains(pointer);
        // Only the card takes clicks (not its shadow), and only while it asks.
        ClickThrough = !over || _answered || _phase == Phase.Out;
        int hover = ButtonAt(pointer);
        if (hover != _hover)
        {
            _hover = hover;
            Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
            if (!_answered) _card = _asking[hover + 1];
        }

        double t = _clock.Elapsed.TotalSeconds;
        switch (_phase)
        {
            case Phase.In when t >= Motion.SpringSeconds:
            case Phase.Swap when t >= SwapSeconds:
                _question = null;
                Start(Phase.Rest);
                break;
            case Phase.Rest when over:
                _clock.Restart(); // the pointer on it keeps it
                break;
            case Phase.Rest when t >= _stay:
                Start(Phase.Out);
                return;
        }

        t = _clock.Elapsed.TotalSeconds;
        switch (_phase)
        {
            case Phase.In:
                // In: from a little lower, a little smaller and transparent, as the fix card comes.
                float x = Motion.Spring(t);
                Compose(10 * (1 - x), 0.95f + 0.05f * x, (_card, _at, x));
                break;
            case Phase.Rest:
                if (_shown != _card && Present(_card, _at)) _shown = _card;
                break;
            case Phase.Swap:
                float p = Motion.EaseOut(t / SwapSeconds);
                Compose(0, 1, (_question!.Value.Card, _question.Value.At, 1 - p), (_card, _at, p));
                break;
            case Phase.Out:
                // Out: sinking a little and fading.
                float q = Motion.EaseOut(t / LeaveSeconds);
                if (q >= 1)
                {
                    Dismiss();
                    return;
                }
                Compose(6 * q, 1 - 0.02f * q, (_card, _at, 1 - q));
                break;
        }
    }

    /// <summary>One frame: each card faded to its opacity, <paramref name="lift"/> pixels lower and scaled.</summary>
    private void Compose(float lift, float scale, params (Picture Card, Point At, float Opacity)[] cards)
    {
        var bounds = Rectangle.Empty;
        foreach (var c in cards)
        {
            var r = new Rectangle(c.At, c.Card.Size);
            bounds = bounds.IsEmpty ? r : Rectangle.Union(bounds, r);
        }
        bounds.Inflate(1, 1);
        bounds.Height += BadgeArt.Px(10, _scale) + 2; // room to rise from 10px lower
        var frame = new Picture(bounds.Width, bounds.Height);
        foreach (var c in cards)
            Motion.DrawOver(frame, c.Card, new PointF(c.At.X - bounds.X + c.Card.Width / 2f,
                c.At.Y - bounds.Y + c.Card.Height / 2f + lift * _scale), scale, c.Opacity);
        Present(frame, bounds.Location);
        _shown = null;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int button = ButtonAt(Cursor.Position);
        if (e.Button != MouseButtons.Left || button < 0 || _card == null) return;
        bool stop = button == 1;
        _answered = true;
        ClickThrough = true;
        Cursor = Cursors.Default;
        Answered?.Invoke(_word, stop);
        if (!stop)
        {
            Start(Phase.Out);
            return;
        }
        // The question fades into the answer, which stays a moment.
        _question = (_card, _at);
        _card = CardArt.Render(new FixCardText(_word, "", Blocked: true), _scale, WebWindow.IsDarkMode, out _body);
        _at = FixCardForm.Place(_line, null, _body, _scale);
        Start(Phase.Swap, DoneSeconds);
        DrawFrame();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _frames.Dispose();
        base.Dispose(disposing);
    }
}
