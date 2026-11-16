using MarstekOverlay.Desktop.Presentation;

using System.Windows.Media;

namespace MarstekOverlay.Desktop.Styling;

/// <summary>The brushes of <c>Palette.xaml</c>, one per <see cref="Tone"/>, for code that cannot bind to them.</summary>
public static class TonePalette
{
    public static object? BrushOf(Tone tone) => PaletteResources.BrushOf(tone.ToString());

    public static Color ColorOf(Tone tone) => PaletteResources.ColorOf(tone.ToString());
}
