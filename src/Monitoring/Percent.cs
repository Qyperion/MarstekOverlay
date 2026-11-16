using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring;

/// <summary>Shares given in percent, as the station reports the charge and the settings give the rest.</summary>
public static class Percent
{
    /// <summary>All of it: a full battery, a loss-free conversion.</summary>
    public const double Whole = 100;

    public static double ToFraction(double percent) => percent / Whole;

    public static Energy Of(Energy whole, double percent) => whole * percent / Whole;
}
