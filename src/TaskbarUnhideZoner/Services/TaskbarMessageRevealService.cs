using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

// Reveals auto-hidden taskbars without changing auto-hide state, focus, or the work area.
// Explorer's taskbar window procedure unhides the bar on WM_ACTIVATE(WA_ACTIVE) without
// checking real activation; the follow-up WM_ACTIVATE(WA_INACTIVE) restarts Explorer's own
// hide timer (~500 ms), mirroring a real deactivation. Calling Reveal() more often than that
// timer keeps the taskbar shown; stopping lets Explorer hide it as usual.
internal sealed class TaskbarMessageRevealService : ITaskbarRevealService
{
    private const uint SendTimeoutMs = 100;

    public bool Reveal()
    {
        var taskbars = EnumerateTaskbars();
        if (taskbars.Count == 0 || IsTaskbarForeground(taskbars))
        {
            return false;
        }

        var hasCursor = NativeMethods.GetCursorPos(out var cursor);
        var sent = false;
        foreach (var taskbar in taskbars)
        {
            if (hasCursor && IsCursorOver(taskbar, cursor))
            {
                continue;
            }

            sent = true;
            Send(taskbar, NativeMethods.WaActive);
            if (!Send(taskbar, NativeMethods.WaInactive))
            {
                // Without the inactive message Explorer never arms its hide timer.
                Send(taskbar, NativeMethods.WaInactive);
            }
        }

        return sent;
    }

    public bool IsAnyTaskbarShown()
    {
        return EnumerateTaskbars().Any(IsTaskbarShown);
    }

    public static bool IsTaskbarShown(IntPtr taskbar)
    {
        if (!NativeMethods.GetWindowRect(taskbar, out var rect) || !TryGetMonitorInfo(taskbar, out var monitor))
        {
            return false;
        }

        var bar = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var visible = Rectangle.Intersect(bar, Rectangle.FromLTRB(monitor.RcMonitor.Left, monitor.RcMonitor.Top, monitor.RcMonitor.Right, monitor.RcMonitor.Bottom));
        return Math.Min(visible.Width, visible.Height) >= Math.Min(bar.Width, bar.Height) - 2;
    }

    public static bool TryGetMonitorInfo(IntPtr hwnd, out NativeMethods.MonitorInfo info)
    {
        info = new NativeMethods.MonitorInfo { CbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        return monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info);
    }

    // Secondary taskbars first and the primary last: the primary's activation path notifies other
    // auto-hide appbars, so it should not run between a secondary's reveal and its hide-timer reset.
    private static List<IntPtr> EnumerateTaskbars()
    {
        var taskbars = new List<IntPtr>();
        var secondary = IntPtr.Zero;
        while ((secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            taskbars.Add(secondary);
        }

        var primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero)
        {
            taskbars.Add(primary);
        }

        return taskbars;
    }

    // While the user hovers or actually uses a taskbar, Explorer keeps it shown by itself;
    // faking a deactivation then could disturb taskbar focus, flyouts, or z-order.
    private static bool IsTaskbarForeground(List<IntPtr> taskbars)
    {
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        return foregroundThread != 0
            && taskbars.Any(taskbar => NativeMethods.GetWindowThreadProcessId(taskbar, out _) == foregroundThread);
    }

    private static bool IsCursorOver(IntPtr taskbar, NativeMethods.Point cursor)
    {
        return NativeMethods.GetWindowRect(taskbar, out var rect)
            && cursor.X >= rect.Left && cursor.X < rect.Right
            && cursor.Y >= rect.Top && cursor.Y < rect.Bottom;
    }

    private static bool Send(IntPtr hwnd, int activationState)
    {
        return NativeMethods.SendMessageTimeout(
            hwnd,
            NativeMethods.WmActivate,
            new IntPtr(activationState),
            IntPtr.Zero,
            NativeMethods.SmtoAbortIfHung,
            SendTimeoutMs,
            out _) != IntPtr.Zero;
    }
}
