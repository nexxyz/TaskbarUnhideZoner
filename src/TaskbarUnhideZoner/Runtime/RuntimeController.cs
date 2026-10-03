using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.Startup;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class RuntimeController : IDisposable, IZoneActivationHandler
{
    // Must stay well below Explorer's ~500 ms auto-hide timer so the taskbar stays shown while in zone.
    private const int DefaultRevealKeepAliveMs = 200;

    private readonly object _sync = new();
    private readonly IZoneEngineController _engine;
    private readonly ITaskbarStateService _taskbarState;
    private readonly ITaskbarRevealService _reveal;
    private readonly Action<AppConfig> _saveConfig;
    private readonly System.Threading.Timer _autohidePollTimer;
    private readonly System.Threading.Timer _revealKeepAliveTimer;
    private readonly int _autohidePollMs;
    private readonly int _revealKeepAliveMs;

    private bool _autoHideEnabled;
    private bool _revealActive;
    private bool _revealVerified;
    private int _revealGeneration;
    private bool _disposed;
    private string? _monitoringError;

    public RuntimeController(AppConfig config)
        : this(
            config,
            new TaskbarStateService(),
            new TaskbarMessageRevealService(),
            handler => new ZoneEngine(config, handler),
            persistedConfig => ConfigStore.Save(Paths.ConfigFilePath, persistedConfig),
            StartupManager.IsEnabled())
    {
    }

    internal RuntimeController(
        AppConfig config,
        ITaskbarStateService taskbarState,
        ITaskbarRevealService reveal,
        Func<IZoneActivationHandler, IZoneEngineController> engineFactory,
        Action<AppConfig> saveConfig,
        bool startupEnabled,
        int revealKeepAliveMs = DefaultRevealKeepAliveMs)
    {
        Config = config;
        _taskbarState = taskbarState;
        _reveal = reveal;
        _engine = engineFactory(this);
        _saveConfig = saveConfig;
        _revealKeepAliveMs = revealKeepAliveMs;
        Config.StartWithWindows = startupEnabled;
        _autohidePollMs = Math.Clamp(Config.AutohideStatePollSeconds, 5, 300) * 1000;

        _revealKeepAliveTimer = new System.Threading.Timer(_ => KeepRevealAlive(), null, Timeout.Infinite, Timeout.Infinite);
        _autoHideEnabled = InitializeAutohideState();
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
                return Config.Enabled && !_autoHideEnabled;
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
            if (_disposed)
            {
                return;
            }

            // The engine forgets an active trigger on reinitialize without reporting a zone leave.
            StopRevealLocked();
            if (Config.Enabled && _autoHideEnabled)
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
            if (_disposed)
            {
                return;
            }

            var observed = _taskbarState.IsAutoHideEnabled();

            // Recovery for app versions before 1.1, which turned auto-hide off while revealing.
            if (Config.PendingAutohideRestore)
            {
                if (!observed)
                {
                    _taskbarState.EnableAutoHide();
                    observed = _taskbarState.IsAutoHideEnabled();
                }

                if (observed)
                {
                    Config.PendingAutohideRestore = false;
                    stateChanged = true;
                    Save();
                }
            }

            if (_autoHideEnabled != observed)
            {
                _autoHideEnabled = observed;
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
            if (_disposed || !Config.Enabled || !_autoHideEnabled || _revealActive)
            {
                return;
            }

            _revealActive = true;
            _revealVerified = false;
            _revealGeneration++;
            _revealKeepAliveTimer.Change(_revealKeepAliveMs, Timeout.Infinite);
        }

        // Cross-process sends stay outside the lock so a slow Explorer cannot stall the tray UI.
        _reveal.Reveal();
    }

    public void OnZoneLeft()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            StopRevealLocked();
        }
    }

    public void Save()
    {
        _saveConfig(Config);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _autohidePollTimer.Dispose();
            StopRevealLocked();
            _revealKeepAliveTimer.Dispose();
            _engine.Dispose();
        }
    }

    private void ApplyRuntimeGateLocked()
    {
        var shouldRun = Config.Enabled && _autoHideEnabled;
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
                StopRevealLocked();
                _engine.Stop();
            }
        }
        else
        {
            StopRevealLocked();
            _engine.Stop();
        }
    }

    private bool InitializeAutohideState()
    {
        var observed = _taskbarState.IsAutoHideEnabled();

        // Recovery for app versions before 1.1, which turned auto-hide off while revealing.
        if (Config.PendingAutohideRestore && !observed)
        {
            _taskbarState.EnableAutoHide();
            observed = _taskbarState.IsAutoHideEnabled();
        }

        if (observed)
        {
            Config.PendingAutohideRestore = false;
        }

        return observed;
    }

    // One-shot timer re-armed after each send, so slow sends cannot overlap or pile up.
    private void KeepRevealAlive()
    {
        int generation;
        bool verify;
        lock (_sync)
        {
            if (_disposed || !_revealActive)
            {
                return;
            }

            generation = _revealGeneration;
            verify = !_revealVerified;
        }

        var verified = false;
        try
        {
            if (_reveal.Reveal() && verify)
            {
                verified = true;
                if (!_reveal.IsAnyTaskbarShown())
                {
                    RollingFileLogger.Error("REVEAL_INEFFECTIVE taskbar not shown after reveal message");
                }
            }
        }
        catch (Exception ex)
        {
            RollingFileLogger.Error($"REVEAL_KEEPALIVE_FAIL {ex.Message}");
        }

        lock (_sync)
        {
            if (!_disposed && _revealActive && generation == _revealGeneration)
            {
                _revealVerified |= verified;
                _revealKeepAliveTimer.Change(_revealKeepAliveMs, Timeout.Infinite);
            }
        }
    }

    private void StopRevealLocked()
    {
        if (!_revealActive)
        {
            return;
        }

        // Explorer hides the taskbar on its own once keep-alive stops, unless the cursor is over it.
        _revealActive = false;
        _revealKeepAliveTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
