using LiteDB;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Storage;

/// <summary>A <see cref="StationSample"/> the way it is stored. The moment it was taken is its key: no two
/// samples share one, and the key is the only index a search by time needs.</summary>
internal sealed class SampleDocument
{
    [BsonId]
    public DateTime AtUtc { get; set; }

    public double? SocPercent { get; set; }
    public double? BatteryWatts { get; set; }
    public double? BackupOutputWatts { get; set; }
    public PowerSource Source { get; set; }

    public static SampleDocument From(StationSample sample) => new()
    {
        AtUtc = sample.At.UtcDateTime,
        SocPercent = sample.SocPercent,
        BatteryWatts = sample.BatteryPower?.Watts,
        BackupOutputWatts = sample.BackupOutputPower?.Watts,
        Source = sample.Source,
    };

    public StationSample ToSample() => new(
        AtUtc.AsUtcMoment,
        SocPercent,
        BatteryWatts is { } battery ? new Power(battery) : null,
        BackupOutputWatts is { } output ? new Power(output) : null,
        Source);
}
