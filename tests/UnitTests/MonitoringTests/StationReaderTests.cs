using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.TestSupport;
using MarstekOverlay.UnitTests.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class StationReaderTests
{
    private static readonly StationEndpoint Station = TestStation.Endpoint;

    private static readonly string[] FastMethods =
    [
        MarstekMethods.EnergySystemGetStatus,
        MarstekMethods.EnergySystemGetMode,
        MarstekMethods.EnergyMeterGetStatus,
        MarstekMethods.PvGetStatus,
    ];

    private static readonly string[] AllMethods =
    [
        MarstekMethods.GetDevice,
        .. FastMethods,
        MarstekMethods.BatteryGetStatus,
        MarstekMethods.WifiGetStatus,
        MarstekMethods.BluetoothGetStatus,
    ];

    private readonly FakeMarstekApiClient _client = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ReadAsync_FullScope_RequestsEverythingStartingWithIdentity()
    {
        // Act
        var snapshot = (await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None)).Snapshot;

        // Assert
        _client.Requests.Should().Equal(AllMethods);
        (snapshot.Device?.Value).Should().Be(_client.Device);
        snapshot.EnergySystem.Should().Be(new Reading<EnergySystemStatus>(_client.EnergySystem, _time.GetUtcNow()));
        snapshot.Battery.Should().NotBeNull();
        snapshot.Wifi.Should().NotBeNull();
        snapshot.Bluetooth.Should().NotBeNull();
        snapshot.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_FastScope_KeepsTheSlowReadingsOfThePreviousSnapshot()
    {
        // Arrange
        var reader = CreateReader();
        var full = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();
        _time.Advance(TimeSpan.FromSeconds(30));

        // Act
        var fast = (await reader.ReadAsync(Station, full, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        _client.Requests.Should().Equal(FastMethods);
        fast.Battery.Should().BeSameAs(full.Battery);
        fast.Device.Should().BeSameAs(full.Device);
        (fast.EnergySystem?.ReceivedAt).Should().Be(_time.GetUtcNow());
    }

    [Fact]
    public async Task ReadAsync_MethodNotFoundTwice_IsNeverRequestedAgain()
    {
        // Arrange
        _client.Failures[MarstekMethods.PvGetStatus] = MethodNotFound(MarstekMethods.PvGetStatus);
        var reader = CreateReader();

        // Act
        var first = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        var second = (await reader.ReadAsync(Station, first, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();
        var third = (await reader.ReadAsync(Station, second, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        first.Pv.Should().BeNull();
        first.Errors.Should().BeEmpty("a missing method is a property of the model, not a fault");
        first.UnsupportedMethods.Keys.Should().Equal(MarstekMethods.PvGetStatus);
        first.AbandonedMethods.Should().BeEmpty("one refusal is not the model's last word");
        second.Errors.Should().BeEmpty();
        second.AbandonedMethods.Should().Equal(MarstekMethods.PvGetStatus);
        third.UnsupportedMethods.Keys.Should().Equal(MarstekMethods.PvGetStatus);
        _client.Requests.Should().Equal(FastMethods.Except([MarstekMethods.PvGetStatus]));
    }

    [Fact]
    public async Task ReadAsync_MethodNotFound_IsDatedByItsLatestRefusal()
    {
        // Arrange
        _client.Failures[MarstekMethods.PvGetStatus] = MethodNotFound(MarstekMethods.PvGetStatus);
        var reader = CreateReader();
        var firstAt = _time.GetUtcNow();

        // Act
        var first = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _time.Advance(TimeSpan.FromSeconds(30));
        var second = (await reader.ReadAsync(Station, first, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        first.UnsupportedMethods[MarstekMethods.PvGetStatus].Should().Be(firstAt);
        second.UnsupportedMethods[MarstekMethods.PvGetStatus].Should().Be(_time.GetUtcNow());
    }

    [Fact]
    public async Task ReadAsync_AbandonedMethodsForgotten_AsksEachOnceMoreAndGivesItUpAgain()
    {
        // Arrange
        _client.Failures[MarstekMethods.PvGetStatus] = MethodNotFound(MarstekMethods.PvGetStatus);
        _client.Meter = new EnergyMeterStatus { IsCtConnected = false };
        var reader = CreateReader();
        var first = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        var abandoned = (await reader.ReadAsync(Station, first, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();
        _time.Advance(TimeSpan.FromHours(1));

        // Act
        var check = (await reader.ReadAsync(
            Station,
            abandoned with { AbandonedMethods = new HashSet<string>() },
            PollScope.Fast,
            CancellationToken.None)).Snapshot;
        var checkRequests = _client.Requests.ToArray();
        _client.Requests.Clear();
        await reader.ReadAsync(Station, check, PollScope.Fast, CancellationToken.None);

        // Assert
        checkRequests.Should().Equal(FastMethods);
        check.AbandonedMethods.Should().BeEquivalentTo([MarstekMethods.EnergyMeterGetStatus, MarstekMethods.PvGetStatus]);
        check.UnsupportedMethods[MarstekMethods.PvGetStatus].Should().Be(_time.GetUtcNow());
        (check.Meter?.ReceivedAt).Should().Be(_time.GetUtcNow());
        _client.Requests.Should().Equal(MarstekMethods.EnergySystemGetStatus, MarstekMethods.EnergySystemGetMode);
    }

    [Fact]
    public async Task ReadAsync_MeterWithoutACt_IsAskedOnceAndKeepsItsAnswer()
    {
        // Arrange
        _client.Meter = new EnergyMeterStatus { IsCtConnected = false };
        var reader = CreateReader();

        // Act
        var first = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();
        var second = (await reader.ReadAsync(Station, first, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        first.AbandonedMethods.Should().Equal(MarstekMethods.EnergyMeterGetStatus);
        first.Errors.Should().BeEmpty();
        _client.Requests.Should().Equal(FastMethods.Except([MarstekMethods.EnergyMeterGetStatus]));
        second.Meter.Should().BeSameAs(first.Meter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task ReadAsync_MeterThatDoesNotSayItLacksACt_IsAskedWithEveryPoll(bool? isCtConnected)
    {
        // Arrange
        _client.Meter = new EnergyMeterStatus { IsCtConnected = isCtConnected };
        var reader = CreateReader();
        var first = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();

        // Act
        await reader.ReadAsync(Station, first, PollScope.Fast, CancellationToken.None);

        // Assert
        first.AbandonedMethods.Should().BeEmpty();
        _client.Requests.Should().Equal(FastMethods);
    }

    [Fact]
    public async Task ReadAsync_MethodNotFoundOnce_IsRequestedAgainAndBelievedWhenItAnswers()
    {
        // Arrange
        _client.Failures[MarstekMethods.EnergySystemGetStatus] = MethodNotFound(MarstekMethods.EnergySystemGetStatus);
        var reader = CreateReader();
        var refused = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Failures.Clear();

        // Act
        var answered = (await reader.ReadAsync(Station, refused, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        refused.UnsupportedMethods.Keys.Should().Equal(MarstekMethods.EnergySystemGetStatus);
        answered.UnsupportedMethods.Should().BeEmpty();
        answered.EnergySystem.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadAsync_MethodNotFoundAfterAnAnswer_KeepsTheReadingAndTheRequest()
    {
        // Arrange
        var reader = CreateReader();
        var previous = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Failures[MarstekMethods.EnergySystemGetStatus] = MethodNotFound(MarstekMethods.EnergySystemGetStatus);

        // Act
        var refused = (await reader.ReadAsync(Station, previous, PollScope.Fast, CancellationToken.None)).Snapshot;
        var refusedAgain = (await reader.ReadAsync(Station, refused, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Requests.Clear();
        await reader.ReadAsync(Station, refusedAgain, PollScope.Fast, CancellationToken.None);

        // Assert
        refused.EnergySystem.Should().BeSameAs(previous.EnergySystem);
        refused.UnsupportedMethods.Should().BeEmpty();
        refused.Errors.Should().Equal(
            new RefusedRequest(MarstekMethods.EnergySystemGetStatus, MarstekRpcException.MethodNotFoundCode, "Method not found"));
        _client.Requests.Should().Equal(FastMethods, "a method that has answered is never given up");
    }

    [Fact]
    public async Task ReadAsync_OneRequestFails_KeepsItsPreviousReadingAndReportsTheError()
    {
        // Arrange
        var reader = CreateReader();
        var previous = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _time.Advance(TimeSpan.FromSeconds(30));
        _client.Failures[MarstekMethods.EnergySystemGetMode] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.EnergySystemGetMode, TimeSpan.FromSeconds(3));

        // Act
        var snapshot = (await reader.ReadAsync(Station, previous, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        snapshot.Mode.Should().BeSameAs(previous.Mode);
        (snapshot.Meter?.ReceivedAt).Should().Be(_time.GetUtcNow(), "the requests after the failed one still run");
        snapshot.Errors.Should().Equal(new UnansweredRequest(MarstekMethods.EnergySystemGetMode, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task ReadAsync_StationUnanswered_SkipsTheRestOfThePoll()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.GetDevice, TimeSpan.FromSeconds(3));
        _client.Failures[MarstekMethods.EnergySystemGetStatus] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.EnergySystemGetStatus, TimeSpan.FromSeconds(3));

        // Act
        var result = await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None);

        // Assert
        _client.Requests.Should().Equal(MarstekMethods.GetDevice, MarstekMethods.EnergySystemGetStatus);
        result.StationAnswered.Should().BeFalse();
        result.IdentityUnanswered.Should().BeFalse("a station that answers nothing is asked again with the next full poll");
        result.Snapshot.HasReadings.Should().BeFalse();
        result.Snapshot.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadAsync_FirstFastRequestUnanswered_SkipsTheRestOfThePoll()
    {
        // Arrange
        _client.Failures[MarstekMethods.EnergySystemGetStatus] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.EnergySystemGetStatus, TimeSpan.FromSeconds(3));

        // Act
        var result = await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None);

        // Assert
        _client.Requests.Should().Equal(MarstekMethods.EnergySystemGetStatus);
        result.StationAnswered.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_IdentityUnanswered_ReadsTheRestUnscaled()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.GetDevice, TimeSpan.FromSeconds(3));

        // Act
        var result = await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None);

        // Assert
        _client.Requests.Should().Equal(AllMethods);
        _client.Profiles.Should().OnlyContain(profile => profile == FirmwareProfile.Unknown);
        result.StationAnswered.Should().BeTrue();
        result.IdentityUnanswered.Should().BeTrue();
        result.Snapshot.Device.Should().BeNull();
        result.Snapshot.EnergySystem.Should().NotBeNull();
        result.Snapshot.Battery.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadAsync_FastScopeWithIdentity_AsksForItFirstAndDecodesTheRestByIt()
    {
        // Act
        var result = await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.FastWithIdentity, CancellationToken.None);

        // Assert
        _client.Requests.Should().Equal(FastMethods.Prepend(MarstekMethods.GetDevice));
        _client.Profiles.Should().OnlyContain(profile => profile == _client.Device.Profile);
        result.IdentityUnanswered.Should().BeFalse();
        (result.Snapshot.Device?.Value).Should().Be(_client.Device);
    }

    [Fact]
    public async Task ReadAsync_IdentityRefused_ReadsTheRest()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] = new MarstekRpcException(MarstekMethods.GetDevice, -32602, "Invalid params");

        // Act
        var result = await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None);

        // Assert
        _client.Requests.Should().Equal(AllMethods);
        result.StationAnswered.Should().BeTrue();
        result.IdentityUnanswered.Should().BeFalse("a refusal will not change on asking again");
        result.Snapshot.EnergySystem.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadAsync_ErrorsOfThePreviousPoll_AreNotCarriedOver()
    {
        // Arrange
        var reader = CreateReader();
        _client.Failures[MarstekMethods.EnergyMeterGetStatus] = new MarstekApiException("EM.GetStatus failed");
        var failed = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;
        _client.Failures.Clear();

        // Act
        var recovered = (await reader.ReadAsync(Station, failed, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        failed.Errors.Should().ContainSingle();
        recovered.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_RequestsThatFail_AreToldApartByWhatWentWrong()
    {
        // Arrange
        _client.Failures[MarstekMethods.EnergySystemGetMode] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.EnergySystemGetMode, TimeSpan.FromSeconds(3));
        _client.Failures[MarstekMethods.EnergyMeterGetStatus] =
            new MarstekRpcException(MarstekMethods.EnergyMeterGetStatus, -32602, "Invalid params");
        _client.Failures[MarstekMethods.PvGetStatus] = new MarstekApiException("PV.GetStatus to the station failed: no route");

        // Act
        var snapshot = (await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Fast, CancellationToken.None)).Snapshot;

        // Assert
        snapshot.Errors.Should().Equal(
            new UnansweredRequest(MarstekMethods.EnergySystemGetMode, TimeSpan.FromSeconds(3)),
            new RefusedRequest(MarstekMethods.EnergyMeterGetStatus, -32602, "Invalid params"),
            new UnexpectedFailure("PV.GetStatus to the station failed: no route"));
    }

    [Fact]
    public async Task ReadAsync_DisabledMethod_IsNeverSent()
    {
        // Arrange
        var reader = CreateReader(new PollingSettings { DisabledMethods = ["bat.getstatus"] });

        // Act
        var snapshot = (await reader.ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None)).Snapshot;

        // Assert
        _client.Requests.Should().NotContain(MarstekMethods.BatteryGetStatus);
        snapshot.Battery.Should().BeNull();
        snapshot.Wifi.Should().NotBeNull();
    }

    [Fact]
    public async Task ReadAsync_ReportsProgressAfterEveryAnswer()
    {
        // Arrange
        List<StationSnapshot> progress = [];

        // Act
        var snapshot = (await CreateReader().ReadAsync(Station, StationSnapshot.Empty, PollScope.Full, CancellationToken.None, progress.Add)).Snapshot;

        // Assert
        progress.Should().HaveCount(AllMethods.Length);
        progress[0].Device.Should().NotBeNull();
        progress[0].EnergySystem.Should().BeNull();
        progress[1].EnergySystem.Should().NotBeNull();
        progress[^1].Should().BeSameAs(snapshot);
    }

    private static MarstekRpcException MethodNotFound(string method) =>
        new(method, MarstekRpcException.MethodNotFoundCode, "Method not found");

    private StationReader CreateReader(PollingSettings? settings = null) =>
        new(_client, Options.Create(settings ?? new PollingSettings()), _time, NullLogger<StationReader>.Instance);
}
