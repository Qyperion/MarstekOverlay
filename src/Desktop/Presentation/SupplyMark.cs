namespace MarstekOverlay.Desktop.Presentation;

/// <summary>What the compact panel and the notification-area icon mark beside the charge, where there is no
/// room for the words of the heading.</summary>
public enum SupplyMark
{
    None,
    OnMains,
    OnBattery,

    /// <summary>The station has stopped answering, so whatever is shown is old.</summary>
    NoConnection,
}
