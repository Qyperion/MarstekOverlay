using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.Monitoring.Alerts;

public abstract record Alert(AlertKind Kind, AlertSeverity Severity);

/// <param name="SocPercent">The charge the battery takes the load over with.</param>
/// <param name="IsSwitchSeen">False for an outage found already going on, after a start or a long gap in the
/// answers: the station was found on the battery, and nobody saw the mains go.</param>
public sealed record MainsLostAlert(double? SocPercent, bool IsSwitchSeen = true)
    : Alert(AlertKind.MainsLost, AlertSeverity.Warning);

/// <param name="Outage">The outage that has just ended; nothing when the journal did not keep it.</param>
public sealed record MainsRestoredAlert(Outage? Outage, double? SocPercent) : Alert(AlertKind.MainsRestored, AlertSeverity.Info);

/// <param name="IsLowestThreshold">The last warning there will be before the battery runs out.</param>
public sealed record LowChargeAlert(double ThresholdPercent, double SocPercent, bool IsLowestThreshold)
    : Alert(AlertKind.LowCharge, IsLowestThreshold ? AlertSeverity.Critical : AlertSeverity.Warning);

/// <param name="LimitCelsius">The limit the temperature is beyond.</param>
public sealed record BatteryTemperatureAlert(double Celsius, double LimitCelsius)
    : Alert(AlertKind.BatteryTemperature, AlertSeverity.Warning)
{
    public bool IsTooHot => Celsius > LimitCelsius;
}

/// <param name="Flow">What the station does not let the battery do. A battery that may not discharge backs
/// nothing up, which is why that one is critical.</param>
public sealed record FlowForbiddenAlert(BatteryFlow Flow)
    : Alert(AlertKind.FlowForbidden, Flow == BatteryFlow.Discharging ? AlertSeverity.Critical : AlertSeverity.Warning);

/// <param name="LatestAnswerAt">Nothing when the station has not answered at all.</param>
public sealed record StaleDataAlert(DateTimeOffset? LatestAnswerAt) : Alert(AlertKind.StaleData, AlertSeverity.Warning);
