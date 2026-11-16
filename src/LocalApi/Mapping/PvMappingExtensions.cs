using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;

using System.Globalization;
using System.Text.Json;

namespace MarstekOverlay.LocalApi.Mapping;

internal static class PvMappingExtensions
{
    private const int FirstChannel = 1;
    private const int MaxChannelCount = 4;

    extension(JsonElement result)
    {
        public PvStatus ToPvStatus(FirmwareProfile profile)
        {
            var channels = Enumerable.Range(FirstChannel, MaxChannelCount)
                .Select(number => result.ToPvChannel(number, "pv" + number.ToString(CultureInfo.InvariantCulture), profile))
                .OfType<PvChannel>()
                .ToList();

            // Older firmware reports its only input without a channel number.
            if (channels.Count == 0 && result.ToPvChannel(FirstChannel, "pv", profile) is { } single)
                channels.Add(single);

            return new PvStatus { Channels = channels };
        }

        private PvChannel? ToPvChannel(int number, string prefix, FirmwareProfile profile)
        {
            var powerField = prefix + "_power";
            var voltageField = prefix + "_voltage";
            var currentField = prefix + "_current";
            if (!result.Has(powerField) && !result.Has(voltageField) && !result.Has(currentField))
                return null;

            return new PvChannel
            {
                Number = number,
                Power = result.Watts(powerField, number == FirstChannel ? profile.PvFirstChannelWattsPerUnit : 1),
                VoltageVolts = result.Number(voltageField),
                CurrentAmperes = result.Number(currentField),
                // The vendor document spells the single-channel field "PV_state".
                IsWorking = result.Flag(prefix + "_state") ?? result.Flag(prefix.ToUpperInvariant() + "_state"),
            };
        }
    }
}
