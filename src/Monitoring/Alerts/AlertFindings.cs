namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>What the watches found in one snapshot.</summary>
internal sealed class AlertFindings
{
    public List<Alert> Active { get; } = [];
    public List<Alert> Raised { get; } = [];

    /// <param name="alert">Nothing while the condition does not hold.</param>
    public void Follow(Latch latch, Alert? alert, bool isCleared)
    {
        var isJustSet = latch.Follow(
            isMet: alert is not null,
            isCleared,
            isNewsAtFirstSight: alert?.Severity == AlertSeverity.Critical);

        if (alert is null)
            return;

        Active.Add(alert);
        if (isJustSet)
            Raised.Add(alert);
    }

    public AlertReport ToReport() => new([.. Active.OrderByDescending(alert => alert.Severity)], Raised);
}
