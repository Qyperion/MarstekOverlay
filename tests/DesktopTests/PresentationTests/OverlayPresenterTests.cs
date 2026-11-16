using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.DesktopTests.PresentationTests;

/// <summary>The panel as it reads late on the 12th, with every setting at its default.</summary>
public abstract class OverlayPresenterTests : LocalizedTests
{
    protected static readonly DateTimeOffset Now = Moments.Local(day: 12, hour: 22, minute: 42, second: 46);
    protected static readonly DateTimeOffset AnsweredAt = Now - TimeSpan.FromSeconds(12);
    protected static readonly DateTimeOffset YesterdayEvening = Moments.Local(day: 11, hour: 21, minute: 5);

    private static readonly TimeSpan ChartPeriod = TimeSpan.FromHours(3);

    private readonly ForecastSettings _forecastSettings = new();
    private readonly AlertSettings _alertSettings = new();

    protected OverlaySettings Overlay { get; } = new();

    protected PollingSettings Polling { get; } = new();

    protected OverlayState Present(StationState state, HistoryChart? chart = null, OverlaySettings? overlay = null) =>
        OverlayPresenter.Present(state, _forecastSettings, _alertSettings, overlay ?? Overlay, Polling, chart, Now);

    /// <summary>The row of the kind, shown for this once whether or not it is shown by default.</summary>
    protected OverlayRow RowOf(OverlayRowKind kind, StationState state)
    {
        var overlay = new OverlaySettings();
        overlay.Show(kind, isShown: true);

        return Present(state, overlay: overlay).Rows.Single(row => row.Kind == kind);
    }

    protected static HistoryChart Chart(params double[] loadsWatts)
    {
        var from = Now - ChartPeriod;
        var samples = loadsWatts
            .Select((watts, index) => new StationSample(
                from + TimeSpan.FromMinutes(index + 1),
                SocPercent: 100,
                BatteryPower: Power.Zero,
                BackupOutputPower: new Power(watts),
                PowerSource.Mains))
            .ToList();

        return HistoryChart.Build(samples, from, Now, columns: 220, longestBridgedGap: TimeSpan.FromMinutes(2));
    }
}
