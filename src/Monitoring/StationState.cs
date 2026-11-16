using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring;

/// <summary>What a snapshot came to once everything that follows the station had seen it.</summary>
/// <param name="LatestOutage">The latest outage that has ended.</param>
/// <param name="Alerts">What is wrong right now, the gravest first.</param>
public sealed record StationState(
    StationSnapshot Snapshot,
    PowerSupply Supply,
    Outage? OngoingOutage,
    Outage? LatestOutage,
    RuntimeForecast Forecast,
    IReadOnlyList<Alert> Alerts);
