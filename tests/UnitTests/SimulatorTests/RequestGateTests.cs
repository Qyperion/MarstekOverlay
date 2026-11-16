using FluentAssertions;

using MarstekOverlay.StationSimulator;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

public class RequestGateTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Admits_RequestsAtLeastTheSpacingApart_AreAllAdmitted()
    {
        // Arrange
        var gate = new RequestGate(TimeSpan.FromSeconds(0.5));

        // Act
        var admitted = new[] { 0, 0.5, 1.5 }.Select(seconds => gate.Admits(Start.AddSeconds(seconds))).ToList();

        // Assert
        admitted.Should().Equal(true, true, true);
    }

    [Fact]
    public void Admits_RequestSoonerThanTheSpacing_IsDroppedWithoutRestartingTheWait()
    {
        // Arrange
        var gate = new RequestGate(TimeSpan.FromSeconds(0.5));

        // Act
        var admitted = new[] { 0, 0.3, 0.6, 0.9, 1.2, 1.4 }.Select(seconds => gate.Admits(Start.AddSeconds(seconds))).ToList();

        // Assert
        admitted.Should().Equal(true, false, true, false, true, false);
    }

    [Fact]
    public void Admits_ZeroSpacing_AdmitsEverything()
    {
        // Arrange
        var gate = new RequestGate(TimeSpan.Zero);

        // Act
        var admitted = new[] { 0, 0, 0.1 }.Select(seconds => gate.Admits(Start.AddSeconds(seconds))).ToList();

        // Assert
        admitted.Should().Equal(true, true, true);
    }
}
