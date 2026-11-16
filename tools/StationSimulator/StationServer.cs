using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.StationSimulator;

/// <summary>Answers requests on the loopback interface and plays the timeline. Only the loopback: a simulator
/// that answered discovery broadcasts on the LAN could be taken for a real station by another app.</summary>
public sealed class StationServer(SimulatorOptions options, TimeProvider timeProvider) : IDisposable
{
    private readonly UdpClient _socket = new(new IPEndPoint(IPAddress.Loopback, options.Port));
    private readonly Responder _responder = new(options);
    private readonly SimulatedStation _station = new(options, timeProvider.GetUtcNow());
    private readonly RequestGate _gate = new(options.RequestSpacing);

    public void Dispose() => _socket.Dispose();

    public async Task RunAsync(CancellationToken token)
    {
        Log(string.Create(
            CultureInfo.InvariantCulture,
            $"Listening on 127.0.0.1:{options.Port} as {StationAnswers.Model}: {options.Scenario}, {options.SocPercent}%, load {options.LoadWatts} W, speed x{options.Speed}, request spacing {options.RequestSpacing.TotalSeconds} s"));

        await Task.WhenAll(ServeAsync(token), PlayTimelineAsync(token));
        Log("Stopped");
    }

    private async Task ServeAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var datagram = await _socket.ReceiveAsync(token);
                if (!TryReadRequest(datagram.Buffer, out var id, out var method))
                    continue;

                if (!_gate.Admits(timeProvider.GetUtcNow()))
                {
                    Log($"{method,-18} dropped: within {options.RequestSpacing.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s of the previous answered request");
                    continue;
                }

                StationReading? reading = null;
                var answer = _responder.Respond(id, method, () => reading = _station.Read(timeProvider.GetUtcNow()));
                Log($"{method,-18} {Describe(answer, reading)}");

                if (answer is not null)
                    await _socket.SendAsync(Encoding.UTF8.GetBytes(answer.ToJsonString()), datagram.RemoteEndPoint, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                // Windows reports an answer sent to a client that has already closed its socket on the next receive.
            }
        }
    }

    private async Task PlayTimelineAsync(CancellationToken token)
    {
        foreach (var timelineEvent in options.Timeline)
        {
            var wait = _station.StartedAt + timelineEvent.After - timeProvider.GetUtcNow();
            try
            {
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, timeProvider, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            switch (timelineEvent.Action)
            {
                case TimelineAction.Outage or TimelineAction.Mains:
                    _station.SwitchMains(timelineEvent.Action == TimelineAction.Mains, timeProvider.GetUtcNow());
                    break;
                default:
                    _responder.Apply(timelineEvent);
                    break;
            }

            Log($"Now: {timelineEvent.Action} {timelineEvent.Method}".TrimEnd());
        }
    }

    private static bool TryReadRequest(byte[] datagram, out int id, out string method)
    {
        id = 0;
        method = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(datagram);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("id", out var idElement)
                || !idElement.TryGetInt32(out id)
                || !root.TryGetProperty("method", out var methodElement)
                || methodElement.GetString() is not { } name)
                return false;

            method = name;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Describe(JsonObject? answer, StationReading? reading)
    {
        if (answer is null)
            return "silent";

        if (answer["error"] is JsonObject error)
            return $"refused {error["code"]}";

        return reading is null
            ? "answered"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"answered: {reading.SocPercent:0.00}%, mains {(reading.IsMainsPresent ? "on" : "off")}, grid {reading.OngridWatts} W, load {reading.LoadWatts} W");
    }

    private static void Log(string message) =>
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:HH:mm:ss.fff} {message}"));
}
