using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;

using System.Security;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class FailureReasonsTests : LocalizedTests
{
    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void OfFile_AccessDenied_SaysSoInTheLanguageOfTheApp(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var reasons = new[]
        {
            FailureReasons.OfFile(new UnauthorizedAccessException("Access to the path is denied.")),
            FailureReasons.OfFile(new SecurityException("Requested access is not allowed.")),
        };

        // Assert
        reasons.Should().AllBe(Translated("access is denied"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void OfFile_FileOrFolderUnavailable_SaysTheFileIsInUseOrCannotBeReached(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var reasons = new[]
        {
            FailureReasons.OfFile(new IOException("The process cannot access the file.")),
            FailureReasons.OfFile(new DirectoryNotFoundException("Could not find a part of the path.")),
        };

        // Assert
        reasons.Should().AllBe(Translated("the file is in use by another program or cannot be reached"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void OfFile_AnyOtherFailure_SaysAnUnexpectedErrorOccurred(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var reason = FailureReasons.OfFile(new InvalidOperationException("Something else."));

        // Assert
        reason.Should().Be(Translated("an unexpected error occurred"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void OfRegistry_AccessDenied_SaysSoInTheLanguageOfTheApp(string language)
    {
        // Arrange
        Speak(language);

        // Act
        var reasons = new[]
        {
            FailureReasons.OfRegistry(new SecurityException("Requested registry access is not allowed.")),
            FailureReasons.OfRegistry(new UnauthorizedAccessException("Cannot write to the registry key.")),
        };

        // Assert
        reasons.Should().AllBe(Translated("access is denied"));
    }

    [Fact]
    public void OfRegistry_KeyDeletedUnderTheApp_IsNoFileInUse()
    {
        // Act
        var reason = FailureReasons.OfRegistry(
            new IOException("Illegal operation attempted on a registry key that has been marked for deletion."));

        // Assert
        reason.Should().Be("an unexpected error occurred");
    }
}
