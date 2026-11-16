using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Monitoring.Forecast;

public class ForecastSettings
{
    public const double MinReserveSocPercent = 0;
    public const double MaxReserveSocPercent = 99;
    public const double MinEfficiencyPercent = 50;
    public const double MaxEfficiencyPercent = 100;

    /// <summary>The state of charge the forecast counts down to; it changes nothing on the station.</summary>
    public double ReserveSocPercent { get; set; } = 12;

    /// <summary>How much of what the battery gives up reaches the outputs. An allowance for conversion losses
    /// rather than a measured efficiency.</summary>
    public double DischargeEfficiencyPercent { get; set; } = 90;

    /// <summary>How much of what the station draws while charging ends up in the battery, as measured on
    /// Venus E 3.0.</summary>
    public double ChargeEfficiencyPercent { get; set; } = 97;

    /// <summary>What the battery takes in or gives up for what is measured at the outputs: it stores less than a
    /// charge draws and gives up more than a discharge delivers.</summary>
    /// <param name="atTheOutputs">Positive while charging.</param>
    public Energy InTheBattery(Energy atTheOutputs) => atTheOutputs.WattHours > 0
        ? Percent.Of(atTheOutputs, ChargeEfficiencyPercent)
        : atTheOutputs / Percent.ToFraction(DischargeEfficiencyPercent);

    /// <summary>The reverse of <see cref="InTheBattery"/>: what the outputs measure for what the battery takes
    /// in or gives up.</summary>
    /// <param name="inTheBattery">Positive while charging.</param>
    public Energy AtTheOutputs(Energy inTheBattery) => inTheBattery.WattHours > 0
        ? inTheBattery / Percent.ToFraction(ChargeEfficiencyPercent)
        : Percent.Of(inTheBattery, DischargeEfficiencyPercent);
}
