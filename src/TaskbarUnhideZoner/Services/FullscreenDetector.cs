using System.Runtime.InteropServices;
using System.Text;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

internal static class FullscreenDetector
{
    private const int MonitorEdgeSlackPx = 2;
    private static readonly object ClassCacheSync = new();
    private static IntPtr _lastClassWindow;
    private static bool _lastClassIsDesktopHost;

    public static bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IsIgnoredForegroundWindow(hwnd))
        {
            return false;
        }

        if (!TryGetForegroundRect(hwnd, out var rect))
        {
            return false;
        }

        if (!TryGetMonitorRect(hwnd, out var monitorRect))
        {
            return false;
        }

        return CoversMonitor(rect, monitorRect);
    }

    private static bool IsIgnoredForegroundWindow(IntPtr hwnd)
    {
        var shell = NativeMethods.FindWindow("Shell_TrayWnd", null);
        return hwnd == shell || IsDesktopHostWindow(hwnd);
    }

    private static bool TryGetForegroundRect(IntPtr hwnd, out NativeMethods.Rect rect)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out rect))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetMonitorRect(IntPtr hwnd, out NativeMethods.Rect monitorRect)
    {
        monitorRect = default;
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

        monitorRect = info.RcMonitor;
        return true;
    }

    private static bool CoversMonitor(NativeMethods.Rect windowRect, NativeMethods.Rect monitorRect)
    {
        var fgWidth = windowRect.Right - windowRect.Left;
        var fgHeight = windowRect.Bottom - windowRect.Top;
        var monWidth = monitorRect.Right - monitorRect.Left;
        var monHeight = monitorRect.Bottom - monitorRect.Top;

        return Math.Abs(windowRect.Left - monitorRect.Left) <= MonitorEdgeSlackPx
               && Math.Abs(windowRect.Top - monitorRect.Top) <= MonitorEdgeSlackPx
               && Math.Abs(fgWidth - monWidth) <= MonitorEdgeSlackPx
               && Math.Abs(fgHeight - monHeight) <= MonitorEdgeSlackPx;
    }

    private static bool IsDesktopHostWindow(IntPtr hwnd)
    {
        lock (ClassCacheSync)
        {
            if (hwnd == _lastClassWindow)
            {
                return _lastClassIsDesktopHost;
            }
        }

        var className = new StringBuilder(256);
        var len = NativeMethods.GetClassName(hwnd, className, className.Capacity);
        if (len <= 0)
        {
            return false;
        }

        var name = className.ToString();
        var isDesktopHost = string.Equals(name, "Progman", StringComparison.Ordinal)
                            || string.Equals(name, "WorkerW", StringComparison.Ordinal)
                            || string.Equals(name, "Shell_TrayWnd", StringComparison.Ordinal)
                            || string.Equals(name, "Shell_SecondaryTrayWnd", StringComparison.Ordinal);

        lock (ClassCacheSync)
        {
            _lastClassWindow = hwnd;
            _lastClassIsDesktopHost = isDesktopHost;
        }

        return isDesktopHost;
    }
}
