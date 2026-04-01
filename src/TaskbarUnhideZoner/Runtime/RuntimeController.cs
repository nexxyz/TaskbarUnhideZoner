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

    public RuntimeController(AppConfig config)
    {
        Config = config;
        _taskbarState = new TaskbarStateService();
        _engine = new ZoneEngine(config, this);
        Config.StartWithWindows = StartupManager.IsEnabled();
        _autohidePollMs = Math.Clamp(Config.AutohideStatePollSeconds, 5, 300) * 1000;

        _baselineAutoHideEnabled = _taskbarState.IsAutoHideEnabled();
        ApplyRuntimeGateLocked();

        _autohidePollTimer = new System.Threading.Timer(_ => RefreshAutohideState(), null, _autohidePollMs, _autohidePollMs);
    }

    public event EventHandler? StateChanged;

    public AppConfig Config { get; }

    public string ActiveBackend => _engine.ActiveBackendName;

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

    public void SetZoneMode(ZoneMode mode)
    {
        Config.Zone.Mode = mode;
        Save();
    }

    public void SetEdgePosition(EdgePosition edge)
    {
        Config.Zone.Mode = ZoneMode.EdgeBar;
        Config.Zone.Edge = edge;
        Save();
    }

    public void SetHotZone(Rectangle rectangle)
    {
        Config.Zone.Mode = ZoneMode.HotZone;
        Config.Zone.HotZone.X = rectangle.X;
        Config.Zone.HotZone.Y = rectangle.Y;
        Config.Zone.HotZone.Width = rectangle.Width;
        Config.Zone.HotZone.Height = rectangle.Height;
        Save();
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
            _engine.Start();
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

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
