using System.IO;

namespace MarstekOverlay.Desktop.Settings;

public static class StorageSettingsValidator
{
    public static bool IsValid(StorageSettings settings)
    {
        return settings is not null
            && IsFileName(settings.UserSettingsFileName)
            && IsFileName(settings.DatabaseFileName)
            && !string.Equals(settings.UserSettingsPath, settings.DatabasePath, StringComparison.OrdinalIgnoreCase);
    }

    // Not a folder: a path that ends in a separator, or names nothing at all, would be resolved to the data folder itself.
    private static bool IsFileName(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.IndexOfAny(Path.GetInvalidPathChars()) < 0
        && !string.IsNullOrWhiteSpace(Path.GetFileName(path));
}
