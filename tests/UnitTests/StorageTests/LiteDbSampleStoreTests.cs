using FluentAssertions;

using LiteDB;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.Storage;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace MarstekOverlay.UnitTests.StorageTests;

public class LiteDbSampleStoreTests
{
    private const string FileName = "history.db";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset TakenAt = new(2026, 11, 12, 21, 5, 30, 250, TimeSpan.FromHours(2));

    private static readonly StationSample OnMains = new(TakenAt, 100, Power.Zero, new Power(262), PowerSource.Mains);

    [Fact]
    public void Save_Samples_SurviveReopening()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var onBattery = new StationSample(TakenAt + PollInterval, 99, new Power(-280.5), new Power(280.5), PowerSource.Battery);
        var withoutReadings = new StationSample(TakenAt + PollInterval * 2, null, null, null, PowerSource.Unknown);

        using (var database = Open(directory))
        {
            var store = CreateStore(database);
            store.Save(OnMains);
            store.Save(onBattery);
            store.Save(withoutReadings);
        }

        // Act
        using var reopened = Open(directory);
        var found = CreateStore(reopened).FindSince(TakenAt);

        // Assert
        found.Should().Equal(OnMains, onBattery, withoutReadings);
    }

    [Fact]
    public void Save_SampleTakenBetweenMilliseconds_ComesBackAtItsMillisecond()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var takenAt = TakenAt + TimeSpan.FromTicks(4321);

        // Act
        store.Save(OnMains with { At = takenAt });

        // Assert
        store.FindSince(TakenAt).Should().Equal(OnMains);
        store.FindSince(takenAt).Should().Equal(OnMains);
    }

    [Fact]
    public void FindSince_ReturnsTheSamplesFromThatMomentOnOldestFirst()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var samples = Enumerable.Range(0, 6).Select(poll => OnMains with { At = TakenAt + PollInterval * poll }).ToList();

        foreach (var sample in samples.AsEnumerable().Reverse())
            store.Save(sample);

        // Act
        var found = store.FindSince(samples[2].At);

        // Assert
        found.Should().Equal(samples.Skip(2));
    }

    [Fact]
    public void Save_SampleTakenAtTheMomentOfAStoredOne_ReplacesIt()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        store.Save(OnMains);
        var corrected = OnMains with { BatteryPower = new Power(-262), Source = PowerSource.Battery };

        // Act
        store.Save(corrected);

        // Assert
        store.FindSince(TakenAt - PollInterval).Should().Equal(corrected);
    }

    [Fact]
    public void DeleteBefore_RemovesOnlyTheSamplesTakenEarlier()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var samples = Enumerable.Range(0, 6).Select(poll => OnMains with { At = TakenAt + PollInterval * poll }).ToList();

        foreach (var sample in samples)
            store.Save(sample);

        // Act
        var deleted = store.DeleteBefore(samples[4].At);

        // Assert
        deleted.Should().Be(4);
        store.FindSince(TakenAt - PollInterval).Should().Equal(samples.Skip(4));
    }

    [Fact]
    public void DeleteBefore_TheEndOfTime_RemovesEverySample()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);

        for (var poll = 0; poll < 6; poll++)
            store.Save(OnMains with { At = TakenAt + PollInterval * poll });

        // Act
        var deleted = store.DeleteBefore(DateTimeOffset.MaxValue);

        // Assert
        deleted.Should().Be(6);
        store.FindSince(DateTimeOffset.MinValue).Should().BeEmpty();
    }

    [Fact]
    public void EveryOperation_DatabaseClosed_AnswersWithNothingAndLogs()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var database = Open(directory);
        var logger = new RecordingLogger<LiteDbSampleStore>();
        var store = new LiteDbSampleStore(database, logger);
        database.Dispose();

        // Act
        var save = () => store.Save(OnMains);
        var found = store.FindSince(TakenAt);
        var deleted = store.DeleteBefore(TakenAt);

        // Assert
        save.Should().NotThrow();
        found.Should().BeEmpty();
        deleted.Should().Be(0);
        logger.Entries.Should().HaveCount(3)
            .And.OnlyContain(entry => entry.Level == LogLevel.Warning && (entry.Exception is LiteException || entry.Exception is ObjectDisposedException));
    }

    private static HistoryDatabase Open(TemporaryDirectory directory) =>
        new(directory.GetFilePath(FileName), NullLogger<HistoryDatabase>.Instance);

    private static LiteDbSampleStore CreateStore(HistoryDatabase database) =>
        new(database, NullLogger<LiteDbSampleStore>.Instance);
}
