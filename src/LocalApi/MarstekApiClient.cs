using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Mapping;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.LocalApi.Settings;
using MarstekOverlay.LocalApi.Transport;

using Microsoft.Extensions.Options;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.LocalApi;

public sealed class MarstekApiClient(IJsonRpcTransport transport, IOptions<MarstekApiSettings> options) : IMarstekApiClient
{
    private static readonly JsonObject FirstInstance = new() { ["id"] = 0 };

    // A Bluetooth MAC of "0" addresses every device, in a broadcast and in a request to one station alike.
    private static readonly JsonObject AnyDevice = new() { ["ble_mac"] = "0" };

    private readonly TimeSpan _discoveryDuration = TimeSpan.FromMilliseconds(options.Value.DiscoveryDurationMs);

    public async Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token)
    {
        var replies = await transport.BroadcastAsync(port, MarstekMethods.GetDevice, AnyDevice, _discoveryDuration, token);

        // The station is recorded under the address its answer came from: the "ip" inside the answer can be stale.
        return replies
            .Where(reply => reply.Result.IsDeviceIdentity())
            .DistinctBy(reply => reply.Source)
            .Select(reply => new DiscoveredDevice(new StationEndpoint(reply.Source, port), reply.Result.ToDeviceInfo()))
            .ToList();
    }

    public async Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token) =>
        (await transport.InvokeAsync(station, MarstekMethods.GetDevice, AnyDevice, token)).ToDeviceInfo();

    public async Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.WifiGetStatus, token)).ToWifiStatus();

    public async Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.BluetoothGetStatus, token)).ToBluetoothStatus();

    public async Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.BatteryGetStatus, token)).ToBatteryStatus();

    public async Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.PvGetStatus, token)).ToPvStatus(profile);

    public async Task<EnergySystemStatus> GetEnergySystemStatusAsync(
        StationEndpoint station,
        FirmwareProfile profile,
        CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.EnergySystemGetStatus, token)).ToEnergySystemStatus(profile);

    public async Task<EnergySystemMode> GetEnergySystemModeAsync(
        StationEndpoint station,
        FirmwareProfile profile,
        CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.EnergySystemGetMode, token)).ToEnergySystemMode(profile);

    public async Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(
        StationEndpoint station,
        FirmwareProfile profile,
        CancellationToken token) =>
        (await GetStatusAsync(station, MarstekMethods.EnergyMeterGetStatus, token)).ToEnergyMeterStatus(profile);

    private Task<JsonElement> GetStatusAsync(StationEndpoint station, string method, CancellationToken token) =>
        transport.InvokeAsync(station, method, FirstInstance, token);
}
