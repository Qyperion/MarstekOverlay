using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.StationSimulator;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

public class SimulatorOptionsTests
{
    [Fact]
    public void TryParse_NoArguments_GivesAFullStationOnTheDefaultPort()
    {
        // Act
        var options = Parsed([]);

        // Assert
        options.Port.Should().Be(StationEndpoint.DefaultPort);
        options.Scenario.Should().Be(Scenario.Full);
        options.SocPercent.Should().Be(100);
        options.RequestSpacing.Should().Be(TimeSpan.FromSeconds(0.5));
        options.Refused.Should().Equal(new Dictionary<string, int> { [MarstekMethods.PvGetStatus] = RpcErrors.MethodNotFound });
    }

    [Theory]
    [InlineData("battery", Scenario.Battery, 80, false)]
    [InlineData("Charging", Scenario.Charging, 60, true)]
    [InlineData("TOPUP", Scenario.TopUp, 100, true)]
    public void TryParse_Scenario_SetsItsStartingCharge(string name, Scenario scenario, double soc, bool isMainsPresent)
    {
        // Act
        var options = Parsed(["--scenario", name]);

        // Assert
        options.Scenario.Should().Be(scenario);
        options.SocPercent.Should().Be(soc);
        options.IsMainsPresentAtStart.Should().Be(isMainsPresent);
    }

    [Fact]
    public void TryParse_TopUpScenario_TopsUpEveryMinuteUnlessTold()
    {
        // Act
        var byDefault = Parsed(["--scenario", "topup"]);
        var told = Parsed(["--scenario", "topup", "--top-up-every", "90"]);

        // Assert
        byDefault.TopUpEvery.Should().Be(TimeSpan.FromMinutes(1));
        told.TopUpEvery.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void TryParse_MethodNamesInAnyCase_BecomeTheWireNames()
    {
        // Act
        var options = Parsed(["--silent", "bat.getstatus", "--refuse", "es.getmode:-32603", "--at", "60:silent:ES.Getstatus", "--at", "30:outage"]);

        // Assert
        options.Silent.Should().Equal(MarstekMethods.BatteryGetStatus);
        options.Refused.Should().Contain(MarstekMethods.EnergySystemGetMode, -32603);
        options.Timeline.Should().Equal(
            new TimelineEvent(TimeSpan.FromSeconds(30), TimelineAction.Outage),
            new TimelineEvent(TimeSpan.FromSeconds(60), TimelineAction.Silent, MarstekMethods.EnergySystemGetStatus));
    }

    [Fact]
    public void TryParse_RefuseWithoutCode_RefusesWithInvalidParams()
    {
        // Act
        var options = Parsed(["--refuse", "ES.GetStatus"]);

        // Assert
        options.Refused.Should().Contain(MarstekMethods.EnergySystemGetStatus, RpcErrors.InvalidParams);
    }

    [Theory]
    [InlineData("--silent", "Bat.Status")]
    [InlineData("--refuse", "ES.SetMode:-32603")]
    [InlineData("--at", "60:silent:Nothing.Get")]
    public void TryParse_UnknownMethod_FailsNamingTheKnownOnes(string option, string value)
    {
        // Act
        var parsed = SimulatorOptions.TryParse([option, value], out var options, out var error);

        // Assert
        parsed.Should().BeFalse();
        options.Should().BeNull();
        error.Should().StartWith("Unknown method").And.Contain(MarstekMethods.BatteryGetStatus).And.Contain(MarstekMethods.EnergyMeterGetStatus);
    }

    [Theory]
    [InlineData("--scenario", "1", "--scenario 1: expected battery, charging, full or topup.")]
    [InlineData("--port", "30000.7", "--port 30000.7: expected a whole number from 1 to 65535.")]
    [InlineData("--port", "0", "--port 0: expected a whole number from 1 to 65535.")]
    [InlineData("--load", "-1", "--load -1: expected a number from 0 to 5000.")]
    [InlineData("--soc", "many", "--soc many: expected a number from 0 to 100.")]
    [InlineData("--at", "5:0", "--at 5:0: expected <seconds>:<action>[:<method>[:code]], at most 604800 s.")]
    [InlineData("--at", "604801:outage", "--at 604801:outage: expected <seconds>:<action>[:<method>[:code]], at most 604800 s.")]
    [InlineData("--refuse", "ES.GetStatus:x", "--refuse ES.GetStatus:x: expected <method>[:code].")]
    public void TryParse_ValueOutOfShape_FailsNamingTheOption(string option, string value, string expected)
    {
        // Act
        var parsed = SimulatorOptions.TryParse([option, value], out _, out var error);

        // Assert
        parsed.Should().BeFalse();
        error.Should().Be(expected);
    }

    [Theory]
    [InlineData(new[] { "--sock", "50" }, "Unknown option --sock.")]
    [InlineData(new[] { "--port", "30001", "--verbose" }, "Unknown option --verbose.")]
    [InlineData(new[] { "--port" }, "--port needs a value.")]
    public void TryParse_OptionOutOfPlace_FailsNamingIt(string[] args, string expected)
    {
        // Act
        var parsed = SimulatorOptions.TryParse(args, out _, out var error);

        // Assert
        parsed.Should().BeFalse();
        error.Should().Be(expected);
    }

    [Fact]
    public void TryParse_Numbers_AreReadWithAnyCaseOfTheName()
    {
        // Act
        var options = Parsed(["--PORT", "40000", "--load", "0", "--request-spacing", "0", "--duration", "90"]);

        // Assert
        options.Port.Should().Be(40000);
        options.LoadWatts.Should().Be(0);
        options.RequestSpacing.Should().Be(TimeSpan.Zero);
        options.Duration.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void IsHelp_HelpSwitch_IsRecognised(string argument)
    {
        // Act
        var isHelp = SimulatorOptions.IsHelp(argument);

        // Assert
        isHelp.Should().BeTrue();
    }

    [Fact]
    public void Usage_ListsEveryKnownMethod()
    {
        // Act
        var usage = SimulatorOptions.Usage;

        // Assert
        KnownMethods.All.Should().HaveCount(8).And.OnlyContain(method => usage.Contains(method));
    }

    private static SimulatorOptions Parsed(string[] args)
    {
        SimulatorOptions.TryParse(args, out var options, out var error).Should().BeTrue(error);
        return options ?? throw new InvalidOperationException(error);
    }
}
