using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.Monitoring;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class ErrorPresenterTests : LocalizedTests
{
    [Fact]
    public void Describe_KindOfErrorWithNoWording_Throws()
    {
        // Act
        var describe = () => ErrorPresenter.Describe(new UnwordedError());

        // Assert
        describe.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_RequestWithoutAnAnswer_NamesTheRequestAndHowLongItWaited(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = ErrorPresenter.Describe(new UnansweredRequest("ES.GetStatus", TimeSpan.FromSeconds(3)));

        // Assert
        text.Should().Be(Translated("The station did not answer ES.GetStatus within 3 s."));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_TimeoutOfAFractionOfASecond_FollowsTheRegionalFormat(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = ErrorPresenter.Describe(new UnansweredRequest("Bat.GetStatus", TimeSpan.FromMilliseconds(2500)));

        // Assert
        text.Should().Be(Translated("The station did not answer Bat.GetStatus within 2.5 s."));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_RequestTheStationRefused_PassesItsWordsAndCodeOn(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var text = ErrorPresenter.Describe(new RefusedRequest("ES.GetMode", -32602, "Invalid params"));

        // Assert
        text.Should().Be(Translated("The station answered ES.GetMode with an error: Invalid params (code -32602)."));
    }

    [Fact]
    public void Describe_AnythingRarer_IsLeftAsTheSystemWordsIt()
    {
        // Arrange
        Speak(Ukrainian);

        // Act
        var text = ErrorPresenter.Describe(new UnexpectedFailure("ES.GetStatus to 192.168.1.50:30000 failed: no route to host"));

        // Assert
        text.Should().Be("ES.GetStatus to 192.168.1.50:30000 failed: no route to host");
    }

    [Fact]
    public void Describe_SeveralErrors_PutsEachOnALineOfItsOwn()
    {
        // Arrange
        PollError[] errors =
        [
            new UnansweredRequest("ES.GetStatus", TimeSpan.FromSeconds(3)),
            new UnexpectedFailure("No route to host"),
        ];

        // Act
        var text = ErrorPresenter.Describe(errors);

        // Assert
        text.Should().Be("The station did not answer ES.GetStatus within 3 s." + Environment.NewLine + "No route to host");
    }

    [Fact]
    public void Describe_CleanPoll_HasNothingToTell()
    {
        ErrorPresenter.Describe([]).Should().BeNull();
    }

    private sealed record UnwordedError : PollError;
}
