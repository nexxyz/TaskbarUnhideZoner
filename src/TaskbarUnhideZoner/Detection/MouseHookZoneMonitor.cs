using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Detection;

internal sealed class MouseHookZoneMonitor : IZoneMonitor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _sync = new();
    private readonly object _latestSync = new();
    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hookHandle;
    private System.Threading.Timer? _dispatchTimer;
    private bool _running;
    private bool _hasLatest;
    private System.Drawing.Point _latestPoint;
    private long _latestElapsedMs;
    private long _lastHookEventElapsedMs;

    public string Name => "mouse-hook";

    public event EventHandler<CursorPositionEventArgs>? CursorPositionChanged;

    public void Start()
    {
        lock (_sync)
        {
            if (_running)
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

            _dispatchTimer = new System.Threading.Timer(OnDispatchTick, null, 20, 20);
            _running = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_running)
            {
                return;
            }

            _dispatchTimer?.Dispose();
            _dispatchTimer = null;

            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
            }

            _hookHandle = IntPtr.Zero;
            _proc = null;
            _running = false;

            lock (_latestSync)
            {
                _hasLatest = false;
                _lastHookEventElapsedMs = 0;
            }
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
                lock (_latestSync)
                {
                    _latestPoint = new System.Drawing.Point(data.Pt.X, data.Pt.Y);
                    _latestElapsedMs = _stopwatch.ElapsedMilliseconds;
                    _hasLatest = true;
                    _lastHookEventElapsedMs = _latestElapsedMs;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void OnDispatchTick(object? _)
    {
        System.Drawing.Point point;
        long elapsedMs;
        var emit = false;

        lock (_latestSync)
        {
            if (_hasLatest)
            {
                point = _latestPoint;
                elapsedMs = _latestElapsedMs;
                _hasLatest = false;
                emit = true;
            }
            else
            {
                var now = _stopwatch.ElapsedMilliseconds;
                var sinceLastHookEventMs = now - _lastHookEventElapsedMs;

                if (sinceLastHookEventMs >= 100 && NativeMethods.GetCursorPos(out var p))
                {
                    point = new System.Drawing.Point(p.X, p.Y);
                    elapsedMs = now;
                    emit = true;
                }
                else
                {
                    return;
                }
            }
        }

        if (emit)
        {
            CursorPositionChanged?.Invoke(this, new CursorPositionEventArgs(point, elapsedMs));
        }
    }
}
