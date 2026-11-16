using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Models;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class StationSectionsTests : LocalizedTests
{
    private static readonly DateTimeOffset AnsweredAt = Moments.Local(day: 12, hour: 12, minute: 42);

    // As Venus E 3.0 answers: it does not know the request about solar inputs.
    private static readonly Dictionary<string, DateTimeOffset> WithoutSolarInputs = new() { [MarstekMethods.PvGetStatus] = AnsweredAt };

    [Fact]
    public void Describe_NothingAnswered_ListsEveryRequestAsUnanswered()
    {
        // Act
        var sections = StationSections.Describe(StationSnapshot.Empty);

        // Assert
        sections.Select(section => section.Method).Should().Equal(
            MarstekMethods.GetDevice,
            MarstekMethods.EnergySystemGetStatus,
            MarstekMethods.EnergySystemGetMode,
            MarstekMethods.EnergyMeterGetStatus,
            MarstekMethods.PvGetStatus,
            MarstekMethods.BatteryGetStatus,
            MarstekMethods.WifiGetStatus,
            MarstekMethods.BluetoothGetStatus);
        sections.Should().AllSatisfy(section =>
        {
            section.State.Should().Be(SectionState.NoAnswer);
            section.ReceivedAt.Should().BeNull();
            section.Rows.Should().BeEmpty();
            section.Note.Should().Be("no answer");
        });
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_DeviceOfAModelNotKnown_SaysItsFamilyIsUnknown(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Device = new Reading<DeviceInfo>(new DeviceInfo { Model = "Jupiter X" }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.GetDevice);

        // Assert
        section.Rows[2].Value.Should().Be(Translated("unknown"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_MethodTheStationTurnedDown_IsToldApartFromSilence(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with { UnsupportedMethods = WithoutSolarInputs };

        // Act
        var sections = StationSections.Describe(snapshot);

        // Assert
        var solarInputs = SectionOf(sections, MarstekMethods.PvGetStatus);
        solarInputs.Title.Should().Be(Translated("Solar inputs"));
        solarInputs.State.Should().Be(SectionState.NotSupported);
        solarInputs.Note.Should().Be(Translated("not supported by this station"));
        solarInputs.ReceivedAt.Should().Be(AnsweredAt, "a refusal is an answer too, and tells when the station was asked");
        SectionOf(sections, MarstekMethods.BatteryGetStatus).Note.Should().Be(Translated("no answer"));
    }

    [Fact]
    public void Describe_EnergySystem_NamesEveryValueWithItsUnit()
    {
        // Arrange
        var snapshot = StationReadings.Answered(AnsweredAt, new EnergySystemStatus
        {
            BatterySocPercent = 100,
            BatteryCapacity = new Energy(5120),
            PvPower = Power.Zero,
            OngridPower = Power.Zero,
            OffgridPower = new Power(286),
            TotalGridInputEnergy = new Energy(18490),
        });

        // Act
        var section = SectionOf(StationSections.Describe(snapshot, PowerSource.Mains), MarstekMethods.EnergySystemGetStatus);

        // Assert
        section.Title.Should().Be("Energy system");
        section.State.Should().Be(SectionState.Answered);
        section.Note.Should().BeNull();
        section.ReceivedAt.Should().Be(AnsweredAt);
        section.Rows.Should().Equal(
            new SectionRow("State of charge", "100%"),
            new SectionRow("Battery capacity", "5.12 kWh"),
            new SectionRow("Battery power", "0 W"),
            new SectionRow("Solar power", "0 W"),
            new SectionRow("Grid power", "0 W"),
            new SectionRow("Backup output power", "286 W"),
            new SectionRow("Total solar energy", Value: null),
            new SectionRow("Total energy to grid", Value: null),
            new SectionRow("Total energy from grid", "18.49 kWh"),
            new SectionRow("Total load energy", Value: null));
    }

    [Fact]
    public void Describe_EnergySystemOfAStationWithoutSolarInputs_LeavesTheirZerosOut()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt, loadWatts: 286, gridEnergyWattHours: 18490) with
        {
            UnsupportedMethods = WithoutSolarInputs,
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot, PowerSource.Mains), MarstekMethods.EnergySystemGetStatus);

        // Assert
        section.Rows.Select(row => row.Label).Should().Equal(
            "State of charge",
            "Battery capacity",
            "Battery power",
            "Grid power",
            "Backup output power",
            "Total energy to grid",
            "Total energy from grid",
            "Total load energy");
    }

    [Fact]
    public void Describe_EnergySystemWithCountersThatNeverMoved_LeavesThemOut()
    {
        // Arrange
        var snapshot = StationReadings.Answered(AnsweredAt, new EnergySystemStatus
        {
            BatterySocPercent = 100,
            PvPower = Power.Zero,
            OngridPower = Power.Zero,
            OffgridPower = new Power(286),
            TotalPvEnergy = new Energy(0),
            TotalGridOutputEnergy = new Energy(0),
            TotalGridInputEnergy = new Energy(24690),
            TotalLoadEnergy = new Energy(0),
        });

        // Act
        var section = SectionOf(StationSections.Describe(snapshot, PowerSource.Mains), MarstekMethods.EnergySystemGetStatus);

        // Assert
        section.Rows.Select(row => row.Label).Should().Equal(
            "State of charge",
            "Battery capacity",
            "Battery power",
            "Solar power",
            "Grid power",
            "Backup output power",
            "Total energy from grid");
        ValueOf(section, "Total energy from grid").Should().Be("24.69 kWh");
    }

    [Fact]
    public void Describe_EnergySystemBesideTheBatteryOfTheSameCapacity_LeavesTheCapacityToTheBattery()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt) with
        {
            Battery = new Reading<BatteryStatus>(new BatteryStatus { RatedCapacity = StationReadings.Capacity }, AnsweredAt),
        };

        // Act
        var sections = StationSections.Describe(snapshot, PowerSource.Mains);

        // Assert
        SectionOf(sections, MarstekMethods.EnergySystemGetStatus).Rows.Should().NotContain(row => row.Label == "Battery capacity");
        ValueOf(SectionOf(sections, MarstekMethods.BatteryGetStatus), "Rated capacity").Should().Be("5.12 kWh");
    }

    [Fact]
    public void Describe_SolarPowerFromAStationThatTurnsItsInputsDown_IsShownAllTheSame()
    {
        // Arrange
        var snapshot = StationReadings.Answered(AnsweredAt, new EnergySystemStatus { PvPower = new Power(300) }) with
        {
            UnsupportedMethods = WithoutSolarInputs,
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetStatus);

        // Assert
        ValueOf(section, "Solar power").Should().Be("300 W");
        ValueOf(section, "Total solar energy").Should().BeNull();
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_SourceNotKnownAndGridPortSilent_OwnsUpThatTheBatteryPowerIsAGuess(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationReadings.HoldingFull(AnsweredAt, loadWatts: 286);

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetStatus);

        // Assert
        ValueOf(section, Translated("Battery power")).Should().Be(
            Translated("discharging 286 W (calculated; true only if the station is running on the battery)"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_SourceNotKnownButGridAtWork_TellsTheBatteryPowerByItsDirection(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationReadings.Charging(AnsweredAt, loadWatts: 286);

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetStatus);

        // Assert
        ValueOf(section, Translated("Battery power")).Should().Be(Translated("charging 2200 W"));
    }

    [Theory]
    [InlineData(-180, "discharging 180 W")]
    [InlineData(-0.4, "0 W")]
    [InlineData(0, "0 W")]
    [InlineData(35, "charging 35 W")]
    public void Describe_BatteryPowerReportedByTheStation_IsToldByItsDirectionWithoutASign(double watts, string expected)
    {
        // Arrange
        var snapshot = StationReadings.Answered(AnsweredAt, new EnergySystemStatus { BatteryPower = new Power(watts) });

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetStatus);

        // Assert
        ValueOf(section, "Battery power").Should().Be(expected);
    }

    [Fact]
    public void Describe_Device_ShowsTheFirmwareAndTheFamilyItStandsFor()
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Device = new Reading<DeviceInfo>(new DeviceInfo { Model = "VenusE 3.0", FirmwareVersion = 150, WifiName = "Home" }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.GetDevice);

        // Assert
        section.Rows.Should().Equal(
            new SectionRow("Model", "VenusE 3.0"),
            new SectionRow("Firmware", "150"),
            new SectionRow("Family", "Venus E 3.0"),
            new SectionRow("Bluetooth MAC", Value: null),
            new SectionRow("Wi-Fi MAC", Value: null),
            new SectionRow("Wi-Fi network", "Home"),
            new SectionRow("Reported IP", Value: null));
    }

    [Fact]
    public void Describe_DeviceBesideAnswersThatTellTheSame_KeepsOnlyWhatTheyDoNotTell()
    {
        // Arrange
        var device = new DeviceInfo
        {
            Model = "VenusE 3.0",
            FirmwareVersion = 150,
            BluetoothMac = "acd929a7f2c1",
            WifiMac = "485519C3D2B0",
            WifiName = "Home",
            ReportedIp = "192.168.1.40",
        };
        var snapshot = StationSnapshot.Empty with
        {
            Device = new Reading<DeviceInfo>(device, AnsweredAt),
            Wifi = new Reading<WifiStatus>(new WifiStatus { Mac = "485519c3d2b0", Ssid = "Home", Ip = "192.168.1.50" }, AnsweredAt),
            Bluetooth = new Reading<BluetoothStatus>(new BluetoothStatus { Mac = "acd929a7f2c1" }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.GetDevice);

        // Assert
        section.Rows.Should().Equal(
            new SectionRow("Model", "VenusE 3.0"),
            new SectionRow("Firmware", "150"),
            new SectionRow("Family", "Venus E 3.0"),
            new SectionRow("Reported IP", "192.168.1.40"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_Battery_WordsThePermissions(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Battery = new Reading<BatteryStatus>(
                new BatteryStatus
                {
                    SocPercent = 99,
                    TemperatureCelsius = 22.5,
                    RemainingCapacity = new Energy(5068.8),
                    RatedCapacity = new Energy(5120),
                    IsChargingAllowed = true,
                    IsDischargingAllowed = false,
                },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.BatteryGetStatus);

        // Assert
        section.Rows.Should().Equal(
            new SectionRow(Translated("State of charge"), "99%"),
            new SectionRow(Translated("Temperature"), Translated("22.5 °C")),
            new SectionRow(Translated("Remaining capacity"), Translated("5.07 kWh")),
            new SectionRow(Translated("Rated capacity"), Translated("5.12 kWh")),
            new SectionRow(Translated("Charging allowed"), Translated("yes")),
            new SectionRow(Translated("Discharging allowed"), Translated("no")));
    }

    [Fact]
    public void Describe_BatteryBesideTheEnergySystem_LeavesTheChargeToIt()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt) with
        {
            Battery = new Reading<BatteryStatus>(new BatteryStatus { SocPercent = 99, TemperatureCelsius = 22.5 }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.BatteryGetStatus);

        // Assert
        section.Rows.Select(row => row.Label).Should().Equal(
            "Temperature",
            "Remaining capacity",
            "Rated capacity",
            "Charging allowed",
            "Discharging allowed");
    }

    [Fact]
    public void Describe_ModeBesideTheEnergySystem_LeavesTheFlowsAndTheChargeToIt()
    {
        // Arrange
        var snapshot = StationReadings.HoldingFull(AnsweredAt, loadWatts: 286) with
        {
            Mode = new Reading<EnergySystemMode>(
                new EnergySystemMode
                {
                    Mode = OperatingMode.Manual,
                    OngridPower = Power.Zero,
                    OffgridPower = new Power(290),
                    BatterySocPercent = 100,
                },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetMode);

        // Assert
        section.Rows.Should().Equal(new SectionRow("Mode", "Manual"));
    }

    [Fact]
    public void Describe_ModeWithoutACopyOfTheMeter_LeavesTheMeterOut()
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Mode = new Reading<EnergySystemMode>(
                new EnergySystemMode { Mode = OperatingMode.Manual, OffgridPower = new Power(286), BatterySocPercent = 100 },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetMode);

        // Assert
        section.Rows.Should().Equal(
            new SectionRow("Mode", "Manual"),
            new SectionRow("Grid power", Value: null),
            new SectionRow("Backup output power", "286 W"),
            new SectionRow("State of charge", "100%"));
    }

    [Fact]
    public void Describe_ModeWithACopyOfAMeterThatIsNotConnected_LeavesItsZerosOut()
    {
        // Arrange
        var meter = new EnergyMeterStatus { IsCtConnected = false, TotalPower = Power.Zero };
        var snapshot = StationSnapshot.Empty with
        {
            Mode = new Reading<EnergySystemMode>(new EnergySystemMode { Mode = OperatingMode.Ai, Meter = meter }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetMode);

        // Assert
        section.Rows.Select(row => row.Label).Should().Equal("Mode", "Grid power", "Backup output power", "State of charge");
    }

    [Fact]
    public void Describe_ModeWithACopyOfTheMeter_AddsWhatTheCopyHolds()
    {
        // Arrange
        var meter = new EnergyMeterStatus { IsCtConnected = true, TotalPower = Power.Zero };
        var snapshot = StationSnapshot.Empty with
        {
            Mode = new Reading<EnergySystemMode>(new EnergySystemMode { Mode = OperatingMode.Ai, Meter = meter }, AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergySystemGetMode);

        // Assert
        section.Rows.Select(row => (row.Label, row.Value)).Should().Equal(
            ("Mode", "AI"),
            ("Grid power", null),
            ("Backup output power", null),
            ("State of charge", null),
            ("CT connected", "yes"),
            ("Total power", "0 W"));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_MeterWithoutACt_SaysSoInsteadOfListingItsZeros(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Meter = new Reading<EnergyMeterStatus>(
                new EnergyMeterStatus { IsCtConnected = false, PhaseAPower = Power.Zero, TotalPower = Power.Zero },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergyMeterGetStatus);

        // Assert
        section.State.Should().Be(SectionState.NotConnected);
        section.Note.Should().Be(Translated("CT not connected"));
        section.Rows.Should().BeEmpty();
        section.ReceivedAt.Should().Be(AnsweredAt);
    }

    [Fact]
    public void Describe_Meter_KeepsARowForEveryValueWhetherSentOrNot()
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Meter = new Reading<EnergyMeterStatus>(
                new EnergyMeterStatus { IsCtConnected = true, PhaseAPower = new Power(120), InputEnergy = new Energy(1250.5) },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.EnergyMeterGetStatus);

        // Assert
        section.Rows.Select(row => (row.Label, row.Value)).Should().Equal(
            ("CT connected", "yes"),
            ("Phase A power", "120 W"),
            ("Phase B power", null),
            ("Phase C power", null),
            ("Total power", null),
            ("Input energy", "1.25 kWh"),
            ("Output energy", null));
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_SolarInputs_PutEachInputOnOneRow(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Pv = new Reading<PvStatus>(
                new PvStatus
                {
                    Channels =
                    [
                        new PvChannel { Number = 1, Power = new Power(120), VoltageVolts = 36.5, CurrentAmperes = 3.29, IsWorking = true },
                        new PvChannel { Number = 2, Power = Power.Zero, IsWorking = false },
                    ],
                },
                AnsweredAt),
        };

        // Act
        var section = SectionOf(StationSections.Describe(snapshot), MarstekMethods.PvGetStatus);

        // Assert
        section.Rows.Should().Equal(
            new SectionRow(Translated("Total power"), Translated("120 W")),
            new SectionRow(Translated("Input 1"), Translated("120 W, 36.5 V, 3.29 A, working")),
            new SectionRow(Translated("Input 2"), Translated("0 W, standby")));
    }

    [Fact]
    public void Describe_Wifi_PassesTheTextsOfTheStationThrough()
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Wifi = new Reading<WifiStatus>(new WifiStatus { Ssid = "Home", Ip = "192.168.1.50" }, AnsweredAt),
        };

        // Act
        var wifi = SectionOf(StationSections.Describe(snapshot), MarstekMethods.WifiGetStatus);

        // Assert
        ValueOf(wifi, "Network").Should().Be("Home");
        ValueOf(wifi, "Signal").Should().BeNull();
        ValueOf(wifi, "IP").Should().Be("192.168.1.50");
        ValueOf(wifi, "Gateway").Should().BeNull();
    }

    [Theory]
    [InlineData(-45, "-45 dBm, excellent")]
    [InlineData(-50, "-50 dBm, excellent")]
    [InlineData(-51, "-51 dBm, good")]
    [InlineData(-60, "-60 dBm, good")]
    [InlineData(-61, "-61 dBm, fair")]
    [InlineData(-70, "-70 dBm, fair")]
    [InlineData(-71, "-71 dBm, weak")]
    public void Describe_WifiSignal_SaysHowGoodItIs(double dbm, string expected)
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Wifi = new Reading<WifiStatus>(new WifiStatus { SignalStrengthDbm = dbm }, AnsweredAt),
        };

        // Act
        var wifi = SectionOf(StationSections.Describe(snapshot), MarstekMethods.WifiGetStatus);

        // Assert
        ValueOf(wifi, "Signal").Should().Be(expected);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Describe_WifiSignalAndBluetoothState_AreWordedInTheLanguageOfTheApp(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Wifi = new Reading<WifiStatus>(new WifiStatus { SignalStrengthDbm = -62 }, AnsweredAt),
            Bluetooth = new Reading<BluetoothStatus>(new BluetoothStatus { State = "disconnect" }, AnsweredAt),
        };

        // Act
        var sections = StationSections.Describe(snapshot);

        // Assert
        ValueOf(SectionOf(sections, MarstekMethods.WifiGetStatus), Translated("Signal")).Should().Be(Translated("-62 dBm, fair"));
        ValueOf(SectionOf(sections, MarstekMethods.BluetoothGetStatus), Translated("State")).Should().Be(Translated("not connected"));
    }

    [Theory]
    [InlineData("connect", "connected")]
    [InlineData("Disconnect", "not connected")]
    [InlineData("advertising", "advertising")]
    [InlineData(null, null)]
    public void Describe_BluetoothState_WordsTheStatesSeenAndPassesAnyOtherThrough(string? state, string? expected)
    {
        // Arrange
        var snapshot = StationSnapshot.Empty with
        {
            Bluetooth = new Reading<BluetoothStatus>(new BluetoothStatus { State = state }, AnsweredAt),
        };

        // Act
        var bluetooth = SectionOf(StationSections.Describe(snapshot), MarstekMethods.BluetoothGetStatus);

        // Assert
        ValueOf(bluetooth, "State").Should().Be(expected);
    }

    [Theory]
    [InlineData(English)]
    [InlineData(Ukrainian)]
    public void Report_ListsTheSectionsAsPlainText(string language)
    {
        // Arrange
        Speak(language);
        var snapshot = StationSnapshot.Empty with
        {
            Meter = new Reading<EnergyMeterStatus>(new EnergyMeterStatus { IsCtConnected = false }, AnsweredAt),
            Bluetooth = new Reading<BluetoothStatus>(new BluetoothStatus { State = "disconnect" }, AnsweredAt),
            UnsupportedMethods = WithoutSolarInputs,
            Errors = [new UnansweredRequest(MarstekMethods.WifiGetStatus, TimeSpan.FromSeconds(3))],
        };

        // Act
        var report = StationReport.Describe(snapshot);

        // Assert
        var lines = report.Split(Environment.NewLine);
        lines.Should().ContainInConsecutiveOrder(
            Translated("Energy meter") + " (EM.GetStatus)",
            "  " + Translated("CT not connected"),
            string.Empty);
        lines.Should().ContainInConsecutiveOrder(
            Translated("Solar inputs") + " (PV.GetStatus)",
            "  " + Translated("not supported by this station"),
            string.Empty);
        lines.Should().ContainInConsecutiveOrder(
            "Bluetooth (BLE.GetStatus)",
            "  " + Translated("State") + ": " + Translated("not connected"),
            "  " + Translated("MAC") + ": —",
            string.Empty,
            Translated("Errors"),
            "  " + Translated("The station did not answer Wifi.GetStatus within 3 s."));
        lines[^1].Should().Be("  " + Translated("The station did not answer Wifi.GetStatus within 3 s."));
    }

    private static StationSection SectionOf(IReadOnlyList<StationSection> sections, string method) =>
        sections.Single(section => section.Method == method);

    private static string? ValueOf(StationSection section, string label) =>
        section.Rows.Single(row => row.Label == label).Value;
}
