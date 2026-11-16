using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Supply;

using System.Globalization;

namespace MarstekOverlay.Desktop.Presentation;

public enum SectionState
{
    Answered,
    NoAnswer,
    NotSupported,

    /// <summary>The station answered that what the request reads is not connected to it.</summary>
    NotConnected,
}

/// <param name="Value">Nothing when the answer came without it.</param>
public sealed record SectionRow(string Label, string? Value);

/// <summary>One answer of the station, value by value.</summary>
/// <param name="Method">The wire name of the request.</param>
/// <param name="ReceivedAt">When the station answered the request or turned it down; nothing while it has done
/// neither.</param>
/// <param name="Rows">Empty unless the station has answered with something to show.</param>
/// <param name="Note">What stands in for the values of a request that brought none.</param>
public sealed record StationSection(
    string Title,
    string Method,
    SectionState State,
    DateTimeOffset? ReceivedAt,
    IReadOnlyList<SectionRow> Rows,
    string? Note = null);

/// <summary>Everything a snapshot holds, named and worded once for every place that lists it: the report of a
/// connection test and the cards of the details window. A value that several answers bring is told once, with
/// the answer that reads it most often or that it belongs to.</summary>
public static class StationSections
{
    // The usual reading of a Wi-Fi signal: a station at -70 dBm or weaker loses answers.
    private const double ExcellentSignalFromDbm = -50;
    private const double GoodSignalFromDbm = -60;
    private const double FairSignalFromDbm = -70;

    private const string BluetoothDisconnectedState = "disconnect";

    /// <summary>What stands in for a value that is not known.</summary>
    public static string NoValue => Strings.Common_NoValue;

    /// <param name="source">Where the power comes from, when that is known: it tells whether the load on the
    /// backup output is the battery's to carry.</param>
    public static IReadOnlyList<StationSection> Describe(StationSnapshot snapshot, PowerSource source = PowerSource.Unknown)
    {
        var energySystem = snapshot.EnergySystem?.Value;
        var wifi = snapshot.Wifi?.Value;
        var bluetooth = snapshot.Bluetooth?.Value;

        return
        [
            Section(snapshot, Strings.Report_Device, MarstekMethods.GetDevice, snapshot.Device, device => Rows(
                new(Strings.Report_Model, device.Model),
                new(Strings.Report_Firmware, device.FirmwareVersion?.ToString(CultureInfo.InvariantCulture)),
                new(Strings.Report_Family, device.Profile.Family.DisplayName),
                Unrepeated(Strings.Report_BluetoothMac, device.BluetoothMac, bluetooth?.Mac),
                Unrepeated(Strings.Report_WifiMac, device.WifiMac, wifi?.Mac),
                Unrepeated(Strings.Report_WifiNetwork, device.WifiName, wifi?.Ssid),
                // Worth a row when it differs: it can stay at a previous DHCP address for hours.
                Unrepeated(Strings.Report_ReportedIp, device.ReportedIp, wifi?.Ip))),

            Section(
                snapshot,
                Strings.Report_EnergySystem,
                MarstekMethods.EnergySystemGetStatus,
                snapshot.EnergySystem,
                status => DescribeEnergySystem(snapshot, status, source)),

            // The power flows and the charge are the energy system's, which is asked for them first.
            Section(snapshot, Strings.Report_OperatingMode, MarstekMethods.EnergySystemGetMode, snapshot.Mode, mode =>
            [
                new(Strings.Report_Mode, Text(mode.Mode, operatingMode => operatingMode.DisplayName)),
                .. Rows(
                    energySystem?.OngridPower is null ? new(Strings.Report_GridPower, Text(mode.OngridPower, Units.Format)) : null,
                    energySystem?.OffgridPower is null ? new(Strings.Report_BackupOutputPower, Text(mode.OffgridPower, Units.Format)) : null,
                    energySystem?.BatterySocPercent is null ? new(Strings.Report_StateOfCharge, Text(mode.BatterySocPercent, Units.Percent)) : null),
                .. DescribeMeterCopy(mode.Meter),
            ]),

            Section(
                snapshot,
                Strings.Report_EnergyMeter,
                MarstekMethods.EnergyMeterGetStatus,
                snapshot.Meter,
                DescribeMeter,
                Strings.Report_CtNotConnected),

            Section(snapshot, Strings.Report_SolarInputs, MarstekMethods.PvGetStatus, snapshot.Pv, pv =>
            [
                new(Strings.Report_TotalPower, Text(pv.TotalPower, Units.Format)),
                .. pv.Channels.Select(DescribePvChannel),
            ]),

            // The charge is read with every poll by the energy system, with this answer only on the slow beat.
            Section(snapshot, Strings.Report_Battery, MarstekMethods.BatteryGetStatus, snapshot.Battery, battery => Rows(
                energySystem?.BatterySocPercent is null ? new(Strings.Report_StateOfCharge, Text(battery.SocPercent, Units.Percent)) : null,
                new(Strings.Report_Temperature, Text(battery.TemperatureCelsius, Units.Celsius)),
                new(Strings.Report_RemainingCapacity, Text(battery.RemainingCapacity, Units.Format)),
                new(Strings.Report_RatedCapacity, Text(battery.RatedCapacity, Units.Format)),
                new(Strings.Report_ChargingAllowed, Text(battery.IsChargingAllowed, YesNo)),
                new(Strings.Report_DischargingAllowed, Text(battery.IsDischargingAllowed, YesNo)))),

            Section(snapshot, Strings.Report_Wifi, MarstekMethods.WifiGetStatus, snapshot.Wifi, answer =>
            [
                new(Strings.Report_Network, answer.Ssid),
                new(Strings.Report_Signal, Text(answer.SignalStrengthDbm, DescribeSignal)),
                new(Strings.Report_Ip, answer.Ip),
                new(Strings.Report_Gateway, answer.Gateway),
                new(Strings.Report_SubnetMask, answer.SubnetMask),
                new(Strings.Report_Dns, answer.Dns),
                new(Strings.Report_Mac, answer.Mac),
            ]),

            Section(snapshot, Strings.Report_Bluetooth, MarstekMethods.BluetoothGetStatus, snapshot.Bluetooth, answer =>
            [
                new(Strings.Report_State, DescribeBluetoothState(answer)),
                new(Strings.Report_Mac, answer.Mac),
            ]),
        ];
    }

    private static StationSection Section<T>(
        StationSnapshot snapshot,
        string title,
        string method,
        Reading<T>? reading,
        Func<T, IEnumerable<SectionRow>> describe,
        string? notConnected = null)
    {
        if (reading is null)
        {
            return snapshot.UnsupportedMethods.TryGetValue(method, out var refusedAt)
                ? new StationSection(title, method, SectionState.NotSupported, refusedAt, Rows: [], Strings.Report_NotSupported)
                : new StationSection(title, method, SectionState.NoAnswer, ReceivedAt: null, Rows: [], Strings.Report_NoAnswer);
        }

        // The rest of such an answer is zeros, which would read as measurements.
        return PolledMethod.Named(method).IsDisconnected(snapshot)
            ? new StationSection(title, method, SectionState.NotConnected, reading.ReceivedAt, Rows: [], notConnected)
            : new StationSection(title, method, SectionState.Answered, reading.ReceivedAt, [.. describe(reading.Value)]);
    }

    private static IEnumerable<SectionRow> DescribeEnergySystem(StationSnapshot snapshot, EnergySystemStatus status, PowerSource source)
    {
        var isSolarShown = IsSolarShown(snapshot, status);

        // The battery's own answer tells the same capacity beside what is left of it.
        var isCapacityRepeated = status.BatteryCapacity is { } capacity && capacity == snapshot.Battery?.Value.RatedCapacity;

        return Rows(
            new(Strings.Report_StateOfCharge, Text(status.BatterySocPercent, Units.Percent)),
            isCapacityRepeated ? null : new(Strings.Report_BatteryCapacity, Text(status.BatteryCapacity, Units.Format)),
            new(Strings.Report_BatteryPower, DescribeBatteryPower(status, source)),
            isSolarShown ? new(Strings.Report_SolarPower, Text(status.PvPower, Units.Format)) : null,
            new(Strings.Report_GridPower, Text(status.OngridPower, Units.Format)),
            new(Strings.Report_BackupOutputPower, Text(status.OffgridPower, Units.Format)),
            isSolarShown ? Counter(Strings.Report_TotalSolarEnergy, status.TotalPvEnergy) : null,
            Counter(Strings.Report_TotalEnergyToGrid, status.TotalGridOutputEnergy),
            Counter(Strings.Report_TotalEnergyFromGrid, status.TotalGridInputEnergy),
            Counter(Strings.Report_TotalLoadEnergy, status.TotalLoadEnergy));
    }

    // A counter that has never moved tells nothing, and some never will: Venus E 3.0 leaves its load counter at
    // zero while the load is on the backup output.
    private static SectionRow? Counter(string label, Energy? energy) =>
        energy is { WattHours: 0 } ? null : new SectionRow(label, Text(energy, Units.Format));

    /// <returns>No row for a value that another answer of the snapshot tells the same.</returns>
    private static SectionRow? Unrepeated(string label, string? value, string? toldElsewhere) =>
        value is not null && string.Equals(value, toldElsewhere, StringComparison.OrdinalIgnoreCase)
            ? null
            : new SectionRow(label, value);

    private static IEnumerable<SectionRow> Rows(params SectionRow?[] rows) => rows.OfType<SectionRow>();

    private static string DescribeSignal(double dbm) => Phrases.List(
        Units.Dbm(dbm),
        dbm switch
        {
            >= ExcellentSignalFromDbm => Strings.Signal_Excellent,
            >= GoodSignalFromDbm => Strings.Signal_Good,
            >= FairSignalFromDbm => Strings.Signal_Fair,
            _ => Strings.Signal_Weak,
        });

    // A state the firmware words in a way not seen yet is passed through as it came.
    private static string? DescribeBluetoothState(BluetoothStatus bluetooth)
    {
        if (bluetooth.IsConnected == true)
            return Strings.Report_BluetoothConnected;

        return string.Equals(bluetooth.State, BluetoothDisconnectedState, StringComparison.OrdinalIgnoreCase)
            ? Strings.Report_BluetoothDisconnected
            : bluetooth.State;
    }

    // A station without solar inputs still reports their power and energy, as zeros. A value that is not zero is
    // shown whatever the station says about its inputs.
    private static bool IsSolarShown(StationSnapshot snapshot, EnergySystemStatus status) =>
        !snapshot.UnsupportedMethods.ContainsKey(MarstekMethods.PvGetStatus)
        || status.PvPower is { Watts: not 0 }
        || status.TotalPvEnergy is { WattHours: not 0 };

    // The copy is present in some operating modes only; a column of dashes would say nothing, and neither would
    // the zeros of a meter that is not connected.
    private static IEnumerable<SectionRow> DescribeMeterCopy(EnergyMeterStatus meter) =>
        meter.IsCtConnected == false ? [] : DescribeMeter(meter).Where(row => row.Value is not null);

    private static IEnumerable<SectionRow> DescribeMeter(EnergyMeterStatus meter) =>
    [
        new(Strings.Report_CtConnected, Text(meter.IsCtConnected, YesNo)),
        new(Strings.Report_PhaseAPower, Text(meter.PhaseAPower, Units.Format)),
        new(Strings.Report_PhaseBPower, Text(meter.PhaseBPower, Units.Format)),
        new(Strings.Report_PhaseCPower, Text(meter.PhaseCPower, Units.Format)),
        new(Strings.Report_TotalPower, Text(meter.TotalPower, Units.Format)),
        new(Strings.Report_InputEnergy, Text(meter.InputEnergy, Units.Format)),
        new(Strings.Report_OutputEnergy, Text(meter.OutputEnergy, Units.Format)),
    ];

    private static SectionRow DescribePvChannel(PvChannel channel)
    {
        string?[] parts =
        [
            Text(channel.Power, Units.Format),
            Text(channel.VoltageVolts, Units.Volts),
            Text(channel.CurrentAmperes, Units.Amperes),
            Text(channel.IsWorking, isWorking => isWorking ? Strings.Report_Working : Strings.Report_Standby),
        ];

        return new SectionRow(Phrases.Format(Strings.Report_SolarInput, channel.Number), Phrases.List(parts));
    }

    // A connection test has one answer to go by and no source, so it cannot tell a load the battery carries
    // from one the grid feeds past it.
    private static string? DescribeBatteryPower(EnergySystemStatus status, PowerSource source)
    {
        if (PowerSourceRule.BatteryPower(status, source) is not { } battery)
            return null;

        var power = Phrases.BatteryPower(battery.Power) ?? Units.Format(Power.Zero);

        return PowerSourceRule.IsBatteryPowerInDoubt(status, source)
            ? Phrases.Format(Strings.Report_CalculatedIfOnBattery, power)
            : power;
    }

    private static string YesNo(bool value) => value ? Strings.Common_Yes : Strings.Common_No;

    private static string? Text<T>(T? value, Func<T, string> format)
        where T : struct => value is { } present ? format(present) : null;
}
