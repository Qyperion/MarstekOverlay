using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Desktop.ViewModels;

using Microsoft.Win32;

using System.ComponentModel;
using System.Windows;

namespace MarstekOverlay.Desktop.Views;

public partial class DetailsWindow : Window
{
    private const string ExportExtension = ".csv";

    private readonly DetailsViewModel _viewModel;

    public DetailsWindow(DetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        // Opened where it was left; without a place to return to it stays in the middle of the screen.
        if (viewModel.WindowPlace is not null)
            WindowStartupLocation = WindowStartupLocation.Manual;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        this.UseDarkTitleBar();
        ReturnToPlace();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        // Of a maximized or minimized window this is the place it would be restored to.
        var place = RestoreBounds;
        if (!place.IsEmpty)
            _viewModel.RememberWindowPlace(new SavedWindowPlace(place.Left, place.Top, place.Width, place.Height, WindowState == WindowState.Maximized));
    }

    // Done here rather than in the constructor: the monitors are measured in pixels, and what a pixel is in
    // the units of the window is known only once the window has a handle.
    private void ReturnToPlace()
    {
        if (_viewModel.WindowPlace is not { } saved)
            return;

        var wanted = new Rect(saved.Left, saved.Top, Math.Max(saved.Width, 0), Math.Max(saved.Height, 0));
        var place = WindowPlacement.Within(this.FindWorkAreas(), wanted, new Size(MinWidth, MinHeight));

        (Left, Top, Width, Height) = (place.Left, place.Top, place.Width, place.Height);

        if (saved.IsMaximized)
            WindowState = WindowState.Maximized;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = _viewModel.Journal.ExportFileName,
            Filter = Strings.Journal_ExportFilter,
            DefaultExt = ExportExtension,
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) == true)
            _viewModel.Journal.Export(dialog.FileName);
    }
}
