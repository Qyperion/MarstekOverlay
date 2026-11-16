using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.TestSupport;

using System.Diagnostics.CodeAnalysis;

namespace MarstekOverlay.UnitTests.MonitoringTests;

/// <summary>Alerts as the pipeline reaches them: after the tracker and the journal have seen the snapshot.</summary>
public abstract class AlertMonitorTests
{
    protected const double WattHourPrecision = 0.001;

    protected static readonly TimeSpan SlowPollInterval = TimeSpan.FromSeconds(new PollingSettings().SlowIntervalSeconds);

    protected AlertMonitorTests()
    {
        Restart();
    }

    protected PollClock Time { get; } = new();

    protected InMemoryOutageStore Store { get; } = new();

    protected AlertSettings Settings { get; } = new();

    protected PipelineSteps Steps { get; private set; }

    protected DateTimeOffset Now => Time.Now;

    protected AlertReport Update(StationSnapshot snapshot)
    {
        var supply = Steps.Tracker.Update(snapshot);
        Steps.Journal.Record(supply, snapshot);

        return Steps.Alerts.Update(snapshot, Steps.Journal.LatestFinished, Settings, Now);
    }

    protected AlertReport UpdateAfterInterval(Func<DateTimeOffset, StationSnapshot> read) => Update(read(Time.NextPoll()));

    /// <summary>Has the station found on the battery, which takes two answers and is told of once.</summary>
    protected void BeginOnBattery(double soc)
    {
        Update(StationReadings.OnBattery(Now, soc: soc));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: soc));
    }

    /// <summary>Starts the app again over the same journal.</summary>
    [MemberNotNull(nameof(Steps))]
    protected void Restart(TimeSpan? pollInterval = null)
    {
        var polling = new PollingSettings { IntervalSeconds = (int)(pollInterval ?? PollClock.Interval).TotalSeconds };
        Steps = new PipelineSteps(Time, polling, Store);
    }
}
