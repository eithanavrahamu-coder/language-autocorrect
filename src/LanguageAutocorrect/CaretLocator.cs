using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace LanguageAutocorrect;

/// <summary>
/// Finds the text caret on screen, trying (1) Win32 caret, (2) MSAA caret object,
/// (3) UI Automation text selection of the focused element, (4) the focused text box itself.
/// Returns screen coordinates in physical pixels.
/// </summary>
internal static class CaretLocator
{
    /// <param name="focusedElement">Gets the focused element through UI Automation (slower), or null to skip it.</param>
    public static Rectangle? Find(Native.GUITHREADINFO gti, IntPtr focusWindow, Func<AutomationElement?>? focusedElement)
    {
        return FromWin32(gti) ?? FromMsaa(focusWindow) ?? (focusedElement?.Invoke() is { } el ? FromUia(el) : null);
    }

    private static Rectangle? FromWin32(Native.GUITHREADINFO gti)
    {
        if (gti.hwndCaret == IntPtr.Zero) return null;
        var r = gti.rcCaret;
        if (r.Right - r.Left <= 0 && r.Bottom - r.Top <= 0) return null;
        var tl = new Native.POINT { X = r.Left, Y = r.Top };
        var br = new Native.POINT { X = r.Right, Y = r.Bottom };
        if (!Native.ClientToScreen(gti.hwndCaret, ref tl) || !Native.ClientToScreen(gti.hwndCaret, ref br)) return null;
        return Valid(Rectangle.FromLTRB(tl.X, tl.Y, Math.Max(br.X, tl.X + 1), br.Y));
    }

    private static Rectangle? FromMsaa(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;
        object? obj = null;
        try
        {
            var iid = Native.IID_IAccessible;
            if (Native.AccessibleObjectFromWindow(hwnd, Native.OBJID_CARET, ref iid, out obj) != 0 || obj is not Accessibility.IAccessible acc)
                return null;
            acc.accLocation(out int l, out int t, out int w, out int h, 0);
            if (w <= 0 && h <= 0) return null;
            return Valid(new Rectangle(l, t, Math.Max(w, 1), h));
        }
        catch
        {
            return null;
        }
        finally
        {
            if (obj != null && Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
        }
    }

    private static Rectangle? FromUia(AutomationElement el)
    {
        try
        {
            return FromTextPattern(el) ?? FromBounds(el);
        }
        catch
        {
            return null;
        }
    }

    private static Rectangle? FromTextPattern(AutomationElement el)
    {
        if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var p)) return null;
        var sel = ((TextPattern)p).GetSelection();
        if (sel.Length == 0) return null;
        var range = sel[0].Clone();
        // An empty selection (just a caret) has no rectangle; measure the character next to it.
        var rects = range.GetBoundingRectangles();
        bool atEnd = false;
        if (rects.Length == 0)
        {
            range.ExpandToEnclosingUnit(TextUnit.Character);
            rects = range.GetBoundingRectangles();
            if (rects.Length == 0)
            {
                // Caret at the very end: use the previous character's right edge.
                range = sel[0].Clone();
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -1);
                rects = range.GetBoundingRectangles();
                atEnd = true;
            }
        }
        if (rects.Length == 0) return null;
        var r = rects[0];
        int x = (int)(atEnd ? r.Right : r.Left);
        return Valid(new Rectangle(x, (int)r.Top, 1, Math.Max(1, (int)r.Height)));
    }

    /// <summary>
    /// An empty text box has no text to measure, and some text boxes can't be measured at all:
    /// use the box itself, from its left edge (a one-line box's full height, so the badge sits just below it).
    /// </summary>
    private static Rectangle? FromBounds(AutomationElement el)
    {
        var c = el.Current;
        if (c.ControlType != ControlType.Edit) return null;
        var b = c.BoundingRectangle;
        if (b.IsEmpty || b.Width < 1 || b.Height < 1) return null;
        return Valid(new Rectangle((int)b.Left + 4, (int)b.Top, 1, (int)Math.Min(b.Height, 60)));
    }

    private static Rectangle? Valid(Rectangle r) =>
        r.Height > 0 && r.Height < 400 && !(r.X == 0 && r.Y == 0) ? r : null;
}
