using CommunityToolkit.Mvvm.ComponentModel;

using MarstekOverlay.Desktop.Presentation;

namespace MarstekOverlay.Desktop.ViewModels;

public sealed partial class CardRowViewModel(string label, string value) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial string Value { get; set; } = value;

    // What a screen reader calls the row.
    public override string ToString() => Phrases.Readings(Label, Value);
}

/// <summary>One answer of the station as a card. The card and its rows outlive the answers they show: a new
/// answer changes the values in place, so that the window does not rebuild what is on screen every second.</summary>
public sealed partial class StationCardViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Method { get; private set; } = string.Empty;

    /// <summary>How long ago the station gave the answer; nothing while there is none.</summary>
    [ObservableProperty]
    public partial string? Age { get; private set; }

    [ObservableProperty]
    public partial string? AgeHint { get; private set; }

    /// <summary>The answer is older than its request leaves it between two polls: the station has stopped
    /// answering the request, and the values on the card are what it said last.</summary>
    [ObservableProperty]
    public partial bool IsStale { get; private set; }

    /// <summary>Why the card has no values.</summary>
    [ObservableProperty]
    public partial string? Note { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<CardRowViewModel> Rows { get; private set; } = [];

    // What a screen reader calls the card.
    public override string ToString() => Title;

    /// <param name="trustedFor">How old the answer of this request may get while the station still answers it.</param>
    public void Show(StationSection section, DateTimeOffset now, TimeSpan trustedFor)
    {
        Title = section.Title;
        Method = section.Method;
        Note = section.Note;

        // Only values can go out of date: a request that brought none is not sent again, so its answer stands.
        IsStale = section.State == SectionState.Answered && now - section.ReceivedAt > trustedFor;
        Age = section.ReceivedAt is { } receivedAt ? Phrases.Ago(now - receivedAt) : null;
        AgeHint = DetailsPresenter.DescribeAnswer(section.ReceivedAt, IsStale);

        if (Rows.Select(row => row.Label).SequenceEqual(section.Rows.Select(row => row.Label)))
        {
            foreach (var (shown, row) in Rows.Zip(section.Rows))
                shown.Value = row.Value ?? StationSections.NoValue;
        }
        else
        {
            Rows = [.. section.Rows.Select(row => new CardRowViewModel(row.Label, row.Value ?? StationSections.NoValue))];
        }
    }
}
