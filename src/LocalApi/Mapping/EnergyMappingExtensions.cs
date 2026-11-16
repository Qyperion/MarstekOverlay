using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

using System.Text.Json;

namespace MarstekOverlay.LocalApi.Mapping;

internal static class EnergyMappingExtensions
{
    private const double UInt16Range = 65536;

    extension(JsonElement result)
    {
        public EnergySystemStatus ToEnergySystemStatus(FirmwareProfile profile) => new()
        {
            BatterySocPercent = result.Number("bat_soc"),
            BatteryCapacity = result.WattHours("bat_cap"),
            PvPower = result.Watts("pv_power"),
            OngridPower = result.Watts("ongrid_power"),
            OffgridPower = SignedFromUInt16(result.Watts("offgrid_power")),
            BatteryPower = result.Watts("bat_power"),
            TotalPvEnergy = result.WattHours("total_pv_energy", profile.PvEnergyWattHoursPerUnit),
            TotalGridOutputEnergy = result.WattHours("total_grid_output_energy"),
            TotalGridInputEnergy = result.WattHours("total_grid_input_energy"),
            TotalLoadEnergy = result.WattHours("total_load_energy"),
        };

        public EnergySystemMode ToEnergySystemMode(FirmwareProfile profile) => new()
        {
            Mode = result.ToOperatingMode(),
            OngridPower = result.Watts("ongrid_power"),
            OffgridPower = SignedFromUInt16(result.Watts("offgrid_power")),
            BatterySocPercent = result.Number("bat_soc"),
            Meter = result.ToEnergyMeterStatus(profile),
        };

        public EnergyMeterStatus ToEnergyMeterStatus(FirmwareProfile profile) => new()
        {
            IsCtConnected = result.Flag("ct_state"),
            PhaseAPower = result.Watts("a_power"),
            PhaseBPower = result.Watts("b_power"),
            PhaseCPower = result.Watts("c_power"),
            TotalPower = result.Watts("total_power"),
            InputEnergy = result.WattHours("input_energy", profile.MeterEnergyWattHoursPerUnit),
            OutputEnergy = result.WattHours("output_energy", profile.MeterEnergyWattHoursPerUnit),
        };

        // The same firmware line answers with the name on one build and with its number on another.
        private OperatingMode? ToOperatingMode()
        {
            if (result.Number("mode") is { } number)
                return Enum.IsDefined((OperatingMode)(int)number) ? (OperatingMode)(int)number : null;

            return Enum.TryParse<OperatingMode>(result.Text("mode"), ignoreCase: true, out var mode) && Enum.IsDefined(mode)
                ? mode
                : null;
        }
    }

    // Some firmware sends the off-grid power as an unsigned 16-bit value, so a small negative one arrives near 65536.
    private static Power? SignedFromUInt16(Power? power) =>
        power is { Watts: > short.MaxValue } wrapped ? new Power(wrapped.Watts - UInt16Range) : power;
}
