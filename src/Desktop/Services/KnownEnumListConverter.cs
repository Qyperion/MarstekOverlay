using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Reads a list of enum names and leaves out the ones this build does not know, so that a name from
/// another version of the app costs one entry rather than the whole file.</summary>
/// <param name="skipped">Told of every entry left out: the name of the enum and the entry as the file has it.</param>
public sealed class KnownEnumListConverter(Action<string, string> skipped) : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(List<>)
        && typeToConvert.GetGenericArguments()[0].IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(ListConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]);

        return Activator.CreateInstance(converterType, skipped) as JsonConverter
            ?? throw new InvalidOperationException($"No converter for {typeToConvert}");
    }

    private sealed class ListConverter<TEnum>(Action<string, string> skipped) : JsonConverter<List<TEnum>>
        where TEnum : struct, Enum
    {
        public override List<TEnum> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"A list of {typeof(TEnum).Name} names was expected");

            var items = new List<TEnum>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String && Known(reader.GetString()) is { } item)
                {
                    items.Add(item);
                    continue;
                }

                skipped(typeof(TEnum).Name, reader.TokenType == JsonTokenType.String ? reader.GetString() ?? string.Empty : reader.TokenType.ToString());
                reader.Skip();
            }

            return items;
        }

        public override void Write(Utf8JsonWriter writer, List<TEnum> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                writer.WriteStringValue(item.ToString());

            writer.WriteEndArray();
        }

        // Only a name: a number would mean something else once the enum is reordered.
        private static TEnum? Known(string? name) =>
            Enum.GetNames<TEnum>().FirstOrDefault(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase)) is { } match
                ? Enum.Parse<TEnum>(match)
                : null;
    }
}
