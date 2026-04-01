using TaskbarUnhideZoner.Detection;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class ZoneEngine : IDisposable
{
    private readonly AppConfig _config;
    private readonly IZoneActivationHandler _handler;
    private readonly ZoneStateMachine _stateMachine;
    private readonly object _sync = new();
    private IZoneMonitor? _monitor;
    private bool _running;
    private bool _zoneTriggered;

    public ZoneEngine(AppConfig config, IZoneActivationHandler handler)
    {
        _config = config;
        _handler = handler;
        _stateMachine = new ZoneStateMachine(
            triggerDelayMs: () => _config.TriggerDelayMs,
            cooldownMs: () => _config.Trigger.CooldownMs);
    }

    public string ActiveBackendName => _monitor?.Name ?? "none";

    public void Start()
    {
        lock (_sync)
        {
            if (_running)
            {
                return;
            }

            _monitor = ZoneMonitorFactory.Create(_config);
            _monitor.CursorPositionChanged += OnCursorPositionChanged;
            _monitor.Start();
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

            if (_monitor != null)
            {
                _monitor.CursorPositionChanged -= OnCursorPositionChanged;
                _monitor.Dispose();
                _monitor = null;
            }

            _stateMachine.ForceOutside();
            _zoneTriggered = false;
            _running = false;
        }
    }

    public void Reinitialize()
    {
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnCursorPositionChanged(object? sender, CursorPositionEventArgs e)
    {
        if (_config.Fullscreen.SuspendWhenFullscreenAppActive && FullscreenDetector.IsForegroundFullscreen())
        {
            if (_zoneTriggered)
            {
                _handler.OnZoneLeft();
                _zoneTriggered = false;
            }

            _stateMachine.ForceOutside();
            return;
        }

        var inZone = ZoneGeometry.IsInZone(_config.Zone, e.Position, SystemInformation.VirtualScreen);
        if (_zoneTriggered && !inZone)
        {
            _handler.OnZoneLeft();
            _zoneTriggered = false;
        }

        if (!_stateMachine.Update(inZone, e.ElapsedMs))
        {
            return;
        }

        _handler.OnZoneTriggered(e.Position);
        _zoneTriggered = true;
    }
}
