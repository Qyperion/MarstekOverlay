using MarstekOverlay.LocalApi.Exceptions;

using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.LocalApi.Transport;

public interface IJsonRpcTransport
{
    /// <returns>The <c>result</c> member of the station's answer.</returns>
    /// <exception cref="MarstekApiException">No answer in time, a network fault or an error answer.</exception>
    Task<JsonElement> InvokeAsync(StationEndpoint station, string method, JsonObject parameters, CancellationToken token);

    /// <summary>Sends one request to every reachable subnet and collects whatever answers arrive in time.</summary>
    /// <exception cref="MarstekApiException">The request could not be sent anywhere, or a network fault.</exception>
    Task<IReadOnlyList<BroadcastReply>> BroadcastAsync(
        int port,
        string method,
        JsonObject parameters,
        TimeSpan listenFor,
        CancellationToken token);
}
