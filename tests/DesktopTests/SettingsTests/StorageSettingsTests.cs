using FluentAssertions;

using MarstekOverlay.Desktop.Settings;

using Xunit;

namespace MarstekOverlay.DesktopTests.SettingsTests;

public class StorageSettingsTests
{
    [Fact]
    public void RootDirectory_RelativePath_IsTakenFromTheFolderOfTheExe()
    {
        // Arrange
        var settings = new StorageSettings { RootDirectory = "Data" };

        // Act
        var root = settings.RootDirectory;

        // Assert
        root.Should().Be(Path.Combine(ExeFolder.Location, "Data"));
    }

    [Fact]
    public void IsValid_TheDefaults_IsTrue()
    {
        // Act
        var isValid = StorageSettingsValidator.IsValid(new StorageSettings());

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "history.db")]
    [InlineData("  ", "history.db")]
    [InlineData(@"Settings\", "history.db")]
    [InlineData("settings.json", "")]
    [InlineData("settings.json", "Settings.JSON")]
    public void IsValid_FileNameThatNamesNoFileOfItsOwn_IsFalse(string userSettingsFileName, string databaseFileName)
    {
        // Arrange
        var settings = new StorageSettings { UserSettingsFileName = userSettingsFileName, DatabaseFileName = databaseFileName };

        // Act
        var isValid = StorageSettingsValidator.IsValid(settings);

        // Assert
        isValid.Should().BeFalse();
    }
}
