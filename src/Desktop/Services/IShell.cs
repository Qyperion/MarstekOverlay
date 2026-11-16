namespace MarstekOverlay.Desktop.Services;

public interface IShell
{
    bool IsOverlayVisible { get; }

    void ToggleOverlay();
    void ShowOverlay();
    void HideOverlay();
    void ShowSettings();
    void ShowDetails();
    void Exit();
}
