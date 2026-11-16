using MarstekOverlay.LocalApi.Models;

using System.Text.Json;

namespace MarstekOverlay.LocalApi.Mapping;

internal static class DeviceMappingExtensions
{
    private const string ModelField = "device";

    extension(JsonElement result)
    {
        /// <summary>Tells a station's answer to a discovery broadcast from other JSON traffic on the port.</summary>
        public bool IsDeviceIdentity() => result.Text(ModelField) is not null;

        public DeviceInfo ToDeviceInfo() => new()
        {
            Model = result.Text(ModelField) ?? string.Empty,
            FirmwareVersion = (int?)result.Number("ver"),
            BluetoothMac = result.Text("ble_mac"),
            WifiMac = result.Text("wifi_mac"),
            WifiName = result.Text("wifi_name"),
            ReportedIp = result.Text("ip"),
        };

        public WifiStatus ToWifiStatus() => new()
        {
            Mac = result.Text("wifi_mac"),
            Ssid = result.Text("ssid"),
            SignalStrengthDbm = result.Number("rssi"),
            Ip = result.Text("sta_ip"),
            Gateway = result.Text("sta_gate"),
            SubnetMask = result.Text("sta_mask"),
            Dns = result.Text("sta_dns"),
        };

        public BluetoothStatus ToBluetoothStatus() => new()
        {
            State = result.Text("state"),
            Mac = result.Text("ble_mac"),
        };

        public BatteryStatus ToBatteryStatus() => new()
        {
            SocPercent = result.Number("soc"),
            IsChargingAllowed = result.Flag("charg_flag"),
            IsDischargingAllowed = result.Flag("dischrg_flag"),
            TemperatureCelsius = result.Number("bat_temp"),
            RemainingCapacity = result.WattHours("bat_capacity"),
            RatedCapacity = result.WattHours("rated_capacity"),
        };
    }
}
