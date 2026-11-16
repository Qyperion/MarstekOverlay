using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
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

public class StationMonitorTests
{
    private static readonly StationEndpoint Station = TestStation.Endpoint;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly FakeMarstekApiClient _client = new();
    private readonly FakeTimeProvider _time = new();

    // Released once per poll: every poll, fast or full, asks for the energy system status exactly once.
    private readonly SemaphoreSlim _polls = new(0);

    public StationMonitorTests()
    {
        _client.Requested += method =>
        {
            if (method == MarstekMethods.EnergySystemGetStatus)
                _polls.Release();
        };
    }

    [Fact]
    public async Task Start_PollsEverythingFirstThenOnlyTheFastReadingsOnEveryInterval()
    {
        // Arrange
        using var monitor = CreateMonitor();

        // Act
        monitor.Start(Station);
        await WaitForPollAsync();
        await WaitForPollAsync(advanceTime: true);
        monitor.Stop();

        // Assert
        _client.Requests.Count(method => method == MarstekMethods.GetDevice).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.EnergySystemGetStatus).Should().Be(2);
    }

    [Fact]
    public async Task Start_PublishesTheSnapshot()
    {
        // Arrange
        using var monitor = CreateMonitor();
        var published = new TaskCompletionSource<StationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotChanged += snapshot =>
        {
            if (snapshot.Bluetooth is not null)
                published.TrySetResult(snapshot);
        };

        // Act
        monitor.Start(Station);
        var snapshot = await published.Task.WaitAsync(TestTimeout);

        // Assert
        (snapshot.EnergySystem?.Value).Should().Be(_client.EnergySystem);
        (snapshot.Device?.Value).Should().Be(_client.Device);
    }

    [Fact]
    public async Task RequestRefresh_StartsTheNextPollWithoutWaitingForTheInterval()
    {
        // Arrange
        using var monitor = CreateMonitor();
        monitor.Start(Station);
        await WaitForPollAsync();

        // Act
        var refreshed = false;
        for (var attempt = 0; attempt < 100 && !refreshed; attempt++)
        {
            // Repeated because a request made while the first poll is still finishing is served by that poll.
            monitor.RequestRefresh();
            refreshed = await _polls.WaitAsync(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        refreshed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAbandoned_AsksAtOnceAndOnlyOnceMoreForWhatWasGivenUp()
    {
        // Arrange
        _client.Meter = new EnergyMeterStatus { IsCtConnected = false };
        var meterRequests = 0;
        var askedAgain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.Requested += method =>
        {
            if (method == MarstekMethods.EnergyMeterGetStatus && ++meterRequests == 2)
                askedAgain.TrySetResult();
        };
        using var monitor = CreateMonitor();
        monitor.Start(Station);
        await WaitForPollAsync();
        await WaitForPollAsync(advanceTime: true);
        var requestsBeforeTheCheck = RequestsOf(MarstekMethods.EnergyMeterGetStatus);

        // Act
        monitor.CheckAbandoned();

        // No time is advanced: the poll that asks again starts without waiting for the interval.
        await askedAgain.Task.WaitAsync(TestTimeout);

        // Two more polls begun prove that the one after the check has ended.
        var pollsByThen = RequestsOf(MarstekMethods.EnergySystemGetStatus);
        while (RequestsOf(MarstekMethods.EnergySystemGetStatus) < pollsByThen + 2)
            await WaitForPollAsync(advanceTime: true);
        monitor.Stop();

        // Assert
        requestsBeforeTheCheck.Should().Be(1);
        RequestsOf(MarstekMethods.EnergyMeterGetStatus).Should().Be(2);
    }

    [Fact]
    public async Task Stop_EndsPolling()
    {
        // Arrange
        using var monitor = CreateMonitor();
        monitor.Start(Station);
        await WaitForPollAsync();

        // Act
        monitor.Stop();
        _time.Advance(Interval * 3);
        var polledAgain = await _polls.WaitAsync(TimeSpan.FromMilliseconds(300));

        // Assert
        polledAgain.Should().BeFalse();
    }

    [Fact]
    public async Task Start_IdentityDisabled_ReadsTheSlowRequestsOnlyOnTheSlowBeat()
    {
        // Arrange
        using var monitor = CreateMonitor(disabledMethods: [MarstekMethods.GetDevice]);

        // Act
        await PollThreeTimesAsync(monitor);

        // Assert
        _client.Requests.Should().NotContain(MarstekMethods.GetDevice);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.EnergySystemGetStatus).Should().Be(3);
    }

    [Fact]
    public async Task Start_IdentityUnknownToTheStation_ReadsTheSlowRequestsOnlyOnTheSlowBeat()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRpcException(MarstekMethods.GetDevice, MarstekRpcException.MethodNotFoundCode, "Method not found");
        using var monitor = CreateMonitor();

        // Act
        await PollThreeTimesAsync(monitor);

        // Assert
        _client.Requests.Count(method => method == MarstekMethods.GetDevice).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.EnergySystemGetStatus).Should().Be(3);
    }

    [Fact]
    public async Task Start_IdentityNeverAnswered_AsksForItOnceMoreAndKeepsReadingTheFastRequests()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.GetDevice, TimeSpan.FromSeconds(3));
        using var monitor = CreateMonitor();

        // Act
        await PollFourTimesAsync(monitor);

        // Assert
        PollsOf(MarstekMethods.GetDevice).Should().Equal(1, 2);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
        _client.Requests.Count(method => method == MarstekMethods.EnergyMeterGetStatus).Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Start_IdentityUnansweredOnce_IsAskedForWithTheNextPollOnly()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.GetDevice, TimeSpan.FromSeconds(3));
        var identityRequests = 0;
        _client.Requested += method =>
        {
            // Raised before the answer is looked up, so the retry is answered.
            if (method == MarstekMethods.GetDevice && ++identityRequests == 2)
                _client.Failures.Clear();
        };
        using var monitor = CreateMonitor();
        var identified = new TaskCompletionSource<StationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotChanged += snapshot =>
        {
            if (snapshot.Device is not null && snapshot.EnergySystem is not null)
                identified.TrySetResult(snapshot);
        };

        // Act
        await PollFourTimesAsync(monitor);

        // Assert
        PollsOf(MarstekMethods.GetDevice).Should().Equal(1, 2);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
        var snapshot = await identified.Task.WaitAsync(TestTimeout);
        (snapshot.Device?.Value).Should().Be(_client.Device);
    }

    [Fact]
    public async Task Start_IdentityRefused_IsNotAskedForAgainBeforeTheSlowBeat()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] = new MarstekRpcException(MarstekMethods.GetDevice, -32602, "Invalid params");
        using var monitor = CreateMonitor();

        // Act
        await PollFourTimesAsync(monitor);

        // Assert
        PollsOf(MarstekMethods.GetDevice).Should().Equal(1);
    }

    [Fact]
    public async Task Start_FullPollUnanswered_IsRepeatedOnTheNextPoll()
    {
        // Arrange
        _client.Failures[MarstekMethods.GetDevice] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.GetDevice, TimeSpan.FromSeconds(3));
        _client.Failures[MarstekMethods.EnergySystemGetStatus] =
            new MarstekRequestTimeoutException(Station, MarstekMethods.EnergySystemGetStatus, TimeSpan.FromSeconds(3));
        var identityRequests = 0;
        _client.Requested += method =>
        {
            // Raised before the answer is looked up, so the station answers from the second poll on.
            if (method == MarstekMethods.GetDevice && ++identityRequests == 2)
                _client.Failures.Clear();
        };
        using var monitor = CreateMonitor();

        // Act
        await PollThreeTimesAsync(monitor);

        // Assert
        _client.Requests.Count(method => method == MarstekMethods.GetDevice).Should().Be(2);
        _client.Requests.Count(method => method == MarstekMethods.BatteryGetStatus).Should().Be(1);
    }

    [Fact]
    public async Task Start_UnexpectedFailureMidPoll_KeepsTheAnswersOfThatPoll()
    {
        // Arrange
        _client.Failures[MarstekMethods.EnergyMeterGetStatus] = new InvalidOperationException("Broken answer");
        using var monitor = CreateMonitor();
        var failed = new TaskCompletionSource<StationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.SnapshotChanged += snapshot =>
        {
            if (snapshot.Errors.OfType<UnexpectedFailure>().Any())
                failed.TrySetResult(snapshot);
        };

        // Act
        monitor.Start(Station);
        var snapshot = await failed.Task.WaitAsync(TestTimeout);

        // Assert
        snapshot.Device.Should().NotBeNull();
        snapshot.EnergySystem.Should().NotBeNull();
        snapshot.Mode.Should().NotBeNull();
        snapshot.Errors.Should().Equal(new UnexpectedFailure("Broken answer"));
    }

    [Fact]
    public async Task Start_SubscriberThrows_KeepsPolling()
    {
        // Arrange
        using var monitor = CreateMonitor();
        monitor.SnapshotChanged += _ => throw new InvalidOperationException("Broken subscriber");

        // Act
        monitor.Start(Station);
        var polledTwice = async () =>
        {
            // The first poll breaks off on the answer to its first request, before the energy system status.
            await WaitForPollAsync(advanceTime: true);
            await WaitForPollAsync(advanceTime: true);
        };

        // Assert
        await polledTwice.Should().NotThrowAsync();
        monitor.Stop();
    }

    private StationMonitor CreateMonitor(List<string>? disabledMethods = null)
    {
        var options = Options.Create(new PollingSettings
        {
            IntervalSeconds = (int)Interval.TotalSeconds,
            SlowIntervalSeconds = (int)Interval.TotalSeconds * 10,
            DisabledMethods = disabledMethods ?? [],
        });
        var reader = new StationReader(_client, options, _time, NullLogger<StationReader>.Instance);

        return new StationMonitor(reader, options, _time, NullLogger<StationMonitor>.Instance);
    }

    // A poll is seen when it asks for the energy system status, so only the third proves the second has ended.
    private async Task PollThreeTimesAsync(StationMonitor monitor)
    {
        monitor.Start(Station);
        await WaitForPollAsync();
        await WaitForPollAsync(advanceTime: true);
        await WaitForPollAsync(advanceTime: true);
        monitor.Stop();
    }

    private async Task PollFourTimesAsync(StationMonitor monitor)
    {
        monitor.Start(Station);
        await WaitForPollAsync();
        await WaitForPollAsync(advanceTime: true);
        await WaitForPollAsync(advanceTime: true);
        await WaitForPollAsync(advanceTime: true);
        monitor.Stop();
    }

    private int RequestsOf(string method) => _client.Requests.Count(request => request == method);

    /// <returns>The numbers of the polls, counted from 1, that sent the method.</returns>
    private List<int> PollsOf(string method)
    {
        List<int> polls = [];
        var poll = 0;
        string? previous = null;
        foreach (var request in _client.Requests)
        {
            // Every poll opens with the identity or, without it, with the energy system status.
            if (request == MarstekMethods.GetDevice
                || (request == MarstekMethods.EnergySystemGetStatus && previous != MarstekMethods.GetDevice))
                poll++;

            if (request == method)
                polls.Add(poll);

            previous = request;
        }

        return polls;
    }

    private async Task WaitForPollAsync(bool advanceTime = false)
    {
        var deadline = DateTime.UtcNow + TestTimeout;
        while (!await _polls.WaitAsync(TimeSpan.FromMilliseconds(50)))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The monitor did not poll in time.");

            // Advanced in a loop: the monitor may not have started waiting for the interval yet.
            if (advanceTime)
                _time.Advance(Interval);
        }
    }
}
