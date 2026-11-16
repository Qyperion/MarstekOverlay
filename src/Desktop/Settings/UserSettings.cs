using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;

namespace MarstekOverlay.Desktop.Settings;

/// <summary>What the user sets up in the app itself, kept apart from <c>appsettings.json</c> that ships with it.</summary>
public class UserSettings
{
    /// <summary>A language code such as "uk"; empty means English.</summary>
    public string Language { get; set; } = string.Empty;

    public StationSettings Station { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
    public ForecastSettings Forecast { get; set; } = new();
    public AlertSettings Alerts { get; set; } = new();
    public DetailsSettings Details { get; set; } = new();
}
