namespace MarstekOverlay.LocalApi.Models;

public readonly record struct Energy(double WattHours)
{
    private const double WattHoursPerKilowattHour = 1000;

    public double KilowattHours => WattHours / WattHoursPerKilowattHour;

    public static Energy operator +(Energy a, Energy b) => new(a.WattHours + b.WattHours);
    public static Energy operator -(Energy a, Energy b) => new(a.WattHours - b.WattHours);
    public static Energy operator -(Energy energy) => new(-energy.WattHours);
    public static Energy operator *(Energy energy, double factor) => new(energy.WattHours * factor);
    public static Energy operator /(Energy energy, double divisor) => new(energy.WattHours / divisor);

    /// <summary>How long <paramref name="power"/> takes to move <paramref name="energy"/>.</summary>
    public static TimeSpan operator /(Energy energy, Power power) => TimeSpan.FromHours(energy.WattHours / power.Watts);
}
