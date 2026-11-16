using MarstekOverlay.LocalApi.Firmware;

namespace MarstekOverlay.LocalApi.Models;

public sealed record DeviceInfo
{
    /// <summary>A display name ("VenusE 3.0") or a vendor SKU ("VNSE3-0"), whichever the firmware reports.</summary>
    public string Model { get; init; } = string.Empty;

    public int? FirmwareVersion { get; init; }
    public string? BluetoothMac { get; init; }
    public string? WifiMac { get; init; }
    public string? WifiName { get; init; }

    /// <summary>Can stay at the previous DHCP address for hours; the address an answer came from is the one to use.</summary>
    public string? ReportedIp { get; init; }

    public FirmwareProfile Profile => FirmwareProfile.For(Model, FirmwareVersion);
}
