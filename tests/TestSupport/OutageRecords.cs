using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.TestSupport;

public static class OutageRecords
{
    /// <summary>An outage seen from its start to its end.</summary>
    public static Outage Finished(
        DateTimeOffset startedAt,
        TimeSpan duration,
        double wattHours = 1560,
        double startSoc = 99,
        double endSoc = 91) => new()
        {
            StartedAt = startedAt,
            IsStartObserved = true,
            EndedAt = startedAt + duration,
            IsEndObserved = true,
            ConsumedEnergy = new Energy(wattHours),
            StartSocPercent = startSoc,
            EndSocPercent = endSoc,
        };
}
