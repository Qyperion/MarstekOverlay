using System.Text.Json.Nodes;

namespace MarstekOverlay.StationSimulator;

/// <summary>Decides what each method gets: an answer, an error or silence. Thread-safe: the timeline changes it
/// while requests are answered.</summary>
public sealed class Responder(SimulatorOptions options)
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _silent = [.. options.Silent];
    private readonly Dictionary<string, int> _refused = [with(options.Refused)];
    private bool _isOffline;

    public void Apply(TimelineEvent timelineEvent)
    {
        lock (_lock)
        {
            switch (timelineEvent)
            {
                case { Action: TimelineAction.Silent, Method: { } method }:
                    _silent.Add(method);
                    break;
                case { Action: TimelineAction.Refuse, Method: { } method }:
                    _refused[method] = timelineEvent.Code;
                    break;
                case { Action: TimelineAction.Answer, Method: { } method }:
                    _silent.Remove(method);
                    _refused.Remove(method);
                    break;
                case { Action: TimelineAction.Offline }:
                    _isOffline = true;
                    break;
                case { Action: TimelineAction.Online }:
                    _isOffline = false;
                    break;
            }
        }
    }

    /// <returns>Nothing when the request goes unanswered.</returns>
    public JsonObject? Respond(int id, string method, Func<StationReading> read)
    {
        lock (_lock)
        {
            if (_isOffline || _silent.Contains(method))
                return null;

            if (_refused.TryGetValue(method, out var code))
                return Error(id, code);
        }

        return StationAnswers.For(method, read()) is { } result
            ? new JsonObject { ["id"] = id, ["src"] = StationAnswers.Source, ["result"] = result }
            : Error(id, RpcErrors.MethodNotFound);
    }

    private static JsonObject Error(int id, int code)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = RpcErrors.Message(code) };
        if (code == RpcErrors.MethodNotFound)
            error["data"] = RpcErrors.MissingMethodData;

        // The real station once sent a parse error with id 0 instead of the request's, so the client skipped it,
        // timed out and asked again.
        var answeredId = code == RpcErrors.ParseError ? 0 : id;
        return new JsonObject { ["id"] = answeredId, ["src"] = StationAnswers.Source, ["error"] = error };
    }
}
