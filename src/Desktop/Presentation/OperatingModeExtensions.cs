using MarstekOverlay.LocalApi.Models;

namespace MarstekOverlay.Desktop.Presentation;

public static class OperatingModeExtensions
{
    extension(OperatingMode mode)
    {
        // Product terms, written the way the Marstek app and the Open API spell them in every language.
        public string DisplayName => mode switch
        {
            OperatingMode.Ai => "AI",
            OperatingMode.Ups => "UPS",
            _ => mode.ToString(),
        };
    }
}
