using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.LocalApi.Firmware;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Net;

using Xunit;

namespace MarstekOverlay.DesktopTests.ViewModelsTests;

public sealed class StationProbeViewModelTests : LocalizedTests
{
    private static readonly StationEndpoint Station = TestStation.Endpoint;

    private readonly StationHarness _station = new(Moments.Local(14, 12, 0));
    private readonly ScriptedApiClient _client = new();
    private readonly StatusLine _status = new();
    private readonly List<IDisposable> _opened = [];

    public override void Dispose()
    {
        foreach (var opened in _opened)
            opened.Dispose();

        _station.Dispose();
        base.Dispose();
    }

    [Fact]
    public void FindStations_PortOutOfRange_MarksThePortWithoutSearching()
    {
        // Arrange
        var probe = Open();
        probe.Port = "70000";

        // Act
        probe.FindStationsCommand.Execute(null);

        // Assert
        probe.Errors.Fields.Should().Equal(nameof(StationProbeViewModel.Port));
        _status.Message.Should().Be(new StatusMessage(Translated("The values marked in red need correcting."), IsError: true));
        _client.Searches.Should().Be(0);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public async Task FindStations_StationsAnswer_ListsEachByItsModelAddressAndFirmware(string language)
    {
        // Arrange
        Speak(language);
        var probe = Open();
        _client.Found =
        [
            new DiscoveredDevice(Station, new DeviceInfo { Model = "VenusE 3.0", FirmwareVersion = 147 }),
            new DiscoveredDevice(Station with { Address = IPAddress.Parse("192.168.1.51") }, new DeviceInfo { Model = "VNSE3-0" }),
        ];

        // Act
        await probe.FindStationsCommand.ExecuteAsync(null);

        // Assert
        probe.Devices.Select(device => device.Name).Should().Equal(
            "VenusE 3.0 — 192.168.1.50:30000, v147",
            "VNSE3-0 — 192.168.1.51:30000");
        _status.Message.Should().Be(new StatusMessage(
            Translated("Stations found: 2. Select one to use its address."),
            IsError: false));
    }

    [Fact]
    public async Task FindStations_NobodyAnswers_SaysWhatToCheck()
    {
        // Arrange
        var probe = Open();

        // Act
        await probe.FindStationsCommand.ExecuteAsync(null);

        // Assert
        probe.Devices.Should().BeEmpty();
        _status.Message.IsError.Should().BeTrue();
        _status.Message.Text.Should().StartWith(Translated("No station answered."));
    }

    [Fact]
    public async Task FindStations_TransportFails_SaysSoInsteadOfFailing()
    {
        // Arrange
        var probe = Open();
        _client.SearchFailure = new MarstekApiException("The network is unreachable.");

        // Act
        await probe.FindStationsCommand.ExecuteAsync(null);

        // Assert
        _status.Message.Should().Be(new StatusMessage(
            Translated("The search failed. Details: The network is unreachable."),
            IsError: true));
        probe.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task FindStations_WhileUnderWay_KeepsTheConnectionTestFromStarting()
    {
        // Arrange
        var probe = Open();
        var answer = new TaskCompletionSource<IReadOnlyList<DiscoveredDevice>>();
        _client.PendingSearch = answer.Task;

        // Act
        var search = probe.FindStationsCommand.ExecuteAsync(null);
        var mayTestMeanwhile = probe.TestConnectionCommand.CanExecute(null);
        answer.SetResult([]);
        await search;

        // Assert
        mayTestMeanwhile.Should().BeFalse();
        probe.TestConnectionCommand.CanExecute(null).Should().BeTrue();
        probe.FindStationsCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task SelectedDevice_FoundStation_FillsInItsAddressAndSaysSo()
    {
        // Arrange
        var probe = Open();
        var edits = 0;
        probe.Edited += () => edits++;
        _client.Found = [new DiscoveredDevice(Station with { Port = 30001 }, new DeviceInfo { Model = "VenusE 3.0" })];
        await probe.FindStationsCommand.ExecuteAsync(null);

        // Act
        probe.SelectedDevice = probe.Devices[0];

        // Assert
        probe.Host.Should().Be("192.168.1.50");
        probe.Port.Should().Be("30001");
        edits.Should().Be(1);
        _status.Message.Text.Should().Be(Translated("Address of VenusE 3.0 filled in. Test the connection, then save."));
    }

    [Theory]
    [InlineData(true, "192.168.1.50:30000 did not answer. Check the address, the port and that Local API is enabled in the Marstek app. Details: ")]
    [InlineData(false, "192.168.1.50:30000 did not answer. Check the address, the port and that Local API is enabled in the Marstek app.")]
    public async Task TestConnection_StationSilent_PointsAtTheLogWhenThereIsOne(bool isLogEnabled, string expected)
    {
        // Arrange
        using var log = new DiagnosticLog(_station.Storage, Options.Create(new LoggerOptions { IsEnabledFileLog = isLogEnabled }));
        var probe = Open(log);

        // Act
        await probe.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _status.Message.IsError.Should().BeTrue();
        _status.Message.Text.Should().Be(expected + log.FilePath);
        probe.Report.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TestConnection_StationAnswers_ListsWhatItReported()
    {
        // Arrange
        _client.EnergySystem = new EnergySystemStatus { BatterySocPercent = 80 };
        var probe = Open();

        // Act
        await probe.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _status.Message.Should().Be(new StatusMessage(
            Translated("192.168.1.50:30000 answered. Everything it reports is listed below."),
            IsError: false));
        probe.Report.Should().NotBeEmpty();
        probe.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void TestConnection_AddressWithALeadingZero_MarksItWithoutReading()
    {
        // Arrange
        var probe = Open();
        probe.Host = "192.168.1.050";

        // Act
        probe.TestConnectionCommand.Execute(null);

        // Assert
        probe.Errors.Fields.Should().Equal(nameof(StationProbeViewModel.Host));
        _client.Requests.Should().Be(0);
    }

    [Fact]
    public void Edit_FieldThatWasMarked_IsNoLongerMarkedAndSaysSoToTheWindow()
    {
        // Arrange
        var probe = Open();
        var announced = new List<string?>();
        probe.Port = "0";
        probe.TryReadEndpoint(out _);
        probe.ErrorsChanged += (_, change) => announced.Add(change.PropertyName);

        // Act
        probe.Port = "30001";

        // Assert
        probe.HasErrors.Should().BeFalse();
        probe.GetErrors(nameof(StationProbeViewModel.Port)).Cast<string>().Should().BeEmpty();
        announced.Should().Equal(nameof(StationProbeViewModel.Port));
    }

    [Fact]
    public void TryReadEndpoint_WrongAddressAndPort_MarksBothAtOnce()
    {
        // Arrange
        var probe = Open();
        probe.Host = "station.local";
        probe.Port = "70000";

        // Act
        var isRead = probe.TryReadEndpoint(out var station);

        // Assert
        isRead.Should().BeFalse();
        station.Should().BeNull();
        probe.GetErrors(nameof(StationProbeViewModel.Host)).Cast<string>()
            .Should().Equal(Translated("The station address must be an IPv4 address such as 192.168.1.50."));
        probe.GetErrors(nameof(StationProbeViewModel.Port)).Cast<string>()
            .Should().Equal(Translated("The port must be a number from 1 to 65535."));
    }

    [Fact]
    public void TryReadEndpoint_AddressAndPortAsSaved_IsTheStation()
    {
        // Arrange
        var probe = Open();

        // Act
        var isRead = probe.TryReadEndpoint(out var station);

        // Assert
        isRead.Should().BeTrue();
        station.Should().Be(Station);
        probe.HasErrors.Should().BeFalse();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Errors_SavedAddressThatIsNone_IsMarkedAsTheWindowOpens(string language)
    {
        // Arrange
        Speak(language);
        _station.Settings.Current.Station.Host = "192.168.1";

        // Act
        var probe = Open();

        // Assert
        probe.Host.Should().Be("192.168.1");
        probe.GetErrors(nameof(StationProbeViewModel.Host)).Cast<string>()
            .Should().Equal(Translated("The station address must be an IPv4 address such as 192.168.1.50."));
        probe.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Errors_SavedPortOutOfRange_IsMarkedAsTheWindowOpens()
    {
        // Arrange
        _station.Settings.Current.Station.Port = 0;

        // Act
        var probe = Open();

        // Assert
        probe.Errors.Fields.Should().Equal(nameof(StationProbeViewModel.Port));
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.50")]
    public void Errors_NoAddressOrAValidOneSaved_MarkNothing(string host)
    {
        // Arrange
        _station.Settings.Current.Station.Host = host;

        // Act
        var probe = Open();

        // Assert
        probe.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void RefreshTexts_SavedAddressThatIsNone_IsMarkedAgainInTheNewLanguage()
    {
        // Arrange
        _station.Settings.Current.Station.Host = "192.168.1";
        var probe = Open();
        _ = probe.Errors;

        // Act
        Speak(Ukrainian);
        probe.RefreshTexts();

        // Assert
        probe.GetErrors(nameof(StationProbeViewModel.Host)).Cast<string>()
            .Should().Equal(Translated("The station address must be an IPv4 address such as 192.168.1.50."));
    }

    [Fact]
    public void Host_SavedAddressThatIsNoneBeingCorrected_IsNoLongerMarked()
    {
        // Arrange
        _station.Settings.Current.Station.Host = "192.168.1";
        var probe = Open();
        _ = probe.Errors;

        // Act
        probe.Host = "192.168.1.5";

        // Assert
        probe.HasErrors.Should().BeFalse();
    }

    private StationProbeViewModel Open(DiagnosticLog? log = null)
    {
        if (log is null)
        {
            log = new DiagnosticLog(_station.Storage, Options.Create(new LoggerOptions()));
            _opened.Add(log);
        }

        var reader = new StationReader(
            _client,
            Options.Create(new PollingSettings()),
            TimeProvider.System,
            NullLogger<StationReader>.Instance);
        var probe = new StationProbeViewModel(_station.Settings, _client, reader, log, _status);
        _opened.Add(probe);
        return probe;
    }

    /// <summary>Finds what the test says, and a station that answers no request but the one the test gives an answer to.</summary>
    private sealed class ScriptedApiClient : IMarstekApiClient
    {
        public IReadOnlyList<DiscoveredDevice> Found { get; set; } = [];

        public Task<IReadOnlyList<DiscoveredDevice>>? PendingSearch { get; set; }

        public MarstekApiException? SearchFailure { get; set; }

        public EnergySystemStatus? EnergySystem { get; set; }

        public int Searches { get; private set; }

        public int Requests { get; private set; }

        public Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(int port, CancellationToken token)
        {
            Searches++;
            return SearchFailure is { } failure ? Task.FromException<IReadOnlyList<DiscoveredDevice>>(failure)
                : PendingSearch ?? Task.FromResult(Found);
        }

        public Task<DeviceInfo> GetDeviceAsync(StationEndpoint station, CancellationToken token) => Silent<DeviceInfo>(station);

        public Task<WifiStatus> GetWifiStatusAsync(StationEndpoint station, CancellationToken token) => Silent<WifiStatus>(station);

        public Task<BluetoothStatus> GetBluetoothStatusAsync(StationEndpoint station, CancellationToken token) =>
            Silent<BluetoothStatus>(station);

        public Task<BatteryStatus> GetBatteryStatusAsync(StationEndpoint station, CancellationToken token) =>
            Silent<BatteryStatus>(station);

        public Task<PvStatus> GetPvStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
            Silent<PvStatus>(station);

        public Task<EnergySystemStatus> GetEnergySystemStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
            EnergySystem is { } answer ? Task.FromResult(answer) : Silent<EnergySystemStatus>(station);

        public Task<EnergySystemMode> GetEnergySystemModeAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
            Silent<EnergySystemMode>(station);

        public Task<EnergyMeterStatus> GetEnergyMeterStatusAsync(StationEndpoint station, FirmwareProfile profile, CancellationToken token) =>
            Silent<EnergyMeterStatus>(station);

        private Task<T> Silent<T>(StationEndpoint station)
        {
            Requests++;
            return Task.FromException<T>(new MarstekRequestTimeoutException(station, typeof(T).Name, TimeSpan.FromSeconds(3)));
        }
    }
}
