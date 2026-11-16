using System.Globalization;

namespace MarstekOverlay.Desktop.Localization;

/// <summary>Each language has a <c>Strings.&lt;code&gt;.resx</c> next to this file; English lives in <c>Strings.resx</c>.</summary>
public static class AppLanguages
{
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    public static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk");

    public static IReadOnlyList<CultureInfo> All { get; } = [English, Ukrainian];

    /// <returns>The saved choice; English without one, or when the app does not speak it.</returns>
    public static CultureInfo Resolve(string? savedCode) => Find(savedCode) ?? English;

    /// <summary>A language is offered under its own name, so it can be found by someone who cannot read the current one.</summary>
    public static string NativeName(CultureInfo language) => language.TextInfo.ToTitleCase(language.NativeName);

    private static CultureInfo? Find(string? code) =>
        All.FirstOrDefault(language => string.Equals(language.Name, code, StringComparison.OrdinalIgnoreCase));
}
