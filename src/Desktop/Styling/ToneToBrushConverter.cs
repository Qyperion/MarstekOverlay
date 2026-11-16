using MarstekOverlay.Desktop.Presentation;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MarstekOverlay.Desktop.Styling;

public sealed class ToneToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Tone tone ? TonePalette.BrushOf(tone) : DependencyProperty.UnsetValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
