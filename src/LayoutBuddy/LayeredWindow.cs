using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LayoutBuddy;

/// <summary>
/// A borderless, always-on-top window that shows exactly the picture it's given, soft edges and shadow included.
/// It never takes the focus, and clicks go through it unless <see cref="ClickThrough"/> is turned off.
/// </summary>
internal abstract class LayeredWindow : Form
{
    private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
        WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x8000000;
    private const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;

    private bool _clickThrough = true;

    protected LayeredWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            if (_clickThrough) cp.ExStyle |= WS_EX_TRANSPARENT;
            return cp;
        }
    }

    /// <summary>Whether clicks go through to whatever is below.</summary>
    protected bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (value == _clickThrough) return;
            _clickThrough = value;
            if (!IsHandleCreated) return;
            long style = Native.GetWindowLongPtr(Handle, Native.GWL_EXSTYLE).ToInt64();
            style = value ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
            Native.SetWindowLongPtr(Handle, Native.GWL_EXSTYLE, new IntPtr(style));
        }
    }

    /// <summary>Shows <paramref name="picture"/> with its top-left corner at <paramref name="location"/> (screen pixels).</summary>
    protected bool Present(Picture picture, Point location, byte opacity = 255)
    {
        var screenDc = Native.GetDC(IntPtr.Zero);
        var memDc = Native.CreateCompatibleDC(screenDc);
        var header = new Native.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(), biWidth = picture.Width, biHeight = -picture.Height,
            biPlanes = 1, biBitCount = 32,
        };
        var bitmap = Native.CreateDIBSection(screenDc, ref header, 0, out var bits, IntPtr.Zero, 0);
        var old = bitmap != IntPtr.Zero ? Native.SelectObject(memDc, bitmap) : IntPtr.Zero;
        try
        {
            if (bitmap == IntPtr.Zero) return false;
            Marshal.Copy(picture.Pixels, 0, bits, picture.Pixels.Length);
            var dst = new Native.POINT { X = location.X, Y = location.Y };
            var size = new Native.SIZE { cx = picture.Width, cy = picture.Height };
            var src = new Native.POINT();
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = opacity, AlphaFormat = Native.AC_SRC_ALPHA,
            };
            if (!Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA))
                return false;
            UpdateBounds(); // WinForms doesn't notice the window moved
            return true;
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                Native.SelectObject(memDc, old);
                Native.DeleteObject(bitmap);
            }
            Native.DeleteDC(memDc);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void WndProc(ref Message m)
    {
        // A click (on a window that takes clicks) must leave the focus in the app being typed in.
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        // Don't take the suggested size: the picture is redrawn for the new DPI.
        e.Cancel = true;
        base.OnDpiChanged(e);
    }
}
