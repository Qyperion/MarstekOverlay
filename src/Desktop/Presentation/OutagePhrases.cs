using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Outages;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Words an outage the same way on the panel, in the journal and in the notifications.</summary>
public static class OutagePhrases
{
    /// <param name="lasted">How long it has lasted, when that is not its <see cref="Outage.Duration"/>.</param>
    public static string Duration(Outage outage, TimeSpan? lasted = null) =>
        Phrases.Duration(lasted ?? outage.Duration, isExact: outage.IsWhollyObserved);

    /// <returns>The charge at the start and at the end; nothing while either is unknown.</returns>
    public static string? ChargeChange(Outage outage) =>
        outage is { StartSocPercent: { } start, EndSocPercent: { } end }
            ? Phrases.Format(Strings.Outage_ChargeChange, Units.Percent(start), Units.Percent(end))
            : null;

    /// <returns>What the app did not see of the outage; nothing for one it saw whole.</returns>
    public static string? Unseen(Outage outage) =>
        outage.IsWhollyObserved
            ? null
            : Phrases.Sentences(
                outage.IsStartObserved ? null : Strings.Outage_StartUnseen,
                outage.IsOngoing || outage.IsEndObserved ? null : Strings.Outage_EndUnseen);

    /// <param name="isFresh">Whether the readings are recent enough to say the outage goes on now.</param>
    public static OverlayRow DescribeCurrent(StationSnapshot snapshot, Outage? ongoing, bool isFresh, DateTimeOffset now)
    {
        if (snapshot.EnergySystem is null)
            return new OverlayRow(OverlayRowKind.CurrentOutage, Strings.Overlay_CurrentOutage, Strings.Overlay_ValueMissing);

        if (ongoing is null)
            return new OverlayRow(OverlayRowKind.CurrentOutage, Strings.Overlay_CurrentOutage, Strings.Outage_NotNow);

        // Stale readings say nothing about the time since: the outage is known to have lasted only until then.
        var lasted = (isFresh ? now : ongoing.EndedAt) - ongoing.StartedAt;
        var summary = Phrases.Format(Strings.Outage_Summary, Duration(ongoing, lasted), Units.Format(ongoing.ConsumedEnergy));

        return new OverlayRow(
            OverlayRowKind.CurrentOutage,
            Strings.Overlay_CurrentOutage,
            summary,
            Hint: Phrases.Sentences(
                Phrases.Format(Strings.Supply_BatterySince, Phrases.Moment(ongoing.StartedAt, now)),
                Unseen(ongoing),
                DescribeCharge(ongoing)));
    }

    public static OverlayRow DescribeLatest(Outage? latest, DateTimeOffset now)
    {
        if (latest is null)
            return new OverlayRow(OverlayRowKind.LatestOutage, Strings.Overlay_LatestOutage, Strings.Outage_NoneRecorded);

        var summary = Phrases.Format(
            Strings.Outage_LatestSummary,
            Phrases.Moment(latest.StartedAt, now),
            Duration(latest),
            Units.Format(latest.ConsumedEnergy));

        return new OverlayRow(
            OverlayRowKind.LatestOutage,
            Strings.Overlay_LatestOutage,
            summary,
            Hint: Phrases.Sentences(
                Phrases.Format(Strings.Outage_Period, Phrases.Moment(latest.StartedAt, now), Phrases.Moment(latest.EndedAt, now)),
                Unseen(latest),
                DescribeCharge(latest)));
    }

    private static string? DescribeCharge(Outage outage) =>
        ChargeChange(outage) is { } change ? Phrases.Format(Strings.Outage_Charge, change) : null;
}
