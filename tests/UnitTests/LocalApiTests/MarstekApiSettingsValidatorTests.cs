using FluentAssertions;

using MarstekOverlay.LocalApi.Settings;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class MarstekApiSettingsValidatorTests
{
    [Fact]
    public void IsValid_TheDefaults_IsTrue()
    {
        MarstekApiSettingsValidator.IsValid(new MarstekApiSettings()).Should().BeTrue();
    }

    [Fact]
    public void IsValid_NoRetriesAndNoDelay_IsTrue()
    {
        MarstekApiSettingsValidator.IsValid(new MarstekApiSettings { RetryCount = 0, MinDelayBetweenRequestsMs = 0 })
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 1, 1000, 3000)]
    [InlineData(3000, -1, 1000, 3000)]
    [InlineData(3000, 1, -1, 3000)]
    [InlineData(3000, 1, 1000, 0)]
    public void IsValid_ValueOutsideItsRange_IsFalse(int timeoutMs, int retries, int delayMs, int discoveryMs)
    {
        // Arrange
        var settings = new MarstekApiSettings
        {
            RequestTimeoutMs = timeoutMs,
            RetryCount = retries,
            MinDelayBetweenRequestsMs = delayMs,
            DiscoveryDurationMs = discoveryMs,
        };

        // Act
        var isValid = MarstekApiSettingsValidator.IsValid(settings);

        // Assert
        isValid.Should().BeFalse();
    }
}
