using FluentAssertions;

using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Options;

using Serilog;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class DiagnosticLogTests : IDisposable
{
    private const string FileName = "diagnostics.log";
    private const string RolledOverFileName = "diagnostics_001.log";

    private static readonly DateTimeOffset Now = Moments.Local(14, 12, 0);
    private static readonly DateTimeOffset DayAgo = Now - TimeSpan.FromDays(1);

    private readonly TemporaryDirectory _directory = new();
    private readonly DiagnosticLog _log;

    public DiagnosticLogTests()
    {
        _log = new DiagnosticLog(new StorageSettings { RootDirectory = _directory.Path }, Options.Create(new LoggerOptions()));
    }

    public void Dispose()
    {
        _log.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public void DeleteBefore_EntriesOnBothSidesOfTheMoment_KeepsTheLaterOnesWithTheLinesOfTheirExceptions()
    {
        // Arrange
        string[] kept =
        [
            LogEntries.Written(DayAgo, "Written at the very moment"),
            LogEntries.Written(Now - TimeSpan.FromHours(1), "The outage journal could not be read"),
            "System.IO.IOException: The process cannot access the file.",
            "   at MarstekOverlay.Storage.LiteDbOutageStore.FindEarliest()",
            LogEntries.Written(Now, "Alert raised"),
        ];

        Write(
            FileName,
            [
                LogEntries.Written(Now - TimeSpan.FromDays(3), "Settings were not saved"),
                "System.UnauthorizedAccessException: Access is denied.",
                LogEntries.Written(DayAgo - TimeSpan.FromMilliseconds(1), "Written a moment too early"),
                .. kept,
            ]);

        // Act
        var cleanup = _log.DeleteBefore(DayAgo);

        // Assert
        cleanup.Should().Be(new LogCleanup(Failure: null, IsPartial: false));
        Read(FileName).Should().Equal(kept);
        Directory.EnumerateFiles(_directory.Path).Select(Path.GetFileName).Should().Equal(FileName);
    }

    [Fact]
    public void DeleteBefore_FileWithNothingLater_IsDeletedAndALaterOneLeftAsItWas()
    {
        // Arrange
        string[] later = [LogEntries.Written(Now, "Alert raised")];
        Write(FileName, [LogEntries.Written(Now - TimeSpan.FromDays(3), "Alert raised"), "System.TimeoutException: No answer."]);
        Write(RolledOverFileName, later);

        // Act
        _log.DeleteBefore(DayAgo);

        // Assert
        Exists(FileName).Should().BeFalse();
        Read(RolledOverFileName).Should().Equal(later);
    }

    [Fact]
    public void DeleteBefore_TheEndOfTime_DeletesEveryFileOfTheLogAndNoOther()
    {
        // Arrange
        string[] entries = [LogEntries.Written(Now, "Alert raised")];
        string[] others = ["settings.json", "diagnostics.log.bak", "diagnostics-notes.log", "diagnostics_1.log"];

        Write(FileName, entries);
        Write(RolledOverFileName, entries);
        Write("diagnostics_1002.log", entries);
        foreach (var other in others)
            Write(other, entries);

        // Act
        _log.DeleteBefore(DateTimeOffset.MaxValue);

        // Assert
        Directory.EnumerateFiles(_directory.Path).Select(Path.GetFileName).Should().BeEquivalentTo(others);
    }

    [Fact]
    public void DeleteBefore_NoLogYet_DoesNothing()
    {
        // Act
        var delete = () => _log.DeleteBefore(DateTimeOffset.MaxValue);

        // Assert
        delete.Should().NotThrow();
    }

    [Fact]
    public void Emit_AfterADeletion_GoesOnWritingToTheSameFile()
    {
        // Arrange
        using var logger = new LoggerConfiguration().WriteTo.Sink(_log).CreateLogger();
        logger.Information("Written before the deletion");

        // Act
        _log.DeleteBefore(DateTimeOffset.MaxValue);
        logger.Information("Written after the deletion");
        _log.Dispose();

        // Assert
        Read(FileName).Should().ContainSingle().Which.Should().EndWith("INF] Written after the deletion");
    }

    [Fact]
    public void DeleteBefore_FileHeldBySomethingElse_TellsWhyAndGoesOnWritingOnceItIsFree()
    {
        // Arrange
        using var logger = new LoggerConfiguration().WriteTo.Sink(_log).CreateLogger();
        Write(RolledOverFileName, [LogEntries.Written(Now - TimeSpan.FromDays(3), "Alert raised")]);

        // Act & Assert
        using (File.Open(PathOf(RolledOverFileName), FileMode.Open, FileAccess.Read, FileShare.None))
            _log.DeleteBefore(DayAgo).Should().Match<LogCleanup>(cleanup => cleanup.Failure != null && !cleanup.IsPartial);

        logger.Information("Written after the failure");
        _log.Dispose();
        Read(RolledOverFileName).Should().HaveCount(2).And.Contain(line => line.EndsWith("Written after the failure"));
    }

    [Fact]
    public void DeleteBefore_OneOfTwoFilesHeld_ClearsTheOtherAndLeavesTheHeldOneWhole()
    {
        // Arrange
        string[] held = [LogEntries.Written(Now - TimeSpan.FromDays(3), "Alert raised"), LogEntries.Written(Now, "Alert raised")];
        string[] later = [LogEntries.Written(Now, "Settings saved")];
        Write(FileName, [LogEntries.Written(Now - TimeSpan.FromDays(3), "Settings saved"), .. later]);
        Write(RolledOverFileName, held);

        // Act
        LogCleanup cleanup;
        using (File.Open(PathOf(RolledOverFileName), FileMode.Open, FileAccess.Read, FileShare.None))
            cleanup = _log.DeleteBefore(DayAgo);

        // Assert
        cleanup.Failure.Should().NotBeNull();
        cleanup.IsPartial.Should().BeTrue();
        Read(FileName).Should().Equal(later);
        Read(RolledOverFileName).Should().Equal(held);
    }

    private string PathOf(string fileName) => Path.Combine(_directory.Path, fileName);

    private void Write(string fileName, string[] lines) => File.WriteAllLines(PathOf(fileName), lines);

    private string[] Read(string fileName) => File.ReadAllLines(PathOf(fileName));

    private bool Exists(string fileName) => File.Exists(PathOf(fileName));
}
