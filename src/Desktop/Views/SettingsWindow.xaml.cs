using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.ViewModels;

using System.Windows;

namespace MarstekOverlay.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        viewModel.Start();
        viewModel.CloseRequested += Close;
        Closing += (_, closing) => closing.Cancel = !viewModel.MayClose();
        Closed += (_, _) => viewModel.CloseRequested -= Close;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.UseDarkTitleBar();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // Nothing brings the deleted back, so the button alone does not do it; "No" is what Enter answers.
    private async void DeleteStoredData_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            _viewModel.StoredData.Confirmation,
            Strings.StoredData_Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer == MessageBoxResult.Yes)
            await _viewModel.StoredData.DeleteAsync();
    }
}
