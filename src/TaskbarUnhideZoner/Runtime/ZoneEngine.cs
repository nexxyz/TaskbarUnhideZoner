using TaskbarUnhideZoner.Detection;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class ZoneEngine : IDisposable
{
    private readonly AppConfig _config;
    private readonly TaskbarTriggerService _triggerService;
    private readonly ZoneStateMachine _stateMachine;
    private readonly object _sync = new();
    private IZoneMonitor? _monitor;
    private bool _running;

    public ZoneEngine(AppConfig config, TaskbarTriggerService triggerService)
    {
        _config = config;
        _triggerService = triggerService;
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
            _stateMachine.ForceOutside();
            return;
        }

        var inZone = ZoneGeometry.IsInZone(_config.Zone, e.Position, SystemInformation.VirtualScreen);
        if (!_stateMachine.Update(inZone, e.ElapsedMs))
        {
            return;
        }

        _triggerService.Trigger(e.Position);
    }
}
