using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.TestSupportTests;

public class InMemoryStoreTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 12, 21, 5, 30, 250, TimeSpan.FromHours(2));
    private static readonly TimeSpan BetweenMilliseconds = TimeSpan.FromTicks(4321);

    [Fact]
    public void OutageStore_MomentsBetweenMilliseconds_AreKeptAtTheirMillisecondLikeLiteDb()
    {
        // Arrange
        var store = new InMemoryOutageStore();
        var outage = new Outage
        {
            StartedAt = At,
            IsStartObserved = true,
            EndedAt = At + TimeSpan.FromMinutes(40),
            IsOngoing = true,
            ConsumedEnergy = new Energy(170.5),
        };

        // Act
        var saved = store.Save(outage with { StartedAt = outage.StartedAt + BetweenMilliseconds, EndedAt = outage.EndedAt + BetweenMilliseconds });

        // Assert
        saved.Should().Be(outage with { Id = saved.Id });
        store.FindOngoing().Should().Be(saved);
        store.FindSince(outage.EndedAt + BetweenMilliseconds).Should().Equal(saved);
        store.FindBetween(outage.EndedAt + BetweenMilliseconds, outage.EndedAt + TimeSpan.FromHours(1)).Should().Equal(saved);
    }

    [Fact]
    public void SampleStore_SampleTakenBetweenMilliseconds_IsKeptAtItsMillisecondLikeLiteDb()
    {
        // Arrange
        var store = new InMemorySampleStore();
        var sample = new StationSample(At, 100, Power.Zero, new Power(262), PowerSource.Mains);

        // Act
        store.Save(sample with { At = At + BetweenMilliseconds });

        // Assert
        store.FindSince(At).Should().Equal(sample);
        store.FindSince(At + BetweenMilliseconds).Should().Equal(sample);
        store.FindBetween(At + BetweenMilliseconds, At + BetweenMilliseconds).Should().Equal(sample);
    }
}
