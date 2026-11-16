namespace MarstekOverlay.LocalApi.Models;

public sealed record EnergySystemMode
{
    public OperatingMode? Mode { get; init; }
    public Power? OngridPower { get; init; }
    public Power? OffgridPower { get; init; }
    public double? BatterySocPercent { get; init; }

    /// <summary>A copy of the meter readings that firmware 150 fills with zeros while <c>EM.GetStatus</c> holds
    /// the live values, so it only stands in when the meter itself gave no answer.</summary>
    public EnergyMeterStatus Meter { get; init; } = new();
}
