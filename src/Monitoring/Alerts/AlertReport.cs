namespace MarstekOverlay.Monitoring.Alerts;

/// <param name="Active">What is wrong right now, the gravest first. It stays here for as long as it lasts.</param>
/// <param name="Raised">What has just happened or set in and has not been told yet.</param>
public sealed record AlertReport(IReadOnlyList<Alert> Active, IReadOnlyList<Alert> Raised);
