using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.Startup;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class RuntimeController : IDisposable, IZoneActivationHandler
{
    private readonly object _sync = new();
    private readonly IZoneEngineController _engine;
    private readonly ITaskbarStateService _taskbarState;
    private readonly Action<AppConfig> _saveConfig;
    private readonly System.Threading.Timer _autohidePollTimer;
    private readonly int _autohidePollMs;

    private bool _baselineAutoHideEnabled;
    private bool _managedVisibleActive;
    private DateTime _lastStateWriteUtc;
    private string? _monitoringError;

    public RuntimeController(AppConfig config)
        : this(
            config,
            new TaskbarStateService(),
            handler => new ZoneEngine(config, handler),
            persistedConfig => ConfigStore.Save(Paths.ConfigFilePath, persistedConfig),
            StartupManager.IsEnabled())
    {
    }

    internal RuntimeController(
        AppConfig config,
        ITaskbarStateService taskbarState,
        Func<IZoneActivationHandler, IZoneEngineController> engineFactory,
        Action<AppConfig> saveConfig,
        bool startupEnabled)
    {
        Config = config;
        _taskbarState = taskbarState;
        _engine = engineFactory(this);
        _saveConfig = saveConfig;
        Config.StartWithWindows = startupEnabled;
        _autohidePollMs = Math.Clamp(Config.AutohideStatePollSeconds, 5, 300) * 1000;

        _baselineAutoHideEnabled = InitializeAutohideState();
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
            TriggerAssistPresets.Apply(Config.Trigger.Assist, preset);
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

            if (Config.PendingAutohideRestore)
            {
                if (!observed)
                {
                    _taskbarState.SetAutoHideEnabled(true);
                    observed = _taskbarState.IsAutoHideEnabled();
                }

                if (observed)
                {
                    Config.PendingAutohideRestore = false;
                    stateChanged = true;
                    Save();
                }
            }

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
                Config.PendingAutohideRestore = true;
                _lastStateWriteUtc = DateTime.UtcNow;
                Save();
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
        _saveConfig(Config);
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

    private bool InitializeAutohideState()
    {
        var observed = _taskbarState.IsAutoHideEnabled();

        if (Config.PendingAutohideRestore && !observed)
        {
            _taskbarState.SetAutoHideEnabled(true);
            observed = _taskbarState.IsAutoHideEnabled();
        }

        if (observed)
        {
            Config.PendingAutohideRestore = false;
        }

        return observed;
    }

    private void RestoreBaselineLocked()
    {
        if (!_managedVisibleActive)
        {
            return;
        }

        if (_taskbarState.SetAutoHideEnabled(_baselineAutoHideEnabled))
        {
            _managedVisibleActive = false;
            Config.PendingAutohideRestore = false;
            _lastStateWriteUtc = DateTime.UtcNow;
            Save();
        }
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
