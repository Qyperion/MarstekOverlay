namespace MarstekOverlay.LocalApi.Models;

public sealed record EnergyMeterStatus
{
    public bool? IsCtConnected { get; init; }
    public Power? PhaseAPower { get; init; }
    public Power? PhaseBPower { get; init; }
    public Power? PhaseCPower { get; init; }
    public Power? TotalPower { get; init; }
    public Energy? InputEnergy { get; init; }
    public Energy? OutputEnergy { get; init; }
}
