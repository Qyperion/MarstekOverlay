using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi.Models;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class UnitsTests : LocalizedTests
{
    [Theory]
    [InlineData(999.94, "999.9 Wh")]
    [InlineData(999.96, "1 kWh")]
    [InlineData(1000, "1 kWh")]
    [InlineData(1560, "1.56 kWh")]
    [InlineData(-1560, "-1.56 kWh")]
    public void Format_Energy_SwitchesToKilowattHoursFromWhatReadsAsAThousand(double wattHours, string expected)
    {
        Units.Format(new Energy(wattHours)).Should().Be(expected);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Format_UnitFollowsTheLanguageAndTheDecimalSeparatorItsUsualFormat(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var energy = Units.Format(new Energy(1560));
        var power = Units.Format(new Power(574.6));

        // Assert
        energy.Should().Be(Translated("1.56 kWh"));
        power.Should().Be(Translated("575 W"));
    }

    [Fact]
    public void Format_RegionalFormatOfAnotherLanguage_ChangesTheSeparatorButNotTheUnit()
    {
        // Arrange
        Speak(English, regionalFormat: RegionalFormatOfUkraine);

        // Act
        var energy = Units.Format(new Energy(1560));

        // Assert
        energy.Should().Be("1,56 kWh");
    }

    [Fact]
    public void Percent_KeepsOneDecimalAtMost()
    {
        Units.Percent(97.14).Should().Be("97.1%");
        Units.Percent(100).Should().Be("100%");
    }
}
