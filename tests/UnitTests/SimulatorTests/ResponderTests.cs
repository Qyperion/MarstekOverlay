using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.StationSimulator;

using System.Text.Json.Nodes;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

public class ResponderTests
{
    private const int RequestId = 7;

    private static readonly StationReading Reading = new(
        SocPercent: 80,
        IsMainsPresent: false,
        OngridWatts: 0,
        LoadWatts: 250,
        ChargingWatts: 0,
        GridInputWattHours: 17000,
        TemperatureCelsius: 24);

    [Fact]
    public void Respond_KnownMethod_AnswersWithTheRequestId()
    {
        // Arrange
        var responder = Responder();

        // Act
        var answer = responder.Respond(RequestId, MarstekMethods.EnergySystemGetStatus, () => Reading);

        // Assert
        (answer?["id"]?.GetValue<int>()).Should().Be(RequestId);
        (answer?["result"]).Should().BeOfType<JsonObject>();
    }

    [Fact]
    public void Respond_PvStatusByDefault_IsRefusedAsMissing()
    {
        // Arrange
        var responder = Responder();

        // Act
        var answer = responder.Respond(RequestId, MarstekMethods.PvGetStatus, () => Reading);

        // Assert
        var error = answer?["error"];
        (error?["code"]?.GetValue<int>()).Should().Be(RpcErrors.MethodNotFound);
        (error?["data"]?.GetValue<int>()).Should().Be(RpcErrors.MissingMethodData);
        (answer?["id"]?.GetValue<int>()).Should().Be(RequestId);
    }

    [Fact]
    public void Respond_RefusedWithParseError_AnswersWithIdZero()
    {
        // Arrange
        var responder = Responder("--refuse", "ES.GetStatus:-32700");

        // Act
        var answer = responder.Respond(RequestId, MarstekMethods.EnergySystemGetStatus, () => Reading);

        // Assert
        (answer?["error"]?["code"]?.GetValue<int>()).Should().Be(RpcErrors.ParseError);
        (answer?["id"]?.GetValue<int>()).Should().Be(0);
    }

    [Fact]
    public void Respond_RefusedWithAnotherError_AnswersWithTheRequestId()
    {
        // Arrange
        var responder = Responder("--refuse", "ES.GetStatus:-32603");

        // Act
        var answer = responder.Respond(RequestId, MarstekMethods.EnergySystemGetStatus, () => Reading);

        // Assert
        (answer?["error"]?["code"]?.GetValue<int>()).Should().Be(RpcErrors.InternalError);
        (answer?["id"]?.GetValue<int>()).Should().Be(RequestId);
    }

    [Fact]
    public void Respond_SilentMethodOrOffline_GoesUnanswered()
    {
        // Arrange
        var responder = Responder("--silent", "Bat.GetStatus");

        // Act
        var silent = responder.Respond(RequestId, MarstekMethods.BatteryGetStatus, () => Reading);
        responder.Apply(new TimelineEvent(TimeSpan.Zero, TimelineAction.Offline));
        var offline = responder.Respond(RequestId, MarstekMethods.EnergySystemGetStatus, () => Reading);

        // Assert
        silent.Should().BeNull();
        offline.Should().BeNull();
    }

    [Fact]
    public void Apply_Answer_UndoesSilenceAndRefusal()
    {
        // Arrange
        var responder = Responder("--silent", "Bat.GetStatus", "--refuse", "ES.GetMode");

        // Act
        responder.Apply(new TimelineEvent(TimeSpan.Zero, TimelineAction.Answer, MarstekMethods.BatteryGetStatus));
        responder.Apply(new TimelineEvent(TimeSpan.Zero, TimelineAction.Answer, MarstekMethods.EnergySystemGetMode));
        var answers = new[] { MarstekMethods.BatteryGetStatus, MarstekMethods.EnergySystemGetMode }
            .Select(method => responder.Respond(RequestId, method, () => Reading))
            .ToList();

        // Assert
        answers.Should().AllSatisfy(answer => (answer?["result"]).Should().BeOfType<JsonObject>());
    }

    private static Responder Responder(params string[] args)
    {
        SimulatorOptions.TryParse(args, out var options, out var error).Should().BeTrue(error);
        return new Responder(options ?? new SimulatorOptions());
    }
}
