using FluentAssertions;

using LiteDB;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.Storage;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace MarstekOverlay.UnitTests.StorageTests;

public class LiteDbOutageStoreTests
{
    private const string FileName = "history.db";

    private static readonly DateTimeOffset StartedAt = new(2026, 11, 11, 21, 5, 30, TimeSpan.FromHours(2));

    private static readonly Outage Ongoing = new()
    {
        StartedAt = StartedAt,
        IsStartObserved = true,
        EndedAt = StartedAt + TimeSpan.FromMinutes(40),
        IsOngoing = true,
        ConsumedEnergy = new Energy(170.5),
        StartSocPercent = 87,
        EndSocPercent = 83,
    };

    [Fact]
    public void Save_NewOutage_ReceivesAnIdAndSurvivesReopening()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        Outage saved;

        using (var database = Open(directory))
            saved = CreateStore(database).Save(Ongoing);

        // Act
        using var reopened = Open(directory);
        var found = CreateStore(reopened).FindOngoing();

        // Assert
        saved.Id.Should().BePositive();
        found.Should().Be(saved);
    }

    [Fact]
    public void Save_GridEnergyCounters_SurviveReopening()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var outage = Ongoing with { LatestGridEnergy = new GridEnergyCounters(new Energy(18027), new Energy(3.5)) };
        Outage saved;

        using (var database = Open(directory))
            saved = CreateStore(database).Save(outage);

        // Act
        using var reopened = Open(directory);
        var found = CreateStore(reopened).FindOngoing();

        // Assert
        found.Should().Be(saved);
        found.LatestGridEnergy.Should().Be(new GridEnergyCounters(new Energy(18027), new Energy(3.5)));
    }

    [Fact]
    public void FindOngoing_DocumentStoredBeforeGridEnergyWasKept_ReadsAsNoCounters()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var saved = store.Save(Ongoing with { LatestGridEnergy = new GridEnergyCounters(new Energy(18027), Output: null) });
        var outages = database.Collection<BsonDocument>("outages");
        var document = outages.FindById(saved.Id);
        document.Remove("GridInputWattHours");
        document.Remove("GridOutputWattHours");
        outages.Update(document);

        // Act
        var found = store.FindOngoing();

        // Assert
        found.Should().Be(Ongoing with { Id = saved.Id });
    }

    [Fact]
    public void Save_MomentsBetweenMilliseconds_ComeBackAtTheirMillisecond()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var betweenMilliseconds = TimeSpan.FromTicks(4321);

        // Act
        var saved = store.Save(Ongoing with { StartedAt = StartedAt + betweenMilliseconds, EndedAt = Ongoing.EndedAt + betweenMilliseconds });

        // Assert
        saved.Should().Be(Ongoing with { Id = saved.Id });
        store.FindOngoing().Should().Be(saved);
        store.FindSince(Ongoing.EndedAt + betweenMilliseconds).Should().Equal(saved);
    }

    [Fact]
    public void Save_OutageWithAnId_ReplacesWhatWasStored()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var saved = store.Save(Ongoing);

        // Act
        var finished = store.Save(saved with { IsOngoing = false, IsEndObserved = true, EndSocPercent = null });

        // Assert
        finished.Id.Should().Be(saved.Id);
        store.FindOngoing().Should().BeNull();
        store.FindLatestFinished().Should().Be(finished);
    }

    [Fact]
    public void FindLatestFinished_SeveralOutages_ReturnsTheOneThatBeganLast()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false };

        var latest = store.Save(finished with { StartedAt = StartedAt + TimeSpan.FromDays(2) });
        store.Save(finished);
        store.Save(finished with { StartedAt = StartedAt + TimeSpan.FromDays(1) });

        // Act & Assert
        store.FindLatestFinished().Should().Be(latest);
        store.FindOngoing().Should().BeNull();
    }

    [Fact]
    public void FindSince_ReturnsWhatWasStillGoingOnOrBeganLaterTheLatestFirst()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false, IsEndObserved = true };

        store.Save(finished with { StartedAt = StartedAt - TimeSpan.FromDays(3), EndedAt = StartedAt - TimeSpan.FromDays(2.9) });
        var straddling = store.Save(finished with { StartedAt = StartedAt - TimeSpan.FromHours(1), EndedAt = StartedAt + TimeSpan.FromHours(1) });
        var later = store.Save(finished with { StartedAt = StartedAt + TimeSpan.FromDays(1), EndedAt = StartedAt + TimeSpan.FromDays(1.1) });
        var ongoing = store.Save(Ongoing with { StartedAt = StartedAt + TimeSpan.FromDays(2), EndedAt = StartedAt + TimeSpan.FromDays(2.1) });

        // Act
        var found = store.FindSince(StartedAt);

        // Assert
        found.Should().Equal(ongoing, later, straddling);
    }

    [Fact]
    public void FindSince_NothingRecorded_ReturnsNothing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);

        // Act & Assert
        CreateStore(database).FindSince(StartedAt).Should().BeEmpty();
    }

    [Fact]
    public void FindBetween_ReturnsWhatHadAnyPartInThePeriodTheLatestFirst()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false, IsEndObserved = true };
        var from = StartedAt;
        var to = StartedAt + TimeSpan.FromDays(30);

        store.Save(finished with { StartedAt = from - TimeSpan.FromDays(3), EndedAt = from - TimeSpan.FromDays(2.9) });
        var acrossTheStart = store.Save(finished with { StartedAt = from - TimeSpan.FromHours(1), EndedAt = from + TimeSpan.FromHours(1) });
        var inside = store.Save(finished with { StartedAt = from + TimeSpan.FromDays(10), EndedAt = from + TimeSpan.FromDays(10.1) });
        var acrossTheEnd = store.Save(finished with { StartedAt = to - TimeSpan.FromHours(1), EndedAt = to + TimeSpan.FromHours(1) });
        store.Save(finished with { StartedAt = to, EndedAt = to + TimeSpan.FromHours(2) });
        store.Save(Ongoing with { StartedAt = to + TimeSpan.FromDays(2), EndedAt = to + TimeSpan.FromDays(2.1) });

        // Act
        var found = store.FindBetween(from, to);

        // Assert
        found.Should().Equal(acrossTheEnd, inside, acrossTheStart);
    }

    [Fact]
    public void FindBetween_OutageLongerThanThePeriod_IsFoundInIt()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var lasting = store.Save(Ongoing with { StartedAt = StartedAt - TimeSpan.FromDays(1), EndedAt = StartedAt + TimeSpan.FromDays(40) });

        // Act & Assert
        store.FindBetween(StartedAt, StartedAt + TimeSpan.FromDays(30)).Should().Equal(lasting);
    }

    [Fact]
    public void FindEarliest_SeveralOutages_ReturnsTheOneThatBeganFirst()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false };

        store.Save(finished with { StartedAt = StartedAt + TimeSpan.FromDays(2) });
        var earliest = store.Save(finished with { StartedAt = StartedAt - TimeSpan.FromDays(400) });
        store.Save(Ongoing with { StartedAt = StartedAt + TimeSpan.FromDays(3) });

        // Act & Assert
        store.FindEarliest().Should().Be(earliest);
    }

    [Fact]
    public void FindEarliest_NothingRecorded_ReturnsNothing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);

        // Act & Assert
        CreateStore(database).FindEarliest().Should().BeNull();
    }

    [Fact]
    public void DeleteFinishedBefore_RemovesTheOutagesThatEndedEarlierAndNoOngoingOne()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false, IsEndObserved = true };
        var moment = StartedAt;

        store.Save(finished with { StartedAt = moment - TimeSpan.FromDays(3), EndedAt = moment - TimeSpan.FromDays(2.9) });
        store.Save(finished with { StartedAt = moment - TimeSpan.FromDays(2), EndedAt = moment - TimeSpan.FromDays(1.9) });
        var acrossTheMoment = store.Save(finished with { StartedAt = moment - TimeSpan.FromHours(1), EndedAt = moment + TimeSpan.FromHours(1) });
        var leftOpenLongAgo = store.Save(Ongoing with { StartedAt = moment - TimeSpan.FromDays(5), EndedAt = moment - TimeSpan.FromDays(4.9) });

        // Act
        var deleted = store.DeleteFinishedBefore(moment);

        // Assert
        deleted.Should().Be(2);
        store.FindSince(DateTimeOffset.MinValue).Should().Equal(acrossTheMoment, leftOpenLongAgo);
    }

    [Fact]
    public void DeleteFinishedBefore_TheEndOfTime_LeavesOnlyTheOngoingOutage()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var database = Open(directory);
        var store = CreateStore(database);
        var finished = Ongoing with { IsOngoing = false, IsEndObserved = true };

        store.Save(finished with { StartedAt = StartedAt - TimeSpan.FromDays(3), EndedAt = StartedAt - TimeSpan.FromDays(2.9) });
        store.Save(finished with { StartedAt = StartedAt - TimeSpan.FromDays(1), EndedAt = StartedAt - TimeSpan.FromDays(0.9) });
        var ongoing = store.Save(Ongoing);

        // Act
        var deleted = store.DeleteFinishedBefore(DateTimeOffset.MaxValue);

        // Assert
        deleted.Should().Be(2);
        store.FindLatestFinished().Should().BeNull();
        store.FindSince(DateTimeOffset.MinValue).Should().Equal(ongoing);
    }

    [Fact]
    public void Open_FileHeldBySomethingElse_KeepsWorkingInMemory()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.GetFilePath(FileName);
        using var holder = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        // Act
        using var database = new HistoryDatabase(path, NullLogger<HistoryDatabase>.Instance);
        var store = CreateStore(database);
        var saved = store.Save(Ongoing);

        // Assert
        store.FindOngoing().Should().Be(saved);
    }

    [Fact]
    public void Open_ReadOnlyFile_KeepsWorkingInMemoryAndLogs()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = directory.GetFilePath(FileName);
        using (Open(directory))
        {
        }

        File.SetAttributes(path, FileAttributes.ReadOnly);
        var databaseLogger = new RecordingLogger<HistoryDatabase>();
        var storeLogger = new RecordingLogger<LiteDbOutageStore>();

        try
        {
            // Act
            using var database = new HistoryDatabase(path, databaseLogger);
            var store = new LiteDbOutageStore(database, storeLogger);
            var saved = store.Save(Ongoing);

            // Assert
            store.FindOngoing().Should().Be(saved);
            databaseLogger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error && entry.Exception is UnauthorizedAccessException);
            storeLogger.Entries.Should().BeEmpty();
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Create_DatabaseClosed_LogsRatherThanThrows()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var database = Open(directory);
        var logger = new RecordingLogger<LiteDbOutageStore>();
        database.Dispose();

        // Act
        var create = () => new LiteDbOutageStore(database, logger);

        // Assert
        create.Should().NotThrow();
        logger.Entries.Should().ContainSingle()
            .Which.Should().Match<LoggedEntry>(entry => entry.Level == LogLevel.Warning && (entry.Exception is LiteException || entry.Exception is ObjectDisposedException));
    }

    [Fact]
    public void EveryOperation_DatabaseClosed_AnswersWithNothingAndLogs()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var database = Open(directory);
        var logger = new RecordingLogger<LiteDbOutageStore>();
        var store = new LiteDbOutageStore(database, logger);
        database.Dispose();

        // Act
        var saved = store.Save(Ongoing);
        var ongoing = store.FindOngoing();
        var latestFinished = store.FindLatestFinished();
        var earliest = store.FindEarliest();
        var since = store.FindSince(StartedAt);
        var between = store.FindBetween(StartedAt, StartedAt + TimeSpan.FromDays(30));
        var deleted = store.DeleteFinishedBefore(StartedAt);

        // Assert
        saved.Should().Be(Ongoing);
        ongoing.Should().BeNull();
        latestFinished.Should().BeNull();
        earliest.Should().BeNull();
        since.Should().BeEmpty();
        between.Should().BeEmpty();
        deleted.Should().Be(0);
        logger.Entries.Should().HaveCount(7)
            .And.OnlyContain(entry => entry.Level == LogLevel.Warning && (entry.Exception is LiteException || entry.Exception is ObjectDisposedException));
        logger.Entries.Select(entry => entry.Message).Should().OnlyHaveUniqueItems();
    }

    private static HistoryDatabase Open(TemporaryDirectory directory) =>
        new(directory.GetFilePath(FileName), NullLogger<HistoryDatabase>.Instance);

    private static LiteDbOutageStore CreateStore(HistoryDatabase database) =>
        new(database, NullLogger<LiteDbOutageStore>.Instance);
}
