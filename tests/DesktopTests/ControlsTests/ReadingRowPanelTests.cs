using FluentAssertions;

using MarstekOverlay.Desktop.Controls;
using MarstekOverlay.DesktopTests.TestSupport;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Xunit;

namespace MarstekOverlay.DesktopTests.ControlsTests;

public class ReadingRowPanelTests
{
    private const double LineWidth = 200;
    private const double TextHeight = 16;
    private const double Gap = 10;

    [Fact]
    public void Layout_PairThatFitsTheLine_PutsTheValueAtItsRightEdge() => UiThread.Run(() =>
    {
        // Arrange
        var label = Block(width: 60);
        var value = Block(width: 80);
        var panel = PanelOf(label, value);

        // Act
        Layout(panel);

        // Assert
        panel.DesiredSize.Height.Should().Be(TextHeight);
        LayoutInformation.GetLayoutSlot(label).Should().Be(new Rect(0, 0, 120, TextHeight));
        LayoutInformation.GetLayoutSlot(value).Should().Be(new Rect(120, 0, 80, TextHeight));
    });

    [Fact]
    public void Layout_PairThatFillsTheLineWithItsGap_StaysOnIt() => UiThread.Run(() =>
    {
        // Arrange
        var value = Block(width: LineWidth - 60 - Gap);
        var panel = PanelOf(Block(width: 60), value);

        // Act
        Layout(panel);

        // Assert
        LayoutInformation.GetLayoutSlot(value).Top.Should().Be(0);
    });

    [Fact]
    public void Layout_PairTooLongForTheLine_PutsTheValueUnderTheName() => UiThread.Run(() =>
    {
        // Arrange
        var label = Block(width: 60);
        var value = Block(width: LineWidth - 60 - Gap + 1);
        var panel = PanelOf(label, value);

        // Act
        Layout(panel);

        // Assert
        panel.DesiredSize.Height.Should().Be(2 * TextHeight);
        LayoutInformation.GetLayoutSlot(label).Should().Be(new Rect(0, 0, LineWidth, TextHeight));
        LayoutInformation.GetLayoutSlot(value).Should().Be(new Rect(0, TextHeight, LineWidth, TextHeight));
    });

    [Fact]
    public void Layout_ValueWithoutAName_StartsAtTheLeftEdge() => UiThread.Run(() =>
    {
        // Arrange
        var label = Block(width: 60);
        label.Visibility = Visibility.Collapsed;
        var value = Block(width: 80);
        var panel = PanelOf(label, value);

        // Act
        Layout(panel);

        // Assert
        panel.DesiredSize.Height.Should().Be(TextHeight);
        LayoutInformation.GetLayoutSlot(value).Should().Be(new Rect(0, 0, LineWidth, TextHeight));
    });

    private static Border Block(double width) => new() { Width = width, Height = TextHeight };

    private static ReadingRowPanel PanelOf(UIElement label, UIElement value) => new() { Gap = Gap, Children = { label, value } };

    private static void Layout(ReadingRowPanel panel)
    {
        panel.Measure(new Size(LineWidth, double.PositiveInfinity));
        panel.Arrange(new Rect(0, 0, LineWidth, panel.DesiredSize.Height));
    }
}
