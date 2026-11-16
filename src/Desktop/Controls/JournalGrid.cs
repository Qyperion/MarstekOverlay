using System.Windows;
using System.Windows.Controls;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>The columns of the outage journal: started, ended, duration, energy, charge. The heading and every
/// row are laid out by it, so that they cannot drift apart.</summary>
public sealed class JournalGrid : Grid
{
    private static readonly double[] Shares = [13, 13, 13, 10, 10];

    public JournalGrid()
    {
        foreach (var share in Shares)
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) });
    }
}
