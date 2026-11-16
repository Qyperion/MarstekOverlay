using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;


public class OverlayPresenterHeaderTests : OverlayPresenterTests
{
    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void SettingsRequired_SaysSoWhereverTheStateIsRead(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var state = OverlayPresenter.SettingsRequired();

        // Assert
        var expected = Translated("Settings required");
        state.StateText.Should().Be(expected);
        state.StateTone.Should().Be(Tone.Warning);
        state.CompactText.Should().Be(expected);
        state.Summary.Should().Equal(expected);
        state.Supply.Should().Be(SupplyMark.None);
        state.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Present_NoAnswerYet_Waits()
    {
        // Act
        var state = Present(StationSnapshot.Empty.With());

        // Assert
        state.StateText.Should().Be("Waiting for data");
        state.StateTone.Should().Be(Tone.Secondary);
        state.Supply.Should().Be(SupplyMark.None);
        state.SocText.Should().Be(OverlayState.UnknownSocText);
        state.CompactText.Should().Be("Waiting for data");
        state.Summary.Should().Equal("Waiting for data");
        state.StaleText.Should().BeEmpty();
        state.ErrorHint.Should().BeNull();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_NoAnswerButErrors_TellsThatTheStationIsOutOfReach(string language)
    {
        // Arrange
        Speak(language);
        var silent = StationSnapshot.Empty with
        {
            Errors =
            [
                new UnansweredRequest("ES.GetStatus", TimeSpan.FromSeconds(3)),
                new RefusedRequest("ES.GetMode", -32602, "Invalid params"),
            ],
        };

        // Act
        var state = Present(silent.With());

        // Assert
        state.StateText.Should().Be(Translated("Waiting for the first answer"));
        state.StateTone.Should().Be(Tone.Warning);
        state.Supply.Should().Be(SupplyMark.NoConnection);
        state.StaleText.Should().Be(Translated("No data yet, retrying"));
        state.ErrorHint.Should().Be(
            Translated("The station did not answer ES.GetStatus within 3 s.")
            + Environment.NewLine
            + Translated("The station answered ES.GetMode with an error: Invalid params (code -32602)."));
    }

    [Fact]
    public void Present_AnswerTwoMinutesOld_IsStillFresh()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(Now - TimeSpan.FromMinutes(2));

        // Act
        var state = Present(snapshot.With(Supplies.OnMains(YesterdayEvening)));

        // Assert
        state.Supply.Should().Be(SupplyMark.OnMains);
        state.StaleText.Should().BeEmpty();
    }

    [Fact]
    public void Present_LongerInterval_KeepsAnOlderAnswerFresh()
    {
        // Arrange
        Polling.IntervalSeconds = 120;
        var snapshot = StationReadings.HoldingFull(Now - TimeSpan.FromSeconds(150));

        // Act
        var state = Present(snapshot.With(Supplies.OnMains(YesterdayEvening)));

        // Assert
        state.Supply.Should().Be(SupplyMark.OnMains);
        state.StaleText.Should().BeEmpty();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_AnswerGoneStale_KeepsTheChargeButOwnsUpToItsAge(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationReadings.OnBattery(Now - TimeSpan.FromMinutes(3), soc: 80);

        // Act
        var state = Present(snapshot.With(Supplies.OnBattery(Now - TimeSpan.FromHours(1))));

        // Assert
        var stale = Translated("Data from 22:39:46 is stale");
        var noFreshData = Translated("No fresh data");
        state.StateText.Should().Be(noFreshData);
        state.StateTone.Should().Be(Tone.Warning);
        state.StateHint.Should().BeNull();
        state.Supply.Should().Be(SupplyMark.NoConnection);
        state.SocText.Should().Be("80%");
        state.CompactText.Should().Be(noFreshData);
        state.CompactHint.Should().Be(noFreshData + Environment.NewLine + stale);
        state.Summary.Should().Equal(noFreshData, stale);
        state.StaleText.Should().Be(stale);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_AnswersOnTime_HaveNoLineForTheirAgeOnlyAHint(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With());

        // Assert
        state.StaleText.Should().BeEmpty();
        state.StateHint.Should().Be(Translated("Latest answer at 22:42:34."));
        state.CompactHint.Should().Be(state.StateText + Environment.NewLine + state.StateHint);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OnBattery_HeadsWithTheSourceAndHowLong(string language)
    {
        // Arrange
        Speak(language);
        var supply = Supplies.OnBattery(Now - TimeSpan.FromMinutes(135));

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(supply));

        // Assert
        state.StateText.Should().Be(Translated("On battery · 2 h 15 min"));
        state.StateTone.Should().Be(Tone.Warning);
        state.StateHint.Should().Be(Translated("On battery since 20:27. Latest answer at 22:42:34."));
        state.Supply.Should().Be(SupplyMark.OnBattery);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OnBatterySinceBeforeTheAppWatched_SaysAtLeast(string language)
    {
        // Arrange
        Speak(language);
        var supply = Supplies.OnBattery(Now - TimeSpan.FromMinutes(40), isSinceObserved: false);

        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt).With(supply));

        // Assert
        state.StateText.Should().Be(Translated("On battery · at least 40 min"));
        state.StateHint.Should().Be(Translated(
            "On battery since 22:02. The app did not see when that began, so it may have been longer. Latest answer at 22:42:34."));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OnMainsWithAFullBattery_HeadsWithTheSourceAlone(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var state = Present(StationReadings.HoldingFull(AnsweredAt).With(Supplies.OnMains(YesterdayEvening)));

        // Assert
        state.StateText.Should().Be(Translated("On mains"));
        state.StateTone.Should().Be(Tone.Positive);
        state.StateHint.Should().Be(Translated("On mains since yesterday 21:05. Latest answer at 22:42:34."));
        state.Supply.Should().Be(SupplyMark.OnMains);
        state.CompactHint.Should().Be(state.StateText + Environment.NewLine + state.StateHint);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Present_OnMainsWhileTheBatteryCharges_SaysSo(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var state = Present(StationReadings.Charging(AnsweredAt, soc: 80).With(Supplies.OnMains(YesterdayEvening)));

        // Assert
        state.StateText.Should().Be(Translated("On mains · charging"));
    }

    [Fact]
    public void Present_TopUpOfAFullBattery_IsNotWorthAWord()
    {
        // Act
        var state = Present(StationReadings.Charging(AnsweredAt, soc: 99).With(Supplies.OnMains(YesterdayEvening)));

        // Assert
        state.StateText.Should().Be("On mains");
    }

    [Theory]
    [InlineData(-2450, 250, "Charging", Tone.Positive)]
    [InlineData(0, 250, "Discharging", Tone.Info)]
    [InlineData(0, 0, "Idle", Tone.Secondary)]
    public void Present_SourceUnknown_HeadsWithWhatTheBatteryDoes(double ongridWatts, double loadWatts, string text, Tone tone)
    {
        // Act
        var state = Present(StationReadings.Answered(AnsweredAt, soc: 80, ongridWatts, loadWatts).With());

        // Assert
        state.StateText.Should().Be(text);
        state.StateTone.Should().Be(tone);
        state.StateHint.Should().Be("Latest answer at 22:42:34.");
        state.Supply.Should().Be(SupplyMark.None);
    }

    [Fact]
    public void Present_SourceUnknownAndNoFlows_HasNothingToHeadWith()
    {
        // Act
        var state = Present(StationReadings.Answered(AnsweredAt, new EnergySystemStatus { BatterySocPercent = 80 }).With());

        // Assert
        state.StateText.Should().Be("No data");
        state.StateTone.Should().Be(Tone.Secondary);
    }

    [Theory]
    [InlineData(79.6, "80%", 79.6, Tone.Positive)]
    [InlineData(50, "50%", 50, Tone.Warning)]
    [InlineData(15, "15%", 15, Tone.Critical)]
    [InlineData(104, "104%", 100, Tone.Positive)]
    public void Present_Charge_IsRoundedAndTonedByTheThresholds(double soc, string text, double barPercent, Tone tone)
    {
        // Act
        var state = Present(StationReadings.OnBattery(AnsweredAt, soc: soc).With());

        // Assert
        state.SocText.Should().Be(text);
        state.SocPercent.Should().Be(barPercent);
        state.SocTone.Should().Be(tone);
    }

    [Fact]
    public void Present_ChargeMissingFromTheAnswer_ShowsADash()
    {
        // Act
        var state = Present(StationReadings.Answered(AnsweredAt, soc: null, ongridWatts: 0, loadWatts: 250).With());

        // Assert
        state.SocText.Should().Be(OverlayState.UnknownSocText);
        state.SocPercent.Should().Be(0);
        state.SocTone.Should().Be(Tone.Secondary);
    }
}
