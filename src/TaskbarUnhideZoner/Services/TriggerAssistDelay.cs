using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class TriggerAssistDelay
{
    public static int ComputeDelayMs(AppConfig config, Point cursorPosition)
    {
        var baseDelayMs = Math.Max(1, config.TriggerDelayMs);
        var assist = config.Trigger.Assist;
        if (!assist.Enabled)
        {
            return baseDelayMs;
        }

        var zone = config.Zone.ActiveZone;
        var closeness = config.Zone.Mode switch
        {
            ZoneMode.Top => ComputeTopCloseness(zone, cursorPosition),
            ZoneMode.Bottom => ComputeBottomCloseness(zone, cursorPosition),
            ZoneMode.Left => ComputeLeftCloseness(zone, cursorPosition),
            ZoneMode.Right => ComputeRightCloseness(zone, cursorPosition),
            _ => ComputeCenterCloseness(zone, cursorPosition)
        };

        var minFactor = Math.Clamp(assist.MinDelayPercent / 100.0, 0.10, 1.0);
        var curve = Math.Clamp(assist.CurveExponent, 0.35, 3.0);
        var boost = Math.Pow(closeness, curve);
        var factor = 1.0 - (boost * (1.0 - minFactor));
        var effectiveDelayMs = (int)Math.Round(baseDelayMs * factor);
        return Math.Max(35, effectiveDelayMs);
    }

    private static double ComputeTopCloseness(RectConfig zone, Point cursor)
    {
        return 1.0 - NormalizeWithinEdge(zone.Y, zone.Height, cursor.Y);
    }

    private static double ComputeBottomCloseness(RectConfig zone, Point cursor)
    {
        return NormalizeWithinEdge(zone.Y, zone.Height, cursor.Y);
    }

    private static double ComputeLeftCloseness(RectConfig zone, Point cursor)
    {
        return 1.0 - NormalizeWithinEdge(zone.X, zone.Width, cursor.X);
    }

    private static double ComputeRightCloseness(RectConfig zone, Point cursor)
    {
        return NormalizeWithinEdge(zone.X, zone.Width, cursor.X);
    }

    private static double ComputeCenterCloseness(RectConfig zone, Point cursor)
    {
        if (zone.Width <= 1 || zone.Height <= 1)
        {
            return 1.0;
        }

        var centerX = zone.X + (zone.Width / 2.0);
        var centerY = zone.Y + (zone.Height / 2.0);
        var halfW = zone.Width / 2.0;
        var halfH = zone.Height / 2.0;

        var dx = (cursor.X - centerX) / halfW;
        var dy = (cursor.Y - centerY) / halfH;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        return 1.0 - Math.Clamp(distance, 0.0, 1.0);
    }

    private static double NormalizeWithinEdge(int start, int length, int value)
    {
        if (length <= 1)
        {
            return 0.0;
        }

        var normalized = (value - start) / (double)(length - 1);
        return Math.Clamp(normalized, 0.0, 1.0);
    }
}
