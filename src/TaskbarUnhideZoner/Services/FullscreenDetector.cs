using System.Runtime.InteropServices;
using System.Text;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

internal static class FullscreenDetector
{
    public static bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (hwnd == shell)
        {
            return false;
        }

        if (IsDesktopHostWindow(hwnd))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MonitorInfo { CbSize = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var fgWidth = rect.Right - rect.Left;
        var fgHeight = rect.Bottom - rect.Top;
        var monWidth = info.RcMonitor.Right - info.RcMonitor.Left;
        var monHeight = info.RcMonitor.Bottom - info.RcMonitor.Top;

        const int slack = 2;
        var coversMonitor = Math.Abs(rect.Left - info.RcMonitor.Left) <= slack
                            && Math.Abs(rect.Top - info.RcMonitor.Top) <= slack
                            && Math.Abs(fgWidth - monWidth) <= slack
                            && Math.Abs(fgHeight - monHeight) <= slack;

        return coversMonitor;
    }

    private static bool IsDesktopHostWindow(IntPtr hwnd)
    {
        var className = new StringBuilder(256);
        var len = NativeMethods.GetClassName(hwnd, className, className.Capacity);
        if (len <= 0)
        {
            return false;
        }

        var name = className.ToString();
        return string.Equals(name, "Progman", StringComparison.Ordinal)
               || string.Equals(name, "WorkerW", StringComparison.Ordinal)
               || string.Equals(name, "Shell_TrayWnd", StringComparison.Ordinal)
               || string.Equals(name, "Shell_SecondaryTrayWnd", StringComparison.Ordinal);
    }
}
