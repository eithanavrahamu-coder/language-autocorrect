using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using LanguageAutocorrect.Engine;
using Timer = System.Windows.Forms.Timer;

namespace LanguageAutocorrect;

/// <summary>
/// Makes a fixed word appear the way it does on the website: it sharpens out of a blur and glows for a moment.
/// The word is found by comparing pictures of its line from just before and just after the fix (see
/// <see cref="LineShot"/>), so it works in any app. The effect's own window is left out of those pictures.
/// </summary>
internal sealed class FixFlash : IDisposable
{
    // The website's timing: the word sharpens over 0.35 s; its glow holds, then fades out by 1.8 s.
    private const double SharpenSeconds = 0.35, GlowHoldSeconds = 0.54, GlowSeconds = 1.8;
    private const float GlowStrength = 0.22f;
    // How long the line may stay frozen while the app draws the fix, and how long to look for the word at all.
    private const double FreezeMs = 150, SearchMs = 700;

    private readonly FlashWindow _window = new();
    private readonly Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private Search? _search;
    private Glow? _glow;
    private Action<Rectangle?>? _located;

    public FixFlash()
    {
        _ = _window.Handle;
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>False before Windows 10 2004, which can't leave the effect's window out of the pictures.</summary>
    public bool Available => _window.ExcludedFromCapture;

    /// <summary>
    /// Called on the keyboard thread just before a fix is typed. Takes the picture of the line, then freezes the line
    /// on screen while the app draws the fix, so the new word doesn't show sharp before it sharpens in.
    /// </summary>
    public void BeforeFix(FixWord fix, FocusSnapshot focus)
    {
        // After Enter the word may be on another line, or sent (in chat apps).
        if (!Available || fix.Boundary != KeyKind.Space || focus.Caret is not { } caret) return;
        var window = Native.GetForegroundWindow();
        if (focus.Window != window) return;
        if (LineShot.LineAt(caret, TextArea(focus.FocusWindow, window, caret)) is not { } line) return;
        if (LineShot.Capture(line) is not { } before) return;

        long taken = Stopwatch.GetTimestamp();
        var shown = new ManualResetEventSlim();
        _window.BeginInvoke(() =>
        {
            // Too late to freeze: the app may already be drawing the fix.
            bool freeze = Stopwatch.GetElapsedTime(taken).TotalMilliseconds < 25;
            Begin(new Search(before, fix.Text.Length, window), freeze);
            shown.Set();
        });
        shown.Wait(25);
    }

    /// <summary>
    /// Calls <paramref name="then"/> with where the fixed word is once it's found, or with null if it won't be.
    /// </summary>
    public void WhenLocated(Action<Rectangle?> then)
    {
        if (_search == null)
        {
            then(null);
            return;
        }
        Notify(null);
        _located = then;
    }

    /// <summary>Ends the effect now (the fix was undone, for instance).</summary>
    public void Cancel() => Stop();

    private void Begin(Search search, bool freeze)
    {
        Stop();
        _search = search;
        if (freeze && _window.ShowPicture(search.Before.Opaque(), search.Before.Bounds.Location)) search.Frozen = true;
        _clock.Restart();
        _timer.Start();
    }

    private void Tick()
    {
        try
        {
            if (_search != null) Look(_search);
            else if (_glow != null) Play(_glow);
            else _timer.Stop();
        }
        catch (Exception ex)
        {
            Log.Write("Fix effect error: " + ex.Message);
            Stop();
        }
    }

    /// <summary>Waits for the app to finish drawing the fix, then finds the word.</summary>
    private void Look(Search s)
    {
        double ms = _clock.Elapsed.TotalMilliseconds;
        if (Native.GetForegroundWindow() != s.Window)
        {
            Stop();
            return;
        }
        if (ms > SearchMs)
        {
            bool changed = s.Last != null && s.Last.ChangedColumns(s.Before) > 0;
            Stop("the fixed word wasn't found on its line (the line " + (changed ? "changed too much)" : "didn't change)"));
            return;
        }
        if (s.Frozen && ms > FreezeMs)
        {
            s.Frozen = false;
            _window.Hide();
        }
        if (LineShot.Capture(s.Before.Bounds) is not { } now)
        {
            Stop();
            return;
        }
        // Done drawing when the line looks the same twice in a row (apart from the caret blinking).
        int caretBlink = Math.Max(3, s.Before.Bounds.Height / 6);
        s.Stable = s.Last != null && now.ChangedColumns(s.Last) <= caretBlink ? s.Stable + 1 : 0;
        s.Last = now;
        if (s.Stable < 1 || ms < 30 || now.ChangedColumns(s.Before) <= caretBlink) return;
        if (now.FindNewText(s.Before, s.Length) is not { } spot) return;

        _search = null;
        _glow = new Glow(spot, now, s.Window, sharpen: s.Frozen);
        _clock.Restart();
        Play(_glow);
        Notify(spot.Box);
    }

    private void Play(Glow glow)
    {
        double t = _clock.Elapsed.TotalSeconds;
        if (t >= GlowSeconds || Native.GetForegroundWindow() != glow.Window)
        {
            Stop();
            return;
        }
        // Typing on can move the word sideways (Hebrew in a left-to-right box): the effect goes with it.
        // Gone or changed (undone, scrolled away): the effect would be in the wrong place.
        if (!glow.Follow())
        {
            Stop($"the word changed or left its line after {_clock.ElapsedMilliseconds} ms");
            return;
        }
        float strength = t <= GlowHoldSeconds
            ? GlowStrength
            : GlowStrength * (1 - Motion.EaseOut((t - GlowHoldSeconds) / (GlowSeconds - GlowHoldSeconds)));
        var frame = glow.Sharpen && t < SharpenSeconds ? glow.SharpeningFrame(Motion.EaseOut(t / SharpenSeconds), strength) : glow.GlowFrame(strength);
        _window.ShowPicture(frame, glow.Area.Location);
    }

    /// <param name="why">Written to the log when the effect ends before its time.</param>
    private void Stop(string? why = null)
    {
        if (why != null) Log.Write("Fix effect: " + why);
        _search = null;
        _glow = null;
        _timer.Stop();
        if (_window.Visible) _window.Hide();
        Notify(null);
    }

    private void Notify(Rectangle? box)
    {
        var then = _located;
        _located = null;
        then?.Invoke(box);
    }

    /// <summary>The part of the window the caret is in (the text area, in most apps), or else the whole window.</summary>
    private static Rectangle TextArea(IntPtr focus, IntPtr window, Rectangle caret)
    {
        if (focus != IntPtr.Zero && Native.GetWindowRect(focus, out var f))
        {
            var r = Rectangle.FromLTRB(f.Left, f.Top, f.Right, f.Bottom);
            if (r.Contains(caret.Location)) return r;
        }
        return Native.VisibleBounds(window);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _window.Dispose();
    }

    private sealed class Search(LineShot before, int length, IntPtr window)
    {
        public LineShot Before { get; } = before;
        public int Length { get; } = length;
        public IntPtr Window { get; } = window;
        public LineShot? Last;
        public int Stable;
        public bool Frozen;
    }

    /// <summary>The effect for one found word: what it draws, and where.</summary>
    private sealed class Glow
    {
        public Rectangle Area { get; private set; } // the effect's window, on screen
        public IntPtr Window { get; }
        public bool Sharpen { get; }

        private readonly Rectangle _line; // the word's line on screen
        private Rectangle _word; // the word on screen, hidden under the effect while it sharpens
        private readonly Rectangle _erase; // the same, in Area
        // The word's upper part (along the bottom, spell-check squiggles come and go), to find it again: the middles of
        // its strokes (_inked) and the clear background around them (_clear). The soft edges in between are left out,
        // as they change when the word moves by part of a pixel (web pages place letters that finely).
        private Rectangle _look;
        private readonly int[] _inked, _clear;
        private readonly int _strokeMiddle;
        private readonly float[] _dr, _dg, _db; // the word minus the background, per color, in Area
        private readonly float[] _glowCover; // how much of each pixel the glow covers
        private readonly int _background, _ink, _glowColor;
        private readonly float _maxSigma; // the website's blur(6px) at its text size, scaled to this line

        public Glow(WordSpot spot, LineShot after, IntPtr window, bool sharpen)
        {
            Window = window;
            Sharpen = sharpen;
            var line = after.Bounds;
            int h = line.Height;
            // The website's glow: the word's box with rounded corners, the height of the line.
            int pad = Math.Max(1, (int)Math.Round(h * 0.08));
            var glowBox = Rectangle.FromLTRB(Math.Max(line.Left, spot.Box.Left - pad), line.Top,
                Math.Min(line.Right, spot.Box.Right + pad), line.Bottom);
            // (The website's caret, about as tall as the line, is 21.6px.)
            _maxSigma = Math.Clamp(h * 6 / 21.6f, 2, 14);
            int margin = sharpen ? 3 * ((int)Math.Ceiling(BoxRadius(_maxSigma)) + 1) + 1 : 0;
            Area = Rectangle.Inflate(glowBox, margin, margin);

            _line = line;
            _word = Rectangle.Intersect(Rectangle.Inflate(spot.Box, 1, 1), line);
            _erase = _word with { X = _word.X - Area.X, Y = _word.Y - Area.Y };
            var wordPixels = after.Crop(_word);
            _background = spot.Background;

            int squiggles = line.Top + (int)(h * 0.72f);
            _look = squiggles - _word.Top >= 2
                ? Rectangle.FromLTRB(_word.Left, _word.Top, _word.Right, Math.Min(_word.Bottom, squiggles))
                : _word;
            var look = after.Crop(_look);
            // Stroke middles: at least half as far from the background as the darkest (or lightest) of the letters.
            _strokeMiddle = Math.Max(90, LineShot.Distance(spot.Ink, _background) / 2);
            var inked = new List<int>();
            var clear = new List<int>();
            for (int i = 0; i < look.Length; i++)
            {
                int d = LineShot.Distance(look[i], _background);
                if (d > _strokeMiddle) inked.Add(i);
                else if (d < 20) clear.Add(i);
            }
            (_inked, _clear) = (inked.ToArray(), clear.ToArray());
            _ink = spot.Ink;
            // The website's accent color, lighter on dark backgrounds as in its dark mode.
            _glowColor = Luminance(_background) < 0.45f ? 0x8B7CFF : 0x5847E0;

            int n = Area.Width * Area.Height;
            (_dr, _dg, _db) = (new float[n], new float[n], new float[n]);
            for (int y = 0; y < _erase.Height; y++)
                for (int x = 0; x < _erase.Width; x++)
                {
                    int p = wordPixels[y * _erase.Width + x], i = (y + _erase.Y) * Area.Width + x + _erase.X;
                    _dr[i] = ((p >> 16) & 0xFF) - ((_background >> 16) & 0xFF);
                    _dg[i] = ((p >> 8) & 0xFF) - ((_background >> 8) & 0xFF);
                    _db[i] = (p & 0xFF) - (_background & 0xFF);
                }

            var box = new RectangleF(glowBox.X - Area.X, glowBox.Y - Area.Y, glowBox.Width, glowBox.Height);
            using var path = BadgeArt.RoundedRect(box, Math.Max(2, h * 5 / 21.6f));
            _glowCover = BadgeArt.Coverage(Area.Size, path);
        }

        /// <summary>
        /// The word partway through sharpening (<paramref name="progress"/> 0 → 1): the background with the word
        /// fading in and coming into focus over it, then the glow.
        /// </summary>
        public Picture SharpeningFrame(float progress, float glow)
        {
            float sigma = _maxSigma * (1 - progress);
            var (r, g, b) = (Blurred(_dr, sigma), Blurred(_dg, sigma), Blurred(_db, sigma));
            int w = Area.Width;
            float bgR = (_background >> 16) & 0xFF, bgG = (_background >> 8) & 0xFF, bgB = _background & 0xFF;
            // The ink's direction away from the background: the blur spilling past the word is drawn as ink.
            float kr = ((_ink >> 16) & 0xFF) - bgR, kg = ((_ink >> 8) & 0xFF) - bgG, kb = (_ink & 0xFF) - bgB;
            float k2 = Math.Max(1, kr * kr + kg * kg + kb * kb);

            var frame = new Picture(w, Area.Height);
            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                int x = i % w, y = i / w;
                float a, pr, pg, pb;
                if (_erase.Contains(x, y))
                {
                    (a, pr, pg, pb) = (255, bgR + progress * r[i], bgG + progress * g[i], bgB + progress * b[i]);
                }
                else
                {
                    float c = Math.Clamp((r[i] * kr + g[i] * kg + b[i] * kb) / k2, 0, 1) * progress;
                    (a, pr, pg, pb) = (255 * c, (bgR + kr) * c, (bgG + kg) * c, (bgB + kb) * c);
                }
                frame.Pixels[i] = Over(glow * _glowCover[i], a, pr, pg, pb);
            }
            return frame;
        }

        public Picture GlowFrame(float glow)
        {
            var frame = new Picture(Area.Width, Area.Height);
            for (int i = 0; i < frame.Pixels.Length; i++)
                if (_glowCover[i] > 0) frame.Pixels[i] = Over(glow * _glowCover[i], 0, 0, 0, 0);
            return frame;
        }

        /// <summary>The glow color at <paramref name="strength"/> over a premultiplied pixel.</summary>
        private int Over(float strength, float a, float r, float g, float b)
        {
            float keep = 1 - strength;
            return Motion.Pack(255 * strength + a * keep, ((_glowColor >> 16) & 0xFF) * strength + r * keep,
                ((_glowColor >> 8) & 0xFF) * strength + g * keep, (_glowColor & 0xFF) * strength + b * keep);
        }

        /// <summary><paramref name="values"/> under a Gaussian blur of <paramref name="sigma"/> (between box sizes, smoothly).</summary>
        private float[] Blurred(float[] values, float sigma)
        {
            float radius = BoxRadius(sigma);
            int r0 = (int)radius;
            float mix = radius - r0;
            var low = BadgeArt.Blur((float[])values.Clone(), Area.Width, Area.Height, r0);
            if (mix < 0.02f) return low;
            var high = BadgeArt.Blur((float[])values.Clone(), Area.Width, Area.Height, r0 + 1);
            for (int i = 0; i < low.Length; i++) low[i] += (high[i] - low[i]) * mix;
            return low;
        }

        /// <summary>
        /// Finds the word on its line again, following it if it moved sideways (the nearest match wins).
        /// False if it's nowhere on the line anymore, or looks different.
        /// </summary>
        public bool Follow()
        {
            if (LineShot.Capture(_line) is not { } now) return false;
            int lineWidth = _line.Width, w = _look.Width;
            int top = _look.Top - _line.Top, x0 = _look.Left - _line.Left;
            int allowed = Math.Max(3, (_inked.Length + _clear.Length) * 4 / 100);
            for (int step = 0; step <= 2 * lineWidth; step++)
            {
                int dx = (step + 1) / 2 * (step % 2 == 1 ? 1 : -1); // 0, 1, -1, 2, -2...
                int x = x0 + dx;
                if (x < 0 || x + w > lineWidth || !Matches(now, x, top, allowed)) continue;
                if (dx != 0) Move(dx);
                return true;
            }
            return false;
        }

        private bool Matches(LineShot now, int x, int top, int allowed)
        {
            int w = _look.Width, lineWidth = _line.Width, wrong = 0;
            int Ink(int i) => LineShot.Distance(now.Pixels[(top + i / w) * lineWidth + x + i % w], _background);
            foreach (int i in _inked)
                if (Ink(i) < 60 && ++wrong > allowed) return false;
            foreach (int i in _clear)
                if (Ink(i) > _strokeMiddle && ++wrong > allowed) return false;
            return true;
        }

        private void Move(int dx)
        {
            Area = Area with { X = Area.X + dx };
            _word = _word with { X = _word.X + dx };
            _look = _look with { X = _look.X + dx };
        }

        /// <summary>Three box blurs of this radius spread like a Gaussian of <paramref name="sigma"/>: r² + r = σ².</summary>
        private static float BoxRadius(float sigma) => (MathF.Sqrt(1 + 4 * sigma * sigma) - 1) / 2;

        private static float Luminance(int rgb) =>
            (0.2126f * ((rgb >> 16) & 0xFF) + 0.7152f * ((rgb >> 8) & 0xFF) + 0.0722f * (rgb & 0xFF)) / 255;
    }

    /// <summary>The effect's window: never in screenshots or screen captures (including ours).</summary>
    private sealed class FlashWindow : LayeredWindow
    {
        public bool ExcludedFromCapture { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ExcludedFromCapture = Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE);
        }

        public bool ShowPicture(Picture picture, Point location)
        {
            if (!Present(picture, location)) return false;
            if (!Visible) Show();
            return true;
        }
    }
}
