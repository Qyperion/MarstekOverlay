using MarstekOverlay.Desktop.Presentation;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Where news reaches the user when the panel is not being looked at.</summary>
public interface INotificationChannel
{
    void Notify(Notification notification);
}
