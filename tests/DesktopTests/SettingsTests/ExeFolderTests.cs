using FluentAssertions;

using MarstekOverlay.Desktop.Settings;

using Xunit;

namespace MarstekOverlay.DesktopTests.SettingsTests;

public class ExeFolderTests
{
    [Fact]
    public void Location_BuildThatIsNotPackedIntoOneFile_IsTheBaseDirectory()
    {
        // Act
        var folder = ExeFolder.Location;

        // Assert
        folder.Should().Be(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
    }
}
