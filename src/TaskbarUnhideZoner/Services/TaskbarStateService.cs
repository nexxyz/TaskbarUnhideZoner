using System.Runtime.InteropServices;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Services;

internal sealed class TaskbarStateService
{
    public uint GetStateFlags()
    {
        var data = CreateAppBarData();
        var result = NativeMethods.SHAppBarMessage(NativeMethods.AbmGetState, ref data);
        return unchecked((uint)result.ToInt64());
    }

    public bool IsAutoHideEnabled() => (GetStateFlags() & NativeMethods.AbsAutoHide) != 0;

    public bool SetAutoHideEnabled(bool enabled)
    {
        var current = GetStateFlags();
        var desired = enabled
            ? (current | NativeMethods.AbsAutoHide)
            : (current & ~NativeMethods.AbsAutoHide);

        if (desired == current)
        {
            return true;
        }

        return SetStateFlags(desired);
    }

    public bool SetStateFlags(uint stateFlags)
    {
        var data = CreateAppBarData();
        data.LParam = new IntPtr((int)stateFlags);

        NativeMethods.SHAppBarMessage(NativeMethods.AbmSetState, ref data);
        var readBack = GetStateFlags();
        return (readBack & NativeMethods.AbsAutoHide) == (stateFlags & NativeMethods.AbsAutoHide);
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
