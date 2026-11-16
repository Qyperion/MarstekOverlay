namespace MarstekOverlay.Desktop.Settings;

/// <summary>Where a window was left, in the units WPF measures the desktop in.</summary>
/// <param name="IsMaximized">The rest is then the place the window returns to once restored.</param>
public sealed record SavedWindowPlace(double Left, double Top, double Width, double Height, bool IsMaximized);

public class DetailsSettings
{
    /// <summary>Longer ones are offered only as far back as the samples are kept.</summary>
    public static readonly IReadOnlyList<int> ChartHoursChoices = [1, 3, 12, 24, 72, 168];

    /// <summary>The period the charts of the details window were last set to.</summary>
    public int ChartHours { get; set; } = 24;

    /// <summary>Nothing until the details window has been closed once; it opens in the middle of the screen then.</summary>
    public SavedWindowPlace? Window { get; set; }
}
