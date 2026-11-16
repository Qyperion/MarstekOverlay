using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Words every alert the pipeline raises and sends it out, whichever windows happen to be open.</summary>
public sealed class AlertNotifier(StationPipeline pipeline, INotificationChannel channel, TimeProvider timeProvider) : IDisposable
{
    public void Start() => pipeline.AlertRaised += Notify;

    public void Dispose() => pipeline.AlertRaised -= Notify;

    private void Notify(Alert alert) => channel.Notify(AlertPresenter.Notify(alert, timeProvider.GetUtcNow()));
}
