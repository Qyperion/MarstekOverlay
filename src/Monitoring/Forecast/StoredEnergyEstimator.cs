using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Forecast;

/// <summary>What the battery holds right now. The BMS counts it ten times finer than the state of charge but
/// answers only on the slow poll, while the state of charge arrives on every poll in whole percents. So the BMS
/// value is carried forward by the measured battery power and kept within what the state of charge allows.</summary>
public sealed class StoredEnergyEstimator
{
    // Whether the station rounds the state of charge down, up or to the nearest percent differs by firmware.
    private const double SocTolerancePercent = 1;

    private Energy? _carried;
    private DateTimeOffset _carriedTo;
    private DateTimeOffset? _measuredAt;

    /// <param name="battery">The battery power to carry the BMS value forward by.</param>
    /// <returns>Nothing until the station has reported its state of charge and capacity.</returns>
    public Energy? Update(StationSnapshot snapshot, BatteryPowerReading? battery, ForecastSettings settings)
    {
        if (snapshot.EnergySystem is not { Value: { BatterySocPercent: { } soc and >= 0 and <= Percent.Whole, BatteryCapacity: { WattHours: > 0 } full } } reading)
        {
            _carried = null;
            _measuredAt = null;
            return null;
        }

        if (snapshot.Battery is { Value.RemainingCapacity: { } measured } bms && bms.ReceivedAt != _measuredAt)
        {
            _carried = measured;
            _carriedTo = bms.ReceivedAt;
            _measuredAt = bms.ReceivedAt;
        }

        if (_carried is not { } carried)
            return Percent.Of(full, soc);

        // In a slow poll the BMS answers a few seconds after the status, so its value can be the newer of the two.
        if (reading.ReceivedAt > _carriedTo)
        {
            if (battery is { Power: var power })
                carried += settings.InTheBattery(power * (reading.ReceivedAt - _carriedTo));

            _carriedTo = reading.ReceivedAt;
        }

        var lowest = Percent.Of(full, Math.Max(soc - SocTolerancePercent, 0));
        var highest = Percent.Of(full, Math.Min(soc + SocTolerancePercent, Percent.Whole));
        var estimate = new Energy(Math.Clamp(carried.WattHours, lowest.WattHours, highest.WattHours));

        _carried = estimate;

        return estimate;
    }
}
