using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Logging;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MarstekOverlay.Desktop.Services;

public sealed class UserSettingsStore : IPipelineSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Names survive a reordering of an enum, and the file stays readable.
        Converters = { new JsonStringEnumConverter() },
    };

    // What the file called the one allowance for losses there was before charging got one of its own.
    private const string UsableEnergyPercent = "UsableEnergyPercent";

    // An antivirus or a backup holds a file it looks at for a moment.
    private const int ReadAttempts = 3;
    private static readonly TimeSpan BusyFilePause = TimeSpan.FromMilliseconds(200);

    private readonly string _filePath;
    private readonly ILogger<UserSettingsStore> _logger;

    // Set while the file on disk could not be read: it stays as it is until the user changes something, and is
    // then kept as a copy before anything is written over it.
    private bool _isFileUnread;
    private bool _isChanged;

    public UserSettingsStore(StorageSettings storage, ILogger<UserSettingsStore> logger)
    {
        _filePath = storage.UserSettingsPath;
        _logger = logger;
        Current = Load();
        Current.Repair(logger);
    }

    public event Action? Changed;

    public UserSettings Current { get; }

    /// <summary>Where the file that could not be read is kept before it is first written over.</summary>
    public string BackupPath => _filePath + ".bak";

    StationEndpoint? IPipelineSettings.Station => Current.Station.ToEndpoint();

    ForecastSettings IPipelineSettings.Forecast => Current.Forecast;

    AlertSettings IPipelineSettings.Alerts => Current.Alerts;

    /// <param name="persist">False for a change that is still being tried out, such as a slider being dragged.</param>
    public void Change(Action<UserSettings> change, bool persist = true)
    {
        change(Current);
        _isChanged = true;
        if (persist)
            Save();

        Changed?.Invoke();
    }

    public void Save()
    {
        if (_isFileUnread && !_isChanged)
        {
            _logger.LogInformation("Settings in {Path} were not read and nothing has changed since, left as they are", _filePath);
            return;
        }

        var temporaryPath = _filePath + ".tmp";
        try
        {
            if (Path.GetDirectoryName(_filePath) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            if (_isFileUnread)
                KeepUnreadFile();

            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, SerializerOptions));

            // A write cut short leaves the old file whole, never half of the new one.
            if (File.Exists(_filePath))
                File.Replace(temporaryPath, _filePath, destinationBackupFileName: null);
            else
                File.Move(temporaryPath, _filePath);

            _isFileUnread = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Settings were not saved to {Path}", _filePath);
            TryDelete(temporaryPath);
        }
    }

    private void KeepUnreadFile()
    {
        try
        {
            File.Copy(_filePath, BackupPath, overwrite: true);
            _logger.LogWarning("Settings in {Path} that could not be read are kept in {BackupPath}", _filePath, BackupPath);
        }
        catch (FileNotFoundException)
        {
            // Gone meanwhile: there is nothing left to keep.
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not delete {Path}", path);
        }
    }

    private UserSettings Load()
    {
        if (!File.Exists(_filePath))
            return new UserSettings();

        try
        {
            var options = new JsonSerializerOptions(SerializerOptions);
            options.Converters.Insert(0, new KnownEnumListConverter((type, entry) =>
                _logger.LogWarning("Settings in {Path} name {Entry}, which is no {Type} this version knows, left out", _filePath, entry, type)));

            if (Upgrade(JsonNode.Parse(ReadFile())).Deserialize<UserSettings>(options) is { } settings)
                return settings;

            _logger.LogWarning("Settings in {Path} hold nothing, starting with defaults", _filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(exception, "Settings in {Path} are unreadable, starting with defaults", _filePath);
        }

        _isFileUnread = true;
        return new UserSettings();
    }

    private string ReadFile()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return File.ReadAllText(_filePath);
            }
            catch (IOException exception) when (attempt < ReadAttempts && exception is not FileNotFoundException and not DirectoryNotFoundException)
            {
                _logger.LogDebug(exception, "Settings in {Path} are busy, reading again", _filePath);
                Thread.Sleep(BusyFilePause);
            }
        }
    }

    /// <summary>Brings a file saved by an earlier version to the names of this one. The allowance for losses
    /// that used to cover both directions stays with the discharge, which it was set up for.</summary>
    private static JsonNode? Upgrade(JsonNode? saved)
    {
        if (saved is JsonObject settings
            && settings[nameof(UserSettings.Forecast)] is JsonObject forecast
            && forecast[UsableEnergyPercent] is { } usable
            && !forecast.ContainsKey(nameof(ForecastSettings.DischargeEfficiencyPercent)))
        {
            forecast.Remove(UsableEnergyPercent);
            forecast[nameof(ForecastSettings.DischargeEfficiencyPercent)] = usable;
        }

        return saved;
    }
}
