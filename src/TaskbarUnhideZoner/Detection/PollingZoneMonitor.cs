using System.Diagnostics;
using TaskbarUnhideZoner.Interop;

namespace TaskbarUnhideZoner.Detection;

internal sealed class PollingZoneMonitor : IZoneMonitor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly int _pollIntervalMs;
    private readonly object _sync = new();
    private System.Threading.Timer? _timer;
    private bool _running;

    public PollingZoneMonitor(int pollIntervalMs)
    {
        _pollIntervalMs = pollIntervalMs;
    }

    public string Name => "polling";

    public event EventHandler<CursorPositionEventArgs>? CursorPositionChanged;

    public void Start()
    {
        lock (_sync)
        {
            if (_running)
            {
                return;
            }

            _timer = new System.Threading.Timer(OnTick, null, _pollIntervalMs, _pollIntervalMs);
            _running = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
            _running = false;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnTick(object? _)
    {
        if (!NativeMethods.GetCursorPos(out var p))
        {
            return;
        }

        var position = new System.Drawing.Point(p.X, p.Y);
        CursorPositionChanged?.Invoke(this, new CursorPositionEventArgs(position, _stopwatch.ElapsedMilliseconds));
    }
}
