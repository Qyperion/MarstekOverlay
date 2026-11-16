namespace MarstekOverlay.LocalApi.Firmware;

public static class DeviceFamilyParser
{
    // HMG-50 firmware names itself plain "VenusE", so a bare name is the 2.x generation, never 3.0.
    private const string BareVenusE = "VENUSE";

    public static DeviceFamily Parse(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return DeviceFamily.Unknown;

        var name = string.Concat(model.Where(char.IsLetterOrDigit)).ToUpperInvariant();

        if (StartsWithAny(name, "VENUSEMINI", "VNSEM"))
            return DeviceFamily.VenusEMini;
        if (name == BareVenusE || StartsWithAny(name, "VENUSE2", "VNSE2", "HMG50"))
            return DeviceFamily.VenusE2;
        if (StartsWithAny(name, "VENUSE3", "VNSE3"))
            return DeviceFamily.VenusE;
        if (StartsWithAny(name, "VENUSA", "VNSA"))
            return DeviceFamily.VenusA;
        if (StartsWithAny(name, "VENUSC", "VNSC"))
            return DeviceFamily.VenusC;
        if (StartsWithAny(name, "VENUSD", "VNSD"))
            return DeviceFamily.VenusD;

        return DeviceFamily.Unknown;
    }

    private static bool StartsWithAny(string name, params ReadOnlySpan<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
