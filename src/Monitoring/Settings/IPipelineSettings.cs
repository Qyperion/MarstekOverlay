using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;

namespace MarstekOverlay.Monitoring.Settings;

/// <summary>What the user has chosen about the way the station is followed. Unlike the options of the app, it
/// changes while the app runs, so it is read anew for every snapshot.</summary>
public interface IPipelineSettings
{
    event Action? Changed;

    /// <summary>Nothing until the user has pointed the app at a station.</summary>
    StationEndpoint? Station { get; }

    ForecastSettings Forecast { get; }

    AlertSettings Alerts { get; }
}
