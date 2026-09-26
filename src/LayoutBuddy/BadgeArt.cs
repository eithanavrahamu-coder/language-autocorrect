using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LayoutBuddy.Engine;
using Media = System.Windows.Media;

namespace LayoutBuddy;

/// <summary>A soft shadow under a shape, like CSS box-shadow (offset, blur, spread). The color's alpha is how dark it gets.</summary>
internal readonly record struct Shadow(float OffsetY, float Blur, float Spread, Color Color);

/// <summary>Draws the language badge ("EN", "עב") for the cursor indicator and the tray icon.</summary>
internal static class BadgeArt
{
    private const TextFormatFlags TextFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    // Bold Segoe UI, as on the website; Thai and Korean come from the fonts Windows has for them.
    private static readonly Media.Typeface LabelFace = new(new Media.FontFamily("Segoe UI, Leelawadee UI, Malgun Gothic"),
        System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal);

    public static Color ColorOf(Lang lang) => ColorTranslator.FromHtml(Languages.Get(lang).Color);

    /// <summary>
    /// The cursor badge with its shadow: 26×17 at 96 DPI, wider if the label needs it, like the website's.
    /// <paramref name="pill"/> is where the badge itself sits in the picture; the rest is shadow.
    /// </summary>
    public static Picture Indicator(Lang lang, float scale, out Rectangle pill)
    {
        var label = new Media.FormattedText(Languages.Get(lang).Badge, CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight, LabelFace, 11 * scale, Media.Brushes.White, 1.0);
        int h = Px(17, scale);
        int w = Math.Max(Px(26, scale), (int)Math.Ceiling(label.WidthIncludingTrailingWhitespace) + Px(8, scale));
        // The website's shadow: 0 2px 6px rgba(0, 0, 0, .18).
        var shadow = new Shadow(2 * scale, 6 * scale, 0, Color.FromArgb(46, Color.Black));
        int margin = ShadowMargin(shadow);
        pill = new Rectangle(margin, margin, w, h);
        using var image = Render(lang, new Size(w + 2 * margin, h + 2 * margin), pill, 6 * scale, shadow, scale,
            (g, r) =>
            {
                using var text = LabelBitmap(label, 11 * scale, r.Size);
                g.DrawImageUnscaled(text, 0, 0);
            });
        return Picture.From(image);
    }

    /// <summary>The tray icon: the badge filling a square of <paramref name="size"/> pixels.</summary>
    public static Bitmap TrayIcon(Lang lang, int size)
    {
        string text = Languages.Get(lang).Badge;
        // The biggest bold font that still fits, so short labels read well at 16×16.
        float px = size * 0.56f;
        var font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
        while (px > 6 && TextRenderer.MeasureText(text, font, Size.Empty, TextFlags).Width > size - Math.Max(3, size / 5))
        {
            font.Dispose();
            px -= 0.5f;
            font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
        }
        using (font)
        {
            var square = new Rectangle(0, 0, size, size);
            // Snapped to pixels (unlike the cursor badge): at 16×16 that reads better.
            using var image = Render(lang, square.Size, square, size * 0.22f, null, size / 16f, (g, r) =>
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                TextRenderer.DrawText(g, text, font, r, Color.White, TextFlags);
            });
            // Icons want straight (not premultiplied) alpha.
            return image.Clone(square, PixelFormat.Format32bppArgb);
        }
    }

    private static Bitmap Render(Lang lang, Size canvas, Rectangle pill, float radius, Shadow? shadow, float scale,
        Action<Graphics, Rectangle> drawLabel)
    {
        var color = ColorOf(lang);

        // The face: a gentle top-to-bottom gradient with the label.
        using var face = new Bitmap(pill.Width, pill.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(face))
        {
            var r = new Rectangle(Point.Empty, pill.Size);
            using (var fill = new LinearGradientBrush(r, Mix(color, Color.White, 0.18f), Mix(color, Color.Black, 0.10f),
                       LinearGradientMode.Vertical))
                g.FillRectangle(fill, r);
            drawLabel(g, r);
        }

        var image = Compose(face, canvas, pill, radius, shadow);

        // A thin darker rim so the badge stands out on light backgrounds, and a faint light edge on top.
        using (var g = Graphics.FromImage(image))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float stroke = Math.Max(1f, scale);
            var inner = new RectangleF(pill.X + stroke / 2, pill.Y + stroke / 2, pill.Width - stroke, pill.Height - stroke);
            using var rim = RoundedRect(inner, radius - stroke / 2);
            using (var pen = new Pen(Color.FromArgb(90, Mix(color, Color.Black, 0.45f)), stroke))
                g.DrawPath(pen, rim);
            using var shine = new LinearGradientBrush(new RectangleF(inner.X, inner.Y - stroke, inner.Width, inner.Height + 2 * stroke),
                Color.White, Color.White, LinearGradientMode.Vertical);
            shine.InterpolationColors = new ColorBlend
            {
                Colors = [Color.FromArgb(110, Color.White), Color.FromArgb(0, Color.White), Color.FromArgb(0, Color.White)],
                Positions = [0f, 0.45f, 1f],
            };
            using (var pen = new Pen(shine, stroke))
                g.DrawPath(pen, rim);
        }
        return image;
    }

    /// <summary>
    /// The label in white on transparent, drawn the way the website's browser draws it: smooth and not snapped to
    /// pixels. Placed like CSS does: centered, on the baseline of Segoe UI's line centered in the badge.
    /// </summary>
    private static Bitmap LabelBitmap(Media.FormattedText label, double em, Size size)
    {
        var segoe = new Media.FontFamily("Segoe UI");
        double baseline = (size.Height - segoe.LineSpacing * em) / 2 + segoe.Baseline * em;

        var visual = new Media.DrawingVisual();
        Media.TextOptions.SetTextFormattingMode(visual, Media.TextFormattingMode.Ideal);
        Media.TextOptions.SetTextRenderingMode(visual, Media.TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
            dc.DrawText(label, new System.Windows.Point((size.Width - label.WidthIncludingTrailingWhitespace) / 2, baseline - label.Baseline));
        var target = new Media.Imaging.RenderTargetBitmap(size.Width, size.Height, 96, 96, Media.PixelFormats.Pbgra32);
        target.Render(visual);

        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        target.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Stride * size.Height, data.Stride);
        bitmap.UnlockBits(data);
        return bitmap;
    }

    /// <summary>
    /// Puts <paramref name="face"/> (opaque, the size of <paramref name="body"/>) inside a rounded shape on a
    /// transparent canvas, over its shadow. Premultiplied ARGB.
    /// </summary>
    public static Bitmap Compose(Bitmap face, Size canvas, Rectangle body, float radius, Shadow? shadow)
    {
        float[] cover;
        using (var path = RoundedRect(body, radius))
            cover = Coverage(canvas, path);

        var shade = new float[cover.Length];
        if (shadow is { } s)
        {
            var shape = RectangleF.Inflate(body, s.Spread, s.Spread);
            shape.Offset(0, s.OffsetY);
            using (var path = RoundedRect(shape, Math.Max(0, radius + s.Spread)))
                shade = Coverage(canvas, path);
            shade = Blur(shade, canvas.Width, canvas.Height, BlurRadius(s.Blur));
            for (int i = 0; i < shade.Length; i++) shade[i] *= s.Color.A / 255f;
        }

        var facePixels = Pixels(face);
        var shadowColor = shadow?.Color ?? Color.Black;
        var outPixels = new int[canvas.Width * canvas.Height];
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                int i = y * canvas.Width + x;
                float c = cover[i], sh = shade[i] * (1 - c);
                int fx = x - body.X, fy = y - body.Y;
                int f = c > 0 && fx >= 0 && fy >= 0 && fx < body.Width && fy < body.Height
                    ? facePixels[fy * body.Width + fx] : 0;
                // Premultiplied: the face over its shadow.
                outPixels[i] = Motion.Pack(255 * (c + sh),
                    ((f >> 16) & 0xFF) * c + shadowColor.R * sh, ((f >> 8) & 0xFF) * c + shadowColor.G * sh,
                    (f & 0xFF) * c + shadowColor.B * sh);
            }

        var image = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppPArgb);
        var data = image.LockBits(new Rectangle(Point.Empty, canvas), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        for (int y = 0; y < canvas.Height; y++)
            Marshal.Copy(outPixels, y * canvas.Width, data.Scan0 + y * data.Stride, canvas.Width);
        image.UnlockBits(data);
        return image;
    }

    /// <summary>How far past its shape a shadow reaches.</summary>
    public static int ShadowMargin(Shadow shadow) =>
        (int)Math.Ceiling(3 * BlurRadius(shadow.Blur) + Math.Abs(shadow.OffsetY) + Math.Max(0, shadow.Spread));

    /// <summary>The box size for <see cref="Blur"/> that looks like a CSS blur of <paramref name="blur"/> pixels.</summary>
    public static int BlurRadius(float blur)
    {
        if (blur <= 0) return 0;
        // CSS blurs by a Gaussian of half the blur radius; three box passes of radius r spread by sqrt(r² + r).
        float sigma = blur / 2;
        return Math.Max(1, (int)Math.Round((Math.Sqrt(1 + 4 * sigma * sigma) - 1) / 2));
    }

    public static int Px(float value, float scale) => (int)Math.Round(value * scale);

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));

    public static int[] Pixels(Bitmap bmp)
    {
        var pixels = new int[bmp.Width * bmp.Height];
        var data = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bmp.Height; y++)
            Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bmp.Width, bmp.Width);
        bmp.UnlockBits(data);
        return pixels;
    }

    /// <summary>How much of each pixel of a <paramref name="size"/> canvas the shape covers, antialiased.</summary>
    public static float[] Coverage(Size size, GraphicsPath path)
    {
        using var mask = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(mask))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.FillPath(Brushes.White, path);
        }
        var pixels = Pixels(mask);
        var cover = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) cover[i] = ((pixels[i] >> 24) & 0xFF) / 255f;
        return cover;
    }

    /// <summary>Three box blurs of radius <paramref name="radius"/>, which look close to a Gaussian one. Outside counts as 0.</summary>
    public static float[] Blur(float[] values, int w, int h, int radius)
    {
        if (radius <= 0) return values;
        var a = values;
        var t = new float[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            Box(a, t, w, h, radius, 1, w);
            Box(t, a, h, w, radius, w, 1);
        }
        return a;
    }

    /// <summary>
    /// A running-sum box blur along one direction: <paramref name="lines"/> lines of <paramref name="length"/> values,
    /// <paramref name="step"/> apart within a line and <paramref name="lineStep"/> apart between lines.
    /// </summary>
    private static void Box(float[] src, float[] dst, int length, int lines, int radius, int step, int lineStep)
    {
        float n = 2 * radius + 1;
        for (int line = 0; line < lines; line++)
        {
            int start = line * lineStep;
            float sum = 0;
            for (int k = 0; k <= Math.Min(radius, length - 1); k++) sum += src[start + k * step];
            for (int k = 0; k < length; k++)
            {
                dst[start + k * step] = sum / n;
                if (k + radius + 1 < length) sum += src[start + (k + radius + 1) * step];
                if (k - radius >= 0) sum -= src[start + (k - radius) * step];
            }
        }
    }

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
