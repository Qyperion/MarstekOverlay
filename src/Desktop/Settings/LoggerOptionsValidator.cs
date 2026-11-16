using Serilog.Events;

namespace MarstekOverlay.Desktop.Settings;

internal static class LoggerOptionsValidator
{
    internal static bool IsValid(LoggerOptions settings)
    {
        return settings is not null
            && Enum.TryParse<LogEventLevel>(settings.FileLogMinimumLevel, ignoreCase: false, out var level)
            && Enum.IsDefined(level)
            && (!settings.IsEnabledFileLog || !string.IsNullOrWhiteSpace(settings.LogFilePath));
    }
}
