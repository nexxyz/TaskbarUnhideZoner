using TaskbarUnhideZoner.Detection;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Runtime;

internal static class ZoneMonitorFactory
{
    public static IZoneMonitor Create(AppConfig config)
    {
        var backend = config.Detection.Backend switch
        {
            DetectionBackend.Polling => DetectionBackend.Polling,
            _ => DetectionBackend.MouseHook
        };

        return backend switch
        {
            DetectionBackend.MouseHook => new MouseHookZoneMonitor(),
            DetectionBackend.Polling => new PollingZoneMonitor(config.Detection.PollIntervalMs),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
