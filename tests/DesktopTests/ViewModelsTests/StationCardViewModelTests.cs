using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public class StationCardViewModelTests : LocalizedTests
{
    private static readonly DateTimeOffset AnsweredAt = Moments.Local(day: 12, hour: 22, minute: 42, second: 34);
    private static readonly TimeSpan TrustedFor = TimeSpan.FromMinutes(2);

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Show_AnswerOfTheLatestPoll_TellsItsAgeAndWhenItCame(string language)
    {
        // Arrange
        Speak(language);
        var card = new StationCardViewModel();

        // Act
        card.Show(EnergySystem(StationReadings.HoldingFull(AnsweredAt)), AnsweredAt + TimeSpan.FromSeconds(12), TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse();
        card.Age.Should().Be(Translated("12 s ago"));
        card.AgeHint.Should().Be(Translated("Latest answer at 22:42:34."));
        card.Method.Should().Be(MarstekMethods.EnergySystemGetStatus);
        card.Rows.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Show_AnswerOlderThanItsRequestLeavesIt_IsStaleAndSaysWhy(string language)
    {
        // Arrange
        Speak(language);
        var card = new StationCardViewModel();

        // Act
        card.Show(EnergySystem(StationReadings.HoldingFull(AnsweredAt)), AnsweredAt + TimeSpan.FromMinutes(5), TrustedFor);

        // Assert
        card.IsStale.Should().BeTrue();
        card.Age.Should().Be(Translated("5 min ago"));
        card.AgeHint.Should().Be(Translated("Latest answer at 22:42:34. The station has stopped answering this request."));
    }

    [Fact]
    public void Show_AnswerJustAsOldAsTrusted_IsNotStaleYet()
    {
        // Arrange
        var card = new StationCardViewModel();

        // Act
        card.Show(EnergySystem(StationReadings.HoldingFull(AnsweredAt)), AnsweredAt + TrustedFor, TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse();
    }

    [Fact]
    public void Show_FreshAnswerAfterAStaleOne_ClearsTheMark()
    {
        // Arrange
        var card = new StationCardViewModel();
        var later = AnsweredAt + TimeSpan.FromMinutes(5);
        card.Show(EnergySystem(StationReadings.HoldingFull(AnsweredAt)), later, TrustedFor);

        // Act
        card.Show(EnergySystem(StationReadings.HoldingFull(later)), later, TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse();
        card.Age.Should().Be("just now");
    }

    [Fact]
    public void Show_RequestNeverAnswered_HasNoAgeToTurnStale()
    {
        // Arrange
        var card = new StationCardViewModel();

        // Act
        card.Show(EnergySystem(StationSnapshot.Empty), AnsweredAt + TimeSpan.FromHours(1), TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse();
        card.Age.Should().BeNull();
        card.AgeHint.Should().BeNull();
        card.Note.Should().Be("no answer");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Show_MeterWithoutACtAskedLongAgo_TellsWhenAndIsNotStale(string language)
    {
        // Arrange
        Speak(language);
        var card = new StationCardViewModel();
        var snapshot = StationSnapshot.Empty with
        {
            Meter = new Reading<EnergyMeterStatus>(new EnergyMeterStatus { IsCtConnected = false }, AnsweredAt),
        };

        // Act
        card.Show(Section(snapshot, MarstekMethods.EnergyMeterGetStatus), AnsweredAt + TimeSpan.FromMinutes(135), TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse("the request is not sent again, so its answer does not go out of date");
        card.Note.Should().Be(Translated("CT not connected"));
        card.Age.Should().Be(Translated("2 h 15 min ago"));
        card.AgeHint.Should().Be(Translated("Latest answer at 22:42:34."));
        card.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Show_RequestTheStationTurnedDownLongAgo_TellsWhenAndIsNotStale()
    {
        // Arrange
        var card = new StationCardViewModel();
        var snapshot = StationSnapshot.Empty with
        {
            UnsupportedMethods = new Dictionary<string, DateTimeOffset> { [MarstekMethods.PvGetStatus] = AnsweredAt },
        };

        // Act
        card.Show(Section(snapshot, MarstekMethods.PvGetStatus), AnsweredAt + TimeSpan.FromMinutes(5), TrustedFor);

        // Assert
        card.IsStale.Should().BeFalse();
        card.Note.Should().Be("not supported by this station");
        card.Age.Should().Be("5 min ago");
    }

    private static StationSection EnergySystem(StationSnapshot snapshot) => Section(snapshot, MarstekMethods.EnergySystemGetStatus);

    private static StationSection Section(StationSnapshot snapshot, string method) =>
        StationSections.Describe(snapshot).Single(section => section.Method == method);
}
