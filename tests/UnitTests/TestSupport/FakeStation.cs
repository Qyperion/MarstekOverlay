using MarstekOverlay.LocalApi;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MarstekOverlay.UnitTests.TestSupport;

/// <summary>A UDP endpoint on the loopback interface that answers requests the way a test scripts it.</summary>
internal sealed class FakeStation : IAsyncDisposable
{
    private readonly UdpClient _socket = new(new IPEndPoint(IPAddress.Loopback, 0));

    // Windows routes the whole of 127.0.0.0/8 to the loopback interface, so another sender needs no setup.
    private readonly UdpClient _stranger = new(new IPEndPoint(StrangerAddress, 0));
    private readonly CancellationTokenSource _stopping = new();
    private readonly Func<FakeStationRequest, IEnumerable<string>> _answer;
    private readonly Func<FakeStationRequest, IEnumerable<string>> _strangerAnswer;
    private readonly Task _serving;

    /// <param name="answer">The datagrams to send back for a request; none leaves it unanswered.</param>
    /// <param name="strangerAnswer">Datagrams another host sends to the requester before the station answers.</param>
    public FakeStation(
        Func<FakeStationRequest, IEnumerable<string>> answer,
        Func<FakeStationRequest, IEnumerable<string>>? strangerAnswer = null)
    {
        _answer = answer;
        _strangerAnswer = strangerAnswer ?? (_ => []);
        _serving = ServeAsync();
    }

    /// <summary>A loopback address nothing listens on.</summary>
    public static IPAddress StrangerAddress { get; } = IPAddress.Parse("127.0.0.2");

    public ConcurrentQueue<FakeStationRequest> Requests { get; } = new();

    public StationEndpoint Endpoint =>
        _socket.Client.LocalEndPoint is IPEndPoint local
            ? new StationEndpoint(IPAddress.Loopback, local.Port)
            : throw new InvalidOperationException("The fake station is not bound.");

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        await _serving;
        _socket.Dispose();
        _stranger.Dispose();
        _stopping.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                var datagram = await _socket.ReceiveAsync(_stopping.Token);
                using var document = JsonDocument.Parse(datagram.Buffer);
                var request = new FakeStationRequest(
                    Number: Requests.Count + 1,
                    Id: document.RootElement.GetProperty("id").GetInt32(),
                    Method: document.RootElement.GetProperty("method").GetString() ?? string.Empty,
                    ReceivedAt: DateTimeOffset.UtcNow);
                Requests.Enqueue(request);

                foreach (var answer in _strangerAnswer(request))
                    await _stranger.SendAsync(Encoding.UTF8.GetBytes(answer), datagram.RemoteEndPoint, _stopping.Token);

                foreach (var answer in _answer(request))
                    await _socket.SendAsync(Encoding.UTF8.GetBytes(answer), datagram.RemoteEndPoint, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
                // Windows reports an answer sent to a client that has already closed its socket on the next receive.
            }
        }
    }
}

/// <param name="Number">Counts from one in the order the requests arrived.</param>
internal sealed record FakeStationRequest(int Number, int Id, string Method, DateTimeOffset ReceivedAt);
