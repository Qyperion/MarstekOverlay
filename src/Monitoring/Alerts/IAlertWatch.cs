namespace MarstekOverlay.Monitoring.Alerts;

/// <summary>Keeps what it has told of for one kind of alert.</summary>
internal interface IAlertWatch
{
    /// <summary>Starts over as if the watch had just begun, once the kind is switched off.</summary>
    void Forget();
}
