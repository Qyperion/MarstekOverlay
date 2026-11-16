using System.Windows;
using System.Windows.Media;

namespace MarstekOverlay.Desktop.Styling;

/// <summary>Looks the brushes of <c>Palette.xaml</c> up by name: each is keyed "Brush." and its name.</summary>
internal static class PaletteResources
{
    private const string BrushKeyPrefix = "Brush.";

    public static object? BrushOf(string name) => Application.Current?.TryFindResource(BrushKeyPrefix + name);

    public static Color ColorOf(string name) => BrushOf(name) is SolidColorBrush brush ? brush.Color : Colors.Transparent;
}
