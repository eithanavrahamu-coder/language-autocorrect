using System.Drawing;
using LanguageAutocorrect.Engine;

namespace LanguageAutocorrect.Engine.Tests;

public class BadgePlacementTests
{
    private static readonly Size Badge = new(26, 17);
    private static readonly Rectangle Screen = new(0, 0, 1920, 1040); // above a 40 px taskbar
    private static readonly Rectangle Window = new(200, 100, 800, 600); // bottom edge at 700

    private static Rectangle CaretAt(int x, int top) => new(x, top, 1, 20);

    [Fact]
    public void GoesBelowTheCaret()
    {
        var caret = CaretAt(300, 400);
        Assert.Equal(new Point(302, 423), BadgePlacement.Place(caret, Badge, Screen, Window));
    }

    [Fact]
    public void GoesAboveAtTheBottomOfTheWindow()
    {
        // The caret ends 10 px above the window's bottom edge: the badge (3 + 17 px) doesn't fit below.
        var caret = CaretAt(300, 670);
        Assert.Equal(new Point(302, 650), BadgePlacement.Place(caret, Badge, Screen, Window));
    }

    [Fact]
    public void GoesBelowWhenItJustFits()
    {
        var caret = CaretAt(300, 660); // badge bottom at 680 + 20 = 700, the window's edge
        Assert.Equal(700, BadgePlacement.Place(caret, Badge, Screen, Window).Y + Badge.Height);
    }

    [Fact]
    public void GoesAboveAtTheBottomOfTheScreen()
    {
        var caret = CaretAt(300, 1015);
        Assert.Equal(new Point(302, 995), BadgePlacement.Place(caret, Badge, Screen, Rectangle.Empty));
    }

    [Fact]
    public void GoesAboveWhenTheWindowRunsOffTheScreen()
    {
        var tall = new Rectangle(200, 100, 800, 2000);
        var caret = CaretAt(300, 1015);
        Assert.Equal(995, BadgePlacement.Place(caret, Badge, Screen, tall).Y);
    }

    [Fact]
    public void IgnoresAWindowTheCaretIsNotIn()
    {
        // Near the bottom of the screen but outside the window: only the screen counts.
        var caret = CaretAt(1500, 900);
        Assert.Equal(923, BadgePlacement.Place(caret, Badge, Screen, Window).Y);
    }

    [Fact]
    public void IgnoresAWindowTooShortForTheBadge()
    {
        // A one-line box that is the whole window: there's no room in it, so the badge may go below it.
        var bar = new Rectangle(200, 100, 800, 30);
        var caret = CaretAt(300, 105);
        Assert.Equal(128, BadgePlacement.Place(caret, Badge, Screen, bar).Y);
    }

    [Fact]
    public void GoesAboveInTheTaskbar()
    {
        // Typing in the taskbar's search box: the caret is below the working area.
        var caret = CaretAt(300, 1050);
        Assert.Equal(1030, BadgePlacement.Place(caret, Badge, Screen, Rectangle.Empty).Y);
    }

    [Fact]
    public void StaysInsideOnTheRight()
    {
        var caret = CaretAt(995, 400);
        Assert.Equal(1000 - Badge.Width, BadgePlacement.Place(caret, Badge, Screen, Window).X);
        caret = CaretAt(1915, 400);
        Assert.Equal(1920 - Badge.Width, BadgePlacement.Place(caret, Badge, Screen, Rectangle.Empty).X);
    }

    // Windows search, as measured on a 1920 x 1080 screen: the panel, and its search box down in the taskbar.
    private static readonly Rectangle SearchPanel = new(663, 142, 858, 938);
    private static readonly Rectangle SearchCaret = new(712, 1046, 1, 19);

    [Fact]
    public void GoesLeftOfTheSearchPanelAboveTheTaskbar()
    {
        var pos = BadgePlacement.Beside(SearchCaret, Badge, Screen, SearchPanel);
        Assert.Equal(new Point(663 - 3 - 26, 1040 - 17), pos);
    }

    [Fact]
    public void GoesLevelWithTheCaretBesideAPanel()
    {
        // Start's search box is at the top of the panel.
        var start = new Rectangle(600, 200, 700, 800);
        Assert.Equal(new Point(571, 252), BadgePlacement.Beside(CaretAt(650, 250), Badge, Screen, start));
    }

    [Fact]
    public void GoesRightOfAPanelAtTheLeftEdge()
    {
        var panel = new Rectangle(10, 100, 600, 600);
        Assert.Equal(new Point(613, 392), BadgePlacement.Beside(CaretAt(50, 390), Badge, Screen, panel));
    }

    [Fact]
    public void NoRoomBesideAFullScreenPanel()
    {
        Assert.Null(BadgePlacement.Beside(SearchCaret, Badge, Screen, new Rectangle(0, 0, 1920, 1080)));
    }

    [Fact]
    public void WorksOnASecondScreen()
    {
        var second = new Rectangle(-1280, -200, 1280, 984); // left of the main screen and a bit higher
        var caret = CaretAt(-1000, 760);
        Assert.Equal(new Point(-998, 740), BadgePlacement.Place(caret, Badge, second, Rectangle.Empty));
    }
}
