using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Values with their unit in the app's language. Digits follow the regional format of Windows, which is
/// a setting of its own and need not match the language.</summary>
public static class Units
{
    private const double WattHoursShownAsKilowattHours = 1000;

    // As many as Unit_WattHours shows: the unit is chosen by the value as it will read.
    private const int WattHourDecimals = 1;

    public static string Format(Power power) => Format(Strings.Unit_Watts, power.Watts);

    public static string Format(Energy energy) =>
        Math.Abs(Math.Round(energy.WattHours, WattHourDecimals, MidpointRounding.AwayFromZero)) >= WattHoursShownAsKilowattHours
            ? Format(Strings.Unit_KilowattHours, energy.KilowattHours)
            : Format(Strings.Unit_WattHours, energy.WattHours);

    public static string Percent(double percent) => Format(Strings.Unit_Percent, percent);

    public static string Celsius(double degrees) => Format(Strings.Unit_Celsius, degrees);

    public static string Volts(double volts) => Format(Strings.Unit_Volts, volts);

    public static string Amperes(double amperes) => Format(Strings.Unit_Amperes, amperes);

    public static string Dbm(double dbm) => Format(Strings.Unit_Dbm, dbm);

    private static string Format(string template, double value) => Phrases.Format(template, value);
}
