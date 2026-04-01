using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Detection;

internal sealed class MouseHookZoneMonitor : IZoneMonitor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _sync = new();
    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hookHandle;

    public string Name => "mouse-hook";

    public event EventHandler<CursorPositionEventArgs>? CursorPositionChanged;

    public void Start()
    {
        lock (_sync)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                return;
            }

            _proc = HookCallback;
            var module = NativeMethods.GetModuleHandle(null);
            _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _proc, module, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"SetWindowsHookEx failed with error {errorCode}.");
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_hookHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _proc = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            if (msg == NativeMethods.WmMouseMove || msg == NativeMethods.WmNcMouseMove)
            {
                var data = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
                var point = new System.Drawing.Point(data.Pt.X, data.Pt.Y);
                CursorPositionChanged?.Invoke(this, new CursorPositionEventArgs(point, _stopwatch.ElapsedMilliseconds));
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
