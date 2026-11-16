using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Supply;

/// <summary>What the grid port's energy counters stood at in one answer.</summary>
public readonly record struct GridEnergyCounters(Energy? Input, Energy? Output)
{
    /// <returns>Nothing when the answer has neither counter.</returns>
    public static GridEnergyCounters? Of(EnergySystemStatus status) =>
        status is { TotalGridInputEnergy: null, TotalGridOutputEnergy: null }
            ? null
            : new GridEnergyCounters(status.TotalGridInputEnergy, status.TotalGridOutputEnergy);
}
