using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.LocalApi.Firmware;

namespace MarstekOverlay.Desktop.Presentation;

public static class DeviceFamilyExtensions
{
    extension(DeviceFamily family)
    {
        public string DisplayName => family switch
        {
            DeviceFamily.VenusA => Strings.DeviceFamily_VenusA,
            DeviceFamily.VenusC => Strings.DeviceFamily_VenusC,
            DeviceFamily.VenusD => Strings.DeviceFamily_VenusD,
            DeviceFamily.VenusE => Strings.DeviceFamily_VenusE,
            DeviceFamily.VenusE2 => Strings.DeviceFamily_VenusE2,
            DeviceFamily.VenusEMini => Strings.DeviceFamily_VenusEMini,
            _ => Strings.DeviceFamily_Unknown,
        };
    }
}
