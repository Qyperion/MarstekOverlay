using System.Net;
using System.Text.Json;

namespace MarstekOverlay.LocalApi.Transport;

public sealed record BroadcastReply(IPAddress Source, JsonElement Result);
