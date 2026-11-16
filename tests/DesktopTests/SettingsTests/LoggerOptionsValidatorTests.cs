using FluentAssertions;

using MarstekOverlay.Desktop.Settings;

using Xunit;

namespace MarstekOverlay.DesktopTests.SettingsTests;

public class LoggerOptionsValidatorTests
{
    [Fact]
    public void IsValid_TheDefaults_IsTrue()
    {
        LoggerOptionsValidator.IsValid(new LoggerOptions()).Should().BeTrue();
    }

    [Theory]
    [InlineData("Verbose")]
    [InlineData("Debug")]
    [InlineData("Fatal")]
    public void IsValid_LevelOfSerilog_IsTrue(string level)
    {
        LoggerOptionsValidator.IsValid(new LoggerOptions { FileLogMinimumLevel = level }).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("debug")]
    [InlineData("Trace")]
    [InlineData("7")]
    public void IsValid_LevelSerilogDoesNotKnowByThatName_IsFalse(string level)
    {
        LoggerOptionsValidator.IsValid(new LoggerOptions { FileLogMinimumLevel = level }).Should().BeFalse();
    }

    [Fact]
    public void IsValid_LogWithoutAFile_IsFalse()
    {
        LoggerOptionsValidator.IsValid(new LoggerOptions { LogFilePath = " " }).Should().BeFalse();
    }

    [Fact]
    public void IsValid_LogSwitchedOff_NeedsNoFile()
    {
        LoggerOptionsValidator.IsValid(new LoggerOptions { IsEnabledFileLog = false, LogFilePath = string.Empty })
            .Should().BeTrue();
    }
}
