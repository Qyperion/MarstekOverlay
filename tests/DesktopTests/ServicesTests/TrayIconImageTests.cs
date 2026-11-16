using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public class TrayIconImageTests
{
    // The size of the notification area at 100% scaling, where the charge has eight pixel columns to fill.
    private const int SmallIcon = 16;

    [Theory]
    [InlineData(100, 8)]
    [InlineData(94, 8)]
    [InlineData(50, 4)]
    [InlineData(12, 1)]
    public void LookOf_Charge_FillsItsShareOfTheColumns(double socPercent, int filledColumns)
    {
        // Act
        var look = TrayIconImage.LookOf(State(socPercent), SmallIcon);

        // Assert
        look.FilledColumns.Should().Be(filledColumns);
    }

    [Fact]
    public void LookOf_ChargeTooLowForAColumn_StillShowsOne()
    {
        TrayIconImage.LookOf(State(socPercent: 1), SmallIcon).FilledColumns.Should().Be(1);
    }

    [Fact]
    public void LookOf_NoCharge_FillsNothing()
    {
        TrayIconImage.LookOf(State(socPercent: 0), SmallIcon).FilledColumns.Should().Be(0);
    }

    [Theory]
    [InlineData(24, 14)]
    [InlineData(32, 16)]
    public void LookOf_LargerIcon_HasMoreColumnsToFill(int size, int columnsWhenFull)
    {
        // Act
        var full = TrayIconImage.LookOf(State(socPercent: 100), size);
        var half = TrayIconImage.LookOf(State(socPercent: 50), size);

        // Assert
        full.Should().Be(new TrayIconLook(size, columnsWhenFull, Tone.Positive, SupplyMark.OnMains));
        half.FilledColumns.Should().Be(columnsWhenFull / 2);
    }

    [Fact]
    public void LookOf_ToneAndMark_ComeFromTheState()
    {
        // Arrange
        var state = State(socPercent: 15) with { SocTone = Tone.Critical, Supply = SupplyMark.OnBattery };

        // Act
        var look = TrayIconImage.LookOf(state, SmallIcon);

        // Assert
        look.Should().Be(new TrayIconLook(SmallIcon, FilledColumns: 1, Tone.Critical, SupplyMark.OnBattery));
    }

    [Fact]
    public void LookOf_NoConnection_LooksTheSameWhateverTheStaleReadingsSay()
    {
        // Arrange
        var full = State(socPercent: 100) with { Supply = SupplyMark.NoConnection };
        var low = State(socPercent: 15) with { SocTone = Tone.Critical, Supply = SupplyMark.NoConnection };

        // Act
        var look = TrayIconImage.LookOf(full, SmallIcon);

        // Assert
        look.Should().Be(new TrayIconLook(SmallIcon, FilledColumns: 0, Tone.Secondary, SupplyMark.NoConnection));
        TrayIconImage.LookOf(low, SmallIcon).Should().Be(look);
    }

    [Fact]
    public void LookOf_ChargeThatMovesWithinAColumn_GivesNoReasonToRedraw()
    {
        // Act
        var before = TrayIconImage.LookOf(State(socPercent: 50) with { StateText = "On mains" }, SmallIcon);
        var after = TrayIconImage.LookOf(State(socPercent: 54) with { StateText = "On mains · charging" }, SmallIcon);

        // Assert
        after.Should().Be(before);
    }

    private static OverlayState State(double socPercent) => new()
    {
        SocPercent = socPercent,
        SocTone = Tone.Positive,
        Supply = SupplyMark.OnMains,
    };
}
