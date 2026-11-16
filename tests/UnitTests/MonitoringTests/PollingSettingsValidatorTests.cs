using FluentAssertions;

using MarstekOverlay.Monitoring.Settings;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class PollingSettingsValidatorTests
{
    [Theory]
    [InlineData(30, 300)]
    [InlineData(1, 1)]
    [InlineData(120, 3600)]
    public void IsValid_IntervalsWithinTheLimits_IsTrue(int interval, int slowInterval)
    {
        PollingSettingsValidator.IsValid(new PollingSettings { IntervalSeconds = interval, SlowIntervalSeconds = slowInterval })
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(121, 300)]
    [InlineData(60, 30)]
    [InlineData(30, 3601)]
    public void IsValid_IntervalsOutsideTheLimits_IsFalse(int interval, int slowInterval)
    {
        PollingSettingsValidator.IsValid(new PollingSettings { IntervalSeconds = interval, SlowIntervalSeconds = slowInterval })
            .Should().BeFalse();
    }

    [Fact]
    public void IsValid_MethodsLeftOutByName_IsTrue()
    {
        PollingSettingsValidator.IsValid(new PollingSettings { DisabledMethods = ["Bat.GetStatus"] }).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValid_BlankMethodLeftOut_IsFalse(string method)
    {
        PollingSettingsValidator.IsValid(new PollingSettings { DisabledMethods = [method] }).Should().BeFalse();
    }
}
