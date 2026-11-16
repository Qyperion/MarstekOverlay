namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Saved by name in the user settings.</summary>
public enum AlertKind
{
    MainsLost,
    MainsRestored,
    LowCharge,
    BatteryTemperature,

    /// <summary>The station does not let the battery charge or discharge.</summary>
    FlowForbidden,

    StaleData,
}
