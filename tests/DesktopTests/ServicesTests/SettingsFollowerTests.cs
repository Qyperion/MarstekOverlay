using FluentAssertions;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi;
using MarstekOverlay.TestSupport;

using System.Net;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class SettingsFollowerTests : LocalizedTests
{
    private const string OtherHost = "192.168.1.51";

    private static readonly StationEndpoint OtherStation = new(IPAddress.Parse(OtherHost), StationEndpoint.DefaultPort);

    private readonly ListeningApiClient _client = new();
    private readonly List<StationHarness> _stations = [];
    private readonly List<SettingsFollower> _followers = [];

    public override void Dispose()
    {
        foreach (var follower in _followers)
            follower.Dispose();

        foreach (var station in _stations)
            station.Dispose();

        base.Dispose();
    }

    [Fact]
    public async Task FollowStation_StationSet_WatchesIt()
    {
        // Arrange
        var (_, follower) = Follower();

        // Act
        follower.FollowStation();

        // Assert
        follower.Station.Should().Be(TestStation.Endpoint);
        await _client.AskedAsync(TestStation.Endpoint);
    }

    [Fact]
    public void FollowStation_NoStationSet_WatchesNone()
    {
        // Arrange
        var (_, follower) = Follower(isStationSet: false);

        // Act
        follower.FollowStation();

        // Assert
        follower.Station.Should().BeNull();
    }

    [Fact]
    public async Task FollowStation_AddressChanged_WatchesTheNewStation()
    {
        // Arrange
        var (station, follower) = Follower();
        follower.FollowStation();

        // Act
        station.Settings.Change(current => current.Station.Host = OtherHost);

        // Assert
        follower.Station.Should().Be(OtherStation);
        await _client.AskedAsync(OtherStation);
    }

    [Fact]
    public async Task FollowStation_StationSetLater_StartsWatching()
    {
        // Arrange
        var (station, follower) = Follower(isStationSet: false);
        follower.FollowStation();

        // Act
        station.Settings.Change(current => current.Station.Host = TestStation.Host);

        // Assert
        follower.Station.Should().Be(TestStation.Endpoint);
        await _client.AskedAsync(TestStation.Endpoint);
    }

    [Fact]
    public void FollowStation_AddressCleared_WatchesNone()
    {
        // Arrange
        var (station, follower) = Follower();
        follower.FollowStation();

        // Act
        station.Settings.Change(current => current.Station.Host = string.Empty);

        // Assert
        follower.Station.Should().BeNull();
    }

    [Fact]
    public void FollowStation_SettingsChangedAfterDisposal_AreNotFollowed()
    {
        // Arrange
        var (station, follower) = Follower();
        follower.FollowStation();

        // Act
        follower.Dispose();
        station.Settings.Change(current => current.Station.Host = OtherHost);

        // Assert
        follower.Station.Should().Be(TestStation.Endpoint);
    }

    [Fact]
    public void FollowLanguage_LanguageInSettings_IsSpokenAtOnce()
    {
        // Arrange
        var (station, follower) = Follower();
        station.Settings.Change(current => current.Language = Ukrainian);

        // Act
        follower.FollowLanguage();

        // Assert
        Localizer.Instance.Language.Should().Be(AppLanguages.Ukrainian);
    }

    [Fact]
    public void FollowLanguage_LanguageChangedLater_IsSpoken()
    {
        // Arrange
        var (station, follower) = Follower();
        follower.FollowLanguage();

        // Act
        station.Settings.Change(current => current.Language = Ukrainian);

        // Assert
        Localizer.Instance.Language.Should().Be(AppLanguages.Ukrainian);
    }

    private (StationHarness Station, SettingsFollower Follower) Follower(bool isStationSet = true)
    {
        var station = new StationHarness(Moments.Local(14, 12, 0), isStationSet: isStationSet, station: _client);
        var follower = new SettingsFollower(station.Settings, station.Monitor, Localizer.Instance);
        _stations.Add(station);
        _followers.Add(follower);

        return (station, follower);
    }
}
