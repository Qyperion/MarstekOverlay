using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Settings;
using MarstekOverlay.LocalApi.Transport;
using MarstekOverlay.UnitTests.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class UdpJsonRpcTransportTests
{
    // Enough for a loaded machine to answer; the short timeout is only for the attempts that wait out a silence.
    private const int AnswerTimeoutMs = 5_000;
    private const int ShortTimeoutMs = 250;

    private static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(1);

    private static JsonObject FirstInstance => new() { ["id"] = 0 };

    [Fact]
    public async Task InvokeAsync_ReturnsTheResultOfTheAnswer()
    {
        // Arrange
        await using var station = new FakeStation(request => [Result(request.Id, """{"bat_soc":93}""")]);
        using var transport = CreateTransport();

        // Act
        var result = await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        result.GetProperty("bat_soc").GetInt32().Should().Be(93);
        station.Requests.Should().ContainSingle().Which.Method.Should().Be(MarstekMethods.EnergySystemGetStatus);
    }

    [Fact]
    public async Task InvokeAsync_SkipsAnswersToOtherRequestsAndForeignDatagrams()
    {
        // Arrange
        await using var station = new FakeStation(request =>
        [
            "not json at all",
            Result(request.Id + 1000, """{"bat_soc":1}"""),
            Result(request.Id, """{"bat_soc":93}"""),
        ]);
        using var transport = CreateTransport();

        // Act
        var result = await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        result.GetProperty("bat_soc").GetInt32().Should().Be(93);
    }

    [Fact]
    public async Task InvokeAsync_SkipsAnAnswerFromAnotherAddress()
    {
        // Arrange
        await using var station = new FakeStation(
            answer: request => [Result(request.Id, """{"bat_soc":93}""")],
            strangerAnswer: request => [Result(request.Id, """{"bat_soc":1}""")]);
        using var transport = CreateTransport();

        // Act
        var result = await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        result.GetProperty("bat_soc").GetInt32().Should().Be(93);
    }

    [Fact]
    public async Task InvokeAsync_ParseErrorWithIdZero_IsWaitedOutAndRetried()
    {
        // Arrange
        await using var station = new FakeStation(request =>
            request.Number == 1
                ? ["""{"id":0,"src":"VenusE 3.0-aabbccddeeff","error":{"code":-32700,"message":"Parse error"}}"""]
                : [Result(request.Id, """{"bat_soc":93}""")]);
        using var transport = CreateTransport(retryCount: 1, requestTimeoutMs: ShortTimeoutMs, retryTimeoutMs: AnswerTimeoutMs);

        // Act
        var result = await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        result.GetProperty("bat_soc").GetInt32().Should().Be(93);
        station.Requests.Should().HaveCount(2, "an error without the request's id belongs to no request");
    }

    [Fact]
    public async Task InvokeAsync_ErrorAnswer_ThrowsRpcExceptionWithoutRetrying()
    {
        // Arrange
        await using var station = new FakeStation(request =>
            [$$$"""{"id":{{{request.Id}}},"src":"VenusE 3.0-aabbccddeeff","error":{"code":-32601,"message":"Method not found","data":424}}"""]);
        using var transport = CreateTransport(retryCount: 2);

        // Act
        var act = () => transport.InvokeAsync(station.Endpoint, MarstekMethods.PvGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        var exception = (await act.Should().ThrowAsync<MarstekRpcException>()).Which;
        exception.IsMethodNotFound.Should().BeTrue();
        exception.Method.Should().Be(MarstekMethods.PvGetStatus);
        station.Requests.Should().ContainSingle("an answered request is never repeated");
    }

    [Fact]
    public async Task InvokeAsync_SilentStation_RetriesThenThrowsTimeout()
    {
        // Arrange
        await using var station = new FakeStation(_ => []);
        using var transport = CreateTransport(retryCount: 1, requestTimeoutMs: ShortTimeoutMs);

        // Act
        var act = () => transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MarstekRequestTimeoutException>();
        station.Requests.Should().HaveCount(2);
        station.Requests.Select(request => request.Id).Should().OnlyHaveUniqueItems("a late answer must not be taken for the retry's");
    }

    [Fact]
    public async Task InvokeAsync_AnswerOnlyOnRetry_Succeeds()
    {
        // Arrange
        await using var station = new FakeStation(request =>
            request.Number == 1 ? [] : [Result(request.Id, """{"bat_soc":93}""")]);
        using var transport = CreateTransport(retryCount: 1, requestTimeoutMs: ShortTimeoutMs, retryTimeoutMs: AnswerTimeoutMs);

        // Act
        var result = await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        result.GetProperty("bat_soc").GetInt32().Should().Be(93);
    }

    [Fact]
    public async Task InvokeAsync_KeepsConsecutiveRequestsApart()
    {
        // Arrange
        var minDelay = TimeSpan.FromMilliseconds(200);
        await using var station = new FakeStation(request => [Result(request.Id, "{}")]);
        using var transport = CreateTransport(minDelayBetweenRequestsMs: (int)minDelay.TotalMilliseconds);

        // Act
        await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, CancellationToken.None);
        await transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergyMeterGetStatus, FirstInstance, CancellationToken.None);

        // Assert
        var arrivals = station.Requests.Select(request => request.ReceivedAt).ToList();
        (arrivals[1] - arrivals[0]).Should().BeGreaterThan(minDelay * 0.9);
    }

    [Fact]
    public async Task InvokeAsync_Cancelled_ThrowsCancellationInsteadOfTimeout()
    {
        // Arrange
        await using var station = new FakeStation(_ => []);
        using var transport = CreateTransport(requestTimeoutMs: 30_000);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var act = () => transport.InvokeAsync(station.Endpoint, MarstekMethods.EnergySystemGetStatus, FirstInstance, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BroadcastAsync_CollectsTheResultsWithTheirSource()
    {
        // Arrange
        await using var station = new FakeStation(request =>
        [
            """{"id":1,"error":{"code":-32601,"message":"Method not found"}}""",
            Result(request.Id, """{"device":"VenusE 3.0"}"""),
        ]);
        using var transport = CreateTransport();

        // Act
        var replies = await transport.BroadcastAsync(
            [IPAddress.Loopback], station.Endpoint.Port, MarstekMethods.GetDevice, FirstInstance, ListenFor, CancellationToken.None);

        // Assert
        var reply = replies.Should().ContainSingle().Which;
        reply.Source.Should().Be(IPAddress.Loopback);
        reply.Result.GetProperty("device").GetString().Should().Be("VenusE 3.0");
    }

    [Fact]
    public async Task BroadcastAsync_HostThatRefusesThePort_DoesNotStopTheOthers()
    {
        // Arrange
        await using var station = new FakeStation(request => [Result(request.Id, "{}")]);
        using var transport = CreateTransport();

        // Act
        var replies = await transport.BroadcastAsync(
            [IPAddress.Parse("127.0.0.3"), IPAddress.Loopback], station.Endpoint.Port, MarstekMethods.GetDevice, FirstInstance, ListenFor, CancellationToken.None);

        // Assert
        replies.Should().ContainSingle().Which.Source.Should().Be(IPAddress.Loopback);
    }

    [Fact]
    public async Task BroadcastAsync_NoTargets_ThrowsApiException()
    {
        // Arrange
        using var transport = CreateTransport();

        // Act
        var act = () => transport.BroadcastAsync(
            [], StationEndpoint.DefaultPort, MarstekMethods.GetDevice, FirstInstance, ListenFor, CancellationToken.None);

        // Assert
        await act.Should().ThrowExactlyAsync<MarstekApiException>();
    }

    [Fact]
    public async Task BroadcastAsync_NotSentAnywhere_ThrowsApiExceptionInsteadOfASocketFault()
    {
        // Arrange
        using var transport = CreateTransport();

        // Act
        var act = () => transport.BroadcastAsync(
            [IPAddress.Any], StationEndpoint.DefaultPort, MarstekMethods.GetDevice, FirstInstance, ListenFor, CancellationToken.None);

        // Assert
        (await act.Should().ThrowExactlyAsync<MarstekApiException>()).WithInnerException<SocketException>();
    }

    [Fact]
    public void SerializeRequest_ProducesTheDocumentedWireFormat()
    {
        // Act
        var datagram = JsonRpcMessage.SerializeRequest(7, MarstekMethods.EnergySystemGetStatus, FirstInstance);

        // Assert
        Encoding.UTF8.GetString(datagram).Should().Be("""{"id":7,"method":"ES.GetStatus","params":{"id":0}}""");
    }

    [Theory]
    [InlineData("""{"id":3,"error":{"code":"oops","message":"Busy"}}""")]
    [InlineData("""{"id":3,"error":{"code":null,"message":"Busy"}}""")]
    [InlineData("""{"id":3,"error":{"code":2.5,"message":"Busy"}}""")]
    public void ParseResponse_ErrorCodeThatIsNotAWholeNumber_ReadsAsZero(string datagram)
    {
        // Act
        var response = JsonRpcMessage.ParseResponse(Encoding.UTF8.GetBytes(datagram));

        // Assert
        response.Should().NotBeNull();
        response?.Error.Should().Be(new JsonRpcError(0, "Busy"));
    }

    [Theory]
    [InlineData("192.168.50.143", "255.255.255.0", "192.168.50.255")]
    [InlineData("192.168.50.143", "255.255.0.0", "192.168.255.255")]
    [InlineData("10.1.2.3", "255.255.240.0", "10.1.15.255")]
    [InlineData("172.16.5.9", "255.255.255.255", "172.16.5.9")]
    [InlineData("172.16.5.9", "0.0.0.0", "255.255.255.255")]
    public void DirectedBroadcast_SetsEveryHostBit(string address, string mask, string expected)
    {
        BroadcastAddresses.DirectedBroadcast(IPAddress.Parse(address), IPAddress.Parse(mask))
            .Should().Be(IPAddress.Parse(expected));
    }

    private static string Result(int id, string result) =>
        $$"""{"id":{{id}},"src":"VenusE 3.0-aabbccddeeff","result":{{result}}}""";

    private static UdpJsonRpcTransport CreateTransport(
        int retryCount = 0,
        int requestTimeoutMs = AnswerTimeoutMs,
        int minDelayBetweenRequestsMs = 0,
        int? retryTimeoutMs = null)
    {
        var settings = new MarstekApiSettings
        {
            RetryCount = retryCount,
            RequestTimeoutMs = requestTimeoutMs,
            MinDelayBetweenRequestsMs = minDelayBetweenRequestsMs,
        };

        return new UdpJsonRpcTransport(
            Options.Create(settings),
            TimeProvider.System,
            NullLogger<UdpJsonRpcTransport>.Instance,
            TimeSpan.FromMilliseconds(retryTimeoutMs ?? requestTimeoutMs));
    }
}
