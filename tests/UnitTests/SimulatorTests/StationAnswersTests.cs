using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Mapping;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.StationSimulator;

using System.Text.Json;

using Xunit;

namespace MarstekOverlay.UnitTests.SimulatorTests;

/// <summary>The simulator's answers read through the app's own mapping, so a field renamed on one side shows.</summary>
public class StationAnswersTests
{
    private static readonly StationReading Reading = new(
        SocPercent: 86.99,
        IsMainsPresent: true,
        OngridWatts: -2450,
        LoadWatts: 250,
        ChargingWatts: 2200,
        GridInputWattHours: 17000,
        TemperatureCelsius: 24);

    private static readonly FirmwareProfile Profile = FirmwareProfile.For(StationAnswers.Model, StationAnswers.Firmware);

    [Fact]
    public void For_EveryKnownMethodButPv_HasAnAnswer()
    {
        // Act
        var answered = KnownMethods.All.Where(method => StationAnswers.For(method, Reading) is not null).ToList();

        // Assert
        answered.Should().BeEquivalentTo(KnownMethods.All.Except([MarstekMethods.PvGetStatus]));
    }

    [Fact]
    public void For_GetDevice_ReadsAsVenusE3OnFirmware150()
    {
        // Act
        var device = Answer(MarstekMethods.GetDevice).ToDeviceInfo();

        // Assert
        device.Model.Should().Be(StationAnswers.Model);
        device.FirmwareVersion.Should().Be(150);
        FirmwareProfile.For(device.Model, device.FirmwareVersion).Family.Should().Be(DeviceFamily.VenusE);
    }

    [Fact]
    public void For_EnergySystemStatus_ReadsTheFlowsAndTheChargeRoundedDown()
    {
        // Act
        var status = Answer(MarstekMethods.EnergySystemGetStatus).ToEnergySystemStatus(Profile);

        // Assert
        status.BatterySocPercent.Should().Be(86);
        status.BatteryCapacity.Should().Be(new Energy(SimulatedStation.CapacityWattHours));
        status.OngridPower.Should().Be(new Power(-2450));
        status.OffgridPower.Should().Be(new Power(250));
        status.PvPower.Should().Be(Power.Zero);
        status.BatteryPower.Should().BeNull();
        status.TotalGridInputEnergy.Should().Be(new Energy(17000));
    }

    [Fact]
    public void For_EnergySystemMode_ReadsAsManualWithTheFlows()
    {
        // Act
        var mode = Answer(MarstekMethods.EnergySystemGetMode).ToEnergySystemMode(Profile);

        // Assert
        mode.Mode.Should().Be(OperatingMode.Manual);
        mode.OngridPower.Should().Be(new Power(-2450));
        mode.OffgridPower.Should().Be(new Power(250));
        mode.BatterySocPercent.Should().Be(86);
    }

    [Fact]
    public void For_EnergyMeterStatus_ReadsAsNoMeter()
    {
        // Act
        var meter = Answer(MarstekMethods.EnergyMeterGetStatus).ToEnergyMeterStatus(Profile);

        // Assert
        meter.IsCtConnected.Should().BeFalse();
        meter.TotalPower.Should().Be(Power.Zero);
        meter.InputEnergy.Should().Be(new Energy(0));
    }

    [Fact]
    public void For_BatteryStatus_ReadsTheCapacityInStepsOfATenthOfAPercent()
    {
        // Act
        var battery = Answer(MarstekMethods.BatteryGetStatus).ToBatteryStatus();

        // Assert
        battery.SocPercent.Should().Be(86);
        battery.IsChargingAllowed.Should().BeTrue();
        battery.IsDischargingAllowed.Should().BeTrue();
        battery.TemperatureCelsius.Should().Be(24);
        battery.RemainingCapacity.Should().Be(new Energy(4449.3));
        battery.RatedCapacity.Should().Be(new Energy(SimulatedStation.CapacityWattHours));
    }

    [Fact]
    public void For_WifiAndBluetoothStatus_ReadTheirFields()
    {
        // Act
        var wifi = Answer(MarstekMethods.WifiGetStatus).ToWifiStatus();
        var bluetooth = Answer(MarstekMethods.BluetoothGetStatus).ToBluetoothStatus();

        // Assert
        wifi.Ssid.Should().Be("Simulator");
        wifi.SignalStrengthDbm.Should().Be(-55);
        wifi.Ip.Should().Be("127.0.0.1");
        bluetooth.State.Should().Be("disconnect");
        bluetooth.Mac.Should().Be("acd929a7f2c1");
    }

    private static JsonElement Answer(string method) => JsonSerializer.SerializeToElement(StationAnswers.For(method, Reading));
}
