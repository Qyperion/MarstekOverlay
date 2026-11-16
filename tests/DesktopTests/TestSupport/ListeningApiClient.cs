using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

using System.Collections.Concurrent;

using Xunit;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>Notes which station each request goes to and answers none of them.</summary>
internal sealed class ListeningApiClient : IMarstekApiClient
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<StationEndpoint, TaskCompletionSource> _asked = new();

    /// <summary>Completes once a request has gone to <paramref name="station"/>.</summary>
    public Task AskedAsync(StationEndpoint station) =>
        Asked(station).Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);

    public Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token) =>
        throw new InvalidOperationException("This test looks for no station.");

    public Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token) => Unanswered<DeviceInfo>(station);

    public Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token) => Unanswered<WifiStatus>(station);

    public Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token) => Unanswered<BluetoothStatus>(station);

    public Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token) => Unanswered<BatteryStatus>(station);

    public Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Unanswered<PvStatus>(station);

    public Task<EnergySystemStatus> GetEnergySystemStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Unanswered<EnergySystemStatus>(station);

    public Task<EnergySystemMode> GetEnergySystemModeAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Unanswered<EnergySystemMode>(station);

    public Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
        Unanswered<EnergyMeterStatus>(station);

    private TaskCompletionSource Asked(StationEndpoint station) =>
        _asked.GetOrAdd(station, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    private Task<T> Unanswered<T>(StationEndpoint station)
    {
        Asked(station).TrySetResult();
        return Task.FromException<T>(new TimeoutException("The station did not answer."));
    }
}
