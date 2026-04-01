using TaskbarUnhideZoner.Interop;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal sealed class TaskbarTriggerService
{
    private readonly AppConfig _config;

    public TaskbarTriggerService(AppConfig config)
    {
        _config = config;
    }

    public void Trigger(Point currentCursor)
    {
        TryNoMoveReveal(currentCursor);

        if (_config.Trigger.Strategy == TriggerStrategy.NoMoveThenNudge)
        {
            TryNudge(currentCursor, _config.Trigger.NudgePx);
        }
    }

    private static void TryNoMoveReveal(Point currentCursor)
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero)
        {
            return;
        }

        var lParam = new IntPtr((currentCursor.Y << 16) | (currentCursor.X & 0xFFFF));
        NativeMethods.SendMessageTimeout(taskbar, NativeMethods.WmMouseMove, IntPtr.Zero, lParam, NativeMethods.SmtoAbortIfHung, 40, out _);
    }

    private static void TryNudge(Point currentCursor, int nudgePx)
    {
        var screen = SystemInformation.VirtualScreen;
        var nudged = currentCursor;

        if (currentCursor.Y >= screen.Bottom - 2)
        {
            nudged.Y = Math.Max(screen.Top, currentCursor.Y - nudgePx);
        }
        else if (currentCursor.Y <= screen.Top + 1)
        {
            nudged.Y = Math.Min(screen.Bottom - 1, currentCursor.Y + nudgePx);
        }
        else if (currentCursor.X <= screen.Left + 1)
        {
            nudged.X = Math.Min(screen.Right - 1, currentCursor.X + nudgePx);
        }
        else
        {
            nudged.X = Math.Max(screen.Left, currentCursor.X - nudgePx);
        }

        NativeMethods.SetCursorPos(nudged.X, nudged.Y);
        NativeMethods.SetCursorPos(currentCursor.X, currentCursor.Y);
    }
}
