namespace MarstekOverlay.LocalApi.Models;

public sealed record BatteryStatus
{
    public double? SocPercent { get; init; }
    public bool? IsChargingAllowed { get; init; }
    public bool? IsDischargingAllowed { get; init; }
    public double? TemperatureCelsius { get; init; }

    /// <summary>What the BMS counts as left right now, not a measured capacity of an aged battery.</summary>
    public Energy? RemainingCapacity { get; init; }

    public Energy? RatedCapacity { get; init; }
}
