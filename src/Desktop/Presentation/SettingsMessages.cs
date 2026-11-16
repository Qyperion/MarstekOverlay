using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>What the settings window says on its status line, and how it names a station found on the network.</summary>
public static class SettingsMessages
{
    public static StatusMessage ReadOnlyNote => Info(Strings.Settings_ReadOnlyNote);

    public static StatusMessage FixMarked => Error(Strings.Settings_FixMarked);

    public static StatusMessage UnsavedChanges => Error(Strings.Settings_UnsavedChanges);

    public static StatusMessage AutostartFailed(Exception failure) =>
        Error(Phrases.Format(Strings.Settings_AutostartFailed, FailureReasons.OfRegistry(failure)));

    public static StatusMessage Searching(int port) => Info(Phrases.Format(Strings.Settings_Searching, port));

    public static StatusMessage Found(int count) =>
        count == 0 ? Error(Strings.Settings_NoStationFound) : Info(Phrases.Format(Strings.Settings_StationsFound, count));

    public static StatusMessage SearchFailed(string reason) => Error(Phrases.Format(Strings.Settings_SearchFailed, reason));

    public static StatusMessage AddressFilledIn(DeviceInfo device) => Info(Phrases.Format(Strings.Settings_AddressFilledIn, device.Model));

    public static StatusMessage Reading(StationEndpoint station) => Info(Phrases.Format(Strings.Settings_Reading, station));

    /// <param name="logPath">Where the log is written; null when it is switched off.</param>
    public static StatusMessage Read(StationEndpoint station, StationSnapshot snapshot, string? logPath) =>
        snapshot.HasReadings ? Info(Phrases.Format(Strings.Settings_Answered, station))
        : logPath is null ? Error(Phrases.Format(Strings.Settings_NoAnswerWithoutLog, station))
        : Error(Phrases.Format(Strings.Settings_NoAnswer, station, logPath));

    public static string Device(DiscoveredDevice found) =>
        found.Device.FirmwareVersion is { } version
            ? Phrases.Format(Strings.Settings_FoundStation, found.Device.Model, found.Endpoint, version)
            : Phrases.Format(Strings.Settings_FoundStationWithoutVersion, found.Device.Model, found.Endpoint);

    private static StatusMessage Info(string text) => new(text, IsError: false);

    private static StatusMessage Error(string text) => new(text, IsError: true);
}
