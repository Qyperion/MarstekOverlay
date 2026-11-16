using FluentAssertions;

using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class PowerSourceTrackerTests
{

    private readonly PollClock _time = new();
    private readonly PowerSourceTracker _tracker = new();
    private readonly List<PowerSourceChange> _changes = [];

    public PowerSourceTrackerTests()
    {
        _tracker.SourceChanged += _changes.Add;
    }

    private DateTimeOffset Now => _time.Now;

    [Fact]
    public void Update_NoAnswerYet_IsUnknown()
    {
        _tracker.Update(StationSnapshot.Empty).Should().Be(PowerSupply.Unknown);
    }

    [Fact]
    public void Update_FirstAnswerShowsTheGridAtWork_IsMainsWithoutClaimingToHaveSeenThemComeOn()
    {
        // Act
        var supply = _tracker.Update(StationReadings.Charging(Now));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: false));
        _changes.Should().ContainSingle().Which.Previous.Should().Be(PowerSupply.Unknown);
    }

    [Fact]
    public void Update_FirstAnswersLookLikeAnOutage_IsBatteryOnceTheSecondAgrees()
    {
        // Arrange
        var firstSeenAt = Now;

        // Act
        var afterOne = _tracker.Update(StationReadings.OnBattery(Now));
        var afterTwo = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        afterOne.Should().Be(PowerSupply.Unknown);
        afterTwo.Should().Be(new PowerSupply(PowerSource.Battery, firstSeenAt, IsSinceObserved: false));
    }

    [Fact]
    public void Update_OutageWhileOnMains_IsAnnouncedAtTheSecondAnswerAndDatedByTheFirst()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now));
        _time.NextPoll();
        var switchedAt = Now;

        // Act
        var afterOne = _tracker.Update(StationReadings.OnBattery(Now));
        var afterTwo = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        afterOne.Source.Should().Be(PowerSource.Mains);
        afterTwo.Should().Be(new PowerSupply(PowerSource.Battery, switchedAt, IsSinceObserved: true));
        _changes.Should().HaveCount(2);
        _changes[^1].Previous.Source.Should().Be(PowerSource.Mains);
        _changes[^1].Current.Should().Be(afterTwo);
    }

    [Fact]
    public void Update_MainsBack_IsTakenAtTheFirstAnswer()
    {
        // Arrange
        _tracker.Update(StationReadings.OnBattery(Now));
        UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Act
        var supply = UpdateAfterInterval(at => StationReadings.Charging(at));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: true));
    }

    [Fact]
    public void Update_FullBatteryBetweenTopUps_StaysOnMains()
    {
        // Arrange
        var onMains = _tracker.Update(StationReadings.Charging(Now, soc: 99));

        // Act
        for (var poll = 0; poll < 6; poll++)
            UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        UpdateAfterInterval(at => StationReadings.ToppingUp(at));
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        supply.Should().Be(onMains);
        _changes.Should().ContainSingle();
    }

    [Fact]
    public void Update_FullBatteryAndNothingElseKnown_IsTakenForMains()
    {
        // Arrange
        var firstSeenAt = Now;

        // Act
        _tracker.Update(StationReadings.HoldingFull(Now));
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, firstSeenAt, IsSinceObserved: false));
    }

    [Fact]
    public void Update_FullBatteryDuringAnOutage_IsNotEnoughToCallItOver()
    {
        // Arrange
        _tracker.Update(StationReadings.OnBattery(Now, soc: 99));
        var onBattery = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Act
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        supply.Should().Be(onBattery);
    }

    [Fact]
    public void Update_ChargeMissedBetweenTwoAnswers_IsToldFromTheGridEnergyCounter()
    {
        // Arrange
        _tracker.Update(StationReadings.OnBattery(Now, soc: 99));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Act
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at, gridEnergyWattHours: 15));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: true));
    }

    [Fact]
    public void Update_OutageOnAFullBattery_IsNoticedOnceTheChargeHasStayedDownForLongerThanATopUpTakes()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now, soc: 99));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at));
        _time.NextPoll();
        var noticedAt = Now;

        // Act
        _tracker.Update(StationReadings.OnBattery(Now, soc: 99));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));
        var afterThree = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));
        var afterFour = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));
        var afterFive = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Assert
        Now.Should().Be(noticedAt + PowerSourceRule.LongestTopUpDelay);
        afterThree.Source.Should().Be(PowerSource.Mains);
        afterFour.Source.Should().Be(PowerSource.Mains);
        afterFive.Should().Be(new PowerSupply(PowerSource.Battery, noticedAt, IsSinceObserved: false));
        _changes[^1].IsWatched.Should().BeTrue();
    }

    [Fact]
    public void Update_TopUpLateByAPoll_IsNoOutage()
    {
        // Arrange
        var onMains = _tracker.Update(StationReadings.Charging(Now, soc: 99));
        UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));
        UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));
        UpdateAfterInterval(at => StationReadings.ToppingUp(at));
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at));

        // Assert
        supply.Should().Be(onMains);
        _changes.Should().ContainSingle();
    }

    [Fact]
    public void Update_OutageOnAFullBatteryAcrossAGapInTheAnswers_StillTakesAThirdAnswer()
    {
        // Arrange
        _tracker.Update(StationReadings.HoldingFull(Now));
        _time.NextPoll();
        var noticedAt = Now;
        _tracker.Update(StationReadings.OnBattery(Now, soc: 99));

        // Act
        _time.Advance(PowerSourceRule.LongestTopUpDelay + PollClock.Interval);
        var afterTwo = _tracker.Update(StationReadings.OnBattery(Now, soc: 99));
        var afterThree = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 99));

        // Assert
        afterTwo.Source.Should().Be(PowerSource.Mains);
        afterThree.Should().Be(new PowerSupply(PowerSource.Battery, noticedAt, IsSinceObserved: false));
    }

    [Fact]
    public void Update_OutageWhileTheGridWasLastSeenAtWork_IsNotHeldBackByAnAnswerThatTellsNothing()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now));
        UpdateAfterInterval(at => StationReadings.Idle(at));
        _time.NextPoll();
        var switchedAt = Now;

        // Act
        _tracker.Update(StationReadings.OnBattery(Now, soc: 79));
        var supply = UpdateAfterInterval(at => StationReadings.OnBattery(at, soc: 79));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Battery, switchedAt, IsSinceObserved: true));
    }

    [Fact]
    public void Update_SingleAnswerThatLooksLikeAnOutage_ChangesNothing()
    {
        // Arrange
        var onMains = _tracker.Update(StationReadings.Charging(Now));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at));
        UpdateAfterInterval(at => StationReadings.Charging(at));
        var supply = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        supply.Should().Be(onMains);
        _changes.Should().ContainSingle();
    }

    [Fact]
    public void Update_AnswerThatTellsNothingInBetween_LeavesTheOutageUnconfirmed()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now));

        // Act
        UpdateAfterInterval(at => StationReadings.OnBattery(at));
        UpdateAfterInterval(at => StationReadings.Idle(at));
        var supply = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        supply.Source.Should().Be(PowerSource.Mains);
    }

    [Fact]
    public void Update_AnswerThatTellsNothing_KeepsTheSource()
    {
        // Arrange
        _tracker.Update(StationReadings.OnBattery(Now));
        var onBattery = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Act
        var supply = UpdateAfterInterval(at => StationReadings.Idle(at));

        // Assert
        supply.Should().Be(onBattery);
    }

    [Fact]
    public void Update_SameAnswerAgain_DoesNotCountAsAConfirmation()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now));
        _time.NextPoll();
        var failedPoll = StationReadings.OnBattery(Now);
        _tracker.Update(failedPoll);

        // Act
        var supply = _tracker.Update(failedPoll);

        // Assert
        supply.Source.Should().Be(PowerSource.Mains);
    }

    [Fact]
    public void Update_AfterALongGapOnMains_StartsThePeriodOverWithoutAnnouncingAChange()
    {
        // Arrange
        _tracker.Update(StationReadings.Charging(Now));
        _changes.Clear();

        // Act
        _time.Advance(PowerSourceTracker.LongestUnwatchedGap + PollClock.Interval);
        var supply = _tracker.Update(StationReadings.Charging(Now));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: false));
        _changes.Should().BeEmpty();
    }

    [Fact]
    public void Update_AfterALongGapOnBattery_IsUnknownUntilTheOutageIsConfirmedAgain()
    {
        // Arrange
        _tracker.Update(StationReadings.OnBattery(Now));
        UpdateAfterInterval(at => StationReadings.OnBattery(at));
        _time.Advance(PowerSourceTracker.LongestUnwatchedGap + PollClock.Interval);
        var seenAgainAt = Now;

        // Act
        var afterOne = _tracker.Update(StationReadings.OnBattery(Now));
        var afterTwo = UpdateAfterInterval(at => StationReadings.OnBattery(at));

        // Assert
        afterOne.Should().Be(PowerSupply.Unknown);
        afterTwo.Should().Be(new PowerSupply(PowerSource.Battery, seenAgainAt, IsSinceObserved: false));
    }

    [Fact]
    public void Update_GridEnergyCountedDuringALongGap_SaysNothingAboutNow()
    {
        // Arrange
        _tracker.Update(StationReadings.Idle(Now, gridEnergyWattHours: 18027));

        // Act
        _time.Advance(PowerSourceTracker.LongestUnwatchedGap + PollClock.Interval);
        var supply = _tracker.Update(StationReadings.Idle(Now, gridEnergyWattHours: 19500));

        // Assert
        supply.Should().Be(PowerSupply.Unknown);
    }

    [Fact]
    public void Resume_AnswerSoonEnough_CarriesThePeriodOn()
    {
        // Arrange
        var before = new PowerSupply(PowerSource.Battery, Now - TimeSpan.FromHours(2), IsSinceObserved: true);
        _tracker.Resume(before, watchedUntil: Now);

        // Act
        _time.Advance(TimeSpan.FromMinutes(3));
        var supply = _tracker.Update(StationReadings.OnBattery(Now));

        // Assert
        supply.Should().Be(before);
        _changes.Should().BeEmpty();
    }

    [Fact]
    public void Resume_AnswerTooLate_StartsOver()
    {
        // Arrange
        var before = new PowerSupply(PowerSource.Battery, Now - TimeSpan.FromHours(2), IsSinceObserved: true);
        _tracker.Resume(before, watchedUntil: Now);

        // Act
        _time.Advance(TimeSpan.FromHours(1));
        var supply = _tracker.Update(StationReadings.Charging(Now));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: false));
    }

    [Fact]
    public void Resume_GridEnergyCountedMeanwhile_EndsTheOutageWithoutClaimingToHaveSeenItEnd()
    {
        // Arrange
        var before = new PowerSupply(PowerSource.Battery, Now - TimeSpan.FromHours(2), IsSinceObserved: true);
        _tracker.Resume(before, watchedUntil: Now, new GridEnergyCounters(new Energy(18027), Output: null));

        // Act
        _time.Advance(TimeSpan.FromMinutes(5));
        var supply = _tracker.Update(StationReadings.HoldingFull(Now, gridEnergyWattHours: 18042));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: false));
        _changes.Should().ContainSingle().Which.IsWatched.Should().BeTrue();
    }

    [Fact]
    public void Resume_WithoutGridEnergyCounters_TakesAFullBatteryForTheOutageGoingOn()
    {
        // Arrange
        var before = new PowerSupply(PowerSource.Battery, Now - TimeSpan.FromHours(2), IsSinceObserved: true);
        _tracker.Resume(before, watchedUntil: Now);

        // Act
        _time.Advance(TimeSpan.FromMinutes(5));
        _tracker.Update(StationReadings.HoldingFull(Now, gridEnergyWattHours: 18042));
        var supply = UpdateAfterInterval(at => StationReadings.HoldingFull(at, gridEnergyWattHours: 18042));

        // Assert
        supply.Should().Be(before);
        _changes.Should().BeEmpty();
    }

    [Fact]
    public void Resume_GridAtWorkInTheFirstAnswer_EndsTheOutageWithoutClaimingToHaveSeenItEnd()
    {
        // Arrange
        var before = new PowerSupply(PowerSource.Battery, Now - TimeSpan.FromHours(2), IsSinceObserved: true);
        _tracker.Resume(before, watchedUntil: Now);

        // Act
        _time.Advance(TimeSpan.FromMinutes(3));
        var supply = _tracker.Update(StationReadings.Charging(Now));

        // Assert
        supply.Should().Be(new PowerSupply(PowerSource.Mains, Now, IsSinceObserved: false));
    }

    private PowerSupply UpdateAfterInterval(Func<DateTimeOffset, StationSnapshot> read)
    {
        return _tracker.Update(read(_time.NextPoll()));
    }
}
