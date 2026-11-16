using System.IO;

namespace MarstekOverlay.Desktop.Settings;

public class StorageSettings
{
    /// <summary>A relative path is taken from the folder of the exe: the working directory of a process started
    /// by a shortcut or at sign-in can be anything.</summary>
    public string RootDirectory
    {
        get => string.IsNullOrWhiteSpace(field)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarstekOverlay")
            : Path.GetFullPath(field, ExeFolder.Location);
        set;
    }

    public string UserSettingsFileName { get; set; } = "settings.json";

    /// <summary>Everything recorded about the station over time: the outage journal and the samples.</summary>
    public string DatabaseFileName { get; set; } = "history.db";

    public string UserSettingsPath => Resolve(UserSettingsFileName);

    public string DatabasePath => Resolve(DatabaseFileName);

    public string Resolve(string path) => Path.Combine(RootDirectory, path);
}
