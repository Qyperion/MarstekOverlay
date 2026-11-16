using System.Windows;

namespace MarstekOverlay.Desktop.Views;

/// <summary>Where a window that remembers its place opens. The place it was left at is kept while the desktop
/// still holds all of it; a place on a monitor that is no longer attached, or one a smaller screen cuts off,
/// would leave the window out of reach.</summary>
public static class WindowPlacement
{
    // Sizes add up with rounding errors of their own.
    private const double AreaPrecision = 0.5;

    /// <param name="workAreas">What each monitor has free of the taskbar; they never overlap.</param>
    /// <param name="wanted">Where the window was left.</param>
    /// <param name="smallest">The size the window does not go below.</param>
    /// <returns><paramref name="wanted"/> when the monitors show all of it. Otherwise the window goes whole onto
    /// the monitor that shows most of it, or the first one when none shows any, moved and, if the monitor is
    /// smaller, shrunk just enough to fit.</returns>
    public static Rect Within(IReadOnlyList<Rect> workAreas, Rect wanted, Size smallest)
    {
        var size = new Size(Math.Max(wanted.Width, smallest.Width), Math.Max(wanted.Height, smallest.Height));
        var placed = new Rect(wanted.Location, size);

        if (workAreas.Count == 0)
            return placed;

        var shown = workAreas.Sum(area => Shared(area, placed));
        if (shown >= placed.Width * placed.Height - AreaPrecision)
            return placed;

        var home = workAreas.MaxBy(area => Shared(area, placed));
        var width = Math.Min(placed.Width, home.Width);
        var height = Math.Min(placed.Height, home.Height);

        // For a window as large as the monitor, the far edge less the size can come out a hair short of the near
        // edge in floating point, and the range would be upside down.
        return new Rect(
            Math.Clamp(placed.Left, home.Left, Math.Max(home.Left, home.Right - width)),
            Math.Clamp(placed.Top, home.Top, Math.Max(home.Top, home.Bottom - height)),
            width,
            height);
    }

    private static double Shared(Rect area, Rect window)
    {
        var common = Rect.Intersect(area, window);

        return common.IsEmpty ? 0 : common.Width * common.Height;
    }
}
