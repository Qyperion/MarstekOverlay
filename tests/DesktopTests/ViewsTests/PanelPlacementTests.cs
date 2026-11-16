using FluentAssertions;

using MarstekOverlay.Desktop.Views;

using System.Windows;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewsTests;

public class PanelPlacementTests
{
    // Two monitors side by side, the left one starting left of the origin.
    private static readonly Rect Desktop = new(-1920, 0, 4480, 1440);
    private static readonly Size Panel = new(252, 300);

    [Fact]
    public void Within_PanelThatFitsWhereItWasLeft_StaysThere()
    {
        PanelPlacement.Within(Desktop, new Point(1200, 400), Panel).Should().Be(new Point(1200, 400));
    }

    [Theory]
    // Grown to the right or downwards past an edge, as after a larger scale or a longer compact line.
    [InlineData(2400, 400, 2308, 400)]
    [InlineData(1200, 1300, 1200, 1140)]
    [InlineData(2500, 1400, 2308, 1140)]
    // Saved on a monitor that is no longer attached.
    [InlineData(-5000, -300, -1920, 0)]
    [InlineData(9000, 400, 2308, 400)]
    public void Within_PanelThatWouldRunOverAnEdge_IsMovedJustInside(double left, double top, double placedLeft, double placedTop)
    {
        PanelPlacement.Within(Desktop, new Point(left, top), Panel).Should().Be(new Point(placedLeft, placedTop));
    }

    [Fact]
    public void Within_PanelLargerThanTheDesktop_KeepsItsTopLeftCornerOnIt()
    {
        // Arrange
        var huge = new Size(5000, 2000);

        // Act
        var placed = PanelPlacement.Within(Desktop, new Point(100, 100), huge);

        // Assert
        placed.Should().Be(new Point(-1920, 0));
    }

    [Fact]
    public void Within_PanelNotLaidOutYet_KeepsItsCornerOnTheDesktop()
    {
        PanelPlacement.Within(Desktop, new Point(2560, 1440), new Size(0, 0)).Should().Be(new Point(2560, 1440));
    }
}
