using MarstekOverlay.LocalApi;

using System.Text.Json.Nodes;

namespace MarstekOverlay.StationSimulator;

/// <summary>The results of the read methods, with the fields and the units of Venus E 3.0 on firmware 150.</summary>
public static class StationAnswers
{
    public const string Model = "VenusE 3.0";
    public const int Firmware = 150;
    public const string Source = Model + "-acd929a7f2c1";

    private const string BluetoothMac = "acd929a7f2c1";
    private const string WifiMac = "485519c3d2b0";
    private const string WifiName = "Simulator";
    private const string Address = "127.0.0.1";
    private const double WifiSignalDbm = -55;

    // bat_capacity moves in steps of 0.1% of the rated capacity.
    private const double CapacityStepPercent = 0.1;

    /// <returns>Nothing for a method the station does not have.</returns>
    public static JsonObject? For(string method, StationReading reading) => method switch
    {
        MarstekMethods.GetDevice => new JsonObject
        {
            ["device"] = Model,
            ["ver"] = Firmware,
            ["ble_mac"] = BluetoothMac,
            ["wifi_mac"] = WifiMac,
            ["wifi_name"] = WifiName,
            ["ip"] = Address,
        },
        MarstekMethods.EnergySystemGetStatus => new JsonObject
        {
            ["id"] = 0,
            ["bat_soc"] = Soc(reading),
            ["bat_cap"] = SimulatedStation.CapacityWattHours,
            ["pv_power"] = 0,
            ["ongrid_power"] = reading.OngridWatts,
            ["offgrid_power"] = reading.LoadWatts,
            ["total_pv_energy"] = 0,
            ["total_grid_output_energy"] = 0,
            ["total_grid_input_energy"] = reading.GridInputWattHours,
            ["total_load_energy"] = 0,
        },
        MarstekMethods.EnergySystemGetMode => new JsonObject
        {
            ["id"] = 0,
            ["mode"] = "Manual",
            ["ongrid_power"] = reading.OngridWatts,
            ["offgrid_power"] = reading.LoadWatts,
            ["bat_soc"] = Soc(reading),
        },
        MarstekMethods.EnergyMeterGetStatus => new JsonObject
        {
            ["id"] = 0,
            ["ct_state"] = 0,
            ["a_power"] = 0,
            ["b_power"] = 0,
            ["c_power"] = 0,
            ["total_power"] = 0,
            ["input_energy"] = 0,
            ["output_energy"] = 0,
        },
        MarstekMethods.BatteryGetStatus => new JsonObject
        {
            ["id"] = 0,
            ["soc"] = Soc(reading),
            ["charg_flag"] = true,
            ["dischrg_flag"] = true,
            ["bat_temp"] = reading.TemperatureCelsius,
            ["bat_capacity"] = Math.Round(
                Math.Floor(reading.SocPercent / CapacityStepPercent) * CapacityStepPercent / 100 * SimulatedStation.CapacityWattHours,
                1),
            ["rated_capacity"] = SimulatedStation.CapacityWattHours,
        },
        MarstekMethods.WifiGetStatus => new JsonObject
        {
            ["id"] = 0,
            ["wifi_mac"] = WifiMac,
            ["ssid"] = WifiName,
            ["rssi"] = WifiSignalDbm,
            ["sta_ip"] = Address,
            ["sta_gate"] = Address,
            ["sta_mask"] = "255.0.0.0",
            ["sta_dns"] = Address,
        },
        MarstekMethods.BluetoothGetStatus => new JsonObject
        {
            ["id"] = 0,
            ["state"] = "disconnect",
            ["ble_mac"] = BluetoothMac,
        },
        _ => null,
    };

    // The API rounds the charge down to a whole percent: 86.99% is 86.
    private static double Soc(StationReading reading) => Math.Floor(reading.SocPercent);
}
