using MarstekOverlay.Monitoring.Settings;

namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Tells of the station staying silent for longer than the settings allow.</summary>
internal sealed class SilenceWatch(PollingSettings polling) : IAlertWatch
{
    private readonly Latch _staleData = new();

    private DateTimeOffset? _updatedAt;
    private DateTimeOffset? _answeredAt;
    private DateTimeOffset _listeningSince;

    /// <summary>Keeps count of how long the app has been listening, whether or not the silence is watched.</summary>
    public void Listen(StationSnapshot snapshot, AlertSettings settings, DateTimeOffset now)
    {
        var answeredAt = snapshot.EnergySystem?.ReceivedAt;

        // Silence counts only while the app listens. A computer that slept through the night has not been
        // waiting for an answer all that time, and a station it has just been pointed at owes none yet.
        var wasAway = _updatedAt is not { } updatedAt || now - updatedAt > StaleAfter(settings);
        var isPointedElsewhere = answeredAt is null && _answeredAt is not null;
        if (wasAway || isPointedElsewhere)
            _listeningSince = now;

        _updatedAt = now;
        _answeredAt = answeredAt;
    }

    /// <summary>Expects <see cref="Listen"/> to have heard the same snapshot.</summary>
    public void Follow(AlertSettings settings, DateTimeOffset now, AlertFindings findings)
    {
        var silentSince = _answeredAt is { } answeredAt && answeredAt > _listeningSince ? answeredAt : _listeningSince;
        var isStale = now - silentSince > StaleAfter(settings);

        findings.Follow(_staleData, isStale ? new StaleDataAlert(_answeredAt) : null, isCleared: !isStale);
    }

    public void Forget() => _staleData.Forget();

    private TimeSpan StaleAfter(AlertSettings settings) => SilenceLimit.Of(settings.StaleAfterMinutes, polling);
}
