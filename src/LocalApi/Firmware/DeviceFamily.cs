namespace MarstekOverlay.LocalApi.Firmware;

public enum DeviceFamily
{
    Unknown,
    VenusA,
    VenusC,
    VenusD,

    /// <summary>Venus E 3.0. The 2.x generation is a different platform, see <see cref="VenusE2"/>.</summary>
    VenusE,

    /// <summary>The HMG-50 platform, which shares its firmware line with Venus C.</summary>
    VenusE2,

    VenusEMini,
}
