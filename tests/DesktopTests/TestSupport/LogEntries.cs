using MarstekOverlay.Desktop.Settings;

using System.Globalization;

namespace MarstekOverlay.DesktopTests.TestSupport;

internal static class LogEntries
{
    /// <summary>A line of the log the way the app writes it: dated by the clock on the wall.</summary>
    public static string Written(DateTimeOffset at, string message) =>
        $"[{at.ToLocalTime().ToString(LogConstants.TimestampFormat, CultureInfo.InvariantCulture)} INF] {message}";
}
