using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

using System.Collections.Concurrent;

namespace MarstekOverlay.UnitTests.TestSupport;

internal sealed class FakeMarstekApiClient : IMarstekApiClient
{
    public ConcurrentQueue<string> Requests { get; } = new();
    public ConcurrentDictionary<string, Exception> Failures { get; } = new();

    /// <summary>The profile of every request whose values depend on one, in the order they came.</summary>
    public ConcurrentQueue<FirmwareProfile> Profiles { get; } = new();

    public DeviceInfo Device { get; set; } = new() { Model = "VenusE 3.0", FirmwareVersion = 150 };
    public EnergySystemStatus EnergySystem { get; set; } = new() { BatterySocPercent = 90 };
    public EnergyMeterStatus Meter { get; set; } = new();

    /// <summary>Raised when a request arrives, before it is answered.</summary>
    public event Action<string>? Requested;

    public Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<DiscoveredDevice>>([]);

    public Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token) =>
        AnswerAsync(MarstekMethods.GetDevice, Device);

    public Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token) =>
        AnswerAsync(MarstekMethods.WifiGetStatus, new WifiStatus());

    public Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token) =>
        AnswerAsync(MarstekMethods.BluetoothGetStatus, new BluetoothStatus());

    public Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token) =>
        AnswerAsync(MarstekMethods.BatteryGetStatus, new BatteryStatus());

    public Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        AnswerAsync(MarstekMethods.PvGetStatus, profile, new PvStatus());

    public Task<EnergySystemStatus> GetEnergySystemStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        AnswerAsync(MarstekMethods.EnergySystemGetStatus, profile, EnergySystem);

    public Task<EnergySystemMode> GetEnergySystemModeAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        AnswerAsync(MarstekMethods.EnergySystemGetMode, profile, new EnergySystemMode());

    public Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        AnswerAsync(MarstekMethods.EnergyMeterGetStatus, profile, Meter);

    private Task<T> AnswerAsync<T>(string method, FirmwareProfile profile, T value)
    {
        Profiles.Enqueue(profile);

        return AnswerAsync(method, value);
    }

    private Task<T> AnswerAsync<T>(string method, T value)
    {
        Requests.Enqueue(method);
        Requested?.Invoke(method);

        return Failures.TryGetValue(method, out var failure)
            ? Task.FromException<T>(failure)
            : Task.FromResult(value);
    }
}
