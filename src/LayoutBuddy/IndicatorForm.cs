using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LayoutBuddy.Engine;

namespace LayoutBuddy;

/// <summary>Small click-through badge showing "EN" / "עב" next to the caret.</summary>
internal sealed class IndicatorForm : Form
{
    private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
        WS_EX_NOACTIVATE = 0x8000000;

    public static readonly Color EnglishColor = Color.FromArgb(37, 99, 235);
    public static readonly Color HebrewColor = Color.FromArgb(22, 163, 74);

    private Lang _lang = Lang.English;

    public IndicatorForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Opacity = 0.92;
        DoubleBuffered = true;
        BackColor = EnglishColor;
        Size = ScaledSize();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void ShowAt(Rectangle caret, Lang lang)
    {
        if (lang != _lang)
        {
            _lang = lang;
            BackColor = lang == Lang.Hebrew ? HebrewColor : EnglishColor;
            Invalidate();
        }
        var size = ScaledSize();
        if (Size != size) Size = size;

        // Just below and slightly right of the caret, kept on screen.
        var pos = new Point(caret.Left + 2, caret.Bottom + 3);
        var screen = Screen.FromPoint(pos).WorkingArea;
        if (pos.Y + Height > screen.Bottom) pos.Y = caret.Top - Height - 3;
        if (pos.X + Width > screen.Right) pos.X = screen.Right - Width;
        if (Location != pos) Location = pos;
        if (!Visible) Show();
    }

    private Size ScaledSize()
    {
        float s = DeviceDpi / 96f;
        return new Size((int)(26 * s), (int)(17 * s));
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Size = ScaledSize();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        using var path = RoundedRect(ClientRectangle, Height / 3);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        string text = _lang == Lang.Hebrew ? "עב" : "EN";
        using var bigFont = new Font("Segoe UI", 11f * DeviceDpi / 96f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, text, bigFont, ClientRectangle, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
