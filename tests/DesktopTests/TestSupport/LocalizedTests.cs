using MarstekOverlay.Desktop.Localization;

using System.Globalization;
using System.Resources;

namespace MarstekOverlay.DesktopTests.TestSupport;

/// <summary>Every test starts in English with the regional format of the United States and leaves the language
/// and the format the way it found them. Both carry the culture data of <see cref="PinnedCultures"/>.</summary>
public abstract class LocalizedTests : IDisposable
{
    public const string English = "en";
    public const string Ukrainian = "uk";

    public const string RegionalFormatOfTheUnitedStates = "en-US";
    public const string RegionalFormatOfUkraine = "uk-UA";

    private static readonly Dictionary<string, string> UsualFormats = new()
    {
        [English] = RegionalFormatOfTheUnitedStates,
        [Ukrainian] = RegionalFormatOfUkraine,
    };

    private static readonly ResourceManager Translations =
        new($"{typeof(LocalizedTests).Namespace}.Translations", typeof(LocalizedTests).Assembly);

    private readonly CultureInfo _languageBefore = Localizer.Instance.Language;
    private readonly CultureInfo _formatBefore = CultureInfo.CurrentCulture;

    protected LocalizedTests()
    {
        Speak(English);
    }

    public virtual void Dispose()
    {
        Localizer.Instance.Use(_languageBefore);
        CultureInfo.CurrentCulture = _formatBefore;
    }

    /// <param name="regionalFormat">The format of numbers set in Windows, which need not match the language;
    /// the one usual for the language unless given.</param>
    protected static void Speak(string language, string? regionalFormat = null)
    {
        var pinned = PinnedCultures.Get(language);
        var current = Localizer.Instance.Language;

        // The localizer takes an equal culture for no change, and the one it holds may carry the data of the machine.
        if (current.Equals(pinned) && !ReferenceEquals(current, pinned))
            Localizer.Instance.Use(CultureInfo.InvariantCulture);

        Localizer.Instance.Use(pinned);

        CultureInfo.CurrentCulture = PinnedCultures.Get(regionalFormat ?? UsualFormats[language]);
    }

    /// <summary>What the app is expected to say in the language the test speaks. The tests word their
    /// expectations in English; the same texts in another language are kept in
    /// <c>TestSupport/Translations.&lt;code&gt;.resx</c> under the English ones, as code holds English only.</summary>
    protected static string Translated(string english)
    {
        var language = Localizer.Instance.Language;
        if (language.Equals(AppLanguages.English))
            return english;

        return Translations.GetResourceSet(language, createIfNotExists: true, tryParents: false)?.GetString(english)
            ?? throw new InvalidOperationException($"Translations.{language.Name}.resx has no entry for \"{english}\".");
    }
}
