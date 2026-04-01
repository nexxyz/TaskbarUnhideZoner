using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;
using TaskbarUnhideZoner.Startup;

namespace TaskbarUnhideZoner.Runtime;

internal sealed class RuntimeController : IDisposable
{
    private readonly ZoneEngine _engine;

    public RuntimeController(AppConfig config)
    {
        Config = config;
        TriggerService = new TaskbarTriggerService(config);
        _engine = new ZoneEngine(config, TriggerService);
        Config.StartWithWindows = StartupManager.IsEnabled();

        if (Config.Enabled)
        {
            _engine.Start();
        }
    }

    public AppConfig Config { get; }

    public TaskbarTriggerService TriggerService { get; }

    public string ActiveBackend => _engine.ActiveBackendName;

    public void SetEnabled(bool enabled)
    {
        Config.Enabled = enabled;
        Save();

        if (enabled)
        {
            _engine.Start();
        }
        else
        {
            _engine.Stop();
        }
    }

    public void SetStartup(bool enabled)
    {
        StartupManager.SetEnabled(enabled);
        Config.StartWithWindows = StartupManager.IsEnabled();
        Save();
    }

    public void SetDelayPreset(DelayPreset preset)
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
        if (Config.Enabled)
        {
            _engine.Reinitialize();
        }
    }

    public void Save()
    {
        ConfigStore.Save(Paths.ConfigFilePath, Config);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
