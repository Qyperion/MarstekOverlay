using System.Runtime.InteropServices;

namespace MarstekOverlay.Desktop.Interop;

internal static class NativeMethods
{
    public const int ExtendedStyle = -20;
    public const int TransparentToMouse = 0x20;
    public const int NotActivatedByClick = 0x08000000;

    public const int Moving = 0x0216;

    public const nint Topmost = -1;
    public const nint NotTopmost = -2;
    public const uint KeepSize = 0x0001;
    public const uint KeepPosition = 0x0002;
    public const uint KeepInactive = 0x0010;
    public const uint KeepOrder = 0x0004;

    public const int ImmersiveDarkMode = 20;

    /// <summary>What <see cref="ImmersiveDarkMode"/> was numbered before Windows 10 2004.</summary>
    public const int ImmersiveDarkModeBefore2004 = 19;

    private const string User32 = "user32.dll";
    private const string DesktopWindowManager = "dwmapi.dll";

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(nint icon);

    [DllImport(User32, EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(nint window, int index);

    [DllImport(User32, EntryPoint = "SetWindowLongW")]
    public static extern int SetWindowLong(nint window, int index, int value);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    /// <returns>Zero when the attribute was set.</returns>
    [DllImport(DesktopWindowManager)]
    public static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int valueSize);
}
