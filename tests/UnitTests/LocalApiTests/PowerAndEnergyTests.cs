using FluentAssertions;

using MarstekOverlay.LocalApi.Models;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class PowerAndEnergyTests
{
    [Fact]
    public void Multiply_PowerOverATime_IsTheEnergyMoved()
    {
        (new Power(300) * TimeSpan.FromMinutes(30)).Should().Be(new Energy(150));
        (new Power(-240) * TimeSpan.FromSeconds(30)).Should().Be(new Energy(-2));
    }

    [Fact]
    public void Divide_EnergyByPower_IsHowLongItTakesToMove()
    {
        (new Energy(2250) / new Power(250)).Should().Be(TimeSpan.FromHours(9));
    }

    [Fact]
    public void Arithmetic_Energy_WorksOnTheWattHours()
    {
        // Arrange
        var energy = new Energy(3000);

        // Act & Assert
        (energy - new Energy(600)).Should().Be(new Energy(2400));
        (-energy).Should().Be(new Energy(-3000));
        (energy * 0.9).Should().Be(new Energy(2700));
        (energy / 4).Should().Be(new Energy(750));
    }
}
