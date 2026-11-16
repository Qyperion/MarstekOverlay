using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Desktop.Settings;

public class OverlaySettings
{
    public const int MinOpacityPercent = 20;
    public const int MaxOpacityPercent = 100;
    // As far back as the samples are kept in memory; the chart is redrawn too often to read them from the file.
    public const int MaxChartHours = SampleHistory.KeptInMemoryHours;

    public const int MinScalePercent = 75;
    public const int MaxScalePercent = 200;

    /// <summary>The periods the settings window offers, shortest first.</summary>
    public static readonly IReadOnlyList<int> ChartHoursChoices = [1, 3, 12, MaxChartHours];

    public double Left { get; set; } = 24;
    public double Top { get; set; } = 24;
    public bool IsTopmost { get; set; }

    /// <summary>Of the panel background only; the readings stay fully opaque.</summary>
    public int OpacityPercent { get; set; } = 80;

    public int ScalePercent { get; set; } = 100;

    /// <summary>One line with the charge, the load and the forecast instead of the whole panel.</summary>
    public bool IsCompact { get; set; }

    /// <summary>The panel lets every click through to the window under it, so it can only be switched back
    /// from the notification-area icon.</summary>
    public bool IsClickThrough { get; set; }

    /// <summary>The app starts with the icon in the notification area alone; the panel comes once it is asked for.</summary>
    public bool IsHiddenAtStartup { get; set; }

    /// <summary>Rows left off the panel, for a setup where they never show anything: no solar inputs, no grid feed.</summary>
    public List<OverlayRowKind> HiddenRows { get; set; } = [];

    /// <summary>Rows that are off unless asked for. A list of its own, so that a file saved before such a row
    /// existed keeps showing exactly what it showed.</summary>
    public List<OverlayRowKind> ShownRows { get; set; } = [];

    /// <summary>The chart of the load and the charge under the charge bar.</summary>
    public bool IsChartShown { get; set; } = true;

    public int ChartHours { get; set; } = 3;

    /// <summary>What the chart covers; a file edited by hand may hold any number of hours.</summary>
    public TimeSpan ChartPeriod() => TimeSpan.FromHours(OfferedChartHours(ChartHours));

    /// <summary>The offered period that stands for <paramref name="hours"/>: the longest one not above it, or the
    /// shortest one. The panel draws it and the settings window marks it, so the two cannot disagree.</summary>
    public static int OfferedChartHours(int hours) => ChartHoursChoices.LastOrDefault(choice => choice <= hours, ChartHoursChoices[0]);

    public PanelModes Modes() => new(IsTopmost, IsCompact, IsClickThrough);

    public double Scale() => Percent.ToFraction(Math.Clamp(ScalePercent, MinScalePercent, MaxScalePercent));

    public double Opacity() => Percent.ToFraction(Math.Clamp(OpacityPercent, MinOpacityPercent, MaxOpacityPercent));

    public bool Shows(OverlayRowKind kind) =>
        kind.IsShownByDefault ? !HiddenRows.Contains(kind) : ShownRows.Contains(kind);

    public void Show(OverlayRowKind kind, bool isShown)
    {
        HiddenRows.Remove(kind);
        ShownRows.Remove(kind);

        if (isShown != kind.IsShownByDefault)
            (isShown ? ShownRows : HiddenRows).Add(kind);
    }
}
