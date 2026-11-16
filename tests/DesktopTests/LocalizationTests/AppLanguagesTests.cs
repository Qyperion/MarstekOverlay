using FluentAssertions;

using MarstekOverlay.Desktop.Localization;

using System.Globalization;

using Xunit;

namespace MarstekOverlay.DesktopTests.LocalizationTests;

public sealed class AppLanguagesTests : IDisposable
{
    private readonly CultureInfo _displayLanguageBefore = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _displayLanguageBefore;

    [Theory]
    [InlineData("uk")]
    [InlineData("UK")]
    public void Resolve_SavedLanguage_IsThatLanguage(string savedCode)
    {
        // Act
        var language = AppLanguages.Resolve(savedCode);

        // Assert
        language.Should().Be(AppLanguages.Ukrainian);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("de")]
    public void Resolve_NoLanguageTheAppSpeaks_IsEnglishWhateverWindowsSpeaks(string? savedCode)
    {
        // Arrange
        CultureInfo.CurrentUICulture = AppLanguages.Ukrainian;

        // Act
        var language = AppLanguages.Resolve(savedCode);

        // Assert
        language.Should().Be(AppLanguages.English);
    }
}
