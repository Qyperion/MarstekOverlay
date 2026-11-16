using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.History;

/// <summary>What the chart shows at one moment; a line that has nothing there is left out.</summary>
/// <param name="LoadMax">The highest of the samples the point of the load line stands for; the same as
/// <paramref name="Load"/> where the point is a single sample.</param>
/// <param name="BatteryPower">Positive while charging.</param>
public readonly record struct ChartReading(
    DateTimeOffset At,
    Power? Load,
    Power? LoadMax,
    double? SocPercent,
    Power? BatteryPower,
    bool IsOnBattery);
