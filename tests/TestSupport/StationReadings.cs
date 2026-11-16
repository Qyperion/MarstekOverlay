using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.TestSupport;

/// <summary>Snapshots of the states Venus E 3.0 without solar inputs was seen in, with a load on the backup output.
/// It reports no battery power of its own, so that is worked out from the other flows.</summary>
public static class StationReadings
{
    public static readonly Energy Capacity = new(5120);

    private const double ChargingWatts = 2200;

    /// <summary>No grid: the battery carries the load and nothing charges it.</summary>
    public static StationSnapshot OnBattery(DateTimeOffset at, double loadWatts = 250, double soc = 80) =>
        Answered(at, soc, ongridWatts: 0, loadWatts);

    /// <summary>Grid there, battery not full: the grid port draws what the battery takes plus the load.</summary>
    public static StationSnapshot Charging(DateTimeOffset at, double loadWatts = 250, double soc = 80) =>
        Answered(at, soc, ongridWatts: -(loadWatts + ChargingWatts), loadWatts);

    /// <summary>Grid there, battery full: the grid is passed through unseen, and the flows are those of an outage.</summary>
    public static StationSnapshot HoldingFull(DateTimeOffset at, double loadWatts = 250, double gridEnergyWattHours = 0) =>
        Answered(at, soc: 100, ongridWatts: 0, loadWatts, gridEnergyWattHours);

    /// <summary>Grid there, battery settled a percent short of full: a short charge restores it.</summary>
    public static StationSnapshot ToppingUp(DateTimeOffset at, double loadWatts = 250) =>
        Charging(at, loadWatts, soc: 99);

    /// <summary>Not full, nothing on the backup output and a silent grid port: no telling where the grid is.</summary>
    public static StationSnapshot Idle(DateTimeOffset at, double soc = 80, double gridEnergyWattHours = 0) =>
        Answered(at, soc, ongridWatts: 0, loadWatts: 0, gridEnergyWattHours);

    public static StationSnapshot Answered(
        DateTimeOffset at,
        double? soc,
        double ongridWatts,
        double loadWatts,
        double gridEnergyWattHours = 0) =>
        Answered(at, new EnergySystemStatus
        {
            BatterySocPercent = soc,
            BatteryCapacity = Capacity,
            PvPower = Power.Zero,
            OngridPower = new Power(ongridWatts),
            OffgridPower = new Power(loadWatts),
            TotalGridInputEnergy = new Energy(gridEnergyWattHours),
        });

    public static StationSnapshot Answered(DateTimeOffset at, EnergySystemStatus status) =>
        new() { EnergySystem = new Reading<EnergySystemStatus>(status, at) };

    extension(StationSnapshot snapshot)
    {
        public StationSnapshot WithBattery(
            DateTimeOffset at,
            double temperatureCelsius = 25,
            bool isChargingAllowed = true,
            bool isDischargingAllowed = true) => snapshot with
            {
                Battery = new Reading<BatteryStatus>(
                new BatteryStatus
                {
                    TemperatureCelsius = temperatureCelsius,
                    IsChargingAllowed = isChargingAllowed,
                    IsDischargingAllowed = isDischargingAllowed,
                },
                at),
            };

        /// <summary>The snapshot with what the pipeline would have made of it, told outright.</summary>
        public StationState With(
            PowerSupply supply = default,
            RuntimeForecast forecast = default,
            Outage? ongoingOutage = null,
            Outage? latestOutage = null,
            params Alert[] alerts) =>
            new(snapshot, supply, ongoingOutage, latestOutage, forecast, alerts);
    }
}
