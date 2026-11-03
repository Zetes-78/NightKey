using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NightKey.Services;

internal static class Native
{
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Dark title bar, and hide the window from screenshots, screen recorders and screen sharing
    /// so codes can't be captured by remote-access malware or an accidental Teams share.
    /// </summary>
    public static void Harden(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (!App.ScreenshotMode) SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        int on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
    }
}
