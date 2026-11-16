using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring;

/// <summary>The latest answer to every request. A request that fails keeps its previous reading, so each one
/// carries its own age.</summary>
public sealed record StationSnapshot
{
    public static readonly StationSnapshot Empty = new();

    public Reading<DeviceInfo>? Device { get; init; }
    public Reading<EnergySystemStatus>? EnergySystem { get; init; }
    public Reading<EnergySystemMode>? Mode { get; init; }
    public Reading<EnergyMeterStatus>? Meter { get; init; }
    public Reading<PvStatus>? Pv { get; init; }
    public Reading<BatteryStatus>? Battery { get; init; }
    public Reading<WifiStatus>? Wifi { get; init; }
    public Reading<BluetoothStatus>? Bluetooth { get; init; }

    /// <summary>Wire names of the methods this model or firmware turned down as unknown without ever having
    /// answered them, each with the moment it last did so.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> UnsupportedMethods { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>The methods that are not requested again until a check is asked for
    /// (<see cref="StationMonitor.CheckAbandoned"/>): those of <see cref="UnsupportedMethods"/> that were turned
    /// down a second time, and those whose answer said that what they read is not connected. One refusal is not
    /// the model's last word: a station was seen to turn down a method it has.</summary>
    public IReadOnlySet<string> AbandonedMethods { get; init; } = new HashSet<string>();

    /// <summary>What went wrong in the latest poll only; empty once a poll goes through cleanly.</summary>
    public IReadOnlyList<PollError> Errors { get; init; } = [];

    public bool HasReadings => PolledMethod.All.Any(method => method.HasReading(this));
}
