using System.Runtime.InteropServices;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

internal sealed class TaskbarStateService : ITaskbarStateService
{
    public bool IsAutoHideEnabled() => (GetStateFlags() & NativeMethods.AbsAutoHide) != 0;

    public bool EnableAutoHide()
    {
        var current = GetStateFlags();
        if ((current & NativeMethods.AbsAutoHide) != 0)
        {
            return true;
        }

        var data = CreateAppBarData();
        data.LParam = new IntPtr((int)(current | NativeMethods.AbsAutoHide));
        NativeMethods.SHAppBarMessage(NativeMethods.AbmSetState, ref data);
        return IsAutoHideEnabled();
    }

    private static uint GetStateFlags()
    {
        var data = CreateAppBarData();
        var result = NativeMethods.SHAppBarMessage(NativeMethods.AbmGetState, ref data);
        return unchecked((uint)result.ToInt64());
    }

    private static NativeMethods.AppBarData CreateAppBarData()
    {
        return new NativeMethods.AppBarData
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.AppBarData>(),
            HWnd = NativeMethods.FindWindow("Shell_TrayWnd", null)
        };
    }
}
