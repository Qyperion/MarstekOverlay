namespace MarstekOverlay.LocalApi;

/// <summary>Wire names are case-sensitive: the station answers <c>Wifi.GetStatus</c> and ignores <c>wifi.GetStatus</c>.</summary>
public static class MarstekMethods
{
    public const string GetDevice = "Marstek.GetDevice";
    public const string WifiGetStatus = "Wifi.GetStatus";
    public const string BluetoothGetStatus = "BLE.GetStatus";
    public const string BatteryGetStatus = "Bat.GetStatus";
    public const string PvGetStatus = "PV.GetStatus";
    public const string EnergySystemGetStatus = "ES.GetStatus";
    public const string EnergySystemGetMode = "ES.GetMode";
    public const string EnergyMeterGetStatus = "EM.GetStatus";
}
