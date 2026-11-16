using MarstekOverlay.Desktop.Interop;
using MarstekOverlay.Desktop.ViewModels;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace MarstekOverlay.Desktop.Views;

public partial class OverlayWindow : Window
{
    private const int DoubleClick = 2;

    private readonly OverlayViewModel _viewModel;
    private bool _isDragged;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        (Left, Top) = viewModel.Position;

        // The size follows the content, so it is known only once that has been laid out, and it changes with
        // the scale, with the view and with the text of the compact line.
        SizeChanged += (_, _) => KeepWithinDesktop();
        IsVisibleChanged += (_, _) => viewModel.FollowVisibility(IsVisible);
        viewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(OverlayViewModel.IsClickThrough))
                ApplyClickThrough();
        };
    }

    /// <summary>Above the windows that cover the panel, without taking the keyboard away from them.</summary>
    public void ShowOnTop()
    {
        const uint inPlace = NativeMethods.KeepSize | NativeMethods.KeepPosition | NativeMethods.KeepInactive;

        Show();
        var window = new WindowInteropHelper(this).Handle;

        // A window that is not the active one is raised only by passing through the topmost band.
        NativeMethods.SetWindowPos(window, NativeMethods.Topmost, 0, 0, 0, 0, inPlace);
        if (!_viewModel.IsTopmost)
            NativeMethods.SetWindowPos(window, NativeMethods.NotTopmost, 0, 0, 0, 0, inPlace);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // A click, a drag or a button leaves the keyboard with the window the user was typing in.
        var window = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowLong(window, NativeMethods.ExtendedStyle, NativeMethods.GetWindowLong(window, NativeMethods.ExtendedStyle) | NativeMethods.NotActivatedByClick);
        HwndSource.FromHwnd(window).AddHook(FollowDrag);

        ApplyClickThrough();
    }

    // Windows moves a window that is not active only once the drag ends; it is moved along with the pointer here.
    private static nint FollowDrag(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.Moving)
            return 0;

        var (left, top, right, bottom) = Marshal.PtrToStructure<NativeMethods.ScreenRect>(lParam);
        NativeMethods.SetWindowPos(window, 0, left, top, right - left, bottom - top, NativeMethods.KeepOrder | NativeMethods.KeepInactive);

        return 0;
    }

    // The place the user chose stays the one remembered, so a panel moved aside to fit returns there once it
    // fits again.
    private void KeepWithinDesktop()
    {
        // The content may change size under the pointer, which must not take the panel out of the user's hand.
        if (_isDragged)
            return;

        var (left, top) = _viewModel.Position;
        (Left, Top) = PlaceWithinDesktop(left, top);
    }

    private (double Left, double Top) PlaceWithinDesktop(double left, double top)
    {
        var desktop = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var placed = PanelPlacement.Within(desktop, new Point(left, top), new Size(ActualWidth, ActualHeight));

        return (placed.X, placed.Y);
    }

    // WPF has no word for a window the mouse passes through; the extended style is all there is to it.
    private void ApplyClickThrough()
    {
        var window = new WindowInteropHelper(this).Handle;
        if (window == 0)
            return;

        var style = NativeMethods.GetWindowLong(window, NativeMethods.ExtendedStyle);
        var wanted = _viewModel.IsClickThrough
            ? style | NativeMethods.TransparentToMouse
            : style & ~NativeMethods.TransparentToMouse;

        if (wanted != style)
            NativeMethods.SetWindowLong(window, NativeMethods.ExtendedStyle, wanted);
    }

    // Filled each time it opens, from the same entries as the menu of the notification-area icon.
    private void Card_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (((FrameworkElement)sender).ContextMenu is not { } menu)
            return;

        // A menu of a window that is not active gets no keys and is not closed by a click elsewhere.
        Activate();
        menu.Items.Clear();

        foreach (var group in _viewModel.Menu.Groups())
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());

            foreach (var entry in group)
            {
                var item = new MenuItem { Header = entry.Text, IsChecked = entry.IsChecked == true, ToolTip = entry.Hint };
                item.Click += (_, _) => entry.Choose();
                menu.Items.Add(item);
            }
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The compact line has no buttons; the first click of the two has moved nothing.
        if (e.ClickCount == DoubleClick && _viewModel.IsCompact)
        {
            _viewModel.OpenDetailsCommand.Execute(null);
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        var grabbedAt = (Left, Top);

        _isDragged = true;
        try
        {
            DragMove();
        }
        finally
        {
            _isDragged = false;
        }

        // A click that moved nothing leaves the remembered place alone, wherever the panel stands just now.
        if ((Left, Top) == grabbedAt)
            return;

        (Left, Top) = PlaceWithinDesktop(Left, Top);
        _viewModel.RememberPosition(Left, Top);
    }
}
