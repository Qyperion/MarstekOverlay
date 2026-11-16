using FluentAssertions;

using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Settings;

using Xunit;

namespace MarstekOverlay.UnitTests.MonitoringTests;

public class HistorySettingsValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(3650)]
    public void IsValid_RetentionWithinTheLimits_IsTrue(int days)
    {
        HistorySettingsValidator.IsValid(new HistorySettings { RetentionDays = days }).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    [InlineData(3651)]
    public void IsValid_RetentionOutsideTheLimits_IsFalse(int days)
    {
        HistorySettingsValidator.IsValid(new HistorySettings { RetentionDays = days }).Should().BeFalse();
    }

    [Fact]
    public void MinRetentionDays_CoversWhatTheChartsKeepInMemory()
    {
        TimeSpan.FromDays(HistorySettingsValidator.MinRetentionDays).Should().BeGreaterThanOrEqualTo(
            SampleHistory.KeptInMemory);
    }
}
