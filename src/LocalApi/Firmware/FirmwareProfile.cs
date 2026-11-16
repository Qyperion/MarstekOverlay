namespace MarstekOverlay.LocalApi.Firmware;

/// <summary>
/// How a given model and firmware encodes the values whose unit changed between Open API revisions. An unknown
/// model or version scales nothing: a value shown as sent is safer than one multiplied on a guess.
/// </summary>
public sealed record FirmwareProfile
{
    private const double DeciwattsToWatts = 0.1;
    private const double HundredthsOfKilowattHourToWattHours = 10;
    private const double TenthsOfWattHourToWattHours = 0.1;

    // Control firmware reports "158.1" as 1581, while every plain generation stays below this.
    private const int FirstVersionWithMinorDigit = 1000;

    public static readonly FirmwareProfile Unknown = new();

    public DeviceFamily Family { get; private init; }

    public bool HasPv => Family is DeviceFamily.VenusA or DeviceFamily.VenusD;

    public double PvEnergyWattHoursPerUnit { get; private init; } = 1;

    /// <summary>Only the first PV input is sent in deciwatts; the others arrive in watts.</summary>
    public double PvFirstChannelWattsPerUnit => HasPv ? DeciwattsToWatts : 1;

    public double MeterEnergyWattHoursPerUnit { get; private init; } = 1;

    public static FirmwareProfile For(string? model, int? version)
    {
        var family = DeviceFamilyParser.Parse(model);
        var generation = version >= FirstVersionWithMinorDigit ? version / 10 : version;

        return new FirmwareProfile
        {
            Family = family,
            PvEnergyWattHoursPerUnit =
                generation >= FirstGenerationWithScaledPvEnergy(family) ? HundredthsOfKilowattHourToWattHours : 1,
            MeterEnergyWattHoursPerUnit =
                generation >= FirstGenerationWithScaledMeterEnergy(family) ? TenthsOfWattHourToWattHours : 1,
        };
    }

    private static int? FirstGenerationWithScaledPvEnergy(DeviceFamily family) => family switch
    {
        DeviceFamily.VenusA => 149,
        DeviceFamily.VenusD => 150,
        _ => null,
    };

    private static int? FirstGenerationWithScaledMeterEnergy(DeviceFamily family) => family switch
    {
        DeviceFamily.VenusA or DeviceFamily.VenusD or DeviceFamily.VenusE or DeviceFamily.VenusEMini => 150,
        DeviceFamily.VenusC or DeviceFamily.VenusE2 => 155,
        _ => null,
    };
}
