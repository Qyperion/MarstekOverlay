using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring;

/// <param name="Power">Positive while charging, negative while discharging.</param>
/// <param name="IsDerived">The station did not report the value; it was worked out from the other power flows.</param>
public readonly record struct BatteryPowerReading(Power Power, bool IsDerived)
{
    // Between the two thresholds the battery merely covers the station's own consumption.
    private const double ChargingAboveWatts = 10;
    internal const double DischargingBelowWatts = -20;

    public BatteryFlow Flow => Power.Watts switch
    {
        > ChargingAboveWatts => BatteryFlow.Charging,
        < DischargingBelowWatts => BatteryFlow.Discharging,
        _ => BatteryFlow.Idle,
    };

    public static BatteryPowerReading? From(EnergySystemStatus status)
    {
        if (status.BatteryPower is { } reported)
            return new BatteryPowerReading(reported, IsDerived: false);

        // Whatever comes in from PV and leaves through neither output is what the battery takes.
        return status is { PvPower: { } pv, OngridPower: { } ongrid, OffgridPower: { } offgrid }
            ? new BatteryPowerReading(pv - ongrid - offgrid, IsDerived: true)
            : null;
    }
}
