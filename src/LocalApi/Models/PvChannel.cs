namespace MarstekOverlay.LocalApi.Models;

public sealed record PvChannel
{
    public int Number { get; init; }
    public Power? Power { get; init; }
    public double? VoltageVolts { get; init; }
    public double? CurrentAmperes { get; init; }
    public bool? IsWorking { get; init; }
}
