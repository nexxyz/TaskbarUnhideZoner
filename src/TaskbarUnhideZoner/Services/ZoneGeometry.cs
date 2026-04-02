using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class ZoneGeometry
{
    public static bool IsInZone(ZoneConfig config, Point cursor, Rectangle virtualScreen)
    {
        return GetActiveZoneRectangle(config, virtualScreen).Contains(cursor);
    }

    public static Rectangle GetEdgeRectangle(ZoneConfig config, Rectangle virtualScreen)
    {
        var thickness = Math.Clamp(config.EdgeThicknessPx, 1, 100);

        return config.Edge switch
        {
            EdgePosition.Top => new Rectangle(virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, thickness),
            EdgePosition.Bottom => new Rectangle(virtualScreen.Left, virtualScreen.Bottom - thickness, virtualScreen.Width, thickness),
            EdgePosition.Left => new Rectangle(virtualScreen.Left, virtualScreen.Top, thickness, virtualScreen.Height),
            EdgePosition.Right => new Rectangle(virtualScreen.Right - thickness, virtualScreen.Top, thickness, virtualScreen.Height),
            _ => Rectangle.Empty
        };
    }

    public static Rectangle GetActiveZoneRectangle(ZoneConfig config, Rectangle virtualScreen)
    {
        if (config.ActiveZone is { Width: > 0, Height: > 0 })
        {
            return new Rectangle(config.ActiveZone.X, config.ActiveZone.Y, config.ActiveZone.Width, config.ActiveZone.Height);
        }

        if (virtualScreen != Rectangle.Empty)
        {
            return GetEdgeRectangle(config, virtualScreen);
        }

        foreach (var screen in Screen.AllScreens)
        {
            return GetEdgeRectangle(config, screen.Bounds);
        }

        return Rectangle.Empty;
    }
}
