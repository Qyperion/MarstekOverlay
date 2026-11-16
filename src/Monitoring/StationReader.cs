using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarstekOverlay.Monitoring;

public sealed class StationReader(
    IMarstekApiClient client,
    IOptions<PollingSettings> options,
    TimeProvider timeProvider,
    ILogger<StationReader> logger)
{
    private readonly HashSet<string> _disabledMethods = new(options.Value.DisabledMethods, StringComparer.OrdinalIgnoreCase);

    /// <summary>Never throws for a station that is silent or rejects a request: the outcome is in the snapshot.</summary>
    /// <param name="onProgress">Receives the snapshot after every answer, as the station takes a second per
    /// request and a full poll would otherwise show nothing for most of ten.</param>
    public async Task<PollResult> ReadAsync(
        StationEndpoint station,
        StationSnapshot previous,
        PollScope scope,
        CancellationToken token,
        Action<StationSnapshot>? onProgress = null)
    {
        var poll = new Poll(previous, onProgress);

        foreach (var method in PolledMethod.All.Where(method => method.IsSentWith(scope)))
            await ReadAsync(poll, method, station, token);

        return new PollResult(poll.Snapshot, poll.HasAnswer, poll.HasAnswer && poll.IsIdentityUnanswered);
    }

    private async Task ReadAsync(Poll poll, PolledMethod method, StationEndpoint station, CancellationToken token)
    {
        if (poll.IsStationSilent || poll.Snapshot.AbandonedMethods.Contains(method.Name) || _disabledMethods.Contains(method.Name))
            return;

        try
        {
            var store = await method.RequestAsync(client, station, poll.Profile, timeProvider, token);
            poll.HasAnswer = true;
            poll.Update(snapshot => Answered(store(snapshot), method));
        }
        // A method that has answered exists, whatever the station says later: such a refusal is a fault like any other.
        catch (MarstekRpcException exception) when (exception.IsMethodNotFound && !method.HasReading(poll.Snapshot))
        {
            poll.HasAnswer = true;
            poll.Update(snapshot => TurnedDown(snapshot, method));
        }
        catch (MarstekApiException exception)
        {
            logger.LogWarning("{Error}", exception.Message);
            poll.Update(snapshot => snapshot with { Errors = [.. snapshot.Errors, PollError.From(exception)] });

            // When even the first request of every poll goes unanswered the station is most likely offline, and
            // waiting out the timeout of every remaining request would only delay the next poll. A request of
            // the full poll alone says nothing of the kind: some firmware may never answer it.
            if (exception is MarstekRpcException)
                poll.HasAnswer = true;
            else if (method.SentWith == PollScope.Fast)
                poll.IsStationSilent = !poll.HasAnswer;
            else if (method.Name == MarstekMethods.GetDevice)
                poll.IsIdentityUnanswered = true;
        }
    }

    private StationSnapshot TurnedDown(StationSnapshot snapshot, PolledMethod method)
    {
        var refused = snapshot with
        {
            UnsupportedMethods = new Dictionary<string, DateTimeOffset>(snapshot.UnsupportedMethods)
            {
                [method.Name] = timeProvider.GetUtcNow(),
            },
        };

        if (!snapshot.UnsupportedMethods.ContainsKey(method.Name))
            return refused;

        logger.LogInformation("The station does not support {Method}; it will not be requested again unless a check is asked for", method.Name);

        return Abandoned(refused, method);
    }

    private StationSnapshot Answered(StationSnapshot snapshot, PolledMethod method)
    {
        var answered = snapshot.UnsupportedMethods.ContainsKey(method.Name)
            ? snapshot with { UnsupportedMethods = snapshot.UnsupportedMethods.Where(refusal => refusal.Key != method.Name).ToDictionary() }
            : snapshot;

        if (!method.IsDisconnected(answered))
            return answered;

        logger.LogInformation(
            "{Method} answered that nothing is connected; it will not be requested again unless a check is asked for",
            method.Name);

        return Abandoned(answered, method);
    }

    private static StationSnapshot Abandoned(StationSnapshot snapshot, PolledMethod method) =>
        snapshot with { AbandonedMethods = new HashSet<string>(snapshot.AbandonedMethods) { method.Name } };

    private sealed class Poll(StationSnapshot previous, Action<StationSnapshot>? onProgress)
    {
        public StationSnapshot Snapshot { get; private set; } = previous with { Errors = [] };
        public bool HasAnswer { get; set; }
        public bool IsStationSilent { get; set; }
        public bool IsIdentityUnanswered { get; set; }

        public FirmwareProfile Profile => Snapshot.Device?.Value.Profile ?? FirmwareProfile.Unknown;

        public void Update(Func<StationSnapshot, StationSnapshot> change)
        {
            Snapshot = change(Snapshot);
            onProgress?.Invoke(Snapshot);
        }
    }
}
