using MarstekOverlay.Monitoring.Supply;

namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Tells of the charge coming down to each of the thresholds.</summary>
internal sealed class ChargeThresholdWatch : IAlertWatch
{
    // Without the margin, a charge that hovers at a threshold would raise the same alert over and over.
    private const double RearmMarginPercent = 2;

    private readonly Dictionary<double, Latch> _thresholds = [];

    public void Follow(StationSnapshot snapshot, AlertSettings settings, PowerSource source, AlertFindings findings)
    {
        var thresholds = settings.UsableChargeThresholds();
        foreach (var dropped in _thresholds.Keys.Except(thresholds).ToList())
            _thresholds.Remove(dropped);

        if (snapshot.EnergySystem?.Value is not { BatterySocPercent: { } soc } status)
            return;

        // Once the grid charges the battery, the low charge is on its way out: it needs no warning kept up, and
        // found so at first sight it is not news even when critical.
        var isChargingFromMains = PowerSourceRule.IsChargingFromMains(status, source);
        LowChargeAlert? lowestReached = null;
        LowChargeAlert? lowestJustReached = null;

        for (var index = 0; index < thresholds.Count; index++)
        {
            var threshold = thresholds[index];
            if (!_thresholds.TryGetValue(threshold, out var latch))
                _thresholds[threshold] = latch = new Latch();

            var alert = new LowChargeAlert(threshold, soc, IsLowestThreshold: index == 0);
            var isReached = soc <= threshold;
            var isJustReached = latch.Follow(
                isReached,
                isCleared: soc >= threshold + RearmMarginPercent,
                isNewsAtFirstSight: alert.Severity == AlertSeverity.Critical && !isChargingFromMains);

            if (isReached)
                lowestReached ??= alert;

            if (isJustReached)
                lowestJustReached ??= alert;
        }

        // Several thresholds passed between two answers are one piece of news, told by the lowest of them.
        if (lowestReached is not null && !isChargingFromMains)
            findings.Active.Add(lowestReached);

        if (lowestJustReached is not null)
            findings.Raised.Add(lowestJustReached);
    }

    public void Forget() => _thresholds.Clear();
}
