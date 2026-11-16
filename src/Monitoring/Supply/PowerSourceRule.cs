using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Supply;

/// <summary>Reads the power source off the status answers. The Open API has no flag for the grid being there,
/// so the source is inferred from the power flows; this class is the only place that inference lives.
/// <para>What it rests on, as measured on Venus E 3.0 with firmware 150 and a load on the backup output.
/// While the grid charges the battery, the grid port reports the charging power plus the load. Once the
/// battery is full, the grid is passed through to the backup output and shows nowhere: the grid port reads
/// zero and its energy counter stands still, exactly as in an outage. Only the charge tells the two apart,
/// as the station charges a battery that is not full whenever the grid is there.</para></summary>
public static class PowerSourceRule
{
    // An idle port reads exactly zero on firmware 150; the margin leaves room for firmware that reports noise.
    private const double GridFlowAboveWatts = 10;

    // A charged battery settles a percent lower and is brought back up within a minute.
    private const double ToppedUpFromSocPercent = 99;

    /// <summary>How long a full battery that settled a percent lower may stand idle before its top-up starts,
    /// with the grid there all along. Seen to take up to a minute; until then it looks like an outage.</summary>
    public static readonly TimeSpan LongestTopUpDelay = TimeSpan.FromMinutes(2);

    /// <param name="previous">The grid energy counters of the answer before this one, when it is recent enough
    /// to compare them with.</param>
    public static SupplyEvidence Read(EnergySystemStatus status, GridEnergyCounters? previous = null)
    {
        if (status.OngridPower is null)
            return SupplyEvidence.None;

        if (!IsGridPortSilent(status) || HasGridEnergyMoved(status, previous))
            return SupplyEvidence.GridAtWork;

        if (status.BatterySocPercent >= Percent.Whole)
            return SupplyEvidence.FullBattery;

        return status.BatterySocPercent < Percent.Whole && BatteryPowerReading.From(status)?.Flow == BatteryFlow.Discharging
            ? SupplyEvidence.DrainingBattery
            : SupplyEvidence.None;
    }

    /// <summary>What the battery takes or gives, given the source. The grid passed through to the backup output
    /// is reported nowhere, which leaves the load there looking as if the battery carried it.</summary>
    public static BatteryPowerReading? BatteryPower(EnergySystemStatus status, PowerSource source)
    {
        var reading = BatteryPowerReading.From(status);

        return source == PowerSource.Mains && reading is { IsDerived: true } derived && IsGridPortSilent(status)
            ? derived with { Power = derived.Power + (status.OffgridPower ?? Power.Zero) }
            : reading;
    }

    /// <summary>Whether <see cref="BatteryPower"/> is a guess. Until the source is known, a load on the backup
    /// output beside a silent grid port is taken for the battery's, which is right on the battery only: on the
    /// mains the battery stands idle.</summary>
    public static bool IsBatteryPowerInDoubt(EnergySystemStatus status, PowerSource source) =>
        source == PowerSource.Unknown
        && BatteryPowerReading.From(status) is { IsDerived: true }
        && IsGridPortSilent(status)
        && status.OffgridPower is { Watts: not 0 };

    /// <summary>Charging that merely keeps a full battery full, as opposed to charging an emptied one.</summary>
    public static bool IsTopUp(EnergySystemStatus status) =>
        status.BatterySocPercent >= ToppedUpFromSocPercent && BatteryPowerReading.From(status)?.Flow == BatteryFlow.Charging;

    /// <summary>The grid charging a battery that has room to take it, which a top-up does not count as.</summary>
    public static bool IsChargingFromMains(EnergySystemStatus status, PowerSource source) =>
        source == PowerSource.Mains && BatteryPower(status, source)?.Flow == BatteryFlow.Charging && !IsTopUp(status);

    private static bool IsGridPortSilent(EnergySystemStatus status) =>
        status.OngridPower is { } ongrid && Math.Abs(ongrid.Watts) <= GridFlowAboveWatts;

    // A short burst of charging is easily missed between two answers; the counters keep its trace.
    private static bool HasGridEnergyMoved(EnergySystemStatus status, GridEnergyCounters? previous) =>
        status.TotalGridInputEnergy?.WattHours > previous?.Input?.WattHours
        || status.TotalGridOutputEnergy?.WattHours > previous?.Output?.WattHours;
}
