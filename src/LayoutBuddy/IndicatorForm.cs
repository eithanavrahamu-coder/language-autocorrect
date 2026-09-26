using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

/// <summary>
/// The badge showing "EN" / "עב" next to the caret. When the language changes, the old badge shrinks away and the new
/// one springs in, as on the website. A click on it switches the keyboard (see <see cref="Clicked"/>).
/// </summary>
internal sealed class IndicatorForm : LayeredWindow
{
    private const byte Opacity255 = 235;

    private readonly Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private Picture? _image;
    private Rectangle _pill; // the badge inside _image; the rest is its shadow
    private Lang _lang;
    private int _imageDpi;
    private (Picture Image, Rectangle Pill)? _leaving; // the previous language's badge, while it shrinks away
    private Point _anchor; // where the badge's top-left corner goes on screen
    private Point? _shownAt;

    /// <summary>The badge was clicked (with the left button).</summary>
    public event Action? Clicked;

    public IndicatorForm()
    {
        Cursor = Cursors.Hand;
        _frames.Tick += (_, _) => DrawFrame();
    }

    public void ShowAt(Rectangle caret, Lang lang)
    {
        if (_image == null || lang != _lang || DeviceDpi != _imageDpi)
        {
            bool pop = _image != null && Visible && lang != _lang && DeviceDpi == _imageDpi;
            _leaving = pop ? (_image!, _pill) : null;
            _image = BadgeArt.Indicator(lang, DeviceDpi / 96f, out _pill);
            _lang = lang;
            _imageDpi = DeviceDpi;
            _shownAt = null;
            if (pop)
            {
                _clock.Restart();
                _frames.Start();
            }
        }

        // The badge goes just below and slightly right of the caret, kept on screen.
        var pos = new Point(caret.Left + 2, caret.Bottom + 3);
        var screen = Screen.FromPoint(pos).WorkingArea;
        if (pos.Y + _pill.Height > screen.Bottom) pos.Y = caret.Top - _pill.Height - 3;
        if (pos.X + _pill.Width > screen.Right) pos.X = screen.Right - _pill.Width;
        _anchor = pos;
        // Clicks reach the badge only while the pointer is on it; elsewhere they go to the text below.
        ClickThrough = !new Rectangle(pos, _pill.Size).Contains(Cursor.Position);

        if (_leaving != null) DrawFrame();
        else Place();
        if (!Visible) Show();
    }

    /// <summary>The badge at rest.</summary>
    private void Place()
    {
        var location = new Point(_anchor.X - _pill.X, _anchor.Y - _pill.Y);
        if (location != _shownAt && Present(_image!, location, Opacity255)) _shownAt = location;
    }

    /// <summary>One frame of the switch: the old badge shrinks and fades out while the new one springs in.</summary>
    private void DrawFrame()
    {
        double t = _clock.Elapsed.TotalSeconds;
        if (_leaving is not { } old || t >= Motion.SpringSeconds || _image == null)
        {
            _frames.Stop();
            _leaving = null;
            _shownAt = null;
            if (_image != null) Place();
            return;
        }

        // Each badge scales around its own middle; the frame is big enough for both at their largest.
        float x = Motion.Spring(t);
        var newMiddle = new PointF(_anchor.X + _pill.Width / 2f, _anchor.Y + _pill.Height / 2f);
        var oldMiddle = new PointF(_anchor.X + old.Pill.Width / 2f, _anchor.Y + old.Pill.Height / 2f);
        var bounds = Rectangle.Union(Around(newMiddle, _image, 1.05f), Around(oldMiddle, old.Image, 1f));
        var frame = new Picture(bounds.Width, bounds.Height);
        Motion.DrawOver(frame, old.Image, new PointF(oldMiddle.X - bounds.X, oldMiddle.Y - bounds.Y), 1 - 0.5f * x, 1 - x);
        Motion.DrawOver(frame, _image, new PointF(newMiddle.X - bounds.X, newMiddle.Y - bounds.Y), 0.5f + 0.5f * x, x);
        Present(frame, bounds.Location, Opacity255);
    }

    private static Rectangle Around(PointF middle, Picture image, float scale)
    {
        float halfW = image.Width * scale / 2 + 1, halfH = image.Height * scale / 2 + 1;
        return Rectangle.FromLTRB((int)MathF.Floor(middle.X - halfW), (int)MathF.Floor(middle.Y - halfH),
            (int)MathF.Ceiling(middle.X + halfW), (int)MathF.Ceiling(middle.Y + halfH));
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) return;
        // Next time it shows, it shows at rest.
        _frames.Stop();
        _leaving = null;
        _shownAt = null;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left) Clicked?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _frames.Dispose();
        base.Dispose(disposing);
    }
}
