using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class ZoneGeometry
{
    public static bool IsInZone(ZoneConfig config, Point cursor, Rectangle virtualScreen)
    {
        return config.Mode switch
        {
            ZoneMode.EdgeBar => IsInEdgeZone(config, cursor),
            ZoneMode.HotZone => GetHotZone(config).Contains(cursor),
            _ => false
        };
    }

    public static Rectangle GetHotZone(ZoneConfig config)
    {
        return new Rectangle(config.HotZone.X, config.HotZone.Y, config.HotZone.Width, config.HotZone.Height);
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

    private static bool IsInEdgeZone(ZoneConfig config, Point cursor)
    {
        if (config.EdgeZone is { Width: > 0, Height: > 0 })
        {
            var persisted = new Rectangle(config.EdgeZone.X, config.EdgeZone.Y, config.EdgeZone.Width, config.EdgeZone.Height);
            return persisted.Contains(cursor);
        }

        foreach (var screen in Screen.AllScreens)
        {
            var edgeRect = GetEdgeRectangle(config, screen.Bounds);
            if (edgeRect.Contains(cursor))
            {
                return true;
            }
        }

        return false;
    }
}
