using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Storage;

/// <summary>An <see cref="Outage"/> the way it is stored. Moments are kept in UTC to the millisecond, which is
/// all a stored date holds.</summary>
internal sealed class OutageDocument
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public bool IsStartObserved { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public bool IsEndObserved { get; set; }
    public bool IsOngoing { get; set; }
    public double ConsumedWattHours { get; set; }
    public double? StartSocPercent { get; set; }
    public double? EndSocPercent { get; set; }

    // Absent from a document stored before the counters were kept, as if neither had been reported.
    public double? GridInputWattHours { get; set; }
    public double? GridOutputWattHours { get; set; }

    public static OutageDocument From(Outage outage) => new()
    {
        Id = outage.Id,
        StartedAtUtc = outage.StartedAt.UtcDateTime,
        IsStartObserved = outage.IsStartObserved,
        EndedAtUtc = outage.EndedAt.UtcDateTime,
        IsEndObserved = outage.IsEndObserved,
        IsOngoing = outage.IsOngoing,
        ConsumedWattHours = outage.ConsumedEnergy.WattHours,
        StartSocPercent = outage.StartSocPercent,
        EndSocPercent = outage.EndSocPercent,
        GridInputWattHours = outage.LatestGridEnergy?.Input?.WattHours,
        GridOutputWattHours = outage.LatestGridEnergy?.Output?.WattHours,
    };

    public Outage ToOutage() => new()
    {
        Id = Id,
        StartedAt = StartedAtUtc.AsUtcMoment,
        IsStartObserved = IsStartObserved,
        EndedAt = EndedAtUtc.AsUtcMoment,
        IsEndObserved = IsEndObserved,
        IsOngoing = IsOngoing,
        ConsumedEnergy = new Energy(ConsumedWattHours),
        StartSocPercent = StartSocPercent,
        EndSocPercent = EndSocPercent,
        LatestGridEnergy = GridInputWattHours is null && GridOutputWattHours is null
            ? null
            : new GridEnergyCounters(AsEnergy(GridInputWattHours), AsEnergy(GridOutputWattHours)),
    };

    private static Energy? AsEnergy(double? wattHours) => wattHours is { } value ? new Energy(value) : null;
}
