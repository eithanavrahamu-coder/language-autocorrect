using System;
using AppKit;
using CoreGraphics;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Mac;

/// <summary>The menu bar icon: the keyboard's language as a small colored badge (gray while paused).</summary>
internal static class StatusBadge
{
    private const float Height = 16, Radius = 4.5f;

    public static NSImage Draw(Lang lang, bool dim)
    {
        var info = Languages.Get(lang);
        var color = dim ? NSColor.FromRgb(0x8E, 0x96, 0xA1) : Parse(info.Color);
        var attributes = new NSStringAttributes
        {
            Font = NSFont.BoldSystemFontOfSize(10.5f),
            ForegroundColor = NSColor.White,
        };
        var text = new Foundation.NSAttributedString(info.Badge, attributes);
        var size = text.Size;
        nfloat width = (nfloat)Math.Max(22, Math.Ceiling(size.Width) + 9);
        var image = NSImage.ImageWithSize(new CGSize(width, Height), false, rect =>
        {
            color.SetFill();
            NSBezierPath.FromRoundedRect(rect, Radius, Radius).Fill();
            text.DrawAtPoint(new CGPoint((rect.Width - size.Width) / 2, (rect.Height - size.Height) / 2));
            return true;
        });
        image.Template = false;
        image.AccessibilityDescription = info.Name;
        return image;
    }

    private static NSColor Parse(string hex)
    {
        int rgb = Convert.ToInt32(hex.TrimStart('#'), 16);
        return NSColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }
}
