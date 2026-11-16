namespace MarstekOverlay.LocalApi.Models;

public sealed record EnergySystemStatus
{
    public double? BatterySocPercent { get; init; }
    public Energy? BatteryCapacity { get; init; }
    public Power? PvPower { get; init; }

    /// <summary>Positive while the station feeds the grid-tied output, negative while it draws from the grid.</summary>
    public Power? OngridPower { get; init; }

    public Power? OffgridPower { get; init; }

    /// <summary>Positive while charging. Venus E 3.0 firmware 150 and newer leaves it out of the answer.</summary>
    public Power? BatteryPower { get; init; }

    public Energy? TotalPvEnergy { get; init; }
    public Energy? TotalGridOutputEnergy { get; init; }
    public Energy? TotalGridInputEnergy { get; init; }
    public Energy? TotalLoadEnergy { get; init; }
}
