using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.Forecast;

/// <summary>Estimates when the battery reaches the reserve level while it discharges, a full charge while it
/// charges, or how long it would last while the station is on the mains, at the power averaged over the last
/// few minutes. Anything that breaks a steady flow starts the averaging over.</summary>
public sealed class RuntimeForecaster
{
    public static readonly TimeSpan AveragingWindow = TimeSpan.FromMinutes(5);

    // A load the battery would carry without being seen to discharge is as good as none.
    private const double StandbyLoadAboveWatts = -BatteryPowerReading.DischargingBelowWatts;

    private readonly StoredEnergyEstimator _storedEnergy = new();
    private readonly PowerAverage _average = new(AveragingWindow);

    private (DateTimeOffset? EnergySystem, DateTimeOffset? Battery) _readingsAt;
    private (double Reserve, double Discharge, double Charge) _settingsUsed;
    private Energy? _stored;
    private RuntimeForecast _forecast = new(ForecastStatus.InsufficientData);

    /// <summary>A snapshot that repeats the previous readings, as after a failed poll, repeats the previous
    /// forecast rather than counting as one more sample; with other settings it is estimated anew.</summary>
    /// <param name="source">What the same snapshot told about the power source.</param>
    public RuntimeForecast Update(StationSnapshot snapshot, PowerSource source, ForecastSettings settings)
    {
        var readingsAt = (snapshot.EnergySystem?.ReceivedAt, snapshot.Battery?.ReceivedAt);
        var settingsUsed = (settings.ReserveSocPercent, settings.DischargeEfficiencyPercent, settings.ChargeEfficiencyPercent);
        var isRepeated = snapshot.EnergySystem is not null && readingsAt == _readingsAt;

        if (isRepeated && settingsUsed == _settingsUsed)
            return _forecast;

        var batteryPower = snapshot.EnergySystem is { } answer ? PowerSourceRule.BatteryPower(answer.Value, source) : null;
        if (!isRepeated)
            _stored = _storedEnergy.Update(snapshot, batteryPower, settings);

        _readingsAt = readingsAt;
        _settingsUsed = settingsUsed;
        return _forecast = Estimate(snapshot, batteryPower, source, settings);
    }

    /// <summary>The time <paramref name="load"/> takes to move <paramref name="toGo"/>, nothing when that is
    /// already behind.</summary>
    /// <returns>Nothing for a load that gives nothing to divide by.</returns>
    internal static TimeSpan? Remaining(Energy toGo, Power load)
    {
        if (load.Watts is not > 0)
            return null;

        return toGo.WattHours > 0 ? toGo / load : TimeSpan.Zero;
    }

    private RuntimeForecast Estimate(
        StationSnapshot snapshot,
        BatteryPowerReading? batteryPower,
        PowerSource source,
        ForecastSettings settings)
    {
        var stored = _stored;

        if (stored is not { } energy
            || batteryPower is not { } battery
            || snapshot.EnergySystem is not { Value: { BatterySocPercent: { } soc, BatteryCapacity: { } fullCapacity } status } reading)
            return StartOver(ForecastStatus.InsufficientData, stored);

        var estimated = WhatToEstimate(status, battery.Flow, source);

        if (estimated != ForecastStatus.TimeToFull && soc <= settings.ReserveSocPercent)
            return StartOver(ForecastStatus.ReserveReached, stored);

        if (estimated is not { } flow)
            return StartOver(ForecastStatus.LoadTooLow, stored);

        // An outage leaves only the backup output powered, so that is the load the battery stands by for.
        var load = flow == ForecastStatus.Standby
            ? status.OffgridPower ?? Power.Zero
            : new Power(Math.Abs(battery.Power.Watts));

        if (flow == ForecastStatus.Standby && load.Watts <= StandbyLoadAboveWatts)
            return StartOver(ForecastStatus.LoadTooLow, stored);

        if (_average.Add(reading.ReceivedAt, flow, load) is not { } averageLoad)
            return new RuntimeForecast(ForecastStatus.Collecting, StoredEnergy: stored);

        var isCharging = flow == ForecastStatus.TimeToFull;
        var target = isCharging ? fullCapacity : Percent.Of(fullCapacity, settings.ReserveSocPercent);
        var atTheOutputs = settings.AtTheOutputs(target - energy);

        if (Remaining(isCharging ? atTheOutputs : -atTheOutputs, averageLoad) is not { } remaining)
            return StartOver(ForecastStatus.LoadTooLow, stored);

        return flow == ForecastStatus.Standby
            ? new RuntimeForecast(ForecastStatus.Standby, StoredEnergy: stored, StandbyRuntime: remaining)
            : new RuntimeForecast(flow, reading.ReceivedAt + remaining, stored);
    }

    /// <returns>Nothing for a battery that neither works nor has a load to stand by for.</returns>
    private static ForecastStatus? WhatToEstimate(EnergySystemStatus status, BatteryFlow flow, PowerSource source)
    {
        // On the mains a charged battery idles, but for a short top-up now and then. What matters then is how
        // long it would last without the mains.
        if (source == PowerSource.Mains && (flow != BatteryFlow.Charging || PowerSourceRule.IsTopUp(status)))
            return ForecastStatus.Standby;

        return flow switch
        {
            BatteryFlow.Charging => ForecastStatus.TimeToFull,
            BatteryFlow.Discharging => ForecastStatus.TimeToReserve,
            _ => null,
        };
    }

    private RuntimeForecast StartOver(ForecastStatus status, Energy? stored)
    {
        _average.Clear();
        return new RuntimeForecast(status, StoredEnergy: stored);
    }
}
