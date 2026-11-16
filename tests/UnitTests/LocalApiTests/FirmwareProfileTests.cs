using FluentAssertions;

using MarstekOverlay.LocalApi.Firmware;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class FirmwareProfileTests
{
    private const double Precision = 1e-9;

    [Theory]
    [InlineData("VenusE 3.0", DeviceFamily.VenusE)]
    [InlineData("VNSE3-0", DeviceFamily.VenusE)]
    [InlineData("VenusE", DeviceFamily.VenusE2)]
    [InlineData("VenusE 2.0", DeviceFamily.VenusE2)]
    [InlineData("HMG-50", DeviceFamily.VenusE2)]
    [InlineData("VNSE2", DeviceFamily.VenusE2)]
    [InlineData("Venus E mini", DeviceFamily.VenusEMini)]
    [InlineData("VNSEM-0", DeviceFamily.VenusEMini)]
    [InlineData("VenusA 3.0", DeviceFamily.VenusA)]
    [InlineData("VNSA-0", DeviceFamily.VenusA)]
    [InlineData("VenusC", DeviceFamily.VenusC)]
    [InlineData("Venus D", DeviceFamily.VenusD)]
    [InlineData("VNSD-0", DeviceFamily.VenusD)]
    [InlineData("VenusE Pro", DeviceFamily.Unknown)]
    [InlineData("Jupiter", DeviceFamily.Unknown)]
    [InlineData("", DeviceFamily.Unknown)]
    [InlineData(null, DeviceFamily.Unknown)]
    public void Parse_RecognizesDisplayNamesAndSkus(string? model, DeviceFamily expected)
    {
        DeviceFamilyParser.Parse(model).Should().Be(expected);
    }

    [Theory]
    [InlineData("VenusE 3.0", 147, 1)]
    [InlineData("VenusE 3.0", 150, 0.1)]
    [InlineData("VenusE 3.0", null, 1)]
    [InlineData("VenusA 3.0", 149, 1)]
    [InlineData("VenusA 3.0", 150, 0.1)]
    [InlineData("VenusC", 153, 1)]
    [InlineData("VenusC", 155, 0.1)]
    [InlineData("VenusE", 1581, 0.1)]
    [InlineData("Venus E mini", 145, 1)]
    [InlineData("VNSEM-0", 301, 0.1)]
    [InlineData("VenusE Pro", 1508, 1)]
    public void For_MeterEnergyScale_FollowsFamilyAndGeneration(string model, int? version, double expectedWattHoursPerUnit)
    {
        FirmwareProfile.For(model, version).MeterEnergyWattHoursPerUnit.Should().BeApproximately(expectedWattHoursPerUnit, Precision);
    }

    [Theory]
    [InlineData("VenusA 3.0", 148, 1)]
    [InlineData("VenusA 3.0", 149, 10)]
    [InlineData("Venus D", 149, 1)]
    [InlineData("Venus D", 150, 10)]
    [InlineData("VenusE 3.0", 150, 1)]
    [InlineData("VenusA 3.0", null, 1)]
    public void For_PvEnergyScale_FollowsFamilyAndGeneration(string model, int? version, double expectedWattHoursPerUnit)
    {
        FirmwareProfile.For(model, version).PvEnergyWattHoursPerUnit.Should().BeApproximately(expectedWattHoursPerUnit, Precision);
    }

    [Theory]
    [InlineData("VenusA 3.0", true, 0.1)]
    [InlineData("Venus D", true, 0.1)]
    [InlineData("VenusE 3.0", false, 1)]
    [InlineData("VenusC", false, 1)]
    public void For_Pv_IsLimitedToFamiliesWithSolarInputs(string model, bool hasPv, double firstChannelWattsPerUnit)
    {
        // Act
        var profile = FirmwareProfile.For(model, 150);

        // Assert
        profile.HasPv.Should().Be(hasPv);
        profile.PvFirstChannelWattsPerUnit.Should().BeApproximately(firstChannelWattsPerUnit, Precision);
    }

    [Fact]
    public void Unknown_ScalesNothing()
    {
        FirmwareProfile.Unknown.Should().Be(FirmwareProfile.For("Jupiter", 999));
        FirmwareProfile.Unknown.MeterEnergyWattHoursPerUnit.Should().Be(1);
        FirmwareProfile.Unknown.PvEnergyWattHoursPerUnit.Should().Be(1);
        FirmwareProfile.Unknown.PvFirstChannelWattsPerUnit.Should().Be(1);
    }
}
