using FluentAssertions;

using MarstekOverlay.Desktop.Views;

using System.Windows;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewsTests;

public class WindowPlacementTests
{
    // A large monitor and a smaller one to its right that hangs lower, each less its taskbar.
    private static readonly Rect Main = new(0, 0, 1707, 1027);
    private static readonly Rect Side = new(1707, 200, 1280, 680);
    private static readonly Rect[] BothMonitors = [Main, Side];

    private static readonly Size Smallest = new(780, 460);

    [Fact]
    public void Within_WindowWhollyOnAMonitor_StaysWhereItWasLeft()
    {
        // Arrange
        var wanted = new Rect(2000, 300, 900, 500);

        // Act & Assert
        WindowPlacement.Within(BothMonitors, wanted, Smallest).Should().Be(wanted);
    }

    [Fact]
    public void Within_WindowAcrossTwoMonitorsThatShowAllOfIt_StaysWhereItWasLeft()
    {
        // Arrange
        var wanted = new Rect(1300, 300, 900, 500);

        // Act & Assert
        WindowPlacement.Within(BothMonitors, wanted, Smallest).Should().Be(wanted);
    }

    [Fact]
    public void Within_MonitorTheWindowWasOnIsGone_MovesItOntoTheOneLeft()
    {
        // Arrange
        var wanted = new Rect(2000, 300, 900, 500);

        // Act
        var placed = WindowPlacement.Within([Main], wanted, Smallest);

        // Assert
        placed.Should().Be(new Rect(807, 300, 900, 500));
    }

    [Fact]
    public void Within_WindowPartlyBeyondTheEdges_MovesItWholeOntoTheMonitorThatShowsMostOfIt()
    {
        // Arrange
        var hangingOffTheBottomOfTheSideMonitor = new Rect(2000, 600, 900, 500);

        // Act
        var placed = WindowPlacement.Within(BothMonitors, hangingOffTheBottomOfTheSideMonitor, Smallest);

        // Assert
        placed.Should().Be(new Rect(2000, 380, 900, 500));
        Side.Contains(placed).Should().BeTrue();
    }

    [Fact]
    public void Within_WindowInTheDeadCornerBetweenMonitors_IsMovedThoughTheDesktopAsAWholeHoldsIt()
    {
        // Arrange
        var aboveTheSideMonitor = new Rect(1800, 0, 900, 500);

        // Act
        var placed = WindowPlacement.Within(BothMonitors, aboveTheSideMonitor, Smallest);

        // Assert
        placed.Should().Be(new Rect(1800, 200, 900, 500));
    }

    [Fact]
    public void Within_WindowLargerThanTheMonitor_IsShrunkToIt()
    {
        // Arrange
        var leftOnALargerMonitor = new Rect(100, 100, 1900, 1100);

        // Act
        var placed = WindowPlacement.Within([Side], leftOnALargerMonitor, Smallest);

        // Assert
        placed.Should().Be(Side);
    }

    [Fact]
    public void Within_WindowAsLargeAsAMonitorWithFractionalBounds_IsPutOntoIt()
    {
        // Arrange
        // 2048 by 1080 pixels at 150%, right of a 1280-pixel monitor at the same scale: its right edge less its
        // width falls short of its left edge in floating point.
        var monitor = new Rect(853.3333333333334, 0, 1365.3333333333333, 720);
        var hangingOffTheRight = new Rect(900, 0, monitor.Width, monitor.Height);

        // Act
        var placed = WindowPlacement.Within([monitor], hangingOffTheRight, Smallest);

        // Assert
        placed.Should().Be(monitor);
    }

    [Fact]
    public void Within_PlaceFarFromEveryMonitor_GoesToTheFirstOne()
    {
        // Arrange
        var wanted = new Rect(-9000, -4000, 900, 500);

        // Act
        var placed = WindowPlacement.Within(BothMonitors, wanted, Smallest);

        // Assert
        placed.Should().Be(new Rect(0, 0, 900, 500));
    }

    [Fact]
    public void Within_SizeBelowTheSmallest_IsBroughtUpToIt()
    {
        // Arrange
        var wanted = new Rect(100, 100, 0, 30);

        // Act
        var placed = WindowPlacement.Within(BothMonitors, wanted, Smallest);

        // Assert
        placed.Should().Be(new Rect(100, 100, 780, 460));
    }

    [Fact]
    public void Within_NoMonitorKnown_KeepsThePlace()
    {
        // Arrange
        var wanted = new Rect(2000, 300, 900, 500);

        // Act & Assert
        WindowPlacement.Within([], wanted, Smallest).Should().Be(wanted);
    }
}
