using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public sealed class UserSettingsStoreTests : IDisposable
{
    // What the app wrote before the allowance for losses was split in two and the update time lost its option.
    private const string SavedByAnEarlierVersion = """
        {
          "Language": "uk",
          "Station": { "Host": "192.168.1.50", "Port": 30010 },
          "Overlay": {
            "Left": 3398.5,
            "Top": 674.5,
            "IsTopmost": true,
            "OpacityPercent": 65,
            "IsUpdateTimeRelative": true,
            "HiddenRows": [ "Grid", "Solar" ]
          },
          "Forecast": { "ReserveSocPercent": 15, "UsableEnergyPercent": 85 }
        }
        """;

    private readonly TemporaryDirectory _directory = new();
    private readonly StorageSettings _storage;

    public UserSettingsStoreTests()
    {
        _storage = new StorageSettings { RootDirectory = _directory.Path };
    }

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Load_NoFileYet_StartsWithTheDefaults()
    {
        // Act
        var settings = Open().Current;

        // Assert
        settings.Station.ToEndpoint().Should().BeNull();
        settings.Forecast.DischargeEfficiencyPercent.Should().Be(90);
        settings.Forecast.ChargeEfficiencyPercent.Should().Be(97);
    }

    [Fact]
    public void Load_FileOfAnEarlierVersion_KeepsEveryChoiceAndTakesItsAllowanceForTheDischarge()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, SavedByAnEarlierVersion);

        // Act
        var settings = Open().Current;

        // Assert
        settings.Language.Should().Be("uk");
        settings.Station.Host.Should().Be("192.168.1.50");
        settings.Station.Port.Should().Be(30010);
        settings.Overlay.Left.Should().Be(3398.5);
        settings.Overlay.Top.Should().Be(674.5);
        settings.Overlay.IsTopmost.Should().BeTrue();
        settings.Overlay.OpacityPercent.Should().Be(65);
        settings.Overlay.IsHiddenAtStartup.Should().BeFalse();
        settings.Overlay.HiddenRows.Should().Equal(OverlayRowKind.Grid, OverlayRowKind.Solar);
        settings.Forecast.ReserveSocPercent.Should().Be(15);
        settings.Forecast.DischargeEfficiencyPercent.Should().Be(85);
        settings.Forecast.ChargeEfficiencyPercent.Should().Be(97);
        settings.Details.ChartHours.Should().Be(24);
        settings.Details.Window.Should().BeNull();
    }

    [Fact]
    public void Save_PlaceTheDetailsWindowWasLeftAt_IsThereAfterARestart()
    {
        // Arrange
        var place = new SavedWindowPlace(Left: 1820.5, Top: -40, Width: 1100, Height: 700, IsMaximized: true);

        // Act
        Open().Change(current => current.Details.Window = place);

        // Assert
        Open().Current.Details.Window.Should().Be(place);
    }

    [Fact]
    public void Save_PanelHiddenAtStartup_IsThereAfterARestart()
    {
        // Act
        Open().Change(current => current.Overlay.IsHiddenAtStartup = true);

        // Assert
        Open().Current.Overlay.IsHiddenAtStartup.Should().BeTrue();
    }

    [Fact]
    public void Load_DetailsSavedBeforeTheWindowRememberedItsPlace_KeepsThePeriodAndHasNoPlace()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, """{ "Details": { "ChartHours": 72 } }""");

        // Act
        var details = Open().Current.Details;

        // Assert
        details.ChartHours.Should().Be(72);
        details.Window.Should().BeNull();
    }

    [Fact]
    public void Save_FileOfAnEarlierVersion_IsWrittenBackUnderTheNewNames()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, SavedByAnEarlierVersion);

        // Act
        Open().Save();

        // Assert
        var saved = File.ReadAllText(_storage.UserSettingsPath);
        saved.Should().Contain("\"DischargeEfficiencyPercent\": 85").And.Contain("\"ChargeEfficiencyPercent\": 97");
        saved.Should().NotContain("UsableEnergyPercent").And.NotContain("IsUpdateTimeRelative");
        Open().Current.Forecast.DischargeEfficiencyPercent.Should().Be(85);
    }

    [Fact]
    public void Load_FileThatAlreadyNamesTheDischarge_LeavesItAlone()
    {
        // Arrange
        File.WriteAllText(
            _storage.UserSettingsPath,
            """{ "Forecast": { "UsableEnergyPercent": 85, "DischargeEfficiencyPercent": 92, "ChargeEfficiencyPercent": 95 } }""");

        // Act
        var forecast = Open().Current.Forecast;

        // Assert
        forecast.DischargeEfficiencyPercent.Should().Be(92);
        forecast.ChargeEfficiencyPercent.Should().Be(95);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    [InlineData("""{ "Forecast": 5 }""")]
    public void Load_FileThatCannotBeRead_StartsWithTheDefaults(string content)
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, content);

        // Act
        var settings = Open().Current;

        // Assert
        settings.Forecast.DischargeEfficiencyPercent.Should().Be(90);
        settings.Overlay.OpacityPercent.Should().Be(80);
    }

    [Fact]
    public void Save_FileThatCouldNotBeReadAndNothingChanged_LeavesTheFileAsItIs()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, "not json at all");
        var store = Open();

        // Act
        store.Save();

        // Assert
        File.ReadAllText(_storage.UserSettingsPath).Should().Be("not json at all");
        File.Exists(store.BackupPath).Should().BeFalse();
    }

    [Fact]
    public void Change_FileThatCouldNotBeRead_KeepsACopyOfItBeforeWritingTheChange()
    {
        // Arrange
        const string Truncated = """{ "Station": { "Host": "192.168.1.50" """;
        File.WriteAllText(_storage.UserSettingsPath, Truncated);
        var store = Open();

        // Act
        store.Change(current => current.Overlay.IsTopmost = true);

        // Assert
        File.ReadAllText(store.BackupPath).Should().Be(Truncated);
        Open().Current.Overlay.IsTopmost.Should().BeTrue();
    }

    [Fact]
    public void Save_FileThatWasBusyAtTheStart_IsNotWrittenOverWithTheDefaults()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, """{ "Station": { "Host": "192.168.1.50" } }""");
        UserSettingsStore store;
        using (new FileStream(_storage.UserSettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
            store = Open();

        // Act
        store.Save();

        // Assert
        store.Current.Station.Host.Should().BeEmpty();
        Open().Current.Station.Host.Should().Be("192.168.1.50");
    }

    [Fact]
    public void Change_FileThatCannotBeReplaced_KeepsTheOldFileWholeAndLeavesNothingBehind()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, """{ "Station": { "Host": "192.168.1.50" } }""");
        var store = Open();
        File.SetAttributes(_storage.UserSettingsPath, FileAttributes.ReadOnly);

        try
        {
            // Act
            store.Change(current => current.Station.Host = "192.168.1.60");
        }
        finally
        {
            File.SetAttributes(_storage.UserSettingsPath, FileAttributes.Normal);
        }

        // Assert
        Open().Current.Station.Host.Should().Be("192.168.1.50");
        Directory.GetFiles(_directory.Path).Should().Equal(_storage.UserSettingsPath);
    }

    [Fact]
    public void Change_FileThereBefore_IsReplacedAndLeavesNothingBehind()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, SavedByAnEarlierVersion);

        // Act
        Open().Change(current => current.Station.Host = "192.168.1.60");

        // Assert
        Open().Current.Station.Host.Should().Be("192.168.1.60");
        Directory.GetFiles(_directory.Path).Should().Equal(_storage.UserSettingsPath);
    }

    [Fact]
    public void Load_ListNamingAKindThisVersionDoesNotKnow_LeavesOnlyThatOneOut()
    {
        // Arrange
        File.WriteAllText(
            _storage.UserSettingsPath,
            """
            {
              "Station": { "Host": "192.168.1.50" },
              "Overlay": { "HiddenRows": [ "Grid", "Teleport", "Solar" ], "ShownRows": [ 7, "WifiSignal" ] },
              "Alerts": { "DisabledKinds": [ "SolarFlare", "StaleData" ] }
            }
            """);

        // Act
        var settings = Open().Current;

        // Assert
        settings.Station.Host.Should().Be("192.168.1.50");
        settings.Overlay.HiddenRows.Should().Equal(OverlayRowKind.Grid, OverlayRowKind.Solar);
        settings.Overlay.ShownRows.Should().Equal(OverlayRowKind.WifiSignal);
        settings.Alerts.DisabledKinds.Should().Equal(AlertKind.StaleData);
    }

    [Theory]
    [InlineData("""{ "Language": null }""")]
    [InlineData("""{ "Station": null }""")]
    [InlineData("""{ "Station": { "Host": null } }""")]
    [InlineData("""{ "Overlay": null }""")]
    [InlineData("""{ "Overlay": { "HiddenRows": null, "ShownRows": null } }""")]
    [InlineData("""{ "Forecast": null }""")]
    [InlineData("""{ "Alerts": null }""")]
    [InlineData("""{ "Alerts": { "DisabledKinds": null, "ChargeThresholdsPercent": null } }""")]
    [InlineData("""{ "Details": null }""")]
    public void Load_PartThatIsNull_TakesItsDefaults(string content)
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, content);

        // Act
        var settings = Open().Current;

        // Assert
        settings.Language.Should().NotBeNull();
        settings.Station.ToEndpoint().Should().BeNull();
        settings.Overlay.Shows(OverlayRowKind.Battery).Should().BeTrue();
        settings.Forecast.ChargeEfficiencyPercent.Should().Be(97);
        settings.Alerts.Watches(AlertKind.LowCharge).Should().BeTrue();
        settings.Alerts.ChargeThresholdsPercent.Should().NotBeNull();
        settings.Details.Should().NotBeNull();
    }

    [Fact]
    public void Load_NumbersOutsideWhatTheSettingsWindowAccepts_ArePulledWithin()
    {
        // Arrange
        File.WriteAllText(
            _storage.UserSettingsPath,
            """
            {
              "Overlay": { "OpacityPercent": 5, "ScalePercent": 500, "ChartHours": 0 },
              "Forecast": { "ReserveSocPercent": 150, "DischargeEfficiencyPercent": 120, "ChargeEfficiencyPercent": 0 },
              "Alerts": {
                "ChargeThresholdsPercent": [ 50, 0, 150, 15 ],
                "MinBatteryTemperatureCelsius": 60,
                "MaxBatteryTemperatureCelsius": 10,
                "StaleAfterMinutes": 0
              }
            }
            """);

        // Act
        var settings = Open().Current;

        // Assert
        settings.Overlay.OpacityPercent.Should().Be(20);
        settings.Overlay.ScalePercent.Should().Be(200);
        settings.Overlay.ChartHours.Should().Be(1);
        settings.Forecast.ReserveSocPercent.Should().Be(99);
        settings.Forecast.DischargeEfficiencyPercent.Should().Be(100);
        settings.Forecast.ChargeEfficiencyPercent.Should().Be(50);
        settings.Alerts.ChargeThresholdsPercent.Should().Equal(50, 15);
        settings.Alerts.MinBatteryTemperatureCelsius.Should().Be(5);
        settings.Alerts.MaxBatteryTemperatureCelsius.Should().Be(45);
        settings.Alerts.StaleAfterMinutes.Should().Be(1);
    }

    [Fact]
    public void Load_ChartPeriodThatIsNotOffered_TakesTheOneTheSettingsWindowMarksAndSaysSo()
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, """{ "Overlay": { "ChartHours": 5 } }""");
        var logger = new RecordingLogger<UserSettingsStore>();

        // Act
        var settings = new UserSettingsStore(_storage, logger).Current;

        // Assert
        settings.Overlay.ChartHours.Should().Be(3);
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Overlay.ChartHours"));
    }

    [Theory]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.050")]
    public void Load_AddressThatIsNone_KeepsItAndSaysNoStationIsPolled(string host)
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, $$"""{ "Station": { "Host": "{{host}}" } }""");
        var logger = new RecordingLogger<UserSettingsStore>();

        // Act
        var settings = new UserSettingsStore(_storage, logger).Current;

        // Assert
        settings.Station.Host.Should().Be(host);
        settings.Station.ToEndpoint().Should().BeNull();
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning).Which.Message
            .Should().Contain("Station.Host").And.Contain(host).And.Contain("no station is polled");
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.50")]
    public void Load_NoAddressOrAValidOne_SaysNothingOfIt(string host)
    {
        // Arrange
        File.WriteAllText(_storage.UserSettingsPath, $$"""{ "Station": { "Host": "{{host}}" } }""");
        var logger = new RecordingLogger<UserSettingsStore>();

        // Act
        _ = new UserSettingsStore(_storage, logger);

        // Assert
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Warning);
    }

    private UserSettingsStore Open() => new(_storage, NullLogger<UserSettingsStore>.Instance);
}
