using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class ZoneGeometry
{
    public static bool IsInZone(ZoneConfig config, Point cursor, Rectangle virtualScreen)
    {
        return GetActiveZoneRectangle(config, virtualScreen).Contains(cursor);
    }

    public static Rectangle GetActiveZoneRectangle(ZoneConfig config, Rectangle virtualScreen)
    {
        return new Rectangle(config.ActiveZone.X, config.ActiveZone.Y, config.ActiveZone.Width, config.ActiveZone.Height);
    }
}
