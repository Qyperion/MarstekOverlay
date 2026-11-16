using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Transport;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.UnitTests.TestSupport;

internal sealed class StubJsonRpcTransport : IJsonRpcTransport
{
    public Dictionary<string, string> Results { get; } = [];
    public List<BroadcastReply> BroadcastReplies { get; } = [];
    public List<(string Method, string Parameters)> Requests { get; } = [];

    public static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public Task<JsonElement> InvokeAsync(StationEndpoint station, string method, JsonObject parameters, CancellationToken token)
    {
        Requests.Add((method, parameters.ToJsonString()));
        return Task.FromResult(Parse(Results[method]));
    }

    public Task<IReadOnlyList<BroadcastReply>> BroadcastAsync(
        int port,
        string method,
        JsonObject parameters,
        TimeSpan listenFor,
        CancellationToken token)
    {
        Requests.Add((method, parameters.ToJsonString()));
        return Task.FromResult<IReadOnlyList<BroadcastReply>>(BroadcastReplies);
    }
}
