using MarstekOverlay.Desktop.Presentation;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Where the state stays in sight while the panel is hidden or covered.</summary>
public interface IStatusIndicator
{
    void Indicate(OverlayState state);
}
