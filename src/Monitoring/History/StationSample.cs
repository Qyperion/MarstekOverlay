using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.History;

/// <summary>What one poll told about the station, kept for the charts.</summary>
/// <param name="BatteryPower">Positive while charging; as <see cref="PowerSourceRule.BatteryPower"/> works it
/// out, so the grid passed through to the backup output does not count as the battery's.</param>
public sealed record StationSample(
    DateTimeOffset At,
    double? SocPercent,
    Power? BatteryPower,
    Power? BackupOutputPower,
    PowerSource Source);
