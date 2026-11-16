using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.LocalApi.Transport;

internal static class JsonRpcMessage
{
    public static byte[] SerializeRequest(int id, string method, JsonObject parameters)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", id);
            writer.WriteString("method", method);
            writer.WritePropertyName("params");
            parameters.WriteTo(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <returns>Null for anything that is not an answer to a request, such as foreign traffic on the port.</returns>
    public static JsonRpcResponse? ParseResponse(ReadOnlyMemory<byte> datagram)
    {
        try
        {
            using var document = JsonDocument.Parse(datagram);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            int? id = root.TryGetProperty("id", out var idElement)
                && idElement.ValueKind == JsonValueKind.Number
                && idElement.TryGetInt32(out var number)
                    ? number
                    : null;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                return new JsonRpcResponse(id, default, ParseError(error));

            return root.TryGetProperty("result", out var result)
                ? new JsonRpcResponse(id, result.Clone(), Error: null)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonRpcError ParseError(JsonElement error)
    {
        var code = error.TryGetProperty("code", out var codeElement)
            && codeElement.ValueKind == JsonValueKind.Number
            && codeElement.TryGetInt32(out var number)
            ? number
            : 0;
        var message = error.TryGetProperty("message", out var messageElement)
            && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;

        return new JsonRpcError(code, message ?? "Unknown error");
    }
}

internal sealed record JsonRpcResponse(int? Id, JsonElement Result, JsonRpcError? Error);

internal sealed record JsonRpcError(int Code, string Message);
