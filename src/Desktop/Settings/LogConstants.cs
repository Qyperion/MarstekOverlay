namespace MarstekOverlay.Desktop.Settings;

public static class LogConstants
{
    /// <summary>Local time. An entry is dated by reading this back, so it stays at the start of the line.</summary>
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    public const string OutputTemplate = "[{Timestamp:" + TimestampFormat + "} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    public const long FileSizeLimitBytes = 5_000_000;

    public const int RetainedFileCountLimit = 3;
}
