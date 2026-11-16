using MarstekOverlay.LocalApi.Models;

using System.Text.Json;

namespace MarstekOverlay.LocalApi.Mapping;

/// <summary>Every documented field may be missing or null depending on model and firmware, so each read is optional.</summary>
internal static class JsonElementExtensions
{
    extension(JsonElement element)
    {
        public double? Number(string name) =>
            element.Find(name) is { ValueKind: JsonValueKind.Number } value ? value.GetDouble() : null;

        public string? Text(string name) =>
            element.Find(name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

        public bool? Flag(string name) => element.Find(name) switch
        {
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            { ValueKind: JsonValueKind.Number } value => value.GetDouble() != 0,
            _ => null,
        };

        public Power? Watts(string name, double wattsPerUnit = 1) =>
            element.Number(name) is { } raw ? new Power(raw * wattsPerUnit) : null;

        public Energy? WattHours(string name, double wattHoursPerUnit = 1) =>
            element.Number(name) is { } raw ? new Energy(raw * wattHoursPerUnit) : null;

        public bool Has(string name) => element.Find(name) is not null;

        private JsonElement? Find(string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;
    }
}
