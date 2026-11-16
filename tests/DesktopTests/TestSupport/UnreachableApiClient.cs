using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>For what is tested without a station: any request is a mistake of the test.</summary>
internal sealed class UnreachableApiClient : IMarstekApiClient
{
    public Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token) => Refuse<IReadOnlyList<DiscoveredDevice>>();

    public Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token) => Refuse<DeviceInfo>();

    public Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token) => Refuse<WifiStatus>();

    public Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token) => Refuse<BluetoothStatus>();

    public Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token) => Refuse<BatteryStatus>();

    public Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) => Refuse<PvStatus>();

    public Task<EnergySystemStatus> GetEnergySystemStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Refuse<EnergySystemStatus>();

    public Task<EnergySystemMode> GetEnergySystemModeAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Refuse<EnergySystemMode>();

    public Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Refuse<EnergyMeterStatus>();

    private static Task<T> Refuse<T>() => throw new InvalidOperationException("This test has no station to ask.");
}
