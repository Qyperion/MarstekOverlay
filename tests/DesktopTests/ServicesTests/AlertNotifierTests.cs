using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class AlertNotifierTests : LocalizedTests
{
    private readonly StationHarness _station = new(Moments.Local(14, 12, 0));
    private readonly RecordingChannel _channel = new();

    public override void Dispose()
    {
        _station.Dispose();
        base.Dispose();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Start_AlertRaised_IsWordedInTheLanguageOfTheAppAndSentOut(string language)
    {
        // Arrange
        Speak(language);
        using var notifier = new AlertNotifier(_station.Pipeline, _channel, _station.Time);

        // Act
        notifier.Start();
        LoseMains();

        // Assert
        var sent = _channel.Sent.Should().ContainSingle().Subject;
        sent.Title.Should().Be(Translated("Mains power lost"));
        sent.Text.Should().Be(Translated("The station is running on the battery. Charge: 89%."));
    }

    [Fact]
    public void Dispose_AlertRaisedAfterwards_IsNotSent()
    {
        // Arrange
        var notifier = new AlertNotifier(_station.Pipeline, _channel, _station.Time);
        notifier.Start();

        // Act
        notifier.Dispose();
        LoseMains();

        // Assert
        _channel.Sent.Should().BeEmpty();
    }

    [Fact]
    public void Create_NotStarted_SendsNothing()
    {
        // Arrange
        using var notifier = new AlertNotifier(_station.Pipeline, _channel, _station.Time);

        // Act
        LoseMains();

        // Assert
        _channel.Sent.Should().BeEmpty();
    }

    private void LoseMains()
    {
        _station.Answer(at => StationReadings.Charging(at, soc: 90));
        _station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
        _station.AnswerAfterInterval(at => StationReadings.OnBattery(at, soc: 89));
    }

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<Notification> Sent { get; } = [];

        public void Notify(Notification notification) => Sent.Add(notification);
    }
}
