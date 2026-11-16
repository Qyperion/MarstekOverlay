using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring;

/// <summary>One request of a poll: its wire name, the poll it goes out with and where its answer is kept.</summary>
public abstract class PolledMethod
{
    /// <summary>In the order they are sent (<c>docs/MarstekLocalApi.md</c>).</summary>
    public static IReadOnlyList<PolledMethod> All { get; } =
    [
        // First, because its answer tells how the readings that follow are encoded.
        new Polled<DeviceInfo>(
            MarstekMethods.GetDevice,
            PollScope.Full,
            (client, station, _, token) => client.GetDeviceAsync(station, token),
            snapshot => snapshot.Device,
            (snapshot, reading) => snapshot with { Device = reading }),
        new Polled<EnergySystemStatus>(
            MarstekMethods.EnergySystemGetStatus,
            PollScope.Fast,
            (client, station, profile, token) => client.GetEnergySystemStatusAsync(station, profile, token),
            snapshot => snapshot.EnergySystem,
            (snapshot, reading) => snapshot with { EnergySystem = reading }),
        new Polled<EnergySystemMode>(
            MarstekMethods.EnergySystemGetMode,
            PollScope.Fast,
            (client, station, profile, token) => client.GetEnergySystemModeAsync(station, profile, token),
            snapshot => snapshot.Mode,
            (snapshot, reading) => snapshot with { Mode = reading }),
        new Polled<EnergyMeterStatus>(
            MarstekMethods.EnergyMeterGetStatus,
            PollScope.Fast,
            (client, station, profile, token) => client.GetEnergyMeterStatusAsync(station, profile, token),
            snapshot => snapshot.Meter,
            (snapshot, reading) => snapshot with { Meter = reading },
            isDisconnected: meter => meter.IsCtConnected == false),
        new Polled<PvStatus>(
            MarstekMethods.PvGetStatus,
            PollScope.Fast,
            (client, station, profile, token) => client.GetPvStatusAsync(station, profile, token),
            snapshot => snapshot.Pv,
            (snapshot, reading) => snapshot with { Pv = reading }),
        new Polled<BatteryStatus>(
            MarstekMethods.BatteryGetStatus,
            PollScope.Full,
            (client, station, _, token) => client.GetBatteryStatusAsync(station, token),
            snapshot => snapshot.Battery,
            (snapshot, reading) => snapshot with { Battery = reading }),
        new Polled<WifiStatus>(
            MarstekMethods.WifiGetStatus,
            PollScope.Full,
            (client, station, _, token) => client.GetWifiStatusAsync(station, token),
            snapshot => snapshot.Wifi,
            (snapshot, reading) => snapshot with { Wifi = reading }),
        new Polled<BluetoothStatus>(
            MarstekMethods.BluetoothGetStatus,
            PollScope.Full,
            (client, station, _, token) => client.GetBluetoothStatusAsync(station, token),
            snapshot => snapshot.Bluetooth,
            (snapshot, reading) => snapshot with { Bluetooth = reading }),
    ];

    private static readonly Dictionary<string, PolledMethod> ByName = All.ToDictionary(method => method.Name);

    private PolledMethod(string name, PollScope sentWith)
    {
        Name = name;
        SentWith = sentWith;
    }

    public string Name { get; }

    /// <summary><see cref="PollScope.Fast"/> goes out with every poll, <see cref="PollScope.Full"/> with the
    /// full one only.</summary>
    public PollScope SentWith { get; }

    /// <param name="name">The wire name of one of <see cref="All"/>.</param>
    public static PolledMethod Named(string name) =>
        ByName.TryGetValue(name, out var method)
            ? method
            : throw new ArgumentException($"{name} is not a method the station is polled with.", nameof(name));

    public bool IsSentWith(PollScope scope) => scope switch
    {
        PollScope.Full => true,
        PollScope.FastWithIdentity => SentWith == PollScope.Fast || Name == MarstekMethods.GetDevice,
        _ => SentWith == PollScope.Fast,
    };

    public abstract bool HasReading(StationSnapshot snapshot);

    /// <summary>Whether the latest answer says that what the request reads is not connected to the station. The
    /// rest of such an answer is zeros, and asking again brings the same until someone connects it.</summary>
    public abstract bool IsDisconnected(StationSnapshot snapshot);

    /// <returns>How the answer changes a snapshot.</returns>
    internal abstract Task<Func<StationSnapshot, StationSnapshot>> RequestAsync(
        IMarstekApiClient client,
        StationEndpoint station,
        FirmwareProfile profile,
        TimeProvider timeProvider,
        CancellationToken token);

    private sealed class Polled<T>(
        string name,
        PollScope sentWith,
        Func<IMarstekApiClient, StationEndpoint, FirmwareProfile, CancellationToken, Task<T>> request,
        Func<StationSnapshot, Reading<T>?> read,
        Func<StationSnapshot, Reading<T>?, StationSnapshot> store,
        Func<T, bool>? isDisconnected = null) : PolledMethod(name, sentWith)
    {
        public override bool HasReading(StationSnapshot snapshot) => read(snapshot) is not null;

        public override bool IsDisconnected(StationSnapshot snapshot) =>
            isDisconnected is not null && read(snapshot) is { } reading && isDisconnected(reading.Value);

        internal override async Task<Func<StationSnapshot, StationSnapshot>> RequestAsync(
            IMarstekApiClient client,
            StationEndpoint station,
            FirmwareProfile profile,
            TimeProvider timeProvider,
            CancellationToken token)
        {
            var value = await request(client, station, profile, token);
            var reading = new Reading<T>(value, timeProvider.GetUtcNow());

            return snapshot => store(snapshot, reading);
        }
    }
}
