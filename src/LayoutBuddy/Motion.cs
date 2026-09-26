using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LayoutBuddy;

/// <summary>An image as premultiplied ARGB pixels, row by row: what a layered window shows.</summary>
internal sealed class Picture
{
    public int Width { get; }
    public int Height { get; }
    public int[] Pixels { get; }
    public Size Size => new(Width, Height);

    public Picture(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new int[width * height];
    }

    public static Picture From(Bitmap bitmap)
    {
        var picture = new Picture(bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        for (int y = 0; y < bitmap.Height; y++)
            Marshal.Copy(data.Scan0 + y * data.Stride, picture.Pixels, y * bitmap.Width, bitmap.Width);
        bitmap.UnlockBits(data);
        return picture;
    }
}

/// <summary>The website's animation curves, and drawing one frame of an animation.</summary>
internal static class Motion
{
    /// <summary>
    /// The website's spring (stiffness 520, damping 34) going from 0 to 1: quick, overshooting by about 3%.
    /// Settled after <see cref="SpringSeconds"/>.
    /// </summary>
    public static float Spring(double seconds)
    {
        if (seconds <= 0) return 0;
        // Underdamped: decays at damping / 2 = 17 per second, rings at sqrt(520 - 17²).
        const double decay = 17, ring = 15.198684;
        double e = Math.Exp(-decay * seconds);
        return (float)(1 - e * (Math.Cos(ring * seconds) + decay / ring * Math.Sin(ring * seconds)));
    }

    public const double SpringSeconds = 0.45;

    /// <summary>The website's ease-out curve, cubic-bezier(.2, .8, .2, 1), for <paramref name="t"/> from 0 to 1.</summary>
    public static float EaseOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        // Find where the curve's x is t (Newton's method; x only grows), then read its y there.
        double u = t;
        for (int i = 0; i < 8; i++)
        {
            double error = Bezier(u, .2, .2) - t;
            if (Math.Abs(error) < 1e-5) break;
            double slope = 3 * (1 - u) * (1 - u) * .2 + 3 * u * u * .8; // derivative of x(u) with both x points at .2
            u = Math.Clamp(u - error / Math.Max(slope, 1e-3), 0, 1);
        }
        return (float)Bezier(u, .8, 1);
    }

    private static double Bezier(double u, double p1, double p2) =>
        3 * (1 - u) * (1 - u) * u * p1 + 3 * (1 - u) * u * u * p2 + u * u * u;

    /// <summary>
    /// Draws <paramref name="source"/> over <paramref name="target"/>, scaled by <paramref name="scale"/> around its middle,
    /// with that middle at <paramref name="center"/>, and faded to <paramref name="opacity"/>.
    /// </summary>
    public static void DrawOver(Picture target, Picture source, PointF center, float scale, float opacity)
    {
        opacity = Math.Clamp(opacity, 0, 1);
        if (opacity <= 0 || scale <= 0.01f) return;
        float halfW = source.Width * scale / 2, halfH = source.Height * scale / 2;
        int x0 = Math.Max(0, (int)MathF.Floor(center.X - halfW)), x1 = Math.Min(target.Width, (int)MathF.Ceiling(center.X + halfW));
        int y0 = Math.Max(0, (int)MathF.Floor(center.Y - halfH)), y1 = Math.Min(target.Height, (int)MathF.Ceiling(center.Y + halfH));
        var src = source.Pixels;
        var dst = target.Pixels;
        int sw = source.Width, sh = source.Height;
        int Sample(int x, int y) => x >= 0 && y >= 0 && x < sw && y < sh ? src[y * sw + x] : 0;

        for (int y = y0; y < y1; y++)
        {
            // Where this pixel's middle falls in the source.
            float sy = (y + .5f - center.Y) / scale + sh / 2f - .5f;
            int py = (int)MathF.Floor(sy);
            float fy = sy - py;
            for (int x = x0; x < x1; x++)
            {
                float sx = (x + .5f - center.X) / scale + sw / 2f - .5f;
                int px = (int)MathF.Floor(sx);
                float fx = sx - px;
                int p00 = Sample(px, py), p10 = Sample(px + 1, py), p01 = Sample(px, py + 1), p11 = Sample(px + 1, py + 1);
                if ((p00 | p10 | p01 | p11) == 0) continue;
                float w00 = (1 - fx) * (1 - fy), w10 = fx * (1 - fy), w01 = (1 - fx) * fy, w11 = fx * fy;
                float Channel(int shift) =>
                    (((p00 >> shift) & 0xFF) * w00 + ((p10 >> shift) & 0xFF) * w10 +
                     ((p01 >> shift) & 0xFF) * w01 + ((p11 >> shift) & 0xFF) * w11) * opacity;
                float a = Channel(24), r = Channel(16), g = Channel(8), b = Channel(0);
                int i = y * target.Width + x;
                int d = dst[i];
                float keep = 1 - a / 255f;
                dst[i] = Pack(a + ((d >> 24) & 0xFF) * keep, r + ((d >> 16) & 0xFF) * keep,
                    g + ((d >> 8) & 0xFF) * keep, b + (d & 0xFF) * keep);
            }
        }
    }

    public static int Pack(float a, float r, float g, float b) =>
        (Byte(a) << 24) | (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);

    private static int Byte(float v) => Math.Clamp((int)(v + .5f), 0, 255);
}
