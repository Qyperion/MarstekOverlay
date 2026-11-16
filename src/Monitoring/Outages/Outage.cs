using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.Outages;

/// <summary>One stretch of running on the battery.</summary>
public sealed record Outage
{
    /// <summary>Zero until the outage is stored.</summary>
    public int Id { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    /// <summary>False when the station was already on the battery by the time it was first seen, so the outage
    /// began earlier than <see cref="StartedAt"/>.</summary>
    public bool IsStartObserved { get; init; }

    /// <summary>While the outage is ongoing, the latest moment it was seen.</summary>
    public DateTimeOffset EndedAt { get; init; }

    /// <summary>False when the outage was last seen still going on, so it ended later than <see cref="EndedAt"/>.</summary>
    public bool IsEndObserved { get; init; }

    public bool IsOngoing { get; init; }

    /// <summary>What left through the backup output between <see cref="StartedAt"/> and <see cref="EndedAt"/>.</summary>
    public Energy ConsumedEnergy { get; init; }

    public double? StartSocPercent { get; init; }
    public double? EndSocPercent { get; init; }

    /// <summary>The grid energy counters in the latest answer the outage was seen in; nothing in an outage
    /// recorded before they were kept.</summary>
    public GridEnergyCounters? LatestGridEnergy { get; init; }

    public TimeSpan Duration => EndedAt - StartedAt;

    /// <summary>False for an outage seen only in part, which lasted longer than <see cref="Duration"/> tells.</summary>
    public bool IsWhollyObserved => IsStartObserved && (IsOngoing || IsEndObserved);
}
