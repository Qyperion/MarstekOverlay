using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Desktop.Presentation;

public static class StoredDataExtensions
{
    extension(KeptPeriod period)
    {
        public string DisplayName => period switch
        {
            KeptPeriod.Nothing => Strings.KeptPeriod_Nothing,
            KeptPeriod.Day => Strings.KeptPeriod_Day,
            KeptPeriod.Week => Strings.KeptPeriod_Week,
            KeptPeriod.Month => Strings.KeptPeriod_Month,
            _ => period.ToString(),
        };
    }
}
