using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.Startup;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class RuntimeController : IDisposable, IZoneActivationHandler
{
    private readonly object _sync = new();
    private readonly ZoneEngine _engine;
    private readonly TaskbarStateService _taskbarState;
    private readonly System.Threading.Timer _autohidePollTimer;
    private readonly int _autohidePollMs;

    private bool _baselineAutoHideEnabled;
    private bool _managedVisibleActive;
    private DateTime _lastStateWriteUtc;
    private string? _monitoringError;

    public RuntimeController(AppConfig config)
    {
        Config = config;
        _taskbarState = new TaskbarStateService();
        _engine = new ZoneEngine(config, this);
        Config.StartWithWindows = StartupManager.IsEnabled();
        _autohidePollMs = Math.Clamp(Config.AutohideStatePollSeconds, 5, 300) * 1000;

        _baselineAutoHideEnabled = _taskbarState.IsAutoHideEnabled();
        ApplyRuntimeGateLocked();
        Save();

        _autohidePollTimer = new System.Threading.Timer(_ => RefreshAutohideState(), null, _autohidePollMs, _autohidePollMs);
    }

    public event EventHandler? StateChanged;

    public AppConfig Config { get; }

    public bool IsAutohideOffSuspended
    {
        get
        {
            lock (_sync)
            {
                return Config.Enabled && !_baselineAutoHideEnabled && !_managedVisibleActive;
            }
        }
    }

    public string? MonitoringError
    {
        get
        {
            lock (_sync)
            {
                return _monitoringError;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_sync)
        {
            Config.Enabled = enabled;
            if (!enabled)
            {
                RestoreBaselineLocked();
            }

            ApplyRuntimeGateLocked();
            Save();
        }

        RaiseStateChanged();
    }

    public void SetStartup(bool enabled)
    {
        StartupManager.SetEnabled(enabled);
        Config.StartWithWindows = StartupManager.IsEnabled();
        Save();
    }

    public void SetDelayPreset(DelayPreset preset)
    {
        lock (_sync)
        {
            Config.TriggerDelayMs = preset switch
            {
                DelayPreset.Quick => Config.DelayPresets.QuickMs,
                DelayPreset.Default => Config.DelayPresets.DefaultMs,
                DelayPreset.Long => Config.DelayPresets.LongMs,
                _ => Config.TriggerDelayMs
            };

            Save();
        }
    }

    public void SetEdgeZone(EdgePosition edge, Rectangle rectangle)
    {
        Config.Zone.Mode = edge switch
        {
            EdgePosition.Top => ZoneMode.Top,
            EdgePosition.Bottom => ZoneMode.Bottom,
            EdgePosition.Left => ZoneMode.Left,
            EdgePosition.Right => ZoneMode.Right,
            _ => ZoneMode.HotZone
        };

        Config.Zone.ActiveZone = new RectConfig
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
        Save();
    }

    public void SetHotZone(Rectangle rectangle)
    {
        Config.Zone.Mode = ZoneMode.HotZone;
        Config.Zone.ActiveZone = new RectConfig
        {
            X = rectangle.X,
            Y = rectangle.Y,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
        Save();
    }

    public void SetTriggerAssistPreset(TriggerAssistPreset preset)
    {
        lock (_sync)
        {
            ApplyTriggerAssistPreset(Config.Trigger.Assist, preset);
            Save();
        }
    }

    public void ReinitializeDetection()
    {
        lock (_sync)
        {
            if (Config.Enabled && (_baselineAutoHideEnabled || _managedVisibleActive))
            {
                _engine.Reinitialize();
            }
        }

        RaiseStateChanged();
    }

    public void RefreshAutohideState()
    {
        var stateChanged = false;

        lock (_sync)
        {
            var observed = _taskbarState.IsAutoHideEnabled();

            if (_managedVisibleActive)
            {
                var expected = false;
                if (observed != expected && IsWriteCooldownElapsed())
                {
                    _managedVisibleActive = false;
                    _baselineAutoHideEnabled = observed;
                    stateChanged = true;
                }
            }
            else if (_baselineAutoHideEnabled != observed)
            {
                _baselineAutoHideEnabled = observed;
                stateChanged = true;
            }

            ApplyRuntimeGateLocked();
        }

        if (stateChanged)
        {
            RaiseStateChanged();
        }
    }

    public void OnZoneTriggered(Point cursorPosition)
    {
        lock (_sync)
        {
            if (!Config.Enabled || !_baselineAutoHideEnabled || _managedVisibleActive)
            {
                return;
            }

            if (_taskbarState.SetAutoHideEnabled(false))
            {
                _managedVisibleActive = true;
                _lastStateWriteUtc = DateTime.UtcNow;
            }
        }

        RaiseStateChanged();
    }

    public void OnZoneLeft()
    {
        lock (_sync)
        {
            RestoreBaselineLocked();
            ApplyRuntimeGateLocked();
        }

        RaiseStateChanged();
    }

    public void Save()
    {
        ConfigStore.Save(Paths.ConfigFilePath, Config);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _autohidePollTimer.Dispose();
            RestoreBaselineLocked();
            _engine.Dispose();
        }
    }

    private void ApplyRuntimeGateLocked()
    {
        var shouldRun = Config.Enabled && (_baselineAutoHideEnabled || _managedVisibleActive);
        if (shouldRun)
        {
            try
            {
                _engine.Start();
                _monitoringError = null;
            }
            catch (Exception ex)
            {
                _monitoringError = ex.Message;
                _engine.Stop();
            }
        }
        else
        {
            _engine.Stop();
        }
    }

    private void RestoreBaselineLocked()
    {
        if (!_managedVisibleActive)
        {
            return;
        }

        _taskbarState.SetAutoHideEnabled(_baselineAutoHideEnabled);
        _managedVisibleActive = false;
        _lastStateWriteUtc = DateTime.UtcNow;
    }

    private bool IsWriteCooldownElapsed()
    {
        return (DateTime.UtcNow - _lastStateWriteUtc).TotalMilliseconds >= 1500;
    }

    private static void ApplyTriggerAssistPreset(TriggerAssistConfig assist, TriggerAssistPreset preset)
    {
        switch (preset)
        {
            case TriggerAssistPreset.Off:
                assist.Enabled = false;
                assist.MinDelayPercent = 100;
                assist.CurveExponent = 1.0;
                return;

            case TriggerAssistPreset.Low:
                assist.Enabled = true;
                assist.MinDelayPercent = 90;
                assist.CurveExponent = 3.0;
                return;

            case TriggerAssistPreset.Medium:
                assist.Enabled = true;
                assist.MinDelayPercent = 60;
                assist.CurveExponent = 1.7;
                return;

            case TriggerAssistPreset.Strong:
                assist.Enabled = true;
                assist.MinDelayPercent = 10;
                assist.CurveExponent = 0.55;
                return;

            default:
                assist.Enabled = true;
                assist.MinDelayPercent = 90;
                assist.CurveExponent = 3.0;
                return;
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
