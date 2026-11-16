using System.Windows;
using System.Windows.Controls;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>A name and its value on one line, the name at the left edge and the value at the right. A pair too
/// long for the line goes on two, the value under the name and free to wrap: a grid would squeeze it into a narrow
/// column instead. The first child is the name, the second the value; a value whose name is collapsed is laid
/// out on its own, at the left.</summary>
public sealed class ReadingRowPanel : Panel
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap),
        typeof(double),
        typeof(ReadingRowPanel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private bool _isOnOneLine;

    /// <summary>The least room between the name and the value while they share a line.</summary>
    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    private UIElement? Label => InternalChildren.Count > 0 ? InternalChildren[0] : null;

    private UIElement? Value => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Label is not { } label)
            return default;

        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var line = new Size(availableSize.Width, double.PositiveInfinity);

        label.Measure(line);
        if (Value is not { } value)
            return label.DesiredSize;

        value.Measure(unbounded);

        var together = label.DesiredSize.Width + Gap + value.DesiredSize.Width;
        _isOnOneLine = label.Visibility == Visibility.Visible && together <= availableSize.Width;
        if (_isOnOneLine)
            return new Size(together, Math.Max(label.DesiredSize.Height, value.DesiredSize.Height));

        value.Measure(line);

        return new Size(
            Math.Max(label.DesiredSize.Width, value.DesiredSize.Width),
            label.DesiredSize.Height + value.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Label is not { } label)
            return finalSize;

        if (Value is not { } value)
        {
            label.Arrange(new Rect(finalSize));
            return finalSize;
        }

        if (_isOnOneLine)
        {
            var valueWidth = value.DesiredSize.Width;

            label.Arrange(new Rect(0, 0, Math.Max(finalSize.Width - valueWidth, 0), finalSize.Height));
            value.Arrange(new Rect(Math.Max(finalSize.Width - valueWidth, 0), 0, valueWidth, finalSize.Height));
            return finalSize;
        }

        var labelHeight = label.DesiredSize.Height;

        label.Arrange(new Rect(0, 0, finalSize.Width, labelHeight));
        value.Arrange(new Rect(0, labelHeight, finalSize.Width, Math.Max(finalSize.Height - labelHeight, 0)));
        return finalSize;
    }
}
