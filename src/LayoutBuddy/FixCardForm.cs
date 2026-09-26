using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;
using Media = System.Windows.Media;

namespace LayoutBuddy;

/// <summary>What the card after a fix says: "ghbdtn → привет", or "Kept as typed: ghbdtn" after an undo.</summary>
internal sealed record FixCardText(string Typed, string Fixed, bool Undone = false, bool UndoTip = false);

/// <summary>
/// The small card that appears above the text after a fix, as on the website. It springs in, stays a moment,
/// and fades away. Clicks go through it.
/// </summary>
internal sealed class FixCardForm : LayeredWindow
{
    private const double StaySeconds = 2.6, LeaveSeconds = 0.18;

    private readonly Timer _frames = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private Picture? _card;
    private Rectangle _body; // the card inside _card; the rest is its shadow
    private Point _at; // where _card's top-left corner goes at rest
    private float _scale = 1;
    private bool _atRest;

    public FixCardForm()
    {
        _frames.Tick += (_, _) => DrawFrame();
    }

    /// <param name="line">The caret's line (the card goes above it).</param>
    /// <param name="word">Where the fixed word is on screen, if known: the card lines up with it.</param>
    public void ShowFix(FixCardText text, Rectangle line, Rectangle? word)
    {
        _scale = Native.DpiAt(line.Location) / 96f;
        _card = CardArt.Render(text, _scale, WebWindow.IsDarkMode, out _body);

        var area = Screen.FromPoint(line.Location).WorkingArea;
        // Clear of the line even while it rises into place (it starts 10px lower).
        int gap = Px(12);
        // The struck-out word sits right above the word it was (the card's text starts 14px in).
        int left = word is { } w ? w.Left - Px(14) : line.Left - _body.Width / 2;
        int top = Math.Min(line.Top, word?.Top ?? line.Top) - gap - _body.Height;
        // No room above: below the line and the language badge.
        if (top < area.Top) top = line.Bottom + Px(3 + 17 + 6);
        left = Math.Clamp(left, area.Left + gap, Math.Max(area.Left + gap, area.Right - gap - _body.Width));
        _at = new Point(left - _body.X, top - _body.Y);

        _atRest = false;
        _clock.Restart();
        _frames.Start();
        DrawFrame();
        if (!Visible) Show();
    }

    public void Dismiss()
    {
        _frames.Stop();
        if (Visible) Hide();
    }

    private void DrawFrame()
    {
        if (_card == null) return;
        double t = _clock.Elapsed.TotalSeconds;
        float opacity, lift, scale;
        if (t < Motion.SpringSeconds)
        {
            // In: from a little lower, a little smaller and transparent.
            float x = Motion.Spring(t);
            (opacity, lift, scale) = (x, 10 * (1 - x), 0.95f + 0.05f * x);
        }
        else if (t < StaySeconds)
        {
            if (!_atRest) _atRest = Present(_card, _at);
            return;
        }
        else
        {
            // Out: sinking a little and fading.
            float p = Motion.EaseOut((t - StaySeconds) / LeaveSeconds);
            if (p >= 1)
            {
                Dismiss();
                return;
            }
            (opacity, lift, scale) = (1 - p, 6 * p, 1 - 0.02f * p);
        }
        _atRest = false;
        int room = Px(10) + 2;
        var frame = new Picture(_card.Width + 2, _card.Height + room + 2);
        Motion.DrawOver(frame, _card, new PointF(frame.Width / 2f, 1 + _card.Height / 2f + lift * _scale), scale, opacity);
        Present(frame, new Point(_at.X - 1, _at.Y - 1));
    }

    private int Px(float value) => BadgeArt.Px(value, _scale);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _frames.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Draws the fix card like the website's toast: its colors (light or dark), its sizes, and smooth text like the
/// browser's (the same text drawing as the language badge). The font is the app window's own, Segoe UI Variable.
/// </summary>
internal static class CardArt
{
    private sealed record Theme(Color Card, Color Line, Color Text, Color Muted, Color Faint);

    private static readonly Theme Light = new(Hex("#FFFFFF"), Hex("#E9E5DF"), Hex("#1B1A18"), Hex("#75706A"), Hex("#A7A29B"));
    private static readonly Theme Dark = new(Hex("#222220"), Hex("#2F2E2B"), Hex("#F2F0EC"), Hex("#A29E97"), Hex("#6E6A64"));

    private static readonly Media.FontFamily Family = new("Segoe UI Variable Text, Segoe UI");

    /// <summary>One piece of the card's row: its width, and how it draws itself at x, given the row's middle.</summary>
    private sealed record Piece(double Width, Action<Media.DrawingContext, double, double> Draw);

    public static Picture Render(FixCardText text, float s, bool dark, out Rectangle body)
    {
        var theme = dark ? Dark : Light;
        var normal = System.Windows.FontWeights.Normal;
        // The website's 650 comes out bold in Segoe UI.
        var bold = System.Windows.FontWeights.Bold;

        // The website's toast: a row of pieces 12px apart; the crossed-out word, arrow and fix .45em apart.
        var row = new List<Piece>();
        void Gap(double px) => row.Add(new Piece(px * s, (_, _, _) => { }));
        if (text.Undone)
        {
            row.Add(UndoIcon(15 * s, theme.Text));
            Gap(12);
            row.Add(Words("Kept as typed:", 15 * s, normal, theme.Text));
            Gap(12);
            row.Add(Words(text.Typed, 15 * s, bold, theme.Text));
        }
        else
        {
            row.Add(Words(text.Typed, 15 * s, normal, theme.Muted, strike: (theme.Faint, 1.5 * s)));
            Gap(15 * 0.45);
            row.Add(Words("→", 15 * s, normal, theme.Faint));
            Gap(15 * 0.45);
            row.Add(Words(text.Fixed, 15 * s, bold, theme.Text));
            if (text.UndoTip)
            {
                Gap(12);
                row.Add(Key("Backspace", 13.5 * 0.82 * s, theme, s));
                Gap(6);
                row.Add(Words("to undo", 13.5 * s, normal, theme.Muted));
            }
        }

        // 1px border, 8px × 14px padding, a row 24px high (15px text × 1.6), 12px corners and a soft shadow.
        double border = Math.Max(1, s), padX = 14 * s, padY = 8 * s, rowHeight = 24 * s, content = 0;
        foreach (var piece in row) content += piece.Width;
        int width = (int)Math.Ceiling(2 * (border + padX) + content), height = (int)Math.Round(2 * (border + padY) + rowHeight);
        var shadow = new Shadow(8 * s, 24 * s, -8 * s, Color.FromArgb(56, 20, 18, 15));
        int margin = BadgeArt.ShadowMargin(shadow);
        body = new Rectangle(margin, margin, width, height);
        float radius = 12 * s;

        var visual = new Media.DrawingVisual();
        Media.TextOptions.SetTextFormattingMode(visual, Media.TextFormattingMode.Ideal);
        Media.TextOptions.SetTextRenderingMode(visual, Media.TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brush(theme.Card), null, new System.Windows.Rect(0, 0, width, height));
            double x = (width - content) / 2, middle = height / 2.0;
            foreach (var piece in row)
            {
                piece.Draw(dc, x, middle);
                x += piece.Width;
            }
        }
        using var face = ToBitmap(visual, width, height);
        using var image = BadgeArt.Compose(face, new Size(width + 2 * margin, height + 2 * margin), body, radius, shadow);
        using (var g = Graphics.FromImage(image))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float stroke = (float)border;
            using var outline = BadgeArt.RoundedRect(new RectangleF(body.X + stroke / 2, body.Y + stroke / 2,
                body.Width - stroke, body.Height - stroke), radius - stroke / 2);
            using var pen = new Pen(theme.Line, stroke);
            g.DrawPath(pen, outline);
        }
        return Picture.From(image);
    }

    /// <summary>
    /// Text on the row, optionally struck through like the website's crossed-out word. Like CSS, its baseline is where
    /// the primary font's line, centered on the row, puts it, whichever font the letters come from.
    /// </summary>
    private static Piece Words(string text, double size, System.Windows.FontWeight weight, Color color,
        (Color Color, double Thickness)? strike = null)
    {
        var label = Text(text, size, weight, color);
        if (strike is { } line)
        {
            var pen = new Media.Pen(Brush(line.Color), line.Thickness);
            pen.Freeze();
            label.SetTextDecorations(new System.Windows.TextDecorationCollection
            {
                new System.Windows.TextDecoration(System.Windows.TextDecorationLocation.Strikethrough, pen, 0,
                    System.Windows.TextDecorationUnit.FontRecommended, System.Windows.TextDecorationUnit.Pixel),
            });
        }
        return new Piece(label.WidthIncludingTrailingWhitespace,
            (dc, x, middle) => dc.DrawText(label, new System.Windows.Point(x, Baseline(middle, size, weight) - label.Baseline)));
    }

    /// <summary>A keyboard key, like the website's: a small rounded box with a thicker bottom edge.</summary>
    private static Piece Key(string text, double size, Theme theme, float s)
    {
        var weight = System.Windows.FontWeights.SemiBold;
        var label = Text(text, size, weight, theme.Text);
        double width = Math.Max(1.9 * size, label.WidthIncludingTrailingWhitespace + size), height = 1.75 * size;
        double border = Math.Max(1, s), bottom = 2.5 * s, radius = 7 * s;
        return new Piece(width, (dc, x, middle) =>
        {
            var box = new System.Windows.Rect(x, middle - height / 2, width, height);
            dc.DrawRoundedRectangle(Brush(theme.Line), null, box, radius, radius);
            var inner = new System.Windows.Rect(box.X + border, box.Y + border, box.Width - 2 * border, box.Height - border - bottom);
            dc.DrawRoundedRectangle(Brush(theme.Card), null, inner, radius - border, radius - border);
            dc.DrawText(label, new System.Windows.Point(inner.X + (inner.Width - label.WidthIncludingTrailingWhitespace) / 2,
                Baseline(inner.Y + inner.Height / 2, size, weight) - label.Baseline));
        });
    }

    /// <summary>The website's undo arrow (Lucide's "undo-2"), <paramref name="size"/> pixels square.</summary>
    private static Piece UndoIcon(double size, Color color) => new(size, (dc, x, middle) =>
    {
        double k = size / 24, top = middle - size / 2;
        System.Windows.Point P(double px, double py) => new(x + px * k, top + py * k);
        var pen = new Media.Pen(Brush(color), 2 * k)
        {
            StartLineCap = Media.PenLineCap.Round, EndLineCap = Media.PenLineCap.Round, LineJoin = Media.PenLineJoin.Round,
        };
        var arrow = new Media.StreamGeometry();
        using (var c = arrow.Open())
        {
            c.BeginFigure(P(9, 14), false, false);
            c.PolyLineTo([P(4, 9), P(9, 4)], true, true);
            c.BeginFigure(P(4, 9), false, false);
            c.LineTo(P(14.5, 9), true, true);
            c.ArcTo(P(14.5, 20), new System.Windows.Size(5.5 * k, 5.5 * k), 0, false, Media.SweepDirection.Clockwise, true, true);
            c.LineTo(P(11, 20), true, true);
        }
        dc.DrawGeometry(null, pen, arrow);
    });

    private static Media.FormattedText Text(string text, double size, System.Windows.FontWeight weight, Color color) =>
        new(text, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight,
            new Media.Typeface(Family, System.Windows.FontStyles.Normal, weight, System.Windows.FontStretches.Normal),
            size, Brush(color), 1.0);

    /// <summary>The baseline for text of <paramref name="size"/> whose line is centered on <paramref name="middle"/>.</summary>
    private static double Baseline(double middle, double size, System.Windows.FontWeight weight)
    {
        var line = Text("x", size, weight, Color.Black);
        return middle - line.Height / 2 + line.Baseline;
    }

    private static Bitmap ToBitmap(Media.Visual visual, int width, int height)
    {
        var target = new Media.Imaging.RenderTargetBitmap(width, height, 96, 96, Media.PixelFormats.Pbgra32);
        target.Render(visual);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        target.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Stride * height, data.Stride);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    private static Media.SolidColorBrush Brush(Color c)
    {
        var brush = new Media.SolidColorBrush(Media.Color.FromArgb(c.A, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    private static Color Hex(string hex) => ColorTranslator.FromHtml(hex);
}
