using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace MarstekOverlay.Desktop.Styling;

/// <summary>The colours of <c>Palette.xaml</c> that the windows are built from, for code that cannot bind to them.
/// What a text or a mark says with its colour is a <see cref="Presentation.Tone"/> and comes from
/// <see cref="TonePalette"/>.</summary>
public static class SurfacePalette
{
    public static Color Field => ColorOf();

    public static Color FieldBorder => ColorOf();

    public static Color Accent => ColorOf();

    // Each colour is named in Palette.xaml as it is here.
    private static Color ColorOf([CallerMemberName] string name = "") => PaletteResources.ColorOf(name);
}
