using System.Drawing;

namespace LanguageAutocorrect.Engine;

/// <summary>
/// Where the cursor badge goes: just below and slightly right of the caret, or just above it when there's no room
/// below, for example on the last line of a text box at the bottom of a window or of the screen. Beside Windows' own
/// search and Start panels, which hide anything below or above their caret.
/// </summary>
public static class BadgePlacement
{
    /// <summary>The gap between the caret and the badge.</summary>
    public const int Gap = 3;

    /// <summary>How far right of the caret the badge starts.</summary>
    public const int Indent = 2;

    /// <summary>The badge's top-left corner on screen.</summary>
    /// <param name="caret">The text cursor (or, when it can't be found, the text box).</param>
    /// <param name="badge">The badge's size, without its shadow.</param>
    /// <param name="screen">The working area (without the taskbar) of the screen the caret is on.</param>
    /// <param name="window">The window being typed in, or empty if unknown.</param>
    public static Point Place(Rectangle caret, Size badge, Rectangle screen, Rectangle window)
    {
        // The badge stays in the window being typed in, so it doesn't hang over whatever is behind it. The window
        // only counts when the caret is really in it, and only its part on the screen.
        var room = screen;
        if (window.Contains(caret.Left, caret.Top + caret.Height / 2))
        {
            var visible = Rectangle.Intersect(window, screen);
            if (Fits(caret, badge, visible)) room = visible;
        }

        int below = caret.Bottom + Gap, above = caret.Top - Gap - badge.Height;
        int y = below + badge.Height <= room.Bottom ? below
            : above >= room.Top ? above
            // No room either way: the side with more of it, kept on screen.
            : room.Bottom - caret.Bottom >= caret.Top - room.Top ? Math.Max(room.Top, room.Bottom - badge.Height)
            : room.Top;
        int x = Math.Max(room.Left, Math.Min(caret.Left + Indent, room.Right - badge.Width));
        return new Point(x, y);
    }

    /// <summary>
    /// The badge's top-left corner beside <paramref name="cover"/>, level with the caret: for windows that Windows
    /// always draws over the badge (its search and Start panels), where it would be hidden below or above the caret.
    /// Left of the window, or right when there's no room on the left; null when neither side has room. It stays out of
    /// the taskbar (the search box is down in it), which can be drawn over the badge too.
    /// </summary>
    /// <param name="screen">The working area (without the taskbar) of the screen the caret is on.</param>
    public static Point? Beside(Rectangle caret, Size badge, Rectangle screen, Rectangle cover)
    {
        int left = cover.Left - Gap - badge.Width, right = cover.Right + Gap;
        int? x = left >= screen.Left ? left : right + badge.Width <= screen.Right ? right : null;
        if (x == null) return null;
        int y = caret.Top + caret.Height / 2 - badge.Height / 2;
        return new Point(x.Value, Math.Max(screen.Top, Math.Min(y, screen.Bottom - badge.Height)));
    }

    /// <summary>Whether the badge fits below or above the caret in <paramref name="room"/>.</summary>
    private static bool Fits(Rectangle caret, Size badge, Rectangle room) =>
        room.Width >= badge.Width &&
        (caret.Bottom + Gap + badge.Height <= room.Bottom || caret.Top - Gap - badge.Height >= room.Top);
}
