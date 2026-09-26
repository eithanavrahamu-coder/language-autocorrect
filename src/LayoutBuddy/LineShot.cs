using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LayoutBuddy;

/// <summary>Where a fixed word ended up on screen, with the colors around it.</summary>
internal sealed record WordSpot(Rectangle Box, int Background, int Ink);

/// <summary>
/// A picture of one line of text on screen. Comparing the line just before and just after a fix shows where the
/// fixed word is, in any app and any script, without the app having to tell us.
/// </summary>
internal sealed class LineShot
{
    // How far apart two colors must be (R, G and B differences added up) to count as a change, or as text.
    private const int ChangeThreshold = 60, InkThreshold = 90;

    public Rectangle Bounds { get; }

    /// <summary>0xRRGGBB, row by row.</summary>
    public int[] Pixels { get; }

    private LineShot(Rectangle bounds, int[] pixels)
    {
        Bounds = bounds;
        Pixels = pixels;
    }

    /// <summary>The caret's line, reaching well to both sides but staying inside <paramref name="window"/>.</summary>
    public static Rectangle? LineAt(Rectangle caret, Rectangle window)
    {
        if (caret.Height < 6 || caret.Height > 80) return null;
        int reach = caret.Height * 30;
        var line = Rectangle.FromLTRB(caret.Left - reach, caret.Top, caret.Left + reach, caret.Bottom);
        line.Intersect(window);
        line.Intersect(Screen.FromPoint(caret.Location).Bounds);
        return line.Width >= caret.Height * 2 && line.Height == caret.Height ? line : null;
    }

    public static LineShot? Capture(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;
        var screen = Native.GetDC(IntPtr.Zero);
        var mem = Native.CreateCompatibleDC(screen);
        var header = new Native.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(), biWidth = bounds.Width, biHeight = -bounds.Height,
            biPlanes = 1, biBitCount = 32,
        };
        var dib = Native.CreateDIBSection(screen, ref header, 0, out var bits, IntPtr.Zero, 0);
        try
        {
            if (dib == IntPtr.Zero) return null;
            var old = Native.SelectObject(mem, dib);
            bool ok = Native.BitBlt(mem, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y,
                Native.SRCCOPY | Native.CAPTUREBLT);
            Native.SelectObject(mem, old);
            if (!ok) return null;
            var pixels = new int[bounds.Width * bounds.Height];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (int i = 0; i < pixels.Length; i++) pixels[i] &= 0xFFFFFF;
            return new LineShot(bounds, pixels);
        }
        finally
        {
            if (dib != IntPtr.Zero) Native.DeleteObject(dib);
            Native.DeleteDC(mem);
            Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>The number of columns that look different in <paramref name="other"/> (a picture of the same place).</summary>
    public int ChangedColumns(LineShot other)
    {
        int w = Bounds.Width, changed = 0;
        for (int x = 0; x < w; x++)
            for (int i = x; i < Pixels.Length; i += w)
                if (Distance(Pixels[i], other.Pixels[i]) > ChangeThreshold)
                {
                    changed++;
                    break;
                }
        return changed;
    }

    /// <summary>
    /// Where new text appeared compared to <paramref name="before"/>: pixels that changed and are now text.
    /// Null if nothing new shows yet, or if too much of the line changed to tell (it scrolled or reflowed).
    /// </summary>
    public WordSpot? FindNewText(LineShot before, int length)
    {
        int w = Bounds.Width, h = Bounds.Height;
        int bg = MostCommon(Pixels);
        bool IsNew(int i) => Distance(Pixels[i], before.Pixels[i]) > ChangeThreshold && Distance(Pixels[i], bg) > InkThreshold;

        var column = new int[w];
        var highest = new int[w]; // the top row with something new, per column
        Array.Fill(highest, h);
        for (int i = 0; i < Pixels.Length; i++)
            if (IsNew(i))
            {
                column[i % w]++;
                highest[i % w] = Math.Min(highest[i % w], i / w);
            }

        // Runs of columns with something new in them.
        var runs = new List<(int From, int To, int Tallest)>();
        for (int x = 0; x < w; x++)
        {
            if (column[x] == 0) continue;
            int from = x, tallest = 0, reaches = h;
            while (x < w && column[x] > 0)
            {
                reaches = Math.Min(reaches, highest[x]);
                tallest = Math.Max(tallest, column[x++]);
            }
            // Only along the bottom of the line: a spell-check squiggle or an underline appearing, not letters.
            if (reaches >= h * 0.72f) continue;
            runs.Add((from, x - 1, tallest));
        }
        // The caret, a thin mark as tall as the line, moved to the end of the new text: it isn't part of it.
        int thin = Math.Max(2, h / 10);
        bool IsCaret((int From, int To, int Tallest) r) => r.To - r.From + 1 <= thin && r.Tallest >= h / 2;
        if (runs.Count > 0 && IsCaret(runs[^1])) runs.RemoveAt(runs.Count - 1);
        if (runs.Count > 0 && IsCaret(runs[0])) runs.RemoveAt(0);
        if (runs.Count == 0) return null;

        int left = runs[0].From, right = runs[^1].To;
        // Rows that are lines across the picture (a text box's border, an underline) aren't letters.
        var rule = new bool[h];
        int lettersRows = 0;
        for (int y = 0; y < h; y++)
        {
            int inked = 0;
            for (int x = 0; x < w; x++)
                if (Distance(Pixels[y * w + x], bg) > InkThreshold) inked++;
            rule[y] = inked > w / 2;
            if (!rule[y]) lettersRows++;
        }
        // Letters that look the same as before (a word typed over one of similar shape) belong to the word too:
        // take in the text touching the change, up to a gap as wide as a space.
        var text = new bool[w];
        for (int x = 0; x < w; x++)
        {
            int inked = 0;
            for (int y = 0; y < h; y++)
                if (!rule[y] && Distance(Pixels[y * w + x], bg) > InkThreshold) inked++;
            text[x] = inked > 0 && inked < lettersRows * 0.9f; // a line the full height is an edge or the caret
        }
        int space = Math.Max(2, (int)Math.Round(h * 0.12));
        int Reach(int from, int step)
        {
            int last = from, gap = 0;
            for (int x = from + step; x >= 0 && x < w && gap < space; x += step)
            {
                if (text[x]) (last, gap) = (x, 0);
                else gap++;
            }
            return last;
        }
        (left, right) = (Reach(left, -1), Reach(right, 1));

        // Much wider than the text could be: the whole line moved (it scrolled, or the words were reordered).
        float em = h * 0.75f;
        if (right - left + 1 > length * em * 1.1f + em) return null;

        int top = h, bottom = -1, ink = bg, inkDistance = 0;
        for (int y = 0; y < h; y++)
            for (int x = left; x <= right; x++)
            {
                int i = y * w + x;
                if (!text[x] || rule[y] || Distance(Pixels[i], bg) <= InkThreshold) continue;
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
                int d = Distance(Pixels[i], bg);
                if (d > inkDistance)
                {
                    inkDistance = d;
                    ink = Pixels[i];
                }
            }
        var box = Rectangle.FromLTRB(Bounds.X + left, Bounds.Y + top, Bounds.X + right + 1, Bounds.Y + bottom + 1);
        return new WordSpot(box, bg, ink);
    }

    /// <summary>The picture as it is, to show on screen (frozen) while the app draws the fix.</summary>
    public Picture Opaque()
    {
        var picture = new Picture(Bounds.Width, Bounds.Height);
        for (int i = 0; i < Pixels.Length; i++) picture.Pixels[i] = Pixels[i] | unchecked((int)0xFF000000);
        return picture;
    }

    /// <summary>The pixels of <paramref name="area"/> (inside <see cref="Bounds"/>), row by row.</summary>
    public int[] Crop(Rectangle area)
    {
        var r = Rectangle.Intersect(area, Bounds);
        var crop = new int[area.Width * area.Height];
        for (int y = r.Top; y < r.Bottom; y++)
            Array.Copy(Pixels, (y - Bounds.Y) * Bounds.Width + (r.X - Bounds.X), crop, (y - area.Y) * area.Width + (r.X - area.X), r.Width);
        return crop;
    }

    public static int Distance(int a, int b) =>
        Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) + Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) +
        Math.Abs((a & 0xFF) - (b & 0xFF));

    private static int MostCommon(int[] pixels)
    {
        var counts = new Dictionary<int, int>();
        int best = 0, bestCount = 0;
        foreach (int p in pixels)
        {
            counts.TryGetValue(p, out int n);
            counts[p] = ++n;
            if (n > bestCount)
            {
                bestCount = n;
                best = p;
            }
        }
        return best;
    }
}
