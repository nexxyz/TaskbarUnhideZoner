using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.Startup;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class RuntimeController : IDisposable, IZoneActivationHandler
{
    private readonly object _sync = new();
    private readonly ZoneEngine _engine;
    private readonly TaskbarStateService _taskbarState;
    private readonly ExplorerMessageRevealService _explorerReveal;
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
        _explorerReveal = new ExplorerMessageRevealService();
        _engine = new ZoneEngine(config, this);
        Config.StartWithWindows = StartupManager.IsEnabled();
        _autohidePollMs = Math.Clamp(Config.AutohideStatePollSeconds, 5, 300) * 1000;

        AutoDetectRevealMethodIfNeededLocked();

        _baselineAutoHideEnabled = _taskbarState.IsAutoHideEnabled();
        ApplyRuntimeGateLocked();

        _autohidePollTimer = new System.Threading.Timer(_ => RefreshAutohideState(), null, _autohidePollMs, _autohidePollMs);
    }

    public event EventHandler? StateChanged;

    public AppConfig Config { get; }

    public string ActiveBackend => _engine.ActiveBackendName;

    public RevealMethod CurrentRevealMethod => Config.RevealMethod;

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

    public void SetRevealMethod(RevealMethod method)
    {
        lock (_sync)
        {
            if (Config.RevealMethod == method)
            {
                return;
            }

            if (_managedVisibleActive)
            {
                RestoreBaselineLocked();
            }

            Config.RevealMethod = method;
            Save();
        }

        RaiseStateChanged();
    }

    public void RedetectRevealMethod()
    {
        lock (_sync)
        {
            DetectAndApplyRevealMethodLocked(force: true);
        }

        RaiseStateChanged();
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
        var virtualScreen = SystemInformation.VirtualScreen;
        var thickness = Math.Clamp(Config.Zone.EdgeThicknessPx, 1, 400);
        var rectangle = edge switch
        {
            EdgePosition.Top => new Rectangle(virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, thickness),
            EdgePosition.Bottom => new Rectangle(virtualScreen.Left, virtualScreen.Bottom - thickness, virtualScreen.Width, thickness),
            EdgePosition.Left => new Rectangle(virtualScreen.Left, virtualScreen.Top, thickness, virtualScreen.Height),
            EdgePosition.Right => new Rectangle(virtualScreen.Right - thickness, virtualScreen.Top, thickness, virtualScreen.Height),
            _ => Rectangle.Empty
        };

        SetEdgeZone(edge, rectangle);
    }

    public void SetEdgeZone(EdgePosition edge, Rectangle rectangle)
    {
        Config.Zone.Mode = ZoneMode.EdgeBar;
        Config.Zone.Edge = edge;
        Config.Zone.EdgeThicknessPx = Math.Clamp(edge is EdgePosition.Top or EdgePosition.Bottom ? rectangle.Height : rectangle.Width, 1, 400);
        Config.Zone.EdgeZone = new RectConfig
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

            switch (Config.RevealMethod)
            {
                case RevealMethod.ExplorerMessage:
                    _explorerReveal.TryReveal();
                    break;

                case RevealMethod.AbmStateToggle:
                default:
                    if (_taskbarState.SetAutoHideEnabled(false))
                    {
                        _managedVisibleActive = true;
                        _lastStateWriteUtc = DateTime.UtcNow;
                    }

                    break;
            }
        }

        RaiseStateChanged();
    }

    public void OnZoneLeft()
    {
        lock (_sync)
        {
            if (Config.RevealMethod == RevealMethod.AbmStateToggle)
            {
                RestoreBaselineLocked();
            }

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

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AutoDetectRevealMethodIfNeededLocked()
    {
        if (Config.RevealMethod != RevealMethod.Unset)
        {
            return;
        }

        DetectAndApplyRevealMethodLocked(force: false);
    }

    private void DetectAndApplyRevealMethodLocked(bool force)
    {
        if (!force && Config.RevealMethod != RevealMethod.Unset)
        {
            return;
        }

        var autohideEnabled = _taskbarState.IsAutoHideEnabled();
        if (!autohideEnabled)
        {
            Config.RevealMethod = RevealMethod.AbmStateToggle;
            Save();
            RollingFileLogger.Info("Reveal method detection: autohide off, selected ABM state toggle.");
            return;
        }

        var explorerWorks = _explorerReveal.ProbeWorks(_taskbarState);
        Config.RevealMethod = explorerWorks ? RevealMethod.ExplorerMessage : RevealMethod.AbmStateToggle;
        Save();

        var chosen = Config.RevealMethod == RevealMethod.ExplorerMessage ? "ExplorerMessage" : "AbmStateToggle";
        RollingFileLogger.Info($"Reveal method detection complete, selected {chosen}.");
    }
}
