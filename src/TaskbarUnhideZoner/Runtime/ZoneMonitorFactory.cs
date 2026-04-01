using TaskbarUnhideZoner.Detection;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Runtime;

internal static class ZoneMonitorFactory
{
    public static IZoneMonitor Create(AppConfig config)
    {
        var backends = config.Detection.Backend switch
        {
            DetectionBackend.MouseHook => new[] { DetectionBackend.MouseHook, DetectionBackend.Polling },
            DetectionBackend.Polling => new[] { DetectionBackend.Polling },
            _ => new[] { DetectionBackend.MouseHook, DetectionBackend.Polling }
        };

        Exception? lastError = null;

        foreach (var backend in backends)
        {
            try
            {
                IZoneMonitor monitor = backend switch
                {
                    DetectionBackend.MouseHook => new MouseHookZoneMonitor(),
                    DetectionBackend.Polling => new PollingZoneMonitor(config.Detection.PollIntervalMs),
                    _ => throw new ArgumentOutOfRangeException()
                };

                monitor.Start();
                monitor.Stop();
                return monitor;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException("No zone monitor backend could be initialized.", lastError);
    }
}
