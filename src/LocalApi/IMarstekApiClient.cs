using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.LocalApi;

/// <summary>
/// The read side of the Marstek Device Open API (Rev 3.1). Every request throws <see cref="MarstekApiException"/>
/// when the station does not answer or rejects it; a model that lacks a method rejects it with
/// <see cref="MarstekRpcException.IsMethodNotFound"/>.
/// </summary>
public interface IMarstekApiClient
{
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token);
    Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token);
    Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token);
    Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token);

    /// <summary>Some firmware restarts the station on this request, which is why it can be switched off in settings.</summary>
    Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token);

    Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token);
    Task<EnergySystemStatus> GetEnergySystemStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token);
    Task<EnergySystemMode> GetEnergySystemModeAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token);
    Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token);
}
