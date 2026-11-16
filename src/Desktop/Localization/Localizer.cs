using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace MarstekOverlay.Desktop.Localization;

/// <summary>
/// Switches the language of the running app. Code reads texts from <see cref="Strings"/>; XAML binds to this
/// object's indexer through <see cref="TextExtension"/>, which is what lets open windows follow a switch.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private Localizer()
    {
        Strings.Culture = Language;
    }

    /// <summary>XAML cannot reach the service container, so the one instance is also available statically.</summary>
    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? LanguageChanged;

    public CultureInfo Language { get; private set; } = AppLanguages.Resolve(savedCode: null);

    /// <summary>A missing key shows up as itself in brackets instead of leaving a blank spot in the window.</summary>
    public string this[string key] => Strings.ResourceManager.GetString(key, Language) ?? $"[{key}]";

    public void Use(CultureInfo language)
    {
        if (language.Equals(Language))
            return;

        Language = language;
        Strings.Culture = language;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        LanguageChanged?.Invoke();
    }
}
