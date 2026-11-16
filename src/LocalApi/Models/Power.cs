namespace MarstekOverlay.LocalApi.Models;

public readonly record struct Power(double Watts)
{
    public static readonly Power Zero = new(0);

    public static Power operator +(Power a, Power b) => new(a.Watts + b.Watts);
    public static Power operator -(Power a, Power b) => new(a.Watts - b.Watts);
    public static Power operator -(Power power) => new(-power.Watts);

    public static Energy operator *(Power power, TimeSpan duration) => new(power.Watts * duration.TotalHours);
}
