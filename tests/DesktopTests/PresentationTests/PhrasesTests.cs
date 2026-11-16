using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring.Outages;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class PhrasesTests : LocalizedTests
{
    private static readonly DateTimeOffset Now = Moments.Local(day: 12, hour: 9, minute: 30);

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Duration_AnHourOrMore_TellsHoursAndMinutes(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Duration(TimeSpan.FromMinutes(135), isExact: true);

        // Assert
        text.Should().Be(Translated("2 h 15 min"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Duration_SeenOnlyInPart_IsAtLeastThatLong(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Duration(TimeSpan.FromMinutes(40), isExact: false);

        // Assert
        text.Should().Be(Translated("at least 40 min"));
    }

    [Theory]
    [InlineData(59, "59 min")]
    [InlineData(60, "1 h 00 min")]
    [InlineData(65, "1 h 05 min")]
    [InlineData(1505, "25 h 05 min")]
    public void Duration_AroundTheHour_PadsTheMinutesOnlyAfterHours(int minutes, string expected)
    {
        Phrases.Duration(TimeSpan.FromMinutes(minutes), isExact: true).Should().Be(expected);
    }

    [Theory]
    [InlineData(60, "1 min")]
    [InlineData(119, "1 min")]
    public void Duration_PartOfAMinute_IsLeftOut(int seconds, string expected)
    {
        Phrases.Duration(TimeSpan.FromSeconds(seconds), isExact: true).Should().Be(expected);
    }

    [Theory]
    [InlineData(English, 59, true)]
    [InlineData(English, 0, true)]
    [InlineData(English, -30, true)]
    [InlineData(English, 59, false)]
    [InlineData(Ukrainian, 59, true)]
    [InlineData(Ukrainian, 59, false)]
    public void Duration_UnderAMinute_SaysSoRatherThanCountingNoMinutes(string language, int seconds, bool isExact)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Duration(TimeSpan.FromSeconds(seconds), isExact);

        // Assert
        text.Should().Be(Translated("less than a minute"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Ago_UnderAMinute_IsToldToTheSecond(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Ago(TimeSpan.FromSeconds(12.9));

        // Assert
        text.Should().Be(Translated("12 s ago"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Ago_AMinuteOrMore_IsToldAsADuration(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Ago(TimeSpan.FromMinutes(135.5));

        // Assert
        text.Should().Be(Translated("2 h 15 min ago"));
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(-5)]
    public void Ago_NoTimeToSpeakOf_IsJustNow(double seconds)
    {
        Phrases.Ago(TimeSpan.FromSeconds(seconds)).Should().Be("just now");
    }

    [Theory]
    [InlineData(59, "59 s ago")]
    [InlineData(60, "1 min ago")]
    public void Ago_AroundTheMinute_SwitchesFromSecondsToMinutes(int seconds, string expected)
    {
        Phrases.Ago(TimeSpan.FromSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void Ago_JustNow_IsWordedInTheLanguageOfTheApp()
    {
        // Arrange
        Speak(Ukrainian);

        // Act
        var text = Phrases.Ago(TimeSpan.Zero);

        // Assert
        text.Should().Be(Translated("just now"));
    }

    [Fact]
    public void Moment_Today_IsTheTimeAlone()
    {
        Phrases.Moment(Moments.Local(day: 12, hour: 0, minute: 5), Now).Should().Be("00:05");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Moment_TheDayBefore_IsCalledYesterday(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Moment(Moments.Local(day: 11, hour: 21, minute: 5), Now);

        // Assert
        text.Should().Be(Translated("yesterday 21:05"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Moment_EarlierThanYesterday_NamesTheDate(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Moment(Moments.Local(day: 9, hour: 17, minute: 2), Now);

        // Assert
        text.Should().Be(Translated("9 Nov 17:02"));
    }

    [Fact]
    public void Moment_MinutesBeforeMidnight_IsYesterdayHoweverLittleTimeHasPassed()
    {
        // Arrange
        var justAfterMidnight = Moments.Local(day: 12, hour: 0, minute: 2);

        // Act
        var text = Phrases.Moment(Moments.Local(day: 11, hour: 23, minute: 58), justAfterMidnight);

        // Assert
        text.Should().Be("yesterday 23:58");
    }

    [Fact]
    public void Moment_GivenInAnotherOffset_IsToldInLocalTime()
    {
        // Arrange
        var local = Moments.Local(day: 11, hour: 21, minute: 5);

        // Act
        var text = Phrases.Moment(local.ToUniversalTime(), Now.ToUniversalTime());

        // Assert
        text.Should().Be("yesterday 21:05");
    }

    [Fact]
    public void Date_RegionalFormatOfAnotherLanguage_StillNamesTheMonthInTheLanguageOfTheApp()
    {
        // Arrange
        Speak(English, regionalFormat: RegionalFormatOfUkraine);

        // Act
        var text = Phrases.Date(Moments.Local(day: 9, hour: 17, minute: 2));

        // Assert
        text.Should().Be("9 Nov");
    }

    [Fact]
    public void Date_InUkrainian_NamesTheMonthInUkrainian()
    {
        // Arrange
        Speak(Ukrainian, regionalFormat: RegionalFormatOfTheUnitedStates);

        // Act
        var text = Phrases.Date(Moments.Local(day: 9, hour: 17, minute: 2));

        // Assert
        text.Should().Be(Translated("9 Nov"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Month_IsNamedInFullWithACapitalLetterAndItsYear(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Month(new CalendarMonth(2026, 11));

        // Assert
        text.Should().Be(Translated("November 2026"));
    }

    [Fact]
    public void Month_InUkrainian_FollowsTheLanguageRatherThanTheRegionalFormat()
    {
        // Arrange
        Speak(Ukrainian, regionalFormat: RegionalFormatOfTheUnitedStates);

        // Act
        var text = Phrases.Month(new CalendarMonth(2026, 11));

        // Assert
        text.Should().Be(Translated("November 2026"));
    }

    [Fact]
    public void TimeOfDay_IsOnTheTwentyFourHourClockWhateverTheRegionalFormat()
    {
        Phrases.TimeOfDay(Moments.Local(day: 12, hour: 19, minute: 5)).Should().Be("19:05");
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Period_UpToTwoDays_IsToldInHours(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Period(TimeSpan.FromHours(3));

        // Assert
        text.Should().Be(Translated("3 h"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Period_LongerThanTwoDays_IsToldInDays(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = Phrases.Period(TimeSpan.FromHours(168));

        // Assert
        text.Should().Be(Translated("7 d"));
    }

    [Theory]
    [InlineData(24, "24 h")]
    [InlineData(48, "48 h")]
    [InlineData(72, "3 d")]
    public void Period_AroundTwoDays_SwitchesFromHoursToDays(int hours, string expected)
    {
        Phrases.Period(TimeSpan.FromHours(hours)).Should().Be(expected);
    }

    [Fact]
    public void Sentences_LeaveOutWhatIsMissing()
    {
        Phrases.Sentences("One.", null, "Two.").Should().Be("One. Two.");
    }

    [Fact]
    public void Lines_LeaveOutWhatIsMissing()
    {
        Phrases.Lines(null, "one", "two").Should().Be("one" + Environment.NewLine + "two");
    }

    [Fact]
    public void Readings_StandSideBySide()
    {
        Phrases.Readings("575 W", null, "100%").Should().Be("575 W · 100%");
    }
}
