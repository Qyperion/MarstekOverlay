using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarstekOverlay.LocalApi.Transport;

public sealed class UdpJsonRpcTransport(
    IOptions<MarstekApiSettings> options,
    TimeProvider timeProvider,
    ILogger<UdpJsonRpcTransport> logger) : IJsonRpcTransport, IDisposable
{
    private readonly TimeSpan _requestTimeout = TimeSpan.FromMilliseconds(options.Value.RequestTimeoutMs);
    private readonly TimeSpan _retryTimeout = TimeSpan.FromMilliseconds(options.Value.RequestTimeoutMs);
    private readonly TimeSpan _minDelayBetweenRequests = TimeSpan.FromMilliseconds(options.Value.MinDelayBetweenRequestsMs);
    private readonly int _retryCount = options.Value.RetryCount;

    // A station gets unstable under overlapping or back-to-back requests, so exchanges run one at a time, spaced out.
    private readonly SemaphoreSlim _exchangeGate = new(1, 1);
    private long? _lastExchangeTimestamp;
    private int _lastRequestId;

    internal UdpJsonRpcTransport(
        IOptions<MarstekApiSettings> options,
        TimeProvider timeProvider,
        ILogger<UdpJsonRpcTransport> logger,
        TimeSpan retryTimeout)
        : this(options, timeProvider, logger) =>
        _retryTimeout = retryTimeout;

    public async Task<JsonElement> InvokeAsync(
        StationEndpoint station,
        string method,
        JsonObject parameters,
        CancellationToken token)
    {
        await _exchangeGate.WaitAsync(token);
        try
        {
            for (var retriesLeft = _retryCount; ; retriesLeft--)
            {
                await KeepRequestSpacingAsync(token);
                try
                {
                    var timeout = retriesLeft == _retryCount ? _requestTimeout : _retryTimeout;
                    return await ExchangeAsync(station, method, parameters, timeout, token);
                }
                catch (MarstekRequestTimeoutException) when (retriesLeft > 0)
                {
                    logger.LogDebug("{Method} to {Station} got no answer, retrying", method, station);
                }
                finally
                {
                    _lastExchangeTimestamp = timeProvider.GetTimestamp();
                }
            }
        }
        finally
        {
            _exchangeGate.Release();
        }
    }

    public Task<IReadOnlyList<BroadcastReply>> BroadcastAsync(
        int port,
        string method,
        JsonObject parameters,
        TimeSpan listenFor,
        CancellationToken token) =>
        BroadcastAsync(BroadcastAddresses.Enumerate(), port, method, parameters, listenFor, token);

    internal async Task<IReadOnlyList<BroadcastReply>> BroadcastAsync(
        IEnumerable<IPAddress> targets,
        int port,
        string method,
        JsonObject parameters,
        TimeSpan listenFor,
        CancellationToken token)
    {
        await _exchangeGate.WaitAsync(token);
        try
        {
            await KeepRequestSpacingAsync(token);
            return await CollectBroadcastRepliesAsync(targets, port, method, parameters, listenFor, token);
        }
        finally
        {
            _lastExchangeTimestamp = timeProvider.GetTimestamp();
            _exchangeGate.Release();
        }
    }

    public void Dispose() => _exchangeGate.Dispose();

    private async Task<JsonElement> ExchangeAsync(
        StationEndpoint station,
        string method,
        JsonObject parameters,
        TimeSpan timeout,
        CancellationToken token)
    {
        var requestId = ++_lastRequestId;
        var request = JsonRpcMessage.SerializeRequest(requestId, method, parameters);

        try
        {
            using var socket = new UdpClient(AddressFamily.InterNetwork);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(timeout);

            LogDatagram("->", station.ToIPEndPoint(), request);
            await socket.SendAsync(request, station.ToIPEndPoint(), deadline.Token);

            while (true)
            {
                var datagram = await socket.ReceiveAsync(deadline.Token);
                LogDatagram("<-", datagram.RemoteEndPoint, datagram.Buffer);

                if (!datagram.RemoteEndPoint.Address.Equals(station.Address))
                    continue;

                // An answer to an earlier request that already timed out can still arrive on a retry.
                if (JsonRpcMessage.ParseResponse(datagram.Buffer) is not { } response || response.Id != requestId)
                    continue;

                return response.Error is { } error
                    ? throw new MarstekRpcException(method, error.Code, error.Message)
                    : response.Result;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new MarstekRequestTimeoutException(station, method, timeout);
        }
        catch (SocketException exception)
        {
            throw new MarstekApiException($"{method} to {station} failed: {exception.Message}", exception);
        }
    }

    private async Task<IReadOnlyList<BroadcastReply>> CollectBroadcastRepliesAsync(
        IEnumerable<IPAddress> targets,
        int port,
        string method,
        JsonObject parameters,
        TimeSpan listenFor,
        CancellationToken token)
    {
        var request = JsonRpcMessage.SerializeRequest(++_lastRequestId, method, parameters);
        List<BroadcastReply> replies = [];

        try
        {
            using var socket = new UdpClient(AddressFamily.InterNetwork);
            socket.EnableBroadcast = true;
            using var listening = CancellationTokenSource.CreateLinkedTokenSource(token);

            var sentAnywhere = false;
            SocketException? lastFailure = null;
            foreach (var address in targets)
            {
                var target = new IPEndPoint(address, port);
                try
                {
                    LogDatagram("->", target, request);
                    await socket.SendAsync(request, target, token);
                    sentAnywhere = true;
                }
                catch (SocketException exception)
                {
                    logger.LogDebug("Broadcast to {Target} was not sent: {Reason}", target, exception.Message);
                    lastFailure = exception;
                }
            }

            // A socket that sent nothing was never bound, and receiving on it would throw.
            if (!sentAnywhere)
            {
                var message = $"{method} could not be broadcast to port {port}: {lastFailure?.Message ?? "no network to send it to"}";
                throw lastFailure is null ? new MarstekApiException(message) : new MarstekApiException(message, lastFailure);
            }

            listening.CancelAfter(listenFor);
            while (true)
            {
                try
                {
                    var datagram = await socket.ReceiveAsync(listening.Token);
                    LogDatagram("<-", datagram.RemoteEndPoint, datagram.Buffer);

                    if (JsonRpcMessage.ParseResponse(datagram.Buffer) is { Error: null } response)
                        replies.Add(new BroadcastReply(datagram.RemoteEndPoint.Address, response.Result));
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    return replies;
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Windows reports a host that refused the broadcast port on the next receive; the others still answer.
                }
            }
        }
        catch (SocketException exception)
        {
            throw new MarstekApiException($"{method} broadcast to port {port} failed: {exception.Message}", exception);
        }
    }

    private async Task KeepRequestSpacingAsync(CancellationToken token)
    {
        if (_lastExchangeTimestamp is not { } lastExchange)
            return;

        var remaining = _minDelayBetweenRequests - timeProvider.GetElapsedTime(lastExchange);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, timeProvider, token);
    }

    private void LogDatagram(string direction, IPEndPoint peer, byte[] datagram)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("{Direction} {Peer} {Datagram}", direction, peer, Encoding.UTF8.GetString(datagram));
    }
}
