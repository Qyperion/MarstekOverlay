using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterOutageRowsTests : OverlayPresenterTests
{
    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OutageGoingOn_TellsHowLongAndHowMuch(string language)
    {
        // Arrange
        Speak(language);
        var ongoing = OutageRecords.Finished(Now - TimeSpan.FromMinutes(135), TimeSpan.FromMinutes(135)) with
        {
            EndedAt = AnsweredAt,
            IsOngoing = true,
        };
        var state = StationReadings.OnBattery(AnsweredAt, soc: 91).With(Supplies.OnBattery(ongoing.StartedAt), ongoingOutage: ongoing);

        // Act
        var row = RowOf(OverlayRowKind.CurrentOutage, state);

        // Assert
        row.Value.Should().Be(Translated("2 h 15 min · 1.56 kWh"));
        row.Hint.Should().Be(Translated("On battery since 20:27. Charge 99% → 91%."));
    }

    [Fact]
    public void Present_OutageFoundAlreadyGoingOn_SaysAtLeast()
    {
        // Arrange
        var ongoing = OutageRecords.Finished(Now - TimeSpan.FromMinutes(135), TimeSpan.FromMinutes(135), startSoc: 100) with
        {
            EndedAt = AnsweredAt,
            IsOngoing = true,
            IsStartObserved = false,
        };

        // Act
        var row = RowOf(OverlayRowKind.CurrentOutage, StationReadings.OnBattery(AnsweredAt).With(ongoingOutage: ongoing));

        // Assert
        row.Value.Should().Be("at least 2 h 15 min · 1.56 kWh");
        row.Hint.Should().Be("On battery since 20:27. It had begun before the app first saw it. Charge 100% → 91%.");
    }

    [Fact]
    public void Present_OutageWithStaleReadings_LastsOnlyAsLongAsItWasSeen()
    {
        // Arrange
        var lastSeenAt = Now - TimeSpan.FromMinutes(30);
        var ongoing = OutageRecords.Finished(lastSeenAt - TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(45), startSoc: 100) with { IsOngoing = true };

        // Act
        var row = RowOf(OverlayRowKind.CurrentOutage, StationReadings.OnBattery(lastSeenAt).With(ongoingOutage: ongoing));

        // Assert
        row.Value.Should().Be("45 min · 1.56 kWh");
    }

    [Fact]
    public void Present_NoOutage_SaysSo()
    {
        // Arrange
        var state = StationReadings.HoldingFull(AnsweredAt).With();

        // Act
        var current = RowOf(OverlayRowKind.CurrentOutage, state);
        var latest = RowOf(OverlayRowKind.LatestOutage, state);

        // Assert
        current.Value.Should().Be("none now");
        latest.Value.Should().Be("none recorded");
        latest.Hint.Should().BeNull();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OutageThatEnded_TellsWhenHowLongAndHowMuch(string language)
    {
        // Arrange
        Speak(language);
        var latest = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135), startSoc: 100);

        // Act
        var row = RowOf(OverlayRowKind.LatestOutage, StationReadings.HoldingFull(AnsweredAt).With(latestOutage: latest));

        // Assert
        row.Value.Should().Be(Translated("yesterday 21:05 · 2 h 15 min · 1.56 kWh"));
        row.Hint.Should().Be(Translated("From yesterday 21:05 to yesterday 23:20. Charge 100% → 91%."));
    }

    [Fact]
    public void Present_OutageWhoseEndWasNotSeen_SaysAtLeastAndWhy()
    {
        // Arrange
        var latest = OutageRecords.Finished(YesterdayEvening, TimeSpan.FromMinutes(135), startSoc: 100) with
        {
            IsEndObserved = false,
            StartSocPercent = null,
        };

        // Act
        var row = RowOf(OverlayRowKind.LatestOutage, StationReadings.HoldingFull(AnsweredAt).With(latestOutage: latest));

        // Assert
        row.Value.Should().Be("yesterday 21:05 · at least 2 h 15 min · 1.56 kWh");
        row.Hint.Should().Be(
            "From yesterday 21:05 to yesterday 23:20. The app did not see it end, only when it was last going on.");
    }
}
