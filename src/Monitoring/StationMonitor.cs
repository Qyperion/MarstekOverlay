using MarstekOverlay.LocalApi;
using MarstekOverlay.Monitoring.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarstekOverlay.Monitoring;

/// <summary>Keeps one station's snapshot current: the fast-changing readings on every poll, the rest on a slower beat.</summary>
public sealed class StationMonitor(
    StationReader reader,
    IOptions<PollingSettings> options,
    TimeProvider timeProvider,
    ILogger<StationMonitor> logger) : ISnapshotSource, IDisposable
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(options.Value.IntervalSeconds);
    private readonly TimeSpan _slowInterval = TimeSpan.FromSeconds(options.Value.SlowIntervalSeconds);

    // Held while a snapshot is published, so a poll that is being stopped cannot publish after the next has started.
    private readonly Lock _publishing = new();

    private volatile StationSnapshot _snapshot = StationSnapshot.Empty;
    private CancellationTokenSource? _polling;
    private TaskCompletionSource? _refreshRequest;
    private bool _isCheckRequested;

    /// <summary>Raised on a thread-pool thread after every answer within a poll and once more when the poll ends,
    /// even if it brought nothing new.</summary>
    public event Action<StationSnapshot>? SnapshotChanged;

    public StationSnapshot Snapshot => _snapshot;

    public void Start(StationEndpoint station)
    {
        lock (_publishing)
        {
            Stop();
            _snapshot = StationSnapshot.Empty;

            var polling = _polling = new CancellationTokenSource();
            _ = Task.Run(() => PollAsync(station, polling.Token));
        }
    }

    public void Stop()
    {
        var polling = Interlocked.Exchange(ref _polling, null);
        polling?.Cancel();
        polling?.Dispose();
    }

    /// <summary>Starts the next poll now instead of at its scheduled time; a poll already running serves the request.</summary>
    public void RequestRefresh() => Volatile.Read(ref _refreshRequest)?.TrySetResult();

    /// <summary>Has the requests that were given up (<see cref="StationSnapshot.AbandonedMethods"/>) sent once
    /// more, with a poll that starts now: what they found missing may have been connected since.</summary>
    public void CheckAbandoned()
    {
        Volatile.Write(ref _isCheckRequested, true);
        RequestRefresh();
    }

    public void Dispose() => Stop();

    private async Task PollAsync(StationEndpoint station, CancellationToken token)
    {
        var snapshot = StationSnapshot.Empty;
        DateTimeOffset? lastFullPollAt = null;
        var identityMissed = false;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var startedAt = timeProvider.GetUtcNow();
                var scope = lastFullPollAt is { } fullPollAt && startedAt - fullPollAt < _slowInterval
                    ? identityMissed ? PollScope.FastWithIdentity : PollScope.Fast
                    : PollScope.Full;

                if (Interlocked.Exchange(ref _isCheckRequested, false))
                    snapshot = snapshot with { AbandonedMethods = new HashSet<string>() };

                var result = await ReadAsync(station, snapshot, scope, Publish, token);
                snapshot = result.Snapshot;

                // A full poll the station did not answer sent none of the slow requests.
                if (scope == PollScope.Full && result.StationAnswered)
                    lastFullPollAt = startedAt;

                // Without the model the readings are decoded unscaled, too long to wait a slow interval for. Asked
                // once only, as some firmware may never answer it and every poll would wait out its timeout.
                identityMissed = scope == PollScope.Full && result.IdentityUnanswered;

                Publish(snapshot);
                await WaitForNextPollAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Polling {Station} broke off; it resumes after the interval", station);
                await Task.Delay(_interval, timeProvider, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }

        void Publish(StationSnapshot current)
        {
            lock (_publishing)
            {
                // A poll cancelled mid-flight belongs to a station that is no longer the monitored one.
                token.ThrowIfCancellationRequested();

                _snapshot = current;
                SnapshotChanged?.Invoke(current);
            }
        }
    }

    private async Task<PollResult> ReadAsync(
        StationEndpoint station,
        StationSnapshot previous,
        PollScope scope,
        Action<StationSnapshot> onProgress,
        CancellationToken token)
    {
        var latest = previous with { Errors = [] };

        try
        {
            return await reader.ReadAsync(station, previous, scope, token, current =>
            {
                latest = current;
                onProgress(current);
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Polling {Station} failed unexpectedly", station);

            // The answers of this poll are already shown and must not give way to older ones. Counted as answered:
            // a fault of the reader would recur on an immediate retry, and retrying it would send the slow requests
            // with every poll.
            return new PollResult(
                latest with { Errors = [.. latest.Errors, PollError.From(exception)] },
                StationAnswered: true,
                IdentityUnanswered: false);
        }
    }

    private async Task WaitForNextPollAsync(CancellationToken token)
    {
        var refreshRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _refreshRequest, refreshRequest);

        // Asked for while the poll was running, which had its requests chosen by then and served none of it.
        if (Volatile.Read(ref _isCheckRequested))
            return;

        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
        await Task.WhenAny(refreshRequest.Task, Task.Delay(_interval, timeProvider, waiting.Token));
        await waiting.CancelAsync();

        token.ThrowIfCancellationRequested();
    }
}
