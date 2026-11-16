using FluentAssertions;

using MarstekOverlay.Desktop.Services;

using System.Drawing;
using System.Windows.Forms;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public class TrayMenuRendererTests
{
    private const int BoxSize = 16;

    private static readonly TrayMenuColors Colors = new(
        Background: Color.FromArgb(32, 32, 32),
        Border: Color.FromArgb(64, 64, 64),
        Highlight: Color.FromArgb(0, 90, 160),
        Text: Color.FromArgb(240, 240, 240),
        CheckMark: Color.FromArgb(0, 200, 0));

    [Fact]
    public void ApplyTo_Menu_TakesTheRendererAndTheColours()
    {
        // Arrange
        using var menu = new ContextMenuStrip();
        var renderer = new TrayMenuRenderer(Colors);

        // Act
        renderer.ApplyTo(menu);

        // Assert
        menu.Renderer.Should().BeSameAs(renderer);
        menu.BackColor.Should().Be(Colors.Background);
        menu.ForeColor.Should().Be(Colors.Text);
    }

    [Fact]
    public void DrawItemText_AnyItem_IsWrittenInTheColourOfTheText()
    {
        // Arrange
        using var bitmap = new Bitmap(100, 20);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(FontFamily.GenericSansSerif, 9);
        using var item = new ToolStripMenuItem("Exit");
        var text = new ToolStripItemTextRenderEventArgs(
            graphics, item, item.Text, new Rectangle(0, 0, 100, 20), Color.Black, font, TextFormatFlags.Default);

        // Act
        new TrayMenuRenderer(Colors).DrawItemText(text);

        // Assert
        text.TextColor.Should().Be(Colors.Text);
    }

    [Fact]
    public void DrawItemCheck_TickedItem_DrawsTheTickInTheColourOfTheMarkInsideTheBox()
    {
        // Arrange
        using var bitmap = new Bitmap(BoxSize * 2, BoxSize * 2);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Colors.Background);
        using var item = new ToolStripMenuItem("Compact view") { Checked = true };
        var box = new Rectangle(BoxSize / 2, BoxSize / 2, BoxSize, BoxSize);

        // Act
        new TrayMenuRenderer(Colors).DrawItemCheck(new ToolStripItemImageRenderEventArgs(graphics, item, box));

        // Assert
        var marked = PixelsOf(bitmap).Where(pixel => IsLike(bitmap.GetPixel(pixel.X, pixel.Y), Colors.CheckMark)).ToList();
        marked.Should().NotBeEmpty();
        marked.Should().OnlyContain(pixel => box.Contains(pixel));
    }

    private static IEnumerable<Point> PixelsOf(Bitmap bitmap) =>
        from x in Enumerable.Range(0, bitmap.Width)
        from y in Enumerable.Range(0, bitmap.Height)
        select new Point(x, y);

    // Smoothing blends the edges of the tick into the background; its middle keeps the colour.
    private static bool IsLike(Color pixel, Color color) =>
        Math.Abs(pixel.R - color.R) + Math.Abs(pixel.G - color.G) + Math.Abs(pixel.B - color.B) < 60;
}
