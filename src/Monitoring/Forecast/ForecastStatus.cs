namespace MarstekOverlay.Monitoring.Forecast;

public enum ForecastStatus
{
    InsufficientData,
    ReserveReached,
    LoadTooLow,
    Collecting,
    TimeToReserve,
    TimeToFull,

    /// <summary>The battery is idle while something else carries the load on the backup output.</summary>
    Standby,
}
