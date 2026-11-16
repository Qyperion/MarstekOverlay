using MarstekOverlay.Desktop.Interop;

using System.Windows;
using System.Windows.Interop;

namespace MarstekOverlay.Desktop.Views;

public static class WindowExtensions
{
    private const int Enabled = 1;

    extension(Window window)
    {
        /// <summary>Asks Windows for the dark variant of the title bar it draws, which a dark window would
        /// otherwise wear in white. Works once the window has its handle, that is from
        /// <see cref="Window.OnSourceInitialized"/> on; a Windows too old to know the attribute leaves the
        /// title bar as it was.</summary>
        public void UseDarkTitleBar()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == 0)
                return;

            var enabled = Enabled;
            if (NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.ImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.ImmersiveDarkModeBefore2004, ref enabled, sizeof(int));
        }

        /// <summary>Brings a window that is already open to the front, out of the taskbar if it was minimized.</summary>
        public void BringForward()
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();
        }

        /// <summary>The monitors' working areas in the units the window is placed in. Windows gives them in
        /// pixels, and what a pixel comes to in those units is known once the window has its handle. It is asked
        /// of the handle rather than of the window: while <see cref="Window.OnSourceInitialized"/> runs, the
        /// window is not yet the content of its handle and has no source of its own to ask.</summary>
        public IReadOnlyList<Rect> FindWorkAreas()
        {
            var toUnits = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.CompositionTarget?.TransformFromDevice
                ?? System.Windows.Media.Matrix.Identity;

            return
            [
                .. System.Windows.Forms.Screen.AllScreens.Select(screen =>
                {
                    var area = screen.WorkingArea;

                    return new Rect(
                        toUnits.Transform(new Point(area.Left, area.Top)),
                        toUnits.Transform(new Point(area.Right, area.Bottom)));
                }),
            ];
        }
    }
}
