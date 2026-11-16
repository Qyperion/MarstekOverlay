using MarstekOverlay.Desktop.Services;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>Opens nothing and notes what it was asked to do.</summary>
internal sealed class RecordingShell : IShell
{
    public List<string> Calls { get; } = [];

    public bool IsOverlayVisible => true;

    public void ToggleOverlay() => Calls.Add(nameof(ToggleOverlay));

    public void ShowOverlay() => Calls.Add(nameof(ShowOverlay));

    public void HideOverlay() => Calls.Add(nameof(HideOverlay));

    public void ShowSettings() => Calls.Add(nameof(ShowSettings));

    public void ShowDetails() => Calls.Add(nameof(ShowDetails));

    public void Exit() => Calls.Add(nameof(Exit));
}
