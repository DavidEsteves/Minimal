using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Minimal.Services;

public static class NativeWindowFrame
{
    public static void Apply(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        int darkFrame = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, 20, ref darkFrame, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        // DWM can antialias the actual window silhouette. WindowChrome must
        // retain its thin glass frame and avoid creating a legacy window region.
        int rounded = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
        int border = dark ? 0x00444444 : 0x00DDDDDD; // COLORREF
        _ = DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
