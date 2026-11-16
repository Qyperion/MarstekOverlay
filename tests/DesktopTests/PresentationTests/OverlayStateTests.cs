using FluentAssertions;

using MarstekOverlay.Desktop.Presentation;

using Xunit;

namespace MarstekOverlay.DesktopTests.PresentationTests;

public class OverlayStateTests
{
    [Fact]
    public void KeepingListsOf_ListsThatReadTheSame_AreTakenOverFromWhatIsShown()
    {
        // Arrange
        var shown = State("Data from 22:39:46 is stale");
        var next = State("Data from 22:40:16 is stale");

        // Act
        var kept = next.KeepingListsOf(shown);

        // Assert
        kept.Rows.Should().BeSameAs(shown.Rows);
        kept.Alerts.Should().BeSameAs(shown.Alerts);
        kept.Summary.Should().BeSameAs(shown.Summary);
        kept.StaleText.Should().Be("Data from 22:40:16 is stale");
    }

    [Fact]
    public void KeepingListsOf_ListThatChanged_IsTheNewOne()
    {
        // Arrange
        var shown = State("Data from 22:39:46 is stale");
        var next = State("Data from 22:40:16 is stale") with { Rows = [new OverlayRow(OverlayRowKind.Grid, "Grid", "-2450 W")] };

        // Act
        var kept = next.KeepingListsOf(shown);

        // Assert
        kept.Rows.Should().BeSameAs(next.Rows);
        kept.Alerts.Should().BeSameAs(shown.Alerts);
        kept.Summary.Should().BeSameAs(shown.Summary);
    }

    [Theory]
    [InlineData("Grid", true, null)]
    [InlineData("Grid", false, "Since 12:42.")]
    [InlineData("Mains", false, null)]
    public void KeepingListsOf_RowThatDiffersInAnythingButItsValue_CountsAsChanged(string label, bool isProminent, string? hint)
    {
        // Arrange
        var shown = State("Data from 22:39:46 is stale");
        var next = shown with { Rows = [new OverlayRow(OverlayRowKind.Grid, label, "0 W", isProminent, hint)] };

        // Act
        var kept = next.KeepingListsOf(shown);

        // Assert
        kept.Rows.Should().BeSameAs(next.Rows);
    }

    [Fact]
    public void KeepingListsOf_AlertOfAnotherTone_CountsAsChanged()
    {
        // Arrange
        var shown = State("Data from 22:39:46 is stale");
        var next = shown with { Alerts = [new OverlayAlert("Low charge (threshold 30%)", Tone.Critical)] };

        // Act
        var kept = next.KeepingListsOf(shown);

        // Assert
        kept.Alerts.Should().BeSameAs(next.Alerts);
    }

    [Fact]
    public void KeepingListsOf_ListsKept_MakeARepeatedStateEqualToTheShownOne()
    {
        // Arrange
        var shown = State("Data from 22:39:46 is stale");
        var repeated = State("Data from 22:39:46 is stale");

        // Act
        var kept = repeated.KeepingListsOf(shown);

        // Assert
        repeated.Should().NotBe(shown, "a record compares its lists by reference");
        kept.Should().Be(shown);
    }

    private static OverlayState State(string staleText) => new()
    {
        StateText = "On mains",
        StaleText = staleText,
        Summary = ["On mains · 100%", "Backup output: 250 W"],
        Alerts = [new OverlayAlert("Low charge (threshold 30%)", Tone.Warning)],
        Rows = [new OverlayRow(OverlayRowKind.Grid, "Grid", "0 W")],
    };
}
