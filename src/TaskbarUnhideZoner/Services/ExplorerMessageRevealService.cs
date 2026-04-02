using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

internal sealed class ExplorerMessageRevealService
{
    private static readonly uint[] CandidateMessages =
    {
        0x0400 + 18,
        0x0400 + 19,
        0x0400 + 20,
        0x0400 + 21,
        0x0400 + 22,
        0x0400 + 23,
        0x0400 + 24
    };

    public bool TryReveal()
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero)
        {
            return false;
        }

        var sent = false;
        foreach (var msg in CandidateMessages)
        {
            var result = NativeMethods.SendMessageTimeout(
                taskbar,
                msg,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.SmtoAbortIfHung,
                30,
                out _);
            if (result != IntPtr.Zero)
            {
                sent = true;
            }
        }

        return sent;
    }

    public bool ProbeWorks(TaskbarStateService taskbarState)
    {
        if (!taskbarState.SetAutoHideEnabled(true))
        {
            return false;
        }

        Thread.Sleep(180);
        var before = GetPrimaryTaskbarThickness();

        for (var i = 0; i < 3; i++)
        {
            TryReveal();
            Thread.Sleep(140);
            var after = GetPrimaryTaskbarThickness();
            if (after > before + 6)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetPrimaryTaskbarThickness()
    {
        var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !NativeMethods.GetWindowRect(taskbar, out var rect))
        {
            return 0;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        return Math.Min(width, height);
    }
}
