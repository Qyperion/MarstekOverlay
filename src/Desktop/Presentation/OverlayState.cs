using MarstekOverlay.Desktop.Localization;

namespace MarstekOverlay.Desktop.Presentation;

public sealed record OverlayState
{
    public static string UnknownSocText => Strings.Unit_PercentUnknown;

    public string StateText { get; init; } = string.Empty;
    public Tone StateTone { get; init; } = Tone.Secondary;
    public string? StateHint { get; init; }

    public SupplyMark Supply { get; init; }

    public string SocText { get; init; } = UnknownSocText;
    public double SocPercent { get; init; }
    public Tone SocTone { get; init; } = Tone.Secondary;

    /// <summary>What follows the charge on the single line of the compact panel.</summary>
    public string CompactText { get; init; } = string.Empty;
    public Tone CompactTone { get; init; } = Tone.Secondary;

    /// <summary>The heading and its hint, which the compact panel has no line for.</summary>
    public string? CompactHint { get; init; }

    /// <summary>Every warning in one, for the compact panel that has no rows to list them in.</summary>
    public OverlayAlert? CompactAlert { get; init; }

    /// <summary>The state in a few lines, the most telling first, for a place that may not fit them all.</summary>
    public IReadOnlyList<string> Summary { get; init; } = [];

    /// <summary>Nothing while the chart is switched off.</summary>
    public OverlayChart? Chart { get; init; }

    public IReadOnlyList<OverlayAlert> Alerts { get; init; } = [];

    public IReadOnlyList<OverlayRow> Rows { get; init; } = [];

    /// <summary>What is wrong with the age of the readings. Empty while the answers keep coming: the panel has
    /// no line for the time of an answer that is on time.</summary>
    public string StaleText { get; init; } = string.Empty;

    public string? ErrorHint { get; init; }

    /// <summary>A list that reads the same is taken over from <paramref name="shown"/>, so that a window bound to it
    /// does not rebuild its rows, closing a tooltip open over one of them.</summary>
    public OverlayState KeepingListsOf(OverlayState shown) => this with
    {
        Summary = Summary.SequenceEqual(shown.Summary) ? shown.Summary : Summary,
        Alerts = Alerts.SequenceEqual(shown.Alerts) ? shown.Alerts : Alerts,
        Rows = Rows.SequenceEqual(shown.Rows) ? shown.Rows : Rows,
    };
}
