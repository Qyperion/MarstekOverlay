namespace MarstekOverlay.LocalApi.Models;

/// <summary>Numbered as on the wire: some firmware reports the mode as this integer instead of its name.</summary>
public enum OperatingMode
{
    Auto = 0,
    Ai = 1,
    Manual = 2,
    Passive = 3,
    Ups = 4,
}
