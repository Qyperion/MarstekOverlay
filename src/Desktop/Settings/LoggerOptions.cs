namespace MarstekOverlay.Desktop.Settings;

public record LoggerOptions
{
    public bool IsEnabledFileLog { get; set; } = true;

    /// <summary>"Debug" adds every datagram sent to and received from the station.</summary>
    public string FileLogMinimumLevel { get; set; } = "Information";

    public string LogFilePath { get; set; } = "diagnostics.log";
}
