using TaskbarUnhideZoner.Detection;

namespace TaskbarUnhideZoner.Runtime;

internal static class ZoneMonitorFactory
{
    public static IZoneMonitor Create()
    {
        return new MouseHookZoneMonitor();
    }
}
