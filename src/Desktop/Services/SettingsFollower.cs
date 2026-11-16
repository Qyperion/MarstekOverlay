using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Keeps the language of the app and the station it watches as the settings say, from the moment it is
/// told to until the app exits.</summary>
public sealed class SettingsFollower(UserSettingsStore settings, StationMonitor monitor, Localizer localizer) : IDisposable
{
    /// <summary>The station the monitor is watching; none until <see cref="FollowStation"/> finds one in the
    /// settings.</summary>
    public StationEndpoint? Station { get; private set; }

    public void FollowLanguage()
    {
        settings.Changed -= UseLanguage;
        UseLanguage();
        settings.Changed += UseLanguage;
    }

    public void FollowStation()
    {
        settings.Changed -= WatchStation;
        WatchStation();
        settings.Changed += WatchStation;
    }

    public void Dispose()
    {
        settings.Changed -= UseLanguage;
        settings.Changed -= WatchStation;
    }

    private void UseLanguage() => localizer.Use(AppLanguages.Resolve(settings.Current.Language));

    private void WatchStation()
    {
        var station = settings.Current.Station.ToEndpoint();
        if (station == Station)
            return;

        Station = station;
        if (station is null)
            monitor.Stop();
        else
            monitor.Start(station);
    }
}
