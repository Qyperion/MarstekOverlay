using System.Windows;

namespace MarstekOverlay.Desktop.Views;

/// <summary>Where the panel goes on the desktop. The user's choice is kept, yet the panel is shown whole: a
/// position saved on a monitor that is no longer attached would leave it out of reach, and a panel that grows
/// near an edge would run over it.</summary>
public static class PanelPlacement
{
    /// <param name="wanted">The top left corner the user left the panel at.</param>
    /// <returns>The nearest corner that keeps the whole panel on the desktop. A panel larger than the desktop
    /// keeps its top left corner there.</returns>
    public static Point Within(Rect desktop, Point wanted, Size panel) => new(
        Math.Clamp(wanted.X, desktop.Left, Math.Max(desktop.Left, desktop.Right - panel.Width)),
        Math.Clamp(wanted.Y, desktop.Top, Math.Max(desktop.Top, desktop.Bottom - panel.Height)));
}
