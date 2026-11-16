using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Forecast;

/// <param name="EndsAt">When the reserve level or a full charge is reached; set only for
/// <see cref="ForecastStatus.TimeToReserve"/> and <see cref="ForecastStatus.TimeToFull"/>.</param>
/// <param name="StoredEnergy">What the battery is taken to hold, the starting point of every estimate.</param>
/// <param name="StandbyRuntime">How long the battery would carry the present load down to the reserve level;
/// set only for <see cref="ForecastStatus.Standby"/>.</param>
public readonly record struct RuntimeForecast(
    ForecastStatus Status,
    DateTimeOffset? EndsAt = null,
    Energy? StoredEnergy = null,
    TimeSpan? StandbyRuntime = null);
