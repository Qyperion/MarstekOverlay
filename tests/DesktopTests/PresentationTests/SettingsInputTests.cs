using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring.Settings;

using System.Net;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public sealed class SettingsInputTests : LocalizedTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 30000 ", 30000)]
    [InlineData("65535", 65535)]
    public void TryReadPort_PortNumber_IsRead(string text, int expected)
    {
        // Act
        var isRead = SettingsInput.TryReadPort(text, out var port, out var error);

        // Assert
        isRead.Should().BeTrue();
        port.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(English, "")]
    [InlineData(English, "0")]
    [InlineData(Ukrainian, "65536")]
    [InlineData(English, "-1")]
    [InlineData(English, "30000.5")]
    [InlineData(English, "port")]
    public void TryReadPort_AnythingElse_IsTurnedDown(string language, string text)
    {
        // Arrange
        Speak(language);

        // Act
        var isRead = SettingsInput.TryReadPort(text, out _, out var error);

        // Assert
        isRead.Should().BeFalse();
        error.Should().Be(Translated("The port must be a number from 1 to 65535."));
    }

    [Fact]
    public void TryReadAddress_FourDecimalNumbers_IsTheAddress()
    {
        // Act
        var isRead = SettingsInput.TryReadAddress(" 192.168.1.50 ", out var address, out var error);

        // Assert
        isRead.Should().BeTrue();
        address.Should().Be(IPAddress.Parse("192.168.1.50"));
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(English, "192.168.1")]
    [InlineData(English, "1")]
    [InlineData(Ukrainian, "192.168.1.050")]
    [InlineData(English, "192.168.01.50")]
    [InlineData(English, "::1")]
    [InlineData(English, "")]
    public void TryReadAddress_ShortHandOrNotIPv4_IsTurnedDownRatherThanReadAsAnotherAddress(string language, string text)
    {
        // Arrange
        Speak(language);

        // Act
        var isRead = SettingsInput.TryReadAddress(text, out var address, out var error);

        // Assert
        isRead.Should().BeFalse();
        address.Should().BeNull();
        error.Should().Be(Translated("The station address must be an IPv4 address such as 192.168.1.50."));
    }

    [Theory]
    [InlineData("96.5", 96.5)]
    [InlineData("96,5", 96.5)]
    [InlineData(" 50 ", 50)]
    [InlineData("100", 100)]
    public void TryReadEfficiency_NumberWithinTheLimits_TakesEitherDecimalSeparator(string text, double expected)
    {
        // Act
        var isRead = SettingsInput.TryReadEfficiency(text, out var percent, out var error);

        // Assert
        isRead.Should().BeTrue();
        percent.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("49.9")]
    [InlineData("100.1")]
    [InlineData("nan")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-90")]
    [InlineData("1e2")]
    public void TryReadEfficiency_AnythingElse_IsTurnedDownWithTheLimits(string text)
    {
        // Act
        var isRead = SettingsInput.TryReadEfficiency(text, out _, out var error);

        // Assert
        isRead.Should().BeFalse();
        error.Should().Be(Translated("The efficiency must be from 50 to 100%."));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("99", true)]
    [InlineData("100", false)]
    [InlineData("nan", false)]
    public void TryReadReserve_LimitsAndNoNumber_AreWhereTheyShouldBe(string text, bool expected)
    {
        // Act
        var isRead = SettingsInput.TryReadReserve(text, out _, out var error);

        // Assert
        isRead.Should().Be(expected);
        (error is null).Should().Be(expected);
    }

    [Theory]
    [InlineData(English, "0")]
    [InlineData(Ukrainian, "1441")]
    [InlineData(English, "Infinity")]
    public void TryReadStaleAfter_OutOfRange_IsTurnedDownWithTheLimits(string language, string text)
    {
        // Arrange
        Speak(language);

        // Act
        var isRead = SettingsInput.TryReadStaleAfter(text, out _, out var error);

        // Assert
        isRead.Should().BeFalse();
        error.Should().Be(Translated("The time without an answer must be from 1 to 1440 minutes."));
    }

    [Theory]
    [InlineData("50, 30, 12,5", new[] { 50, 30, 12.5 })]
    [InlineData("12.5;50 ; 30", new[] { 50, 30, 12.5 })]
    [InlineData("50 , 30", new[] { 50.0, 30 })]
    [InlineData("30 30 100 1", new[] { 100.0, 30, 1 })]
    [InlineData("   ", new double[0])]
    public void TryReadChargeThresholds_SpacesAndSemicolonsApart_AreReadHighestFirst(string text, double[] expected)
    {
        // Act
        var isRead = SettingsInput.TryReadChargeThresholds(text, out var thresholds, out var error);

        // Assert
        isRead.Should().BeTrue();
        thresholds.Should().Equal(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(English, "50,30,15")]
    [InlineData(English, "50 nan")]
    [InlineData(English, "Infinity")]
    [InlineData(Ukrainian, "0.5")]
    [InlineData(English, "101")]
    [InlineData(English, "fifty")]
    public void TryReadChargeThresholds_AnyPartThatIsNoPercentage_TurnsDownTheLot(string language, string text)
    {
        // Arrange
        Speak(language);

        // Act
        var isRead = SettingsInput.TryReadChargeThresholds(text, out var thresholds, out var error);

        // Assert
        isRead.Should().BeFalse();
        thresholds.Should().BeEmpty();
        error.Should().Be(Translated("The charge thresholds must be numbers from 1 to 100, separated by spaces or semicolons."));
    }

    [Theory]
    [InlineData("-10", "45", -10, 45)]
    [InlineData("-40", "100", -40, 100)]
    [InlineData("5,5", "44.5", 5.5, 44.5)]
    public void TryReadTemperatureLimits_LowerBelowUpperWithinTheRange_AreRead(string lowestText, string highestText, double lowest, double highest)
    {
        // Act
        var isRead = SettingsInput.TryReadTemperatureLimits(lowestText, highestText, out var readLowest, out var readHighest, out var error);

        // Assert
        isRead.Should().BeTrue();
        readLowest.Should().Be(lowest);
        readHighest.Should().Be(highest);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("30", "30")]
    [InlineData("-41", "45")]
    [InlineData("5", "101")]
    [InlineData("nan", "45")]
    [InlineData("5", "Infinity")]
    [InlineData("", "45")]
    public void TryReadTemperatureLimits_AnythingElse_IsTurnedDownWithTheRange(string lowestText, string highestText)
    {
        // Act
        var isRead = SettingsInput.TryReadTemperatureLimits(lowestText, highestText, out _, out _, out var error);

        // Assert
        isRead.Should().BeFalse();
        error.Should().Be(Translated("The battery temperature limits must lie between -40 and 100 °C, the lower one below the upper."));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Number_AnyLanguage_IsWrittenTheWayTheFieldsReadItBack(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = SettingsInput.Number(96.5);

        // Assert
        text.Should().Be("96.5");
    }

    [Fact]
    public void ChargeThresholds_AnyOrder_AreWrittenHighestFirstApartBySemicolons()
    {
        // Act
        var text = SettingsInput.ChargeThresholds([15, 50, 12.5]);

        // Assert
        text.Should().Be("50; 15; 12.5");
    }

    [Theory]
    [InlineData("1", 45, "In effect: 1.5 min, at least two poll intervals.")]
    [InlineData("1", 120, "In effect: 4 min, at least two poll intervals.")]
    [InlineData("4", 120, null)]
    [InlineData("5", 120, null)]
    [InlineData("1", 30, null)]
    [InlineData("soon", 120, null)]
    [InlineData("0", 120, null)]
    public void StaleAfterInEffect_TimeTypedAndPoll_TellsOfTheLongerWaitOnly(string text, int intervalSeconds, string? expected)
    {
        // Arrange
        var polling = new PollingSettings { IntervalSeconds = intervalSeconds, SlowIntervalSeconds = 300 };

        // Act
        var inEffect = SettingsInput.StaleAfterInEffect(text, polling);

        // Assert
        inEffect.Should().Be(expected);
    }
}
