using FluentAssertions;

using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.DesktopTests.TestSupport;

using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

using Xunit;

namespace MarstekOverlay.DesktopTests.LocalizationTests;

public class StringsTests : LocalizedTests
{
    // "{0}" and "{1:00}" alike: what a text is filled in with, and how.
    private static readonly Regex Placeholder = new(@"\{\d+(:[^}]*)?\}");

    public static TheoryData<string> Translations =>
        [.. AppLanguages.All.Where(language => !language.Equals(AppLanguages.English)).Select(language => language.Name)];

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_HasEveryTextAndNoneOfItsOwn(string language)
    {
        // Act
        var translated = TextsOf(CultureInfo.GetCultureInfo(language));

        // Assert
        translated.Keys.Should().BeEquivalentTo(TextsOf(CultureInfo.InvariantCulture).Keys);
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_FillsEveryTextInWithTheSameValues(string language)
    {
        // Arrange
        var english = TextsOf(CultureInfo.InvariantCulture);
        var translated = TextsOf(CultureInfo.GetCultureInfo(language));

        // Act
        var filledInDifferently = english.Keys
            .Where(translated.ContainsKey)
            .Where(key => !PlaceholdersOf(english[key]).SequenceEqual(PlaceholdersOf(translated[key])))
            .ToList();

        // Assert
        filledInDifferently.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void Translation_LeavesNoTextEmpty(string language)
    {
        TextsOf(CultureInfo.GetCultureInfo(language)).Where(text => string.IsNullOrWhiteSpace(text.Value)).Should().BeEmpty();
    }

    [Fact]
    public void Localizer_MissingKey_ShowsUpAsItselfInBrackets()
    {
        Localizer.Instance["No_Such_Key"].Should().Be("[No_Such_Key]");
    }

    [Fact]
    public void Localizer_Use_SwitchesBothTheCodeAndTheBindings()
    {
        // Arrange
        var raised = new List<string?>();
        PropertyChangedEventHandler record = (_, change) => raised.Add(change.PropertyName);
        Localizer.Instance.PropertyChanged += record;

        // Act
        Speak(Ukrainian);
        Localizer.Instance.PropertyChanged -= record;

        // Assert
        Localizer.Instance[nameof(Strings.Common_Yes)].Should().Be(Strings.Common_Yes).And.NotBe("yes");
        raised.Should().Equal("Item[]");
    }

    private static Dictionary<string, string> TextsOf(CultureInfo language) =>
        Strings.ResourceManager.GetResourceSet(language, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(text => (string)text.Key, text => (string)text.Value!);

    private static IEnumerable<string> PlaceholdersOf(string text) =>
        Placeholder.Matches(text).Select(match => match.Value).Order();
}
