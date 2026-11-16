using FluentAssertions;

using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.LocalApi.Settings;
using MarstekOverlay.LocalApi.Transport;
using MarstekOverlay.TestSupport;
using MarstekOverlay.UnitTests.TestSupport;

using Microsoft.Extensions.Options;

using System.Net;

using Xunit;

namespace MarstekOverlay.UnitTests.LocalApiTests;

public class MarstekApiClientTests
{
    private const double Precision = 1e-9;

    private static readonly StationEndpoint Station = TestStation.Endpoint;
    private static readonly FirmwareProfile VenusE150 = FirmwareProfile.For("VenusE 3.0", 150);

    private readonly StubJsonRpcTransport _transport = new();
    private readonly MarstekApiClient _client;

    public MarstekApiClientTests()
    {
        _client = new MarstekApiClient(_transport, Options.Create(new MarstekApiSettings()));
    }

    [Fact]
    public async Task GetDeviceAsync_MapsIdentityAndAsksForAnyDevice()
    {
        // Arrange
        _transport.Results[MarstekMethods.GetDevice] =
            """{"device":"VenusE 3.0","ver":150,"ble_mac":"aabbccddeeff","wifi_mac":"112233445566","wifi_name":"Home","ip":"192.168.1.50"}""";

        // Act
        var device = await _client.GetDeviceAsync(Station, CancellationToken.None);

        // Assert
        device.Should().Be(new DeviceInfo
        {
            Model = "VenusE 3.0",
            FirmwareVersion = 150,
            BluetoothMac = "aabbccddeeff",
            WifiMac = "112233445566",
            WifiName = "Home",
            ReportedIp = "192.168.1.50",
        });
        _transport.Requests.Should().Equal((MarstekMethods.GetDevice, """{"ble_mac":"0"}"""));
    }

    [Fact]
    public async Task GetEnergySystemStatusAsync_VenusE150Payload_MapsEveryFieldAndLeavesBatteryPowerEmpty()
    {
        // Arrange
        _transport.Results[MarstekMethods.EnergySystemGetStatus] =
            """{"id":0,"bat_soc":93,"bat_cap":5120,"pv_power":0,"ongrid_power":0,"offgrid_power":284,"total_pv_energy":0,"total_grid_output_energy":0,"total_grid_input_energy":17121,"total_load_energy":0}""";

        // Act
        var status = await _client.GetEnergySystemStatusAsync(Station, VenusE150, CancellationToken.None);

        // Assert
        status.Should().Be(new EnergySystemStatus
        {
            BatterySocPercent = 93,
            BatteryCapacity = new Energy(5120),
            PvPower = Power.Zero,
            OngridPower = Power.Zero,
            OffgridPower = new Power(284),
            BatteryPower = null,
            TotalPvEnergy = new Energy(0),
            TotalGridOutputEnergy = new Energy(0),
            TotalGridInputEnergy = new Energy(17121),
            TotalLoadEnergy = new Energy(0),
        });
        _transport.Requests.Should().Equal((MarstekMethods.EnergySystemGetStatus, """{"id":0}"""));
    }

    [Fact]
    public async Task GetEnergySystemStatusAsync_PvEnergyInHundredthsOfKilowattHour_IsConvertedButGridEnergyIsNot()
    {
        // Arrange
        _transport.Results[MarstekMethods.EnergySystemGetStatus] =
            """{"total_pv_energy":1234,"total_grid_input_energy":1234}""";

        // Act
        var status = await _client.GetEnergySystemStatusAsync(Station, FirmwareProfile.For("VenusA 3.0", 149), CancellationToken.None);

        // Assert
        status.TotalPvEnergy.Should().Be(new Energy(12340));
        status.TotalGridInputEnergy.Should().Be(new Energy(1234));
    }

    [Fact]
    public async Task GetEnergySystemStatusAsync_OffgridPowerWrappedAsUInt16_IsReadAsNegative()
    {
        // Arrange
        _transport.Results[MarstekMethods.EnergySystemGetStatus] = """{"offgrid_power":65436}""";

        // Act
        var status = await _client.GetEnergySystemStatusAsync(Station, VenusE150, CancellationToken.None);

        // Assert
        status.OffgridPower.Should().Be(new Power(-100));
    }

    [Theory]
    [InlineData("\"Manual\"", OperatingMode.Manual)]
    [InlineData("\"AI\"", OperatingMode.Ai)]
    [InlineData("\"UPS\"", OperatingMode.Ups)]
    [InlineData("\"Ups\"", OperatingMode.Ups)]
    [InlineData("0", OperatingMode.Auto)]
    [InlineData("3", OperatingMode.Passive)]
    [InlineData("9", null)]
    [InlineData("\"Turbo\"", null)]
    [InlineData("null", null)]
    public async Task GetEnergySystemModeAsync_AcceptsModeAsNameOrNumber(string wireMode, OperatingMode? expected)
    {
        // Arrange
        _transport.Results[MarstekMethods.EnergySystemGetMode] =
            $$"""{"id":0,"mode":{{wireMode}},"ongrid_power":0,"offgrid_power":299,"bat_soc":93}""";

        // Act
        var mode = await _client.GetEnergySystemModeAsync(Station, VenusE150, CancellationToken.None);

        // Assert
        mode.Mode.Should().Be(expected);
        mode.OffgridPower.Should().Be(new Power(299));
        mode.BatterySocPercent.Should().Be(93);
        mode.Meter.Should().Be(new EnergyMeterStatus());
    }

    [Fact]
    public async Task GetEnergyMeterStatusAsync_ScalesEnergyByFirmwareProfile()
    {
        // Arrange
        _transport.Results[MarstekMethods.EnergyMeterGetStatus] =
            """{"id":0,"ct_state":1,"a_power":120,"b_power":0,"c_power":-30,"total_power":90,"input_energy":17121,"output_energy":50}""";

        // Act
        var scaled = await _client.GetEnergyMeterStatusAsync(Station, VenusE150, CancellationToken.None);
        var legacy = await _client.GetEnergyMeterStatusAsync(Station, FirmwareProfile.For("VenusE 3.0", 147), CancellationToken.None);

        // Assert
        scaled.IsCtConnected.Should().BeTrue();
        scaled.PhaseAPower.Should().Be(new Power(120));
        scaled.PhaseCPower.Should().Be(new Power(-30));
        scaled.TotalPower.Should().Be(new Power(90));
        (scaled.InputEnergy?.WattHours).Should().BeApproximately(1712.1, Precision);
        (scaled.OutputEnergy?.WattHours).Should().BeApproximately(5, Precision);
        legacy.InputEnergy.Should().Be(new Energy(17121));
    }

    [Fact]
    public async Task GetBatteryStatusAsync_MapsEveryField()
    {
        // Arrange
        _transport.Results[MarstekMethods.BatteryGetStatus] =
            """{"id":0,"soc":93,"charg_flag":true,"dischrg_flag":false,"bat_temp":26.0,"bat_capacity":4776.0,"rated_capacity":5120.0}""";

        // Act
        var battery = await _client.GetBatteryStatusAsync(Station, CancellationToken.None);

        // Assert
        battery.Should().Be(new BatteryStatus
        {
            SocPercent = 93,
            IsChargingAllowed = true,
            IsDischargingAllowed = false,
            TemperatureCelsius = 26,
            RemainingCapacity = new Energy(4776),
            RatedCapacity = new Energy(5120),
        });
    }

    [Fact]
    public async Task GetWifiStatusAsync_MapsEveryFieldAndToleratesNulls()
    {
        // Arrange
        _transport.Results[MarstekMethods.WifiGetStatus] =
            """{"id":0,"wifi_mac":"112233445566","ssid":"Home","rssi":-54,"sta_ip":"192.168.1.50","sta_gate":"192.168.1.1","sta_mask":"255.255.255.0","sta_dns":null}""";

        // Act
        var wifi = await _client.GetWifiStatusAsync(Station, CancellationToken.None);

        // Assert
        wifi.Should().Be(new WifiStatus
        {
            Mac = "112233445566",
            Ssid = "Home",
            SignalStrengthDbm = -54,
            Ip = "192.168.1.50",
            Gateway = "192.168.1.1",
            SubnetMask = "255.255.255.0",
            Dns = null,
        });
    }

    [Theory]
    [InlineData("connect", true)]
    [InlineData("disconnect", false)]
    public async Task GetBluetoothStatusAsync_TellsConnectionFromState(string state, bool isConnected)
    {
        // Arrange
        _transport.Results[MarstekMethods.BluetoothGetStatus] = $$"""{"id":0,"state":"{{state}}","ble_mac":"aabbccddeeff"}""";

        // Act
        var bluetooth = await _client.GetBluetoothStatusAsync(Station, CancellationToken.None);

        // Assert
        bluetooth.IsConnected.Should().Be(isConnected);
        bluetooth.Mac.Should().Be("aabbccddeeff");
    }

    [Fact]
    public async Task GetPvStatusAsync_MultiChannelPayload_ReadsOnlyTheFirstChannelInDeciwatts()
    {
        // Arrange
        _transport.Results[MarstekMethods.PvGetStatus] =
            """{"id":0,"pv1_power":1500,"pv1_voltage":40.5,"pv1_current":3.7,"pv1_state":1,"pv2_power":120,"pv2_voltage":38,"pv2_current":3.1,"pv2_state":0}""";

        // Act
        var pv = await _client.GetPvStatusAsync(Station, FirmwareProfile.For("VenusA 3.0", 150), CancellationToken.None);

        // Assert
        pv.Channels.Should().HaveCount(2);
        pv.Channels[0].Should().Be(new PvChannel
        {
            Number = 1,
            Power = new Power(150),
            VoltageVolts = 40.5,
            CurrentAmperes = 3.7,
            IsWorking = true,
        });
        pv.Channels[1].Power.Should().Be(new Power(120));
        pv.Channels[1].IsWorking.Should().BeFalse();
        pv.TotalPower.Should().Be(new Power(270));
    }

    [Fact]
    public async Task GetPvStatusAsync_SingleChannelPayload_BecomesChannelOne()
    {
        // Arrange
        _transport.Results[MarstekMethods.PvGetStatus] =
            """{"id":0,"pv_power":580,"pv_voltage":40,"pv_current":1.4,"PV_state":1}""";

        // Act
        var pv = await _client.GetPvStatusAsync(Station, FirmwareProfile.For("Venus D", 148), CancellationToken.None);

        // Assert
        pv.Channels.Should().ContainSingle().Which.Should().Be(new PvChannel
        {
            Number = 1,
            Power = new Power(58),
            VoltageVolts = 40,
            CurrentAmperes = 1.4,
            IsWorking = true,
        });
    }

    [Fact]
    public async Task DiscoverAsync_UsesTheAddressAnAnswerCameFromAndDropsDuplicatesAndStrangers()
    {
        // Arrange
        var source = IPAddress.Parse("192.168.1.77");
        var identity = StubJsonRpcTransport.Parse("""{"device":"VenusE 3.0","ver":150,"ip":"192.168.1.20"}""");
        _transport.BroadcastReplies.AddRange(
        [
            new BroadcastReply(source, identity),
            new BroadcastReply(source, identity),
            new BroadcastReply(IPAddress.Parse("192.168.1.99"), StubJsonRpcTransport.Parse("""{"unrelated":true}""")),
        ]);

        // Act
        var devices = await _client.DiscoverAsync(port: 30123, CancellationToken.None);

        // Assert
        var device = devices.Should().ContainSingle().Subject;
        device.Endpoint.Should().Be(new StationEndpoint(source, 30123));
        device.Device.Model.Should().Be("VenusE 3.0");
        device.Device.ReportedIp.Should().Be("192.168.1.20", "the stale address is kept for information only");
    }
}
